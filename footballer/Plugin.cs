using System;
using System.Numerics;
using AethertekUI;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
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
    [PluginService] internal static IToastGui ToastGui { get; private set; } = null!;
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

    private FootballerFonts uiFonts = null!;
    private UiText uiText = null!;
    private MaterialTheme uiTheme = null!;
    private MaterialOptions<string> languageOptions = null!;
    private string appliedLanguage = "";
    private uint appliedAccent;
    private Vector3 accentDraft;
    private int checkedFontGeneration = -1;
    private bool fontIssueLogged;
    private readonly MainWindow mainWindow;
    private readonly ConfigWindow configWindow;
    private bool pendingOpenMainUi;
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
        ApplyAppearance();
        mainWindow = new MainWindow(this);
        configWindow = new ConfigWindow(this);

        WindowSystem.AddWindow(mainWindow);
        WindowSystem.AddWindow(configWindow);

        RegisterCommands();

        PluginInterface.UiBuilder.Draw += DrawUi;
        PluginInterface.UiBuilder.OpenConfigUi += OpenConfigUi;
        PluginInterface.UiBuilder.OpenMainUi += OpenMainUi;
        pendingOpenMainUi = Configuration.OpenMainWindowOnLoad;
        Framework.Update += OnFrameworkUpdate;

        SetupDtrBar();
        UpdateDtrBar();

        Log.Information("[footballer] Plugin loaded.");
    }

    public void Dispose()
    {
        pendingOpenMainUi = false;
        Framework.Update -= OnFrameworkUpdate;
        PluginInterface.UiBuilder.Draw -= DrawUi;
        PluginInterface.UiBuilder.OpenConfigUi -= OpenConfigUi;
        PluginInterface.UiBuilder.OpenMainUi -= OpenMainUi;

        UnregisterCommands();
        WindowSystem.RemoveAllWindows();
        dtrEntry?.Remove();

        LodestoneProfileService.Dispose();
        configWindow.Dispose();
        mainWindow.Dispose();
        uiFonts.Dispose();
        uiText.Dispose();
    }

    private void DrawUi()
    {
        ApplyAppearance();
        if(!mainWindow.IsOpen && !configWindow.IsOpen) return;
        using var text = uiText.Enter();
        if(!uiFonts.Ready)
        {
            if(!fontIssueLogged && uiFonts.LoadException is { } error) { Log.Error(error,"[footballer] Required UI fonts failed to load.");fontIssueLogged=true; }
            // Do not silently present temporary host fonts as the finished UI.
            DrawFontStatus(uiFonts.LoadException is null);
            return;
        }
        if(checkedFontGeneration!=uiFonts.Generation)
        {
            try
            {
                var generation=uiFonts.Generation;
                uiFonts.CheckGlyphs(uiText.RequiredText);
                checkedFontGeneration=generation;
            }
            catch(Exception ex) { if(!fontIssueLogged) { Log.Error(ex,"[footballer] Required UI glyph coverage failed.");fontIssueLogged=true; } DrawFontStatus(false);return; }
        }
        using var theme = MaterialTheme.Push(uiTheme, ImGuiHelpers.GlobalScale, MaterialStyleMode.ColorsOnly);
        using var geometry=new MaterialStyleScope();
        geometry.Style(Dalamud.Bindings.ImGui.ImGuiStyleVar.WindowPadding,new System.Numerics.Vector2((Configuration.UiCompact ? 12 : 20)*ImGuiHelpers.GlobalScale));
        if (Configuration.UiCompact)
        {
            geometry.Style(ImGuiStyleVar.ItemSpacing, new Vector2(8, 4) * ImGuiHelpers.GlobalScale);
            geometry.Style(ImGuiStyleVar.FramePadding, new Vector2(8, 3) * ImGuiHelpers.GlobalScale);
            geometry.Style(ImGuiStyleVar.CellPadding, new Vector2(6, 3) * ImGuiHelpers.GlobalScale);
        }
        geometry.Style(ImGuiStyleVar.FrameRounding, 4 * ImGuiHelpers.GlobalScale);
        geometry.Style(ImGuiStyleVar.ChildRounding, 4 * ImGuiHelpers.GlobalScale);
        using var body = uiFonts.Push(UiFontRole.Body);
        WindowSystem.Draw();
    }

    private static void DrawFontStatus(bool loading)
    {
        Dalamud.Bindings.ImGui.ImGui.SetNextWindowSize(new System.Numerics.Vector2(460f*ImGuiHelpers.GlobalScale,0f));
        if(Dalamud.Bindings.ImGui.ImGui.Begin("Footballer##FontStatus",Dalamud.Bindings.ImGui.ImGuiWindowFlags.AlwaysAutoResize))
            Dalamud.Bindings.ImGui.ImGui.TextWrapped(UiText.T(loading?"Loading UI fonts...":"UI fonts failed to load. See the plugin log."));
        Dalamud.Bindings.ImGui.ImGui.End();
    }

    private void ApplyAppearance()
    {
        var language=UiText.Languages.Any(l=>l.Code==Configuration.UiLanguage)?Configuration.UiLanguage:"en";
        if(language!=appliedLanguage)
        {
            uiFonts?.Dispose();
            uiText?.Dispose();
            uiText=new(language,role=>uiFonts!.Push(role));
            uiFonts=new(PluginInterface.UiBuilder.FontAtlas,uiText.GlyphRanges(),language);
            languageOptions=new(UiText.Languages.Select(l=>new MaterialOption<string>(l.Code,l.Code,l.Name)).ToArray());
            appliedLanguage=language;
            checkedFontGeneration=-1;
            fontIssueLogged=false;
        }
        if(uiTheme is null || (Configuration.UiAccentRgb & 0xFFFFFF)!=appliedAccent)
        {
            appliedAccent=Configuration.UiAccentRgb & 0xFFFFFF;
            uiTheme=FootballerPresentation.Theme(appliedAccent);
            var color=FootballerPresentation.Rgb(appliedAccent);
            accentDraft=new(color.X,color.Y,color.Z);
        }
        uiTheme.Density = Configuration.UiCompact ? MaterialDensity.Compact : MaterialDensity.Standard;
    }

    public void DrawAppearanceSelector()
    {
        var language=appliedLanguage;
        using var controls=MaterialControls.Push(FootballerPresentation.Controls(28,18));
        var changed=MaterialAppearanceSelector.Draw("appearance",ref accentDraft,ref language,languageOptions,
            new(UiText.T("Color"),UiText.T("Language"),UiText.T("Teal"),UiText.T("Blue"),UiText.T("Pink"),UiText.T("Custom RGB")), languageWidth: 140);
        if(changed.AccentChanged) Configuration.UiAccentRgb=((uint)Math.Clamp((int)MathF.Round(accentDraft.X*255),0,255)<<16)
            |((uint)Math.Clamp((int)MathF.Round(accentDraft.Y*255),0,255)<<8)|(uint)Math.Clamp((int)MathF.Round(accentDraft.Z*255),0,255);
        if(changed.LanguageChanged) Configuration.UiLanguage=language;
        if(changed.AccentChanged || changed.LanguageChanged) Configuration.Save();
    }

    public void OpenMainUi()
    {
        var wasClosed = !mainWindow.IsOpen;
        mainWindow.IsOpen = true;
        if (!wasClosed)
            return;

        ShowShowcaseGuidanceToast();
        if (Configuration.AutoRefreshPartyOnShowcaseOpen)
            QueuePartyResearchRefresh(forceLodestone: false, refreshFeetCaptures: true);
    }

    public void ToggleMainUi()
    {
        if (!mainWindow.IsOpen)
        {
            OpenMainUi();
            return;
        }

        mainWindow.Toggle();
    }

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
        var state = uiText.Label(Configuration.PluginEnabled ? "On" : "Off");
        dtrEntry.Text = Configuration.DtrBarMode switch
        {
            1 => new SeString(new TextPayload($"{glyph} FOOT")),
            2 => new SeString(new TextPayload(glyph)),
            _ => new SeString(new TextPayload($"FOOT: {state}")),
        };
        dtrEntry.Tooltip = new SeString(new TextPayload(uiText.Format("{0} {1}. Click to toggle.", PluginInfo.DisplayName, state)));
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
        if (pendingOpenMainUi)
        {
            pendingOpenMainUi = false;
            OpenMainUi();
        }

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

        if (configuration.Version < 4)
        {
            configuration.InspectPreviewWindowScalePercent = Configuration.DefaultInspectPreviewWindowScalePercent;
            configuration.InspectPreviewTopTrimFraction = footballer.Configuration.DefaultInspectPreviewTopTrimFraction;
            configuration.InspectPreviewBottomTrimFraction = footballer.Configuration.DefaultInspectPreviewBottomTrimFraction;
            configuration.Version = 4;
            changed = true;
        }

        var clampedScalePercent = Math.Clamp(configuration.InspectPreviewWindowScalePercent, 60, 200);
        if (configuration.InspectPreviewWindowScalePercent != clampedScalePercent)
        {
            configuration.InspectPreviewWindowScalePercent = clampedScalePercent;
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

    private void ShowShowcaseGuidanceToast()
    {
        var payload = new SeString(new TextPayload(uiText.Label("Pick the Scaling to match the window, and click refresh party when ready")));
        var showQuestMethod = ToastGui.GetType().GetMethod("ShowQuest", new[] { typeof(SeString) });
        if (showQuestMethod != null)
        {
            showQuestMethod.Invoke(ToastGui, new object[] { payload });
            return;
        }

        ToastGui.ShowNormal(payload);
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
