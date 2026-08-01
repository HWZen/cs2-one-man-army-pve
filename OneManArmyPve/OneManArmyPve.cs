using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.Modules.Memory;

namespace OneManArmyPve;

public class OneManArmyPve : BasePlugin
{
    public override string ModuleName => "One-Man-Army PvE";
    public override string ModuleVersion => "1.0.0";
    public override string ModuleAuthor => "OpenCode";
    public override string ModuleDescription => "1 human (1000 HP) vs 10 bots (100 HP) competitive mode";

    private const int HeroHealth = 1000;
    private const int HeroArmor = 400;
    private const int BotHealth = 100;
    private const int TargetBotCount = 10;

    private bool _enabled;
    private ulong _heroSteamId;
    private int _heroSlot = -1;
    private CsTeam _initialHeroTeam = CsTeam.CounterTerrorist;

    public override void Load(bool hotReload)
    {
        RegisterEventHandler<EventRoundStart>(OnRoundStart);
        RegisterEventHandler<EventPlayerSpawn>(OnPlayerSpawn);
    }

    [ConsoleCommand("oma", "Enable One-Man-Army PvE mode: oma [t|ct]")]
    [CommandHelper(minArgs: 0, usage: "[t|ct]", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    public void OnEnable(CCSPlayerController? caller, CommandInfo command)
    {
        var hero = ResolveHeroForEnable(caller);
        if (hero == null)
        {
            command.ReplyToCommand("[OMA] No valid human player found. Join the server first.");
            return;
        }

        _initialHeroTeam = ParseTeamArg(command.GetArg(1)) ?? _initialHeroTeam;
        _heroSteamId = hero.SteamID;
        _heroSlot = hero.Slot;
        _enabled = true;

        // Optional one-time side selection when enabling.
        if ((CsTeam)hero.TeamNum != _initialHeroTeam)
        {
            try { hero.SwitchTeam(_initialHeroTeam); } catch { }
        }

        ApplyMatchRules();
        AddTimer(0.2f, () =>
        {
            if (!_enabled) return;
            EnforceTeamsAndBotCount();
            EnforceLivePlayerHealth();
        });

        command.ReplyToCommand($"[OMA] Enabled. Hero: {hero.PlayerName}, Team: {TeamLabel(_initialHeroTeam)}, HP: {HeroHealth} vs {TargetBotCount} bots ({BotHealth} HP).");
    }

    [ConsoleCommand("oma_disable", "Disable One-Man-Army PvE mode")]
    [CommandHelper(minArgs: 0, whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    public void OnDisable(CCSPlayerController? caller, CommandInfo command)
    {
        _enabled = false;
        command.ReplyToCommand("[OMA] Disabled.");
    }

    [ConsoleCommand("oma_status", "Show One-Man-Army PvE status")]
    [CommandHelper(minArgs: 0, whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    public void OnStatus(CCSPlayerController? caller, CommandInfo command)
    {
        if (!_enabled)
        {
            command.ReplyToCommand("[OMA] Disabled.");
            return;
        }

        var hero = FindHero();
        var heroName = hero?.PlayerName ?? "<offline>";
        var currentTeam = hero != null && IsPlayableTeam((CsTeam)hero.TeamNum)
            ? (CsTeam)hero.TeamNum
            : _initialHeroTeam;
        command.ReplyToCommand($"[OMA] Enabled. Hero: {heroName}, Team: {TeamLabel(currentTeam)}, HP: {HeroHealth} vs bots: {TargetBotCount} x {BotHealth} HP.");
    }

    private HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
    {
        if (!_enabled) return HookResult.Continue;

        AddTimer(0.2f, () =>
        {
            if (!_enabled) return;
            EnforceTeamsAndBotCount();
            EnforceLivePlayerHealth();
        });

        return HookResult.Continue;
    }

    private HookResult OnPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
    {
        if (!_enabled) return HookResult.Continue;

        var player = @event.Userid;
        if (player == null || !player.IsValid) return HookResult.Continue;

        AddTimer(0.05f, () =>
        {
            if (!_enabled || player == null || !player.IsValid) return;
            ApplyHealth(player);
        });

        return HookResult.Continue;
    }

    private void ApplyMatchRules()
    {
        Server.ExecuteCommand("mp_halftime 1");
        Server.ExecuteCommand("mp_maxrounds 24");
        Server.ExecuteCommand("mp_match_can_clinch 1");
        Server.ExecuteCommand("mp_overtime_enable 1");
        Server.ExecuteCommand("mp_autoteambalance 0");
        Server.ExecuteCommand("mp_limitteams 0");
        Server.ExecuteCommand("sv_infinite_ammo 2");
        Server.ExecuteCommand("bot_quota_mode fill");
        Server.ExecuteCommand($"bot_quota {TargetBotCount}");
    }

    private void EnforceTeamsAndBotCount()
    {
        var hero = FindHero();
        if (hero == null) return;

        _heroSlot = hero.Slot;
        var heroTeamNow = IsPlayableTeam((CsTeam)hero.TeamNum)
            ? (CsTeam)hero.TeamNum
            : _initialHeroTeam;
        var botTeam = Opposite(heroTeamNow);

        foreach (var player in Utilities.GetPlayers())
        {
            if (player == null || !player.IsValid || !player.IsBot) continue;
            if ((CsTeam)player.TeamNum == botTeam) continue;
            try { player.SwitchTeam(botTeam); } catch { }
        }

        int botsOnTargetTeam = Utilities.GetPlayers().Count(p =>
            p != null
            && p.IsValid
            && p.IsBot
            && (CsTeam)p.TeamNum == botTeam);

        if (botsOnTargetTeam < TargetBotCount)
        {
            int missing = TargetBotCount - botsOnTargetTeam;
            string addCmd = botTeam == CsTeam.Terrorist ? "bot_add_t" : "bot_add_ct";
            for (int i = 0; i < missing; i++)
            {
                Server.ExecuteCommand(addCmd);
            }
        }
        else if (botsOnTargetTeam > TargetBotCount)
        {
            int extra = botsOnTargetTeam - TargetBotCount;
            string kickCmd = botTeam == CsTeam.Terrorist ? "bot_kick t" : "bot_kick ct";
            for (int i = 0; i < extra; i++)
            {
                Server.ExecuteCommand(kickCmd);
            }
        }
    }

    private void EnforceLivePlayerHealth()
    {
        foreach (var player in Utilities.GetPlayers())
        {
            if (player == null || !player.IsValid || !player.PawnIsAlive) continue;
            ApplyHealth(player);
        }
    }

    private void ApplyHealth(CCSPlayerController player)
    {
        if (player.PlayerPawn == null || !player.PlayerPawn.IsValid) return;
        var pawn = player.PlayerPawn.Value;
        if (pawn == null || !pawn.IsValid) return;

        bool isHero = IsHero(player);
        int targetHealth = player.IsBot ? BotHealth : (isHero ? HeroHealth : BotHealth);

        try
        {
            pawn.Health = targetHealth;
            Utilities.SetStateChanged(pawn, "CBaseEntity", "m_iHealth");

            if (isHero)
            {
                if (pawn.ItemServices != null && pawn.ItemServices.Handle != nint.Zero)
                {
                    var itemServices = new CCSPlayer_ItemServices(pawn.ItemServices.Handle);
                    if (!itemServices.HasHelmet)
                    {
                        player.GiveNamedItem("item_assaultsuit");
                    }
                }

                pawn.ArmorValue = HeroArmor;
                Utilities.SetStateChanged(pawn, "CCSPlayerPawn", "m_ArmorValue");
            }
        }
        catch
        {
            // Ignore schema/runtime mismatch so plugin keeps running.
        }
    }

    private bool IsHero(CCSPlayerController player)
    {
        if (!player.IsValid || player.IsBot) return false;
        if (_heroSteamId != 0 && player.SteamID == _heroSteamId) return true;
        return _heroSlot >= 0 && player.Slot == _heroSlot;
    }

    private CCSPlayerController? ResolveHeroForEnable(CCSPlayerController? caller)
    {
        if (caller != null && caller.IsValid && !caller.IsBot && !caller.IsHLTV)
            return caller;

        return Utilities.GetPlayers().FirstOrDefault(p =>
            p != null
            && p.IsValid
            && !p.IsBot
            && !p.IsHLTV);
    }

    private CCSPlayerController? FindHero()
    {
        var bySteamId = Utilities.GetPlayers().FirstOrDefault(p =>
            p != null
            && p.IsValid
            && !p.IsBot
            && !p.IsHLTV
            && _heroSteamId != 0
            && p.SteamID == _heroSteamId);
        if (bySteamId != null) return bySteamId;

        return Utilities.GetPlayers().FirstOrDefault(p =>
            p != null
            && p.IsValid
            && !p.IsBot
            && !p.IsHLTV
            && _heroSlot >= 0
            && p.Slot == _heroSlot);
    }

    private static CsTeam Opposite(CsTeam team)
        => team == CsTeam.Terrorist ? CsTeam.CounterTerrorist : CsTeam.Terrorist;

    private static bool IsPlayableTeam(CsTeam team)
        => team == CsTeam.Terrorist || team == CsTeam.CounterTerrorist;

    private static CsTeam? ParseTeamArg(string? arg)
    {
        if (string.IsNullOrWhiteSpace(arg)) return null;

        arg = arg.Trim().ToLowerInvariant();
        return arg switch
        {
            "t" or "2" or "terrorist" => CsTeam.Terrorist,
            "ct" or "3" or "counterterrorist" or "counter-terrorist" => CsTeam.CounterTerrorist,
            _ => null
        };
    }

    private static string TeamLabel(CsTeam team)
        => team == CsTeam.Terrorist ? "T" : "CT";
}
