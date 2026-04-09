using System;
using Dalamud.Game.Command;
using Dalamud.Game.Gui.Dtr;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.IoC;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using footballer.Models;
using footballer.Services;
using footballer.Windows;

namespace footballer;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IObjectTable ObjectTable { get; private set; } = null!;
    [PluginService] internal static IPartyList PartyList { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;
    [PluginService] internal static IDtrBar DtrBar { get; private set; } = null!;
    [PluginService] internal static IGameGui GameGui { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;

    public Configuration Configuration { get; }
    public WindowSystem WindowSystem { get; } = new(PluginInfo.InternalName);
    public PartyShowcaseService PartyShowcaseService { get; }
    public LodestoneProfileService LodestoneProfileService { get; }
    public CommendationPortraitResearchService CommendationPortraitResearchService { get; }
    public CharacterInspectResearchService CharacterInspectResearchService { get; }
    public CharacterInspectPreviewCaptureService CharacterInspectPreviewCaptureService { get; }
    public CharacterInspectPoseService CharacterInspectPoseService { get; }
    public CharacterInspectFootwearService CharacterInspectFootwearService { get; }
    public PartyFeetRefreshService PartyFeetRefreshService { get; }
    public FootShowcaseService FootShowcaseService { get; }
    public bool SessionDebugUnlocked { get; private set; }

    private readonly MainWindow mainWindow;
    private readonly ConfigWindow configWindow;
    private IDtrBarEntry? dtrEntry;
    private CharacterInspectResearchSnapshot? cachedCharacterInspectDebugSnapshot;
    private CommendationPortraitResearchSnapshot? cachedCommendationDebugSnapshot;

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        ApplyConfigurationMigrations(Configuration);
        PartyShowcaseService = new PartyShowcaseService(ClientState, ObjectTable, PartyList);
        LodestoneProfileService = new LodestoneProfileService(Log, PluginInterface.GetPluginConfigDirectory());
        CommendationPortraitResearchService = new CommendationPortraitResearchService(GameGui, Configuration);
        CharacterInspectResearchService = new CharacterInspectResearchService(GameGui);
        CharacterInspectPreviewCaptureService = new CharacterInspectPreviewCaptureService(Log, Configuration, PluginInterface.GetPluginConfigDirectory());
        CharacterInspectPoseService = new CharacterInspectPoseService(Log);
        CharacterInspectFootwearService = new CharacterInspectFootwearService(Configuration, Log);
        PartyFeetRefreshService = new PartyFeetRefreshService(
            Log,
            CharacterInspectResearchService,
            CharacterInspectPreviewCaptureService,
            CharacterInspectPoseService,
            CharacterInspectFootwearService,
            PrintStatus,
            FormatDisplayName);
        FootShowcaseService = new FootShowcaseService();
        mainWindow = new MainWindow(this);
        configWindow = new ConfigWindow(this);

        WindowSystem.AddWindow(mainWindow);
        WindowSystem.AddWindow(configWindow);

        RegisterCommands();

        PluginInterface.UiBuilder.Draw += WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi += OpenConfigUi;
        PluginInterface.UiBuilder.OpenMainUi += OpenMainUi;
        Framework.Update += OnFrameworkUpdate;

        SetupDtrBar();
        UpdateDtrBar();

        if (Configuration.OpenMainWindowOnLoad)
            mainWindow.IsOpen = true;

        Log.Information("[footballer] Plugin loaded.");
    }

    public void Dispose()
    {
        Framework.Update -= OnFrameworkUpdate;
        PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi -= OpenConfigUi;
        PluginInterface.UiBuilder.OpenMainUi -= OpenMainUi;

        UnregisterCommands();
        WindowSystem.RemoveAllWindows();
        dtrEntry?.Remove();

        LodestoneProfileService.Dispose();
        configWindow.Dispose();
        mainWindow.Dispose();
    }

    public void OpenMainUi() => mainWindow.IsOpen = true;

    public void ToggleMainUi() => mainWindow.Toggle();

    public void OpenConfigUi() => configWindow.IsOpen = true;

    public void ToggleConfigUi() => configWindow.Toggle();

    public void SetPluginEnabled(bool enabled, bool printStatus = false)
    {
        Configuration.PluginEnabled = enabled;
        Configuration.Save();
        UpdateDtrBar();

        if (printStatus)
            PrintStatus(enabled ? "Plugin enabled." : "Plugin disabled.");
    }

    public void SetKrangleNames(bool enabled, bool printStatus = false)
    {
        Configuration.KrangleNames = enabled;
        Configuration.Save();
        KrangleService.ClearCache();

        if (printStatus)
            PrintStatus(enabled ? "Krangled labels enabled." : "Krangled labels disabled.");
    }

    public string FormatDisplayName(PartyShowcaseMember member)
        => member.IsLocalPlayer
            ? $"{FormatCharacterName(member.Name)} (You)"
            : FormatCharacterName(member.Name);

    public string FormatWorldName(string? worldName)
        => string.IsNullOrWhiteSpace(worldName)
            ? "No world yet"
            : Configuration.KrangleNames
                ? KrangleService.KrangleServer(worldName)
                : worldName;

    public string FormatCharacterName(string? characterName)
        => string.IsNullOrWhiteSpace(characterName)
            ? "-"
            : Configuration.KrangleNames
                ? KrangleService.KrangleName(characterName)
                : characterName;

    public void ResetWindowPositions()
    {
        mainWindow.QueueResetToOrigin();
        configWindow.QueueResetToOrigin();
        mainWindow.IsOpen = true;
        configWindow.IsOpen = true;
        PrintStatus("Queued both Footballer windows to reset to 1,1.");
    }

    public void JumpWindows()
    {
        mainWindow.QueueRandomVisibleJump();
        configWindow.QueueRandomVisibleJump();
        mainWindow.IsOpen = true;
        configWindow.IsOpen = true;
        PrintStatus("Queued a random visible jump for the Footballer windows.");
    }

    public void OpenUrl(string url)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[footballer] Failed to open URL.");
            PrintStatus($"Could not open link: {url}");
        }
    }

    public IReadOnlyList<PartyShowcaseMember> GetPartyShowcaseMembers()
        => PartyShowcaseService.CaptureCurrentPartySnapshot();

    public CharacterInspectResearchSnapshot? GetCachedCharacterInspectDebugSnapshot()
        => cachedCharacterInspectDebugSnapshot;

    public CommendationPortraitResearchSnapshot? GetCachedCommendationDebugSnapshot()
        => cachedCommendationDebugSnapshot;

    public CharacterInspectResearchSnapshot RefreshCharacterInspectDebugSnapshot()
    {
        cachedCharacterInspectDebugSnapshot = CharacterInspectResearchService.CaptureSnapshot();
        return cachedCharacterInspectDebugSnapshot;
    }

    public CommendationPortraitResearchSnapshot RefreshCommendationDebugSnapshot()
    {
        cachedCommendationDebugSnapshot = CommendationPortraitResearchService.CaptureSnapshot();
        return cachedCommendationDebugSnapshot;
    }

    public InspectPreviewCaptureRecord? GetInspectPreviewCapture(PartyShowcaseMember member)
        => CharacterInspectPreviewCaptureService.GetLatestCapture(member.CharacterKey);

    public InspectPreviewCaptureRecord? GetCurrentInspectPreviewCapture(
        IReadOnlyList<PartyShowcaseMember> members,
        CharacterInspectResearchSnapshot snapshot)
    {
        var captureKey = ResolveInspectPreviewCaptureKey(members, snapshot.CurrentEntityId);
        return captureKey is null
            ? null
            : CharacterInspectPreviewCaptureService.GetLatestCapture(captureKey);
    }

    public InspectPreviewCaptureResult CaptureCurrentInspectPreview(
        IReadOnlyList<PartyShowcaseMember> members)
    {
        var snapshot = CharacterInspectResearchService.CaptureSnapshot();
        if (SessionDebugUnlocked)
            cachedCharacterInspectDebugSnapshot = snapshot;

        var captureKey = ResolveInspectPreviewCaptureKey(members, snapshot.CurrentEntityId);
        if (captureKey is null)
        {
            return new InspectPreviewCaptureResult(
                false,
                "CharacterInspect has no current entity yet, so there is nothing to capture.",
                null);
        }

        var poseBlockReason = CharacterInspectPoseService.GetCaptureBlockReason(snapshot.CurrentEntityId);
        if (!string.IsNullOrWhiteSpace(poseBlockReason))
            return new InspectPreviewCaptureResult(false, poseBlockReason, null);

        var captureBlockReason = CharacterInspectFootwearService.GetCaptureBlockReason(snapshot.CurrentEntityId);
        if (!string.IsNullOrWhiteSpace(captureBlockReason))
            return new InspectPreviewCaptureResult(false, captureBlockReason, null);

        return CharacterInspectPreviewCaptureService.Capture(snapshot, captureKey, snapshot.CurrentEntityId);
    }

    public void OpenInspectPreviewCaptureFolder()
        => OpenUrl(CharacterInspectPreviewCaptureService.CaptureDirectory);

    public bool GetEffectiveRespectLodestonePrivacy()
        => !SessionDebugUnlocked || Configuration.RespectLodestonePrivacy;

    public void QueuePartyResearchRefresh(bool forceLodestone = false, bool refreshFeetCaptures = false)
    {
        var members = PartyShowcaseService.CaptureCurrentPartySnapshot();
        if (members.Count == 0)
        {
            PrintStatus("No local player or party snapshot is available yet.");
            return;
        }

        LodestoneProfileService.QueueRefresh(members, forceLodestone);
        if (refreshFeetCaptures)
        {
            var result = PartyFeetRefreshService.QueueRefresh(members);
            if (result.QueuedCount > 0)
            {
                var message = forceLodestone
                    ? $"Queued Lodestone face refresh plus automatic feet recapture for {result.QueuedCount} party member(s)."
                    : $"Queued missing Lodestone lookups plus automatic feet recapture for {result.QueuedCount} party member(s).";
                if (result.SkippedCount > 0)
                    message += $" Skipped {result.SkippedCount} member(s) without live entity ids.";

                PrintStatus(message);
                return;
            }

            PrintStatus(forceLodestone
                ? "Queued Lodestone face refresh, but no live party members were available for automatic feet recapture."
                : "Queued missing Lodestone lookups, but no live party members were available for automatic feet recapture.");
            return;
        }

        PrintStatus(forceLodestone
            ? "Queued Lodestone face refresh for the current party snapshot."
            : "Queued missing Lodestone lookups for the current party snapshot.");
    }

    public void PrintStatus(string message) => ChatGui.Print($"[{PluginInfo.DisplayName}] {message}");

    public unsafe void OpenCharacterInspect(PartyShowcaseMember member)
    {
        var displayName = FormatCharacterName(member.Name);
        if (member.EntityId == 0)
        {
            PrintStatus($"Cannot inspect {displayName} yet because no live entity id is available.");
            return;
        }

        var agent = AgentInspect.Instance();
        if (agent == null)
        {
            PrintStatus("AgentInspect is unavailable right now.");
            return;
        }

        agent->ExamineCharacter(member.EntityId);
        CharacterInspectPoseService.QueueForInspectRequest(member.EntityId, displayName);
        CharacterInspectFootwearService.QueueForInspectRequest(member.EntityId, displayName);
        PrintStatus($"Requested CharacterInspect for {displayName}.");
    }

    public void HandleWithoutFootwearChanged()
        => CharacterInspectFootwearService.HandleModeChanged();

    public void UpdateDtrBar()
    {
        if (dtrEntry == null)
            return;

        dtrEntry.Shown = Configuration.DtrBarEnabled;
        if (!Configuration.DtrBarEnabled)
            return;

        var glyph = Configuration.PluginEnabled ? Configuration.DtrIconEnabled : Configuration.DtrIconDisabled;
        var state = Configuration.PluginEnabled ? "On" : "Off";
        dtrEntry.Text = Configuration.DtrBarMode switch
        {
            1 => new SeString(new TextPayload($"{glyph} FOOT")),
            2 => new SeString(new TextPayload(glyph)),
            _ => new SeString(new TextPayload($"FOOT: {state}")),
        };
        dtrEntry.Tooltip = new SeString(new TextPayload($"{PluginInfo.DisplayName} {state}. Click to toggle."));
    }

    private void RegisterCommands()
    {
        var helpMessage =
            $"Open {PluginInfo.DisplayName}. Use {PluginInfo.Command} config for settings, {PluginInfo.Command} ws to reset window positions, {PluginInfo.Command} j to jump the windows, or /foot as the short alias.";

        CommandManager.AddHandler(PluginInfo.Command, new CommandInfo(OnCommand) { HelpMessage = helpMessage });
        foreach (var alias in PluginInfo.CommandAliases)
            CommandManager.AddHandler(alias, new CommandInfo(OnCommand) { HelpMessage = helpMessage });
    }

    private void UnregisterCommands()
    {
        CommandManager.RemoveHandler(PluginInfo.Command);
        foreach (var alias in PluginInfo.CommandAliases)
            CommandManager.RemoveHandler(alias);
    }

    private void OnCommand(string command, string arguments)
    {
        var trimmed = arguments.Trim();
        if (trimmed.Equals("config", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("settings", StringComparison.OrdinalIgnoreCase))
        {
            OpenConfigUi();
            return;
        }

        if (trimmed.Equals("on", StringComparison.OrdinalIgnoreCase))
        {
            SetPluginEnabled(true, printStatus: true);
            return;
        }

        if (trimmed.Equals("off", StringComparison.OrdinalIgnoreCase))
        {
            SetPluginEnabled(false, printStatus: true);
            return;
        }

        if (trimmed.Equals("status", StringComparison.OrdinalIgnoreCase))
        {
            PrintStatus(Configuration.PluginEnabled ? "Shell is enabled." : "Shell is disabled.");
            return;
        }

        if (trimmed.Equals("debug", StringComparison.OrdinalIgnoreCase))
        {
            SessionDebugUnlocked = !SessionDebugUnlocked;
            if (SessionDebugUnlocked)
            {
                RefreshCharacterInspectDebugSnapshot();
                RefreshCommendationDebugSnapshot();
            }
            else
            {
                cachedCharacterInspectDebugSnapshot = null;
                cachedCommendationDebugSnapshot = null;
            }

            PrintStatus(SessionDebugUnlocked
                ? "Session debug controls enabled. Hidden research surfaces are now visible in the main window and settings."
                : "Session debug controls disabled. Hidden research surfaces are concealed again.");
            OpenConfigUi();
            return;
        }

        if (trimmed.Equals("ws", StringComparison.OrdinalIgnoreCase))
        {
            ResetWindowPositions();
            return;
        }

        if (trimmed.Equals("j", StringComparison.OrdinalIgnoreCase))
        {
            JumpWindows();
            return;
        }

        ToggleMainUi();
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        UpdateDtrBar();
        CharacterInspectPoseService.OnFrameworkUpdate();
        CharacterInspectFootwearService.OnFrameworkUpdate();
        PartyFeetRefreshService.OnFrameworkUpdate();
    }

    private void SetupDtrBar()
    {
        dtrEntry = DtrBar.Get(PluginInfo.DisplayName);
        dtrEntry.OnClick = _ => SetPluginEnabled(!Configuration.PluginEnabled, printStatus: true);
    }

    private static void ApplyConfigurationMigrations(Configuration configuration)
    {
        var changed = false;

        if (configuration.Version < 3)
        {
            configuration.InspectPreviewTopTrimFraction = footballer.Configuration.DefaultInspectPreviewTopTrimFraction;
            configuration.InspectPreviewBottomTrimFraction = footballer.Configuration.DefaultInspectPreviewBottomTrimFraction;
            configuration.Version = 3;
            changed = true;
        }

        if (!float.IsFinite(configuration.InspectPreviewTopTrimFraction))
        {
            configuration.InspectPreviewTopTrimFraction = footballer.Configuration.DefaultInspectPreviewTopTrimFraction;
            changed = true;
        }

        if (!float.IsFinite(configuration.InspectPreviewBottomTrimFraction))
        {
            configuration.InspectPreviewBottomTrimFraction = footballer.Configuration.DefaultInspectPreviewBottomTrimFraction;
            changed = true;
        }

        if (changed)
            configuration.Save();
    }

    private static string? ResolveInspectPreviewCaptureKey(
        IReadOnlyList<PartyShowcaseMember> members,
        uint currentEntityId)
    {
        if (currentEntityId == 0)
            return null;

        var member = members.FirstOrDefault(candidate => candidate.EntityId != 0 && candidate.EntityId == currentEntityId);
        return member?.CharacterKey ?? $"entity-{currentEntityId:X8}";
    }
}
