using System.Diagnostics;
using System.Text.Json;
using Microsoft.Win32;

namespace DeployTool;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}

// ── 配置模型 ──────────────────────────────────────────────────

internal sealed class DeployConfig
{
    public string Name { get; set; } = "";
    public List<DeployFileEntry> Files { get; set; } = new();
}

internal sealed class DeployFileEntry
{
    public string From { get; set; } = "";
    public string To { get; set; } = "";
    public bool Recursive { get; set; }
    public bool Optional { get; set; }
}

// ── 主窗口 ────────────────────────────────────────────────────

internal sealed class MainForm : Form
{
    private const string ConfigFileName = "deploy.json";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private const string WithBotsGameInfoPath = "backup\\WithBots\\gameinfo.gi";
    private const string OnlineGameInfoPath = "backup\\Online\\gameinfo.gi";

    private readonly Button _installButton;
    private readonly Button _launchButton;
    private readonly TextBox _logBox;
    private readonly Label _statusLabel;

    private Process? _runningCs2;

    public MainForm()
    {
        Text = "CS2-One-Man-Army-PVE 部署工具";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(520, 400);

        var titleLabel = new Label
        {
            Text = "CS2-一人成军PVE",
            AutoSize = true,
            Font = new Font("Microsoft YaHei UI", 13, FontStyle.Bold),
            Location = new Point(20, 15)
        };

        _installButton = new Button
        {
            Text = "安装",
            Font = new Font("Microsoft YaHei UI", 10, FontStyle.Bold),
            Size = new Size(240, 44),
            Location = new Point(20, 52)
        };
        _installButton.Click += (_, _) => InstallPlugin();

        _launchButton = new Button
        {
            Text = "启动 CS2（-insecure）",
            Font = new Font("Microsoft YaHei UI", 10, FontStyle.Bold),
            Size = new Size(230, 44),
            Location = new Point(270, 52)
        };
        _launchButton.Click += (_, _) => LaunchCs2();

        _logBox = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Font = new Font("Consolas", 9),
            BackColor = Color.FromArgb(248, 248, 248),
            Location = new Point(20, 106),
            Size = new Size(480, 248)
        };

        _statusLabel = new Label
        {
            Text = "就绪",
            AutoSize = false,
            Size = new Size(480, 30),
            Location = new Point(20, 364),
            Font = new Font("Microsoft YaHei UI", 9),
            ForeColor = Color.FromArgb(45, 45, 45)
        };

        Controls.Add(titleLabel);
        Controls.Add(_installButton);
        Controls.Add(_launchButton);
        Controls.Add(_logBox);
        Controls.Add(_statusLabel);

        FormClosing += (_, _) => RestoreOnlineGameInfo();
        SystemEvents.SessionEnding += (_, _) => RestoreOnlineGameInfo();
    }

    // ── 安装 ──────────────────────────────────────────────

    private void InstallPlugin()
    {
        SetBusy(true);
        _logBox.Clear();
        try
        {
            string toolDir = AppContext.BaseDirectory;
            string configPath = Path.Combine(toolDir, ConfigFileName);

            DeployConfig? config = LoadOrCreateConfig(configPath);
            if (config == null || config.Files.Count == 0)
            {
                ShowError($"配置文件无效或未定义任何文件：\n{configPath}");
                return;
            }

            Log($"配置名称：{config.Name}");
            Log($"文件条目：{config.Files.Count} 项");
            Log("");

            string? cs2Path = FindCs2InstallPath();
            if (string.IsNullOrWhiteSpace(cs2Path))
            {
                ShowError("未自动定位到 CS2 安装目录。\n请确认已安装 Steam 和 CS2。");
                return;
            }

            Log($"CS2 目录：{cs2Path}");
            Log("");

            var errors = new List<string>();
            int copied = 0;
            int total = config.Files.Count;

            for (int i = 0; i < config.Files.Count; i++)
            {
                var entry = config.Files[i];
                _statusLabel.Text = $"正在处理 ({i + 1}/{total})：{entry.From}";
                _statusLabel.Refresh();

                try
                {
                    int n = ProcessEntry(entry, toolDir, cs2Path);
                    copied += n;
                    Log($"  [成功] {entry.From} -> {entry.To}  ({n} 个文件)");
                }
                catch (Exception ex)
                {
                    string tag = entry.Optional ? "跳过" : "失败";
                    Log($"  [{tag}] {entry.From} -- {ex.Message}");
                    if (!entry.Optional)
                        errors.Add($"{entry.From}：{ex.Message}");
                }
            }

            Log("");
            if (errors.Count == 0)
            {
                _statusLabel.Text = $"安装完成，共复制 {copied} 个文件。";
                Log($"安装完成，共复制 {copied} 个文件。");
                MessageBox.Show(this, $"安装完成。\n共复制 {copied} 个文件。", "完成",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                _statusLabel.Text = $"完成：{copied} 成功，{errors.Count} 失败";
                Log($"完成：{copied} 成功，{errors.Count} 失败");
                MessageBox.Show(this,
                    $"复制 {copied} 个文件，{errors.Count} 个失败：\n\n{string.Join("\n", errors)}",
                    "部分完成", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        catch (Exception ex)
        {
            ShowError($"安装失败：{ex.Message}");
        }
        finally
        {
            SetBusy(false);
        }
    }

    private int ProcessEntry(DeployFileEntry entry, string toolDir, string cs2Path)
    {
        if (string.IsNullOrWhiteSpace(entry.From))
            throw new InvalidOperationException("from 不能为空");
        if (string.IsNullOrWhiteSpace(entry.To))
            throw new InvalidOperationException("to 不能为空");

        string sourcePath = Path.IsPathRooted(entry.From)
            ? entry.From
            : Path.Combine(toolDir, entry.From);

        // 通配符模式
        if (entry.From.Contains('*') || entry.From.Contains('?'))
        {
            string dir = Path.GetDirectoryName(sourcePath) ?? toolDir;
            string pattern = Path.GetFileName(sourcePath);
            string destDir = ResolveDestDir(entry.To, cs2Path);
            Directory.CreateDirectory(destDir);

            var search = entry.Recursive
                ? SearchOption.AllDirectories
                : SearchOption.TopDirectoryOnly;

            int count = 0;
            foreach (string file in Directory.GetFiles(dir, pattern, search))
            {
                string relPath = Path.GetRelativePath(dir, file);
                string destPath = Path.Combine(destDir, relPath);
                Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
                File.Copy(file, destPath, overwrite: true);
                count++;
            }
            return count;
        }

        // 目录模式
        if (Directory.Exists(sourcePath))
        {
            string destDir = ResolveDestDir(entry.To, cs2Path);
            return CopyDirectoryTree(sourcePath, destDir, entry.Recursive);
        }

        // 单文件模式
        if (File.Exists(sourcePath))
        {
            string destPath = ResolveDestFile(entry.To, sourcePath, cs2Path);
            Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
            File.Copy(sourcePath, destPath, overwrite: true);
            return 1;
        }

        // 源不存在
        if (entry.Optional)
            return 0;
        throw new FileNotFoundException($"源不存在：{sourcePath}");
    }

    private static string ResolveDestDir(string to, string cs2Path)
    {
        string dest = Path.IsPathRooted(to) ? to : Path.Combine(cs2Path, to);
        return dest.TrimEnd('/', '\\');
    }

    private static string ResolveDestFile(string to, string sourceFile, string cs2Path)
    {
        string dest = Path.IsPathRooted(to) ? to : Path.Combine(cs2Path, to);

        // 以分隔符结尾 → 目录，保留源文件名
        if (to.EndsWith('/') || to.EndsWith('\\'))
            return Path.Combine(dest, Path.GetFileName(sourceFile));

        // 否则视为完整目标路径（可重命名）
        return dest;
    }

    private static int CopyDirectoryTree(string sourceDir, string destDir, bool recursive)
    {
        Directory.CreateDirectory(destDir);
        int count = 0;

        foreach (string file in Directory.GetFiles(sourceDir))
        {
            File.Copy(file, Path.Combine(destDir, Path.GetFileName(file)), overwrite: true);
            count++;
        }

        if (recursive)
        {
            foreach (string sub in Directory.GetDirectories(sourceDir))
            {
                string name = Path.GetFileName(sub);
                count += CopyDirectoryTree(sub, Path.Combine(destDir, name), recursive);
            }
        }

        return count;
    }

    // ── 配置加载 ──────────────────────────────────────────

    private static DeployConfig? LoadOrCreateConfig(string configPath)
    {
        if (File.Exists(configPath))
        {
            string json = File.ReadAllText(configPath);
            return JsonSerializer.Deserialize<DeployConfig>(json, JsonOpts);
        }

        // 配置文件不存在时生成默认配置
        var defaults = new DeployConfig
        {
            Name = "OneManArmyPve",
            Files = new List<DeployFileEntry>
            {
                new()
                {
                    From = "OneManArmyPve.dll",
                    To = "game/csgo/addons/counterstrikesharp/plugins/OneManArmyPve/"
                }
            }
        };

        try
        {
            var writeOpts = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            string json = JsonSerializer.Serialize(defaults, writeOpts);
            File.WriteAllText(configPath, json);
        }
        catch
        {
            // 写入失败则仅使用内存中的默认配置
        }

        return defaults;
    }

    // ── 启动 CS2 ──────────────────────────────────────────

    private void LaunchCs2()
    {
        UseWaitCursor = true;
        _installButton.Enabled = false;
        try
        {
            string? cs2Path = FindCs2InstallPath();
            if (string.IsNullOrWhiteSpace(cs2Path))
            {
                ShowError("未自动定位到 CS2 安装目录。\n请确认已安装 Steam 和 CS2。");
                return;
            }

            string cs2Exe = Path.Combine(cs2Path, "game", "bin", "win64", "cs2.exe");
            if (!File.Exists(cs2Exe))
            {
                ShowError($"未找到 cs2.exe：{cs2Exe}");
                return;
            }

            if (!CopyGameInfo(cs2Path, WithBotsGameInfoPath, "启用 Bots", remindOnMissing: true))
                return;

            var startInfo = new ProcessStartInfo
            {
                FileName = cs2Exe,
                Arguments = "-insecure",
                WorkingDirectory = Path.GetDirectoryName(cs2Exe)!,
                UseShellExecute = true
            };

            Process? process = Process.Start(startInfo);
            if (process == null)
            {
                CopyGameInfo(cs2Path, OnlineGameInfoPath, "恢复 Online");
                ShowError("启动失败。请手动启动 CS2 并确保启动项包含 -insecure。");
                return;
            }

            _runningCs2 = process;
            _launchButton.Enabled = false;
            _launchButton.Text = "CS2 运行中...";
            _statusLabel.Text = "已启动 CS2（-insecure），退出后自动恢复 gameinfo.gi";
            Log("已启动 CS2（-insecure），退出后自动恢复 gameinfo.gi");

            _ = Task.Run(() => WatchCs2AndRestore(process, cs2Path));
        }
        catch (Exception ex)
        {
            ShowError($"启动失败：{ex.Message}");
        }
        finally
        {
            UseWaitCursor = false;
            Cursor = Cursors.Default;
            _installButton.Enabled = true;
        }
    }

    // ── gameinfo.gi 切换 ──────────────────────────────────

    private bool CopyGameInfo(string cs2Path, string relativePath, string action, bool remindOnMissing = false)
    {
        try
        {
            string source = Path.Combine(cs2Path, "game", "csgo", relativePath);
            string dest = Path.Combine(cs2Path, "game", "csgo", "gameinfo.gi");
            if (!File.Exists(source))
            {
                Log($"[{action}] 未找到源文件：{source}");
                if (remindOnMissing)
                {
                    MessageBox.Show(this,
                        $"未找到：{source}\n\nCS2BotImprover 可能尚未部署到 CS2 目录，请先点击“安装”按钮完成部署后再启动。",
                        "未部署 CS2BotImprover", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                return false;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            File.Copy(source, dest, overwrite: true);
            Log($"[{action}] 已写入 gameinfo.gi：{dest}");
            return true;
        }
        catch (Exception ex)
        {
            Log($"[{action}] 写入 gameinfo.gi 失败：{ex.Message}");
            return false;
        }
    }

    private void RestoreOnlineGameInfo()
    {
        try
        {
            string? cs2Path = FindCs2InstallPath();
            if (string.IsNullOrWhiteSpace(cs2Path)) return;
            CopyGameInfo(cs2Path, OnlineGameInfoPath, "退出恢复 Online");
        }
        catch
        {
            // ignored
        }
    }

    private void WatchCs2AndRestore(Process process, string cs2Path)
    {
        try
        {
            process.WaitForExit();
        }
        catch
        {
            // ignored
        }
        finally
        {
            process.Dispose();
            if (ReferenceEquals(_runningCs2, process))
                _runningCs2 = null;

            CopyGameInfo(cs2Path, OnlineGameInfoPath, "恢复 Online");

            InvokeIfNeeded(() =>
            {
                _launchButton.Enabled = true;
                _launchButton.Text = "启动 CS2（-insecure）";
                _statusLabel.Text = "CS2 已退出，已恢复 gameinfo.gi";
            });
        }
    }

    // ── 辅助方法 ──────────────────────────────────────────

    private void Log(string message)
    {
        if (IsDisposed || Disposing) return;
        InvokeIfNeeded(() => _logBox.AppendText(message + Environment.NewLine));
    }

    private void InvokeIfNeeded(Action action)
    {
        if (IsDisposed || Disposing) return;
        if (InvokeRequired)
        {
            try { BeginInvoke(action); }
            catch { }
        }
        else
        {
            action();
        }
    }

    private void SetBusy(bool busy)
    {
        UseWaitCursor = busy;
        _installButton.Enabled = !busy;
        _launchButton.Enabled = !busy;
        Cursor = busy ? Cursors.WaitCursor : Cursors.Default;
    }

    private void ShowError(string message)
    {
        _statusLabel.Text = message;
        Log(message);
        MessageBox.Show(this, message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    // ── CS2 路径检测 ──────────────────────────────────────

    private static string? FindCs2InstallPath()
    {
        List<string> steamRoots = GetSteamInstallRoots();
        foreach (string root in steamRoots.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            string? cs2 = TryFindCs2InSteamRoot(root);
            if (cs2 != null) return cs2;
        }

        return null;
    }

    private static List<string> GetSteamInstallRoots()
    {
        var roots = new List<string>();

        string? registryPath = ReadSteamPathFromRegistry();
        if (!string.IsNullOrWhiteSpace(registryPath))
        {
            roots.Add(registryPath);
        }

        string[] commonCandidates =
        {
            @"C:\\Program Files (x86)\\Steam",
            @"C:\\Program Files\\Steam"
        };

        roots.AddRange(commonCandidates.Where(Directory.Exists));
        return roots;
    }

    private static string? ReadSteamPathFromRegistry()
    {
        const string keyPath = @"SOFTWARE\\WOW6432Node\\Valve\\Steam";
        const string valueName = "InstallPath";

        try
        {
            using RegistryKey? localMachine = Registry.LocalMachine.OpenSubKey(keyPath);
            string? value = localMachine?.GetValue(valueName) as string;
            if (!string.IsNullOrWhiteSpace(value) && Directory.Exists(value))
            {
                return value;
            }
        }
        catch
        {
            // ignored
        }

        try
        {
            using RegistryKey? currentUser = Registry.CurrentUser.OpenSubKey(@"Software\\Valve\\Steam");
            string? value = currentUser?.GetValue("SteamPath") as string;
            if (!string.IsNullOrWhiteSpace(value) && Directory.Exists(value))
            {
                return value;
            }
        }
        catch
        {
            // ignored
        }

        return null;
    }

    private static string? TryFindCs2InSteamRoot(string steamRoot)
    {
        string steamAppsPath = Path.Combine(steamRoot, "steamapps");
        if (!Directory.Exists(steamAppsPath)) return null;

        var libraryFolders = new List<string> { steamAppsPath };

        string libraryVdf = Path.Combine(steamAppsPath, "libraryfolders.vdf");
        if (File.Exists(libraryVdf))
        {
            string content = File.ReadAllText(libraryVdf);
            foreach (string line in content.Split('\n'))
            {
                string trimmed = line.Trim();
                if (!trimmed.Contains("\"path\"", StringComparison.OrdinalIgnoreCase)) continue;

                string? extracted = ExtractQuotedValue(trimmed);
                if (string.IsNullOrWhiteSpace(extracted)) continue;

                string normalized = extracted.Replace("\\\\", "\\");
                string candidateSteamApps = Path.Combine(normalized, "steamapps");
                if (Directory.Exists(candidateSteamApps))
                {
                    libraryFolders.Add(candidateSteamApps);
                }
            }
        }

        foreach (string library in libraryFolders.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            string appManifest = Path.Combine(library, "appmanifest_730.acf");
            if (!File.Exists(appManifest)) continue;

            string gamePath = Path.Combine(library, "common", "Counter-Strike Global Offensive");
            if (Directory.Exists(gamePath)) return gamePath;
        }

        return null;
    }

    private static string? ExtractQuotedValue(string line)
    {
        int first = line.IndexOf('"');
        if (first < 0) return null;
        int second = line.IndexOf('"', first + 1);
        if (second < 0) return null;
        int third = line.IndexOf('"', second + 1);
        if (third < 0) return null;
        int fourth = line.IndexOf('"', third + 1);
        if (fourth < 0) return null;

        return line.Substring(third + 1, fourth - third - 1);
    }
}
