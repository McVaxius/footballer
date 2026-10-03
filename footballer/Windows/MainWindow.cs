using System;
using AethertekUI;
using System.IO;
using System.Numerics;
using System.Reflection;
using System.Text;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using footballer.Models;
using footballer.Services;

namespace footballer.Windows;

public sealed partial class MainWindow : PositionedWindow, IDisposable
{
    private static readonly int[] PreviewScalePercents = { 60, 70, 80, 90, 100, 110, 120, 130, 140, 150, 160, 170, 180, 190, 200 };
    private readonly Plugin plugin;

    public MainWindow(Plugin plugin)
        : base($"{PluginInfo.DisplayName}##Main")
    {
        this.plugin = plugin;
        Size = new Vector2(1536, 1024);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(820f, 640f),
            MaximumSize = new Vector2(1900f, 1300f),
        };
    }

    public void Dispose()
    {
    }

    public override void Draw()
    {
        var cfg = plugin.Configuration;
        var showDebug = plugin.SessionDebugUnlocked;
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0.0";
        var partyMembers = plugin.GetPartyShowcaseMembers();
        var effectiveRespectPrivacy = plugin.GetEffectiveRespectLodestonePrivacy();
        var cropFractions = plugin.CharacterInspectPreviewCaptureService.GetConfiguredCropFractions();
        var scalePercent = plugin.CharacterInspectPreviewCaptureService.GetConfiguredScalePercent();
        var footCards = plugin.FootShowcaseService.BuildCards(
            partyMembers,
            plugin.LodestoneProfileService,
            cfg,
            effectiveRespectPrivacy,
            plugin.CharacterInspectPreviewCaptureService);

        if (!showDebug)
        {
            DrawPresentation(cfg, scalePercent, footCards);
            FinalizePendingWindowPlacement();
            return;
        }

        UiGui.Text(UiText.F($"{PluginInfo.DisplayName} v{version}"));
        ImGui.SameLine();
        if (UiGui.SmallButton("Ko-fi"))
            plugin.OpenUrl(PluginInfo.SupportUrl);
        ImGui.SameLine();
        if (UiGui.SmallButton("Discord"))
            plugin.OpenUrl(PluginInfo.DiscordUrl);

        ImGui.Separator();

        if (showDebug)
            DrawDebugToolbar(cfg, scalePercent);
        else
            DrawNormalToolbar(cfg, scalePercent);

        if (showDebug)
        {
            UiGui.TextWrapped("Inspect and capture tools are available in debug mode.");
            UiGui.TextWrapped("Feet are hidden when Lodestone does not expose a face.");
            UiGui.TextWrapped(PluginInfo.DiscordFeedbackNote);
            UiGui.TextColored(new Vector4(0.98f, 0.73f, 0.40f, 1f), "Session debug mode is active. Hidden research surfaces are visible below.");

            ImGui.Separator();
            UiGui.TextUnformatted("Live Today");
            foreach (var item in PluginInfo.LiveToday)
                UiGui.BulletText(item);

            ImGui.Separator();
            UiGui.TextUnformatted("Current Defaults");
            UiGui.BulletText(UiText.F($"Krangle labels: {(cfg.KrangleNames ? "Yes" : "No")}"));
            UiGui.BulletText(UiText.F($"Preview scaling: {scalePercent}%"));
            UiGui.BulletText(UiText.F($"Auto refresh party on showcase open: {(cfg.AutoRefreshPartyOnShowcaseOpen ? "Yes" : "No")}"));
            UiGui.BulletText(UiText.F($"Show male feet: {(cfg.ShowMaleFeet ? "Yes" : "No")}"));
            UiGui.BulletText(UiText.F($"Show female feet: {(cfg.ShowFemaleFeet ? "Yes" : "No")}"));
            UiGui.BulletText(UiText.F($"Without footwear: {(cfg.WithoutFootwear ? "Yes" : "No")}"));
            UiGui.BulletText(UiText.F($"Show own feet: {(cfg.ShowOwnFeet ? "Yes" : "No")}"));
            UiGui.BulletText(UiText.F($"Replace party portrait window pictures: {(cfg.ReplaceCommendationPictures ? "Yes" : "No")}"));
            UiGui.BulletText(UiText.F($"Show face next to feet: {(cfg.ShowFaceNextToFeet ? "Yes" : "No")}"));
            UiGui.BulletText(plugin.SessionDebugUnlocked
                ? UiText.F($"Lodestone privacy gate (debug): {(effectiveRespectPrivacy ? "Forced / On" : "Override Off")}")
                : "Lodestone privacy gate: Forced on");

            ImGui.Separator();
            UiGui.TextUnformatted("Live Party Foot Showcase");
            UiGui.TextWrapped("Inspect and capture tools are available in debug mode.");
        }

        if (footCards.Count == 0)
        {
            UiGui.TextWrapped("No local player or party members are available yet, so there is nothing to render in the live showcase.");
        }
        else
        {
            var showcaseColumns = GetFootShowcaseColumnCount(footCards.Count, showDebug);
            if (ImGui.BeginTable("FootballerFootShowcaseTable", showcaseColumns, ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.SizingStretchSame))
            {
                for (var i = 0; i < footCards.Count; i++)
                {
                    if (i % showcaseColumns == 0)
                        ImGui.TableNextRow();

                    ImGui.TableSetColumnIndex(i % showcaseColumns);
                    DrawFootShowcaseCard(footCards[i], showDebug);
                }

                ImGui.EndTable();
            }
        }

        if (showDebug)
        {
            ImGui.Separator();
            if (UiGui.CollapsingHeader("Party Showcase Model"))
            {
                UiGui.TextWrapped("Party snapshot, Lodestone lookup and privacy state.");

                if (partyMembers.Count == 0)
                {
                    UiGui.TextWrapped("No local player or party members are available yet.");
                }
                else if (ImGui.BeginTable("FootballerPartyShowcaseTable", 7, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY | ImGuiTableFlags.SizingFixedFit, new Vector2(-1f, 240f)))
                {
                    ImGui.TableSetupColumn("Slot");
                    ImGui.TableSetupColumn("Character", ImGuiTableColumnFlags.WidthStretch);
                    ImGui.TableSetupColumn("Job");
                    ImGui.TableSetupColumn("CID");
                    ImGui.TableSetupColumn("Lodestone", ImGuiTableColumnFlags.WidthStretch);
                    ImGui.TableSetupColumn("Feet");
                    ImGui.TableSetupColumn("Links");
                    UiGui.TableHeadersRow();

                    foreach (var member in partyMembers)
                    {
                        var lookup = plugin.LodestoneProfileService.GetRecord(member.Name, member.WorldName);
                        var feetGateLabel = LodestoneProfileService.GetFeetGateLabel(lookup, effectiveRespectPrivacy);

                        ImGui.TableNextRow();

                        ImGui.TableNextColumn();
                        UiGui.TextUnformatted(member.Slot.ToString(UiText.Current.Culture));

                        ImGui.TableNextColumn();
                        UiGui.TextUnformatted(GetSafeDisplayName(member));
                        UiGui.TextDisabled(GetSafeWorldLabel(member));

                        ImGui.TableNextColumn();
                        UiGui.TextUnformatted(UiText.F($"{member.JobAbbreviation} {member.Level}"));

                        ImGui.TableNextColumn();
                        UiGui.TextUnformatted(string.IsNullOrWhiteSpace(member.ContentId) ? "-" : member.ContentIdShort);

                        ImGui.TableNextColumn();
                        DrawStatusText(LodestoneProfileService.GetStatusLabel(lookup.Status), lookup.Status);
                        if (!string.IsNullOrWhiteSpace(lookup.Note))
                            UiGui.TextWrapped(lookup.Note);

                        ImGui.TableNextColumn();
                        DrawFeetGateText(feetGateLabel);

                        ImGui.TableNextColumn();
                        DrawInspectButton(member, $"PartyTable{member.CharacterKey}");

                        ImGui.SameLine();
                        if (UiGui.SmallButton($"Search##{member.CharacterKey}"))
                            plugin.OpenUrl(lookup.SearchUrl);

                        if (!string.IsNullOrWhiteSpace(lookup.ProfileUrl))
                        {
                            ImGui.SameLine();
                            if (UiGui.SmallButton($"Profile##{member.CharacterKey}"))
                                plugin.OpenUrl(lookup.ProfileUrl!);
                        }

                        ImGui.SameLine();
                        if (UiGui.SmallButton($"Retry##{member.CharacterKey}"))
                            plugin.LodestoneProfileService.EnsureLookup(member.Name, member.WorldName, force: true);
                    }

                    ImGui.EndTable();
                }
            }

            ImGui.Separator();
            UiGui.TextUnformatted("Inspect Capture");
            UiGui.TextWrapped("Use Inspect, wait for a stable preview, then Capture Current Preview. Refresh party captures each member after a two-second hold.");
            UiGui.BulletText(UiText.F($"Party feet refresh: {plugin.PartyFeetRefreshService.LastStatus}"));
            UiGui.BulletText(UiText.F($"Pose preset: {plugin.CharacterInspectPoseService.LastStatus}"));
            UiGui.BulletText(UiText.F($"Barefoot apply: {plugin.CharacterInspectFootwearService.LastStatus}"));
            UiGui.BulletText(UiText.F($"Preview snip: {plugin.CharacterInspectPreviewCaptureService.LastCaptureStatus}"));

            if (UiGui.SmallButton("Capture Current Preview"))
            {
                var result = plugin.CaptureCurrentInspectPreview(partyMembers);
                plugin.PrintStatus(result.Status);
            }
            ImGui.SameLine();
            if (UiGui.SmallButton("Capture Folder"))
                plugin.OpenInspectPreviewCaptureFolder();
            ImGui.SameLine();
            UiGui.TextDisabled("Stored crop defaults: 65% top / 20% bottom.");

            DrawDebugResearchSections(partyMembers, cropFractions, scalePercent);

            ImGui.Separator();
            UiGui.TextUnformatted("Research Notes");
            foreach (var item in PluginInfo.Concept)
                UiGui.BulletText(item);

            ImGui.Separator();
            UiGui.TextUnformatted("Active Services");
            foreach (var item in PluginInfo.Services)
                UiGui.BulletText(item);

            ImGui.Separator();
            UiGui.TextUnformatted("Retest Focus");
            foreach (var item in PluginInfo.Tests)
                UiGui.BulletText(item);
        }

        FinalizePendingWindowPlacement();
    }

    private void DrawNormalToolbar(Configuration cfg, int scalePercent)
    {
        using var font = UiText.Font(UiFontRole.Action);
        var active = plugin.PartyFeetRefreshService.IsActive;
        var enabled = cfg.PluginEnabled;
        if (UiGui.Toggle("Enabled", ref enabled)) plugin.SetPluginEnabled(enabled, printStatus: true);
        ToolbarNext(active ? "Refreshing..." : "Refresh party", icon: MaterialIcon.Refresh);
        if (UiGui.Action(active ? "Refreshing..." : "Refresh party", active ? "Refreshing..." : "Refresh party", MaterialIcon.Refresh, active))
            plugin.QueuePartyResearchRefresh(forceLodestone: false, refreshFeetCaptures: true);
        ToolbarNext("Foot showcase", toggle: true);
        var showcase = cfg.ShowFootShowcase;
        if (UiGui.Toggle("Foot showcase", ref showcase)) { cfg.ShowFootShowcase = showcase; cfg.Save(); }
        ToolbarNext("Without footwear", toggle: true);
        var withoutFootwear = cfg.WithoutFootwear;
        if (UiGui.Toggle("Without footwear", ref withoutFootwear))
        {
            cfg.WithoutFootwear = withoutFootwear;
            cfg.Save();
            plugin.HandleWithoutFootwearChanged();
        }
        ToolbarNext("Settings", icon: MaterialIcon.Settings);
        if (UiGui.Action("Settings", "Settings", MaterialIcon.Settings)) plugin.OpenConfigUi();
        var krangle = cfg.KrangleNames ? "Un-Krangle" : "Krangle Names";
        ToolbarNext(krangle);
        if (UiGui.Action(krangle, krangle)) plugin.SetKrangleNames(!cfg.KrangleNames, printStatus: true);
        ToolbarNext("Refresh Lodestone", icon: MaterialIcon.Refresh);
        if (UiGui.Action("Refresh Lodestone", "Refresh Lodestone", MaterialIcon.Refresh, active)) plugin.QueuePartyResearchRefresh(forceLodestone: true);
        ToolbarNext("Scaling 100%", icon: MaterialIcon.Refresh);
        DrawPreviewScalingSelector(scalePercent, active);
    }

    private void DrawDebugToolbar(Configuration cfg, int scalePercent)
    {
        var enabled = cfg.PluginEnabled;
        if (UiGui.Checkbox("Enabled", ref enabled))
            plugin.SetPluginEnabled(enabled, printStatus: true);

        ImGui.SameLine();
        var showcase = cfg.ShowFootShowcase;
        if (UiGui.Checkbox("Foot showcase", ref showcase))
        {
            cfg.ShowFootShowcase = showcase;
            cfg.Save();
        }

        ImGui.SameLine();
        var withoutFootwear = cfg.WithoutFootwear;
        if (UiGui.Checkbox("Without footwear", ref withoutFootwear))
        {
            cfg.WithoutFootwear = withoutFootwear;
            cfg.Save();
            plugin.HandleWithoutFootwearChanged();
        }

        ImGui.SameLine();
        if (UiGui.SmallButton("Settings"))
            plugin.OpenConfigUi();

        ImGui.SameLine();
        var krangleNames = cfg.KrangleNames;
        if (UiGui.SmallButton(krangleNames ? "Un-Krangle" : "Krangle Names"))
            plugin.SetKrangleNames(!krangleNames, printStatus: true);

        ImGui.SameLine();
        if (UiGui.SmallButton("Refresh party"))
            plugin.QueuePartyResearchRefresh(forceLodestone: false, refreshFeetCaptures: true);

        ImGui.SameLine();
        if (UiGui.SmallButton("Refresh Lodestone"))
            plugin.QueuePartyResearchRefresh(forceLodestone: true);

        ImGui.SameLine();
        DrawPreviewScalingSelector(scalePercent);
    }

    private void DrawDebugResearchSections(
        IReadOnlyList<PartyShowcaseMember> partyMembers,
        (float TopTrimFraction, float BottomTrimFraction) cropFractions,
        int scalePercent)
    {
        var inspectSnapshot = plugin.GetCachedCharacterInspectDebugSnapshot();
        var currentInspectCapture = inspectSnapshot is null
            ? null
            : plugin.GetCurrentInspectPreviewCapture(partyMembers, inspectSnapshot);
        var commendationSnapshot = plugin.GetCachedCommendationDebugSnapshot();

        ImGui.Separator();
        UiGui.TextUnformatted("Character Inspect Research");
        UiGui.TextWrapped("Debug snapshots refresh on demand.");

        if (UiGui.SmallButton("Refresh Inspect Debug"))
        {
            inspectSnapshot = plugin.RefreshCharacterInspectDebugSnapshot();
            currentInspectCapture = plugin.GetCurrentInspectPreviewCapture(partyMembers, inspectSnapshot);
        }

        ImGui.SameLine();
        if (UiGui.SmallButton("COPY INSPECT REPORT"))
        {
            inspectSnapshot = plugin.RefreshCharacterInspectDebugSnapshot();
            currentInspectCapture = plugin.GetCurrentInspectPreviewCapture(partyMembers, inspectSnapshot);
            ImGui.SetClipboardText(BuildCharacterInspectReport(
                inspectSnapshot,
                currentInspectCapture,
                cropFractions,
                scalePercent,
                plugin.CharacterInspectPoseService.LastStatus,
                plugin.CharacterInspectFootwearService.LastStatus,
                plugin.CharacterInspectPreviewCaptureService.LastCaptureStatus));
            plugin.PrintStatus(BuildInspectReportClipboardStatus(inspectSnapshot));
        }

        ImGui.SameLine();
        UiGui.TextDisabled(inspectSnapshot is null
            ? "Refresh Inspect Debug to populate the snapshot and report."
            : GetInspectReportUsageNote(inspectSnapshot));

        var topTrimPercent = cropFractions.TopTrimFraction * 100f;
        if (UiGui.SliderFloat("Snip top trim %", ref topTrimPercent, 0f, 75f, "%.0f%%"))
        {
            plugin.Configuration.InspectPreviewTopTrimFraction = ClampCropFraction(topTrimPercent / 100f);
            plugin.Configuration.Save();
        }

        var bottomTrimPercent = cropFractions.BottomTrimFraction * 100f;
        if (UiGui.SliderFloat("Snip bottom trim %", ref bottomTrimPercent, 0f, 50f, "%.0f%%"))
        {
            plugin.Configuration.InspectPreviewBottomTrimFraction = ClampCropFraction(bottomTrimPercent / 100f);
            plugin.Configuration.Save();
        }

        if (UiGui.SmallButton("Reset snip profile"))
        {
            plugin.Configuration.InspectPreviewTopTrimFraction = Configuration.DefaultInspectPreviewTopTrimFraction;
            plugin.Configuration.InspectPreviewBottomTrimFraction = Configuration.DefaultInspectPreviewBottomTrimFraction;
            plugin.Configuration.Save();
        }
        ImGui.SameLine();
        UiGui.TextDisabled("Saved globally for future targets.");

        if (inspectSnapshot is null)
        {
            UiGui.TextWrapped("No cached CharacterInspect debug snapshot is available yet.");
        }
        else
        {
            UiGui.BulletText(inspectSnapshot.SafeStatus);
            UiGui.BulletText(inspectSnapshot.NextResearchStep);

            if (ImGui.BeginTable("FootballerCharacterInspectTable", 2, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingFixedFit))
            {
                ImGui.TableSetupColumn("Field");
                ImGui.TableSetupColumn("Value", ImGuiTableColumnFlags.WidthStretch);
                UiGui.TableHeadersRow();

                DrawInspectRow("Agent available", inspectSnapshot.AgentAvailable ? "Yes" : "No");
                DrawInspectRow("Agent address", FormatAddress(inspectSnapshot.AgentAddress));
                DrawInspectRow("Addon visible", inspectSnapshot.AddonVisible ? "Yes" : "No");
                DrawInspectRow("Addon address", FormatAddress(inspectSnapshot.AddonAddress));
                DrawInspectRow("Addon position", inspectSnapshot.AddonAddress == nint.Zero
                    ? "-"
                    : $"({inspectSnapshot.AddonX:0.##}, {inspectSnapshot.AddonY:0.##})");
                DrawInspectRow("Requested entity", FormatEntityId(inspectSnapshot.RequestedEntityId));
                DrawInspectRow("Current entity", FormatEntityId(inspectSnapshot.CurrentEntityId));
                DrawInspectRow("Fetch status", UiText.F($"{inspectSnapshot.FetchCharacterDataStatus} / {inspectSnapshot.FetchSearchCommentStatus} / {inspectSnapshot.FetchFreeCompanyStatus}"));
                DrawInspectRow("Buddy inspect", inspectSnapshot.IsBuddyInspect ? "Yes" : "No");
                DrawInspectRow("CharaView state", inspectSnapshot.CharaViewState.ToString(UiText.Current.Culture));
                DrawInspectRow("Client object", UiText.F($"{inspectSnapshot.CharaViewClientObjectId} / {inspectSnapshot.CharaViewClientObjectIndex}"));
                DrawInspectRow("Loaded / copied", UiText.F($"{(inspectSnapshot.CharaViewCharacterLoaded ? "Y" : "N")} / {(inspectSnapshot.CharaViewCharacterDataCopied ? "Y" : "N")}"));
                DrawInspectRow("Zoom ratio", inspectSnapshot.CharaViewZoomRatio.ToString("0.###", UiText.Current.Culture));
                DrawInspectRow("Preview character", FormatAddress(inspectSnapshot.PreviewCharacterAddress));
                DrawInspectRow("Preview draw object", FormatAddress(inspectSnapshot.PreviewDrawObjectAddress));
                DrawInspectRow("Preview game position", inspectSnapshot.PreviewGameObjectPosition);
                DrawInspectRow("Preview game rotation", inspectSnapshot.PreviewGameObjectRotation);
                DrawInspectRow("Preview draw position", inspectSnapshot.PreviewDrawObjectPosition);
                DrawInspectRow("Preview draw rotation", inspectSnapshot.PreviewDrawObjectRotation);
                DrawInspectRow("Snapshot captured", DateTime.TryParse(inspectSnapshot.SnapshotCapturedAt, out var capturedAt) ? capturedAt.ToString("G", UiText.Current.Culture) : inspectSnapshot.SnapshotCapturedAt);
                DrawInspectRow("Scene camera manager", FormatAddress(inspectSnapshot.CameraManagerAddress));
                DrawInspectRow("Scene manager index", inspectSnapshot.CameraManagerIndex < 0 ? "-" : inspectSnapshot.CameraManagerIndex.ToString(UiText.Current.Culture));
                DrawInspectRow("Raw scene camera", FormatAddress(inspectSnapshot.RawSceneCameraAddress));
                DrawInspectRow("Manager current camera", FormatAddress(inspectSnapshot.ManagerCurrentCameraAddress));
                DrawInspectRow("Active camera", FormatAddress(inspectSnapshot.CameraAddress));
                DrawInspectRow("Camera position", inspectSnapshot.CameraPosition);
                DrawInspectRow("Camera look-at", inspectSnapshot.CameraLookAt);
                DrawInspectRow("Derived yaw / pitch", inspectSnapshot.CameraRotation);
                DrawInspectRow("Camera distance", inspectSnapshot.CameraDistance);
                DrawInspectRow("Camera FoV", inspectSnapshot.CameraFoV);
                DrawInspectRow("Camera status", inspectSnapshot.CameraSnapshotStatus);
                DrawInspectRow("Preview component", FormatAddress(inspectSnapshot.PreviewComponentAddress));
                DrawInspectRow("Preview node", FormatAddress(inspectSnapshot.PreviewNodeAddress));
                DrawInspectRow("Preview bounds", inspectSnapshot.PreviewNodeAddress == nint.Zero
                    ? "-"
                    : $"({inspectSnapshot.PreviewNodeX:0.##}, {inspectSnapshot.PreviewNodeY:0.##}) {inspectSnapshot.PreviewNodeWidth}x{inspectSnapshot.PreviewNodeHeight}");
                DrawInspectRow("Preview node scale", inspectSnapshot.PreviewNodeAddress == nint.Zero
                    ? "-"
                    : $"X {inspectSnapshot.PreviewNodeScaleX:0.###} / Y {inspectSnapshot.PreviewNodeScaleY:0.###}");
                DrawInspectRow("Preview scaling fallback", UiText.F($"{scalePercent}%"));
                DrawInspectRow("Stored snip profile", UiText.F($"{cropFractions.TopTrimFraction:P0} top / {cropFractions.BottomTrimFraction:P0} bottom"));
                DrawInspectRow("Preview visible", inspectSnapshot.PreviewNodeVisible ? "Yes" : "No");
                DrawInspectRow("Collision node", FormatAddress(inspectSnapshot.CollisionNodeAddress));
                DrawInspectRow("Callback base id", inspectSnapshot.PreviewCallbackBaseId.ToString(UiText.Current.Culture));
                DrawInspectRow("Capture ready", inspectSnapshot.CaptureReady ? "Yes" : "No");
                DrawInspectRow("Capture status", inspectSnapshot.ActiveExportStatus);
                DrawInspectRow("Latest snip", currentInspectCapture is null ? "-" : Path.GetFileName(currentInspectCapture.FilePath));
                DrawInspectRow("Snip rect", currentInspectCapture is null
                    ? "-"
                    : UiText.F($"({currentInspectCapture.ClientX}, {currentInspectCapture.ClientY}) {currentInspectCapture.Width}x{currentInspectCapture.Height}"));
                DrawInspectRow("Snip action", plugin.CharacterInspectPreviewCaptureService.LastCaptureStatus);

                ImGui.EndTable();
            }

            DrawInspectExportPayload(inspectSnapshot, currentInspectCapture);
        }

        ImGui.Separator();
        UiGui.TextUnformatted("Party Portrait Window Research");
        UiGui.TextWrapped("Debug snapshots refresh on demand.");

        if (UiGui.SmallButton("Refresh Portrait Debug"))
            commendationSnapshot = plugin.RefreshCommendationDebugSnapshot();

        ImGui.SameLine();
        if (UiGui.SmallButton("COPY TEST REPORT"))
        {
            commendationSnapshot = plugin.RefreshCommendationDebugSnapshot();
            ImGui.SetClipboardText(BuildPortraitTestReport(commendationSnapshot));
            plugin.PrintStatus(BuildTestReportClipboardStatus(commendationSnapshot));
        }

        ImGui.SameLine();
        UiGui.TextDisabled(commendationSnapshot is null
            ? "Refresh Portrait Debug to populate the snapshot and report."
            : GetTestReportUsageNote(commendationSnapshot));

        if (commendationSnapshot is null)
        {
            UiGui.TextWrapped("No cached portrait debug snapshot is available yet.");
            return;
        }

        UiGui.BulletText(UiText.F($"Configured portrait replacement toggle: {(commendationSnapshot.ReplaceCommendationPicturesConfigured ? "On" : "Off")}"));
        UiGui.BulletText(commendationSnapshot.SafeStatus);
        UiGui.BulletText(commendationSnapshot.KnownCallbackSeam);
        UiGui.BulletText(commendationSnapshot.NextResearchStep);

        if (ImGui.BeginTable("FootballerCommendationProbeTable", 4, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingFixedFit))
        {
            ImGui.TableSetupColumn("Addon");
            ImGui.TableSetupColumn("Agent");
            ImGui.TableSetupColumn("Visible");
            ImGui.TableSetupColumn("Role", ImGuiTableColumnFlags.WidthStretch);
            UiGui.TableHeadersRow();

            foreach (var probe in commendationSnapshot.Probes)
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                UiGui.TextUnformatted(probe.AddonName);

                ImGui.TableNextColumn();
                UiGui.TextUnformatted(probe.AgentId?.ToString(UiText.Current.Culture) ?? "-");

                ImGui.TableNextColumn();
                if (probe.Visible)
                    UiGui.TextColored(new Vector4(0.55f, 0.93f, 0.61f, 1f), "Visible");
                else
                    UiGui.TextDisabled("Hidden");

                ImGui.TableNextColumn();
                UiGui.TextWrapped(probe.Role);
            }

            ImGui.EndTable();
        }

        var bannerPartySnapshot = commendationSnapshot.BannerPartySnapshot;
        var bannerPartyAgentSnapshot = commendationSnapshot.BannerPartyAgentSnapshot;
        if (bannerPartySnapshot is null && bannerPartyAgentSnapshot is null)
        {
            UiGui.TextWrapped("No cached BannerParty addon or agent snapshot is available yet.");
            return;
        }

        if (bannerPartyAgentSnapshot is not null)
        {
            ImGui.Separator();
            UiGui.TextUnformatted("Live BannerParty Agent");
            UiGui.TextWrapped(bannerPartyAgentSnapshot.CaptureStatus);
            UiGui.BulletText(UiText.F($"Agent address: {FormatAddress(bannerPartyAgentSnapshot.Address)}"));
            UiGui.BulletText(UiText.F($"Character count: {bannerPartyAgentSnapshot.CharacterCount}"));
            UiGui.BulletText(
                bannerPartyAgentSnapshot.ActiveCharacterRowIndex is int activeRow
                    ? UiText.F($"Active row heuristic: {activeRow} ({bannerPartyAgentSnapshot.ActiveCharacterName ?? "Unknown"})")
                    : "Active row heuristic: none yet");
            UiGui.BulletText(bannerPartyAgentSnapshot.ActiveExportStatus);

            DrawBannerPartyCharacterTable(bannerPartyAgentSnapshot);
            DrawRailMappingTable(bannerPartyAgentSnapshot);
            DrawActiveExportPayload(bannerPartyAgentSnapshot);
        }

        if (bannerPartySnapshot is null)
            return;

        ImGui.Separator();
        UiGui.TextUnformatted("Live BannerParty Addon Capture");
        UiGui.TextWrapped(bannerPartySnapshot.CaptureStatus);
        UiGui.BulletText(UiText.F($"Addon address: {FormatAddress(bannerPartySnapshot.Address)}"));
        UiGui.BulletText($"Addon position: ({bannerPartySnapshot.X:0.##}, {bannerPartySnapshot.Y:0.##})");
        UiGui.BulletText(UiText.F($"Node count: {bannerPartySnapshot.NodeCount}"));

        if (bannerPartySnapshot.LikelyPortraitSlots.Length > 0 &&
            ImGui.BeginTable("FootballerBannerPartySlotsTable", 8, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY | ImGuiTableFlags.SizingFixedFit, new Vector2(-1f, 170f)))
        {
            ImGui.TableSetupColumn("Slot");
            ImGui.TableSetupColumn("Base");
            ImGui.TableSetupColumn("Slider");
            ImGui.TableSetupColumn("Pos");
            ImGui.TableSetupColumn("Size");
            ImGui.TableSetupColumn("Visible");
            ImGui.TableSetupColumn("Interactive");
            ImGui.TableSetupColumn("Note", ImGuiTableColumnFlags.WidthStretch);
            UiGui.TableHeadersRow();

            foreach (var slot in bannerPartySnapshot.LikelyPortraitSlots)
            {
                ImGui.TableNextRow();

                ImGui.TableNextColumn();
                UiGui.TextUnformatted(slot.SlotIndex.ToString(UiText.Current.Culture));

                ImGui.TableNextColumn();
                UiGui.TextUnformatted(slot.BaseNodeId == 0 ? "-" : slot.BaseNodeId.ToString(UiText.Current.Culture));

                ImGui.TableNextColumn();
                UiGui.TextUnformatted(slot.SliderNodeId?.ToString(UiText.Current.Culture) ?? "-");

                ImGui.TableNextColumn();
                UiGui.TextUnformatted($"({slot.X:0.##}, {slot.Y:0.##})");

                ImGui.TableNextColumn();
                UiGui.TextUnformatted(UiText.F($"{slot.Width}x{slot.Height}"));

                ImGui.TableNextColumn();
                DrawBoolPill(slot.AnyVisible, "Yes", "No");

                ImGui.TableNextColumn();
                DrawBoolPill(slot.AnyInteractive, "Yes", "No");

                ImGui.TableNextColumn();
                UiGui.TextWrapped(slot.Note);
            }

            ImGui.EndTable();
        }

        if (!ImGui.BeginTable("FootballerBannerPartyNodeTable", 9, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY | ImGuiTableFlags.SizingFixedFit, new Vector2(-1f, 280f)))
            return;

        ImGui.TableSetupColumn("Idx");
        ImGui.TableSetupColumn("NodeId");
        ImGui.TableSetupColumn("Type");
        ImGui.TableSetupColumn("Visible");
        ImGui.TableSetupColumn("Pos");
        ImGui.TableSetupColumn("Size");
        ImGui.TableSetupColumn("Evt / Flags", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("Text");
        ImGui.TableSetupColumn("Addr");
        UiGui.TableHeadersRow();

        foreach (var node in bannerPartySnapshot.Nodes)
        {
            ImGui.TableNextRow();

            ImGui.TableNextColumn();
            UiGui.TextUnformatted(node.Index.ToString(UiText.Current.Culture));

            ImGui.TableNextColumn();
            UiGui.TextUnformatted(node.NodeId.ToString(UiText.Current.Culture));

            ImGui.TableNextColumn();
            UiGui.TextUnformatted(UiText.F($"{node.TypeName} ({node.RawType})"));

            ImGui.TableNextColumn();
            DrawBoolPill(node.Visible, "Yes", "No");

            ImGui.TableNextColumn();
            UiGui.TextUnformatted($"({node.X:0.##}, {node.Y:0.##})");

            ImGui.TableNextColumn();
            UiGui.TextUnformatted(UiText.F($"{node.Width}x{node.Height}"));

            ImGui.TableNextColumn();
            var eventSummary = node.EventCount > 0
                ? UiText.F($"{node.EventCount} ({node.FirstEventType ?? "?"})")
                : "0";
            var flagSummary = string.IsNullOrWhiteSpace(node.FlagsLabel)
                ? UiText.F($"0x{node.FlagsRaw:X4}")
                : UiText.F($"0x{node.FlagsRaw:X4} {node.FlagsLabel}");
            UiGui.TextWrapped(UiText.F($"{eventSummary} | {(node.AppearsInteractive ? "Interactive" : "Passive")} | {flagSummary}"));

            ImGui.TableNextColumn();
            UiGui.TextWrapped(node.Text ?? "-");

            ImGui.TableNextColumn();
            UiGui.TextUnformatted(FormatAddress(node.Address));
        }

        ImGui.EndTable();
    }

    private void DrawFootShowcaseCard(FootShowcaseCard card, bool showDebug)
    {
        var cardHeight = showDebug ? 390f : 264f;
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, showDebug ? new Vector2(8f, 8f) : new Vector2(4f, 4f));
        ImGui.BeginChild($"FootCard##{card.Member.CharacterKey}", new Vector2(0f, cardHeight), true);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, showDebug ? new Vector2(6f, 4f) : new Vector2(4f, 3f));

        UiGui.TextUnformatted(GetSafeDisplayName(card.Member));
        ShowHoverTooltip(UiText.F($"{GetSafeWorldLabel(card.Member)} | {card.Member.JobAbbreviation} {card.Member.Level}\n{card.VariantLabel}"));

        if (!showDebug)
        {
            UiGui.TextDisabled(UiText.F($"Feet: {card.FootStatusLabel}"));
            ShowHoverTooltip(card.FootStatusNote);
        }
        else
        {
            UiGui.TextDisabled(UiText.F($"{GetSafeWorldLabel(card.Member)} | {card.Member.JobAbbreviation} {card.Member.Level}"));
            ShowHoverTooltip(card.VariantLabel);

            ImGui.Spacing();
            DrawCardStatusBadge("Gate", card.FeetGateLabel, string.IsNullOrWhiteSpace(card.Lookup.Note) ? card.FootStatusNote : card.Lookup.Note);
            ImGui.SameLine();
            DrawCardStatusBadge("Face", card.FaceStatusLabel, card.FaceStatusNote);
            ImGui.SameLine();
            DrawCardStatusBadge("Feet", card.FootStatusLabel, card.FootStatusNote);
        }

        ImGui.Spacing();
        DrawFootShowcaseVisuals(card, showDebug);

        if (showDebug)
        {
            ImGui.Spacing();
            if (UiGui.SmallButton($"Search##FootCard{card.Member.CharacterKey}"))
                plugin.OpenUrl(card.Lookup.SearchUrl);
            ImGui.SameLine();
            DrawInspectButton(card.Member, UiText.F($"FootCard{card.Member.CharacterKey}"));

            if (!string.IsNullOrWhiteSpace(card.Lookup.ProfileUrl))
            {
                ImGui.SameLine();
                if (UiGui.SmallButton($"Profile##FootCard{card.Member.CharacterKey}"))
                    plugin.OpenUrl(card.Lookup.ProfileUrl!);
            }

            ImGui.SameLine();
            if (UiGui.SmallButton($"Retry##FootCard{card.Member.CharacterKey}"))
                plugin.LodestoneProfileService.EnsureLookup(card.Member.Name, card.Member.WorldName, force: true);
        }

        ImGui.PopStyleVar(2);
        ImGui.EndChild();
    }

    private static void DrawFootShowcaseVisuals(FootShowcaseCard card, bool showDebug)
    {
        if (!showDebug)
        {
            var drewFaceCompact = TryDrawLocalImage(card.FaceImagePath, new Vector2(92f, 92f));
            if (drewFaceCompact && !string.IsNullOrWhiteSpace(card.FootImagePath))
                ImGui.SameLine(0f, 4f);

            var drewFootCompact = TryDrawLocalImage(card.FootImagePath, new Vector2(250f, 235f));
            if (!drewFootCompact)
                UiGui.TextDisabled("No feet preview yet — match Scaling, then Refresh party.");

            return;
        }

        ImGui.BeginChild($"FootVisuals##{card.Member.CharacterKey}", new Vector2(0f, 255f), true);

        var drewFace = TryDrawLocalImage(card.FaceImagePath, new Vector2(92f, 92f));
        if (drewFace && !string.IsNullOrWhiteSpace(card.FootImagePath))
            ImGui.SameLine();

        var drewFoot = TryDrawLocalImage(card.FootImagePath, new Vector2(250f, 235f));
        if (!drewFace && !drewFoot)
        {
            if (showDebug)
                UiGui.TextWrapped("No eligible image. Faces come from Lodestone; feet need a saved CharacterInspect capture.");
            else
                UiGui.TextDisabled("No image yet.");
        }

        ImGui.EndChild();
    }

    private static int GetFootShowcaseColumnCount(int cardCount, bool showDebug)
    {
        if (cardCount <= 1)
            return Math.Max(cardCount, 1);

        var availableWidth = ImGui.GetContentRegionAvail().X;
        if (!float.IsFinite(availableWidth) || availableWidth <= 0f)
            return Math.Min(cardCount, showDebug ? 3 : 4);

        var targetCardWidth = showDebug ? 385f : 350f;
        var columns = (int)MathF.Floor((availableWidth + 12f) / targetCardWidth);
        columns = Math.Clamp(columns, 1, showDebug ? 3 : 4);
        return Math.Min(columns, cardCount);
    }

    private static void DrawCardStatusBadge(string label, string value, string? tooltip)
    {
        ImGui.TextUnformatted(UiText.T(label) + ":");
        ImGui.SameLine(0f, 4f);
        UiGui.TextColored(GetCardStatusColor(value), value);
        ShowHoverTooltip(tooltip);
    }

    private static void ShowHoverTooltip(string? text)
    {
        if (string.IsNullOrWhiteSpace(text) || !ImGui.IsItemHovered())
            return;

        ImGui.BeginTooltip();
        ImGui.PushTextWrapPos(420f * MaterialTheme.Metrics.Scale);
        UiGui.TextUnformatted(text);
        ImGui.PopTextWrapPos();
        ImGui.EndTooltip();
    }

    private static bool TryDrawLocalImage(string? imagePath, Vector2 maxSize)
    {
        if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
            return false;

        var wrap = Plugin.TextureProvider.GetFromFile(imagePath).GetWrapOrEmpty();
        if (wrap.Handle == nint.Zero || wrap.Width <= 0 || wrap.Height <= 0)
            return false;

        ImGui.Image(wrap.Handle, ScaleToFit(wrap.Size, maxSize));
        return true;
    }

    private static Vector2 ScaleToFit(Vector2 sourceSize, Vector2 maxSize)
    {
        if (sourceSize.X <= 0f || sourceSize.Y <= 0f)
            return maxSize;

        var widthScale = maxSize.X / sourceSize.X;
        var heightScale = maxSize.Y / sourceSize.Y;
        var scale = MathF.Min(widthScale, heightScale);
        if (float.IsNaN(scale) || float.IsInfinity(scale) || scale <= 0f)
            scale = 1f;

        return new Vector2(sourceSize.X * scale, sourceSize.Y * scale);
    }

    private static void DrawCardStatusLine(string label, string value)
    {
        ImGui.TextUnformatted(UiText.T(label) + ":");
        ImGui.SameLine();
        UiGui.TextColored(GetCardStatusColor(value), value);
    }

    private static void DrawStatusText(string label, LodestoneFaceLookupStatus status)
    {
        var color = status switch
        {
            LodestoneFaceLookupStatus.FaceAvailable => new Vector4(0.55f, 0.93f, 0.61f, 1f),
            LodestoneFaceLookupStatus.PrivacyHidden => new Vector4(0.98f, 0.73f, 0.40f, 1f),
            LodestoneFaceLookupStatus.Running => new Vector4(0.55f, 0.78f, 0.98f, 1f),
            LodestoneFaceLookupStatus.Error => new Vector4(0.96f, 0.45f, 0.45f, 1f),
            _ => new Vector4(0.82f, 0.82f, 0.82f, 1f),
        };

        UiGui.TextColored(color, label);
    }

    private static void DrawFeetGateText(string label)
    {
        var color = label switch
        {
            "Allowed" => new Vector4(0.55f, 0.93f, 0.61f, 1f),
            "Hidden" => new Vector4(0.98f, 0.73f, 0.40f, 1f),
            "Bypassed" => new Vector4(0.55f, 0.78f, 0.98f, 1f),
            _ => new Vector4(0.85f, 0.85f, 0.85f, 1f),
        };

        UiGui.TextColored(color, label);
    }

    private static void DrawBoolPill(bool value, string trueLabel, string falseLabel)
    {
        var color = value
            ? new Vector4(0.55f, 0.93f, 0.61f, 1f)
            : new Vector4(0.84f, 0.84f, 0.84f, 1f);
        UiGui.TextColored(color, value ? trueLabel : falseLabel);
    }

    private static Vector4 GetCardStatusColor(string value)
        => value switch
        {
            "Allowed" or "Visible" or "Inspect state recorded" or "Preview captured" => new Vector4(0.55f, 0.93f, 0.61f, 1f),
            "Bypassed" or "Refreshing" or "Inspect-ready" or "Inspect requested" or "Inspect active" => new Vector4(0.55f, 0.78f, 0.98f, 1f),
            "Hidden" or "Self hidden" or "Filtered" or "No face exposed" => new Vector4(0.98f, 0.73f, 0.40f, 1f),
            "Lookup error" => new Vector4(0.96f, 0.45f, 0.45f, 1f),
            _ => new Vector4(0.84f, 0.84f, 0.84f, 1f),
        };

    private static void DrawInspectExportPayload(
        CharacterInspectResearchSnapshot snapshot,
        InspectPreviewCaptureRecord? currentInspectCapture)
    {
        ImGui.Separator();
        UiGui.TextUnformatted("CharacterInspect Capture State");
        if (currentInspectCapture is not null)
        {
            UiGui.TextWrapped(UiText.F($"Latest saved preview snip: {Path.GetFileName(currentInspectCapture.FilePath)}"));
            if (TryDrawLocalImage(currentInspectCapture.FilePath, new Vector2(280f, 320f)))
                ImGui.Spacing();
        }

        if (snapshot.ActiveExportPayload is null)
        {
            UiGui.TextWrapped(snapshot.ActiveExportStatus);
            return;
        }

        var payload = snapshot.ActiveExportPayload;
        if (!ImGui.BeginTable("FootballerCharacterInspectPayloadTable", 2, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingFixedFit))
            return;

        ImGui.TableSetupColumn("Field");
        ImGui.TableSetupColumn("Value", ImGuiTableColumnFlags.WidthStretch);
        UiGui.TableHeadersRow();

        DrawPayloadRow("Camera position", payload.CameraPosition);
        DrawPayloadRow("Camera target", payload.CameraTarget);
        DrawPayloadRow("Image rotation", payload.ImageRotation.ToString(UiText.Current.Culture));
        DrawPayloadRow("Camera zoom", payload.CameraZoom.ToString(UiText.Current.Culture));
        DrawPayloadRow("Banner timeline", payload.BannerTimeline.ToString(UiText.Current.Culture));
        DrawPayloadRow("Animation progress", payload.AnimationProgress.ToString("0.###", UiText.Current.Culture));
        DrawPayloadRow("Expression", payload.Expression.ToString(UiText.Current.Culture));
        DrawPayloadRow("Head direction", payload.HeadDirection);
        DrawPayloadRow("Eye direction", payload.EyeDirection);
        DrawPayloadRow("Directional light color", payload.DirectionalLightingColor);
        DrawPayloadRow("Directional light brightness", payload.DirectionalLightingBrightness.ToString(UiText.Current.Culture));
        DrawPayloadRow("Directional light angles", UiText.F($"{payload.DirectionalLightingVerticalAngle} / {payload.DirectionalLightingHorizontalAngle}"));
        DrawPayloadRow("Ambient light color", payload.AmbientLightingColor);
        DrawPayloadRow("Ambient light brightness", payload.AmbientLightingBrightness.ToString(UiText.Current.Culture));
        DrawPayloadRow("Banner background", payload.BannerBg.ToString(UiText.Current.Culture));

        ImGui.EndTable();
    }

    private void DrawBannerPartyCharacterTable(BannerPartyAgentSnapshot snapshot)
    {
        if (snapshot.Characters.Length == 0)
            return;

        if (!ImGui.BeginTable("FootballerBannerPartyCharacterTable", 8, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY | ImGuiTableFlags.SizingFixedFit, new Vector2(-1f, 180f)))
            return;

        ImGui.TableSetupColumn("Row");
        ImGui.TableSetupColumn("Character", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("Job");
        ImGui.TableSetupColumn("World");
        ImGui.TableSetupColumn("Visible");
        ImGui.TableSetupColumn("Loaded");
        ImGui.TableSetupColumn("State / Pose");
        ImGui.TableSetupColumn("Hint", ImGuiTableColumnFlags.WidthStretch);
        UiGui.TableHeadersRow();

        foreach (var character in snapshot.Characters)
        {
            ImGui.TableNextRow();

            ImGui.TableNextColumn();
            UiGui.TextUnformatted(character.RowIndex.ToString(UiText.Current.Culture));

            ImGui.TableNextColumn();
            UiGui.TextUnformatted(FormatSafeCharacterName(character.Name));

            ImGui.TableNextColumn();
            UiGui.TextUnformatted(character.Job);

            ImGui.TableNextColumn();
            UiGui.TextUnformatted(character.WorldId == 0 ? "-" : character.WorldId.ToString(UiText.Current.Culture));

            ImGui.TableNextColumn();
            DrawBoolPill(character.CharacterVisible, "Yes", "No");

            ImGui.TableNextColumn();
            DrawBoolPill(character.CharacterLoaded, "Yes", "No");

            ImGui.TableNextColumn();
            UiGui.TextWrapped(UiText.F($"State {character.CharaViewState} | Pose {character.PoseClassJob} | CJ {character.PortraitClassJobId}"));

            ImGui.TableNextColumn();
            UiGui.TextWrapped(character.SelectionHint);
        }

        ImGui.EndTable();
    }

    private void DrawRailMappingTable(BannerPartyAgentSnapshot snapshot)
    {
        if (snapshot.RailMappings.Length == 0)
            return;

        if (!ImGui.BeginTable("FootballerBannerPartyMappingTable", 8, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY | ImGuiTableFlags.SizingFixedFit, new Vector2(-1f, 180f)))
            return;

        ImGui.TableSetupColumn("Rail");
        ImGui.TableSetupColumn("Base / Slider");
        ImGui.TableSetupColumn("Pos");
        ImGui.TableSetupColumn("Visible");
        ImGui.TableSetupColumn("Interactive");
        ImGui.TableSetupColumn("Agent Row");
        ImGui.TableSetupColumn("Character");
        ImGui.TableSetupColumn("Note", ImGuiTableColumnFlags.WidthStretch);
        UiGui.TableHeadersRow();

        foreach (var mapping in snapshot.RailMappings)
        {
            ImGui.TableNextRow();

            ImGui.TableNextColumn();
            if (mapping.LikelySelected)
                UiGui.TextColored(new Vector4(0.55f, 0.93f, 0.61f, 1f), mapping.RailOrder.ToString(UiText.Current.Culture));
            else
                UiGui.TextUnformatted(mapping.RailOrder.ToString(UiText.Current.Culture));

            ImGui.TableNextColumn();
            UiGui.TextWrapped(UiText.F($"{(mapping.BaseNodeId == 0 ? "-" : mapping.BaseNodeId.ToString(UiText.Current.Culture))} / {mapping.SliderNodeId?.ToString(UiText.Current.Culture) ?? "-"}"));

            ImGui.TableNextColumn();
            UiGui.TextWrapped($"({mapping.X:0.##}, {mapping.Y:0.##}) {mapping.Width}x{mapping.Height}");

            ImGui.TableNextColumn();
            DrawBoolPill(mapping.AnyVisible, "Yes", "No");

            ImGui.TableNextColumn();
            DrawBoolPill(mapping.AnyInteractive, "Yes", "No");

            ImGui.TableNextColumn();
            UiGui.TextUnformatted(mapping.AgentRowIndex?.ToString(UiText.Current.Culture) ?? "-");

            ImGui.TableNextColumn();
            UiGui.TextWrapped(mapping.CharacterName is null
                ? "-"
                : UiText.F($"{FormatSafeCharacterName(mapping.CharacterName)} ({mapping.Job ?? "-"})"));

            ImGui.TableNextColumn();
            UiGui.TextWrapped(mapping.MappingNote);
        }

        ImGui.EndTable();
    }

    private static void DrawActiveExportPayload(BannerPartyAgentSnapshot snapshot)
    {
        ImGui.Separator();
        UiGui.TextUnformatted("Read-Only Active Row Payload");

        if (snapshot.ActiveExportPayload is null)
        {
            UiGui.TextWrapped(snapshot.ActiveExportStatus);
            return;
        }

        var payload = snapshot.ActiveExportPayload;
        if (!ImGui.BeginTable("FootballerBannerPartyPayloadTable", 2, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingFixedFit))
            return;

        ImGui.TableSetupColumn("Field");
        ImGui.TableSetupColumn("Value", ImGuiTableColumnFlags.WidthStretch);
        UiGui.TableHeadersRow();

        DrawPayloadRow("Camera position", payload.CameraPosition);
        DrawPayloadRow("Camera target", payload.CameraTarget);
        DrawPayloadRow("Image rotation", payload.ImageRotation.ToString(UiText.Current.Culture));
        DrawPayloadRow("Camera zoom", payload.CameraZoom.ToString(UiText.Current.Culture));
        DrawPayloadRow("Banner timeline", payload.BannerTimeline.ToString(UiText.Current.Culture));
        DrawPayloadRow("Animation progress", payload.AnimationProgress.ToString("0.###", UiText.Current.Culture));
        DrawPayloadRow("Expression", payload.Expression.ToString(UiText.Current.Culture));
        DrawPayloadRow("Head direction", payload.HeadDirection);
        DrawPayloadRow("Eye direction", payload.EyeDirection);
        DrawPayloadRow("Directional light color", payload.DirectionalLightingColor);
        DrawPayloadRow("Directional light brightness", payload.DirectionalLightingBrightness.ToString(UiText.Current.Culture));
        DrawPayloadRow("Directional light angles", UiText.F($"{payload.DirectionalLightingVerticalAngle} / {payload.DirectionalLightingHorizontalAngle}"));
        DrawPayloadRow("Ambient light color", payload.AmbientLightingColor);
        DrawPayloadRow("Ambient light brightness", payload.AmbientLightingBrightness.ToString(UiText.Current.Culture));
        DrawPayloadRow("Banner background", payload.BannerBg.ToString(UiText.Current.Culture));

        ImGui.EndTable();
    }

    private static void DrawPayloadRow(string label, string value)
    {
        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        UiGui.TextUnformatted(label);
        ImGui.TableNextColumn();
        UiGui.TextWrapped(value);
    }

    private void DrawInspectButton(PartyShowcaseMember member, string idSuffix)
    {
        var canInspect = member.EntityId != 0;
        if (!canInspect)
            ImGui.BeginDisabled();

        if (UiGui.SmallButton($"Inspect##{idSuffix}"))
            plugin.OpenCharacterInspect(member);

        if (!canInspect)
            ImGui.EndDisabled();
    }

    private static void DrawInspectRow(string label, string value)
    {
        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        UiGui.TextUnformatted(label);
        ImGui.TableNextColumn();
        UiGui.TextWrapped(value);
    }

    private string BuildPortraitTestReport(CommendationPortraitResearchSnapshot snapshot)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Footballer BannerParty test report");
        builder.AppendLine($"Usage: {GetTestReportUsageNote(snapshot)}");
        builder.AppendLine();
        builder.AppendLine($"Safe status: {snapshot.SafeStatus}");

        var visibleProbes = snapshot.Probes
            .Where(probe => probe.Visible)
            .Select(probe => probe.AddonName)
            .ToArray();
        builder.AppendLine($"Visible addons: {(visibleProbes.Length == 0 ? "none" : string.Join(", ", visibleProbes))}");

        var agentSnapshot = snapshot.BannerPartyAgentSnapshot;
        if (agentSnapshot is null)
        {
            builder.AppendLine("BannerParty agent snapshot: unavailable");
            return builder.ToString();
        }

        builder.AppendLine($"Active row heuristic: {(agentSnapshot.ActiveCharacterRowIndex?.ToString() ?? "none")} | {FormatSafeCharacterName(agentSnapshot.ActiveCharacterName)}");
        builder.AppendLine($"Active export status: {agentSnapshot.ActiveExportStatus}");
        builder.AppendLine();
        builder.AppendLine("Live rows:");
        foreach (var character in agentSnapshot.Characters)
        {
            builder.AppendLine(
                $"{character.RowIndex}. {FormatSafeCharacterName(character.Name)} | Job={character.Job} | Visible={(character.CharacterVisible ? "Y" : "N")} | Loaded={(character.CharacterLoaded ? "Y" : "N")} | State={character.CharaViewState} | CJ={character.PortraitClassJobId}");
        }

        if (agentSnapshot.RailMappings.Length > 0)
        {
            builder.AppendLine();
            builder.AppendLine("Rail mapping:");
            foreach (var mapping in agentSnapshot.RailMappings)
            {
                builder.AppendLine(
                    $"Rail {mapping.RailOrder} -> Row {(mapping.AgentRowIndex?.ToString() ?? "-")} | {FormatSafeCharacterName(mapping.CharacterName)} | Visible={(mapping.AnyVisible ? "Y" : "N")} | Selected={(mapping.LikelySelected ? "Y" : "N")} | Nodes={FormatNodePair(mapping.BaseNodeId, mapping.SliderNodeId)}");
            }
        }

        if (agentSnapshot.ActiveExportPayload is not null)
        {
            var payload = agentSnapshot.ActiveExportPayload;
            builder.AppendLine();
            builder.AppendLine("Active payload:");
            builder.AppendLine($"Camera position: {payload.CameraPosition}");
            builder.AppendLine($"Camera target: {payload.CameraTarget}");
            builder.AppendLine($"Zoom={payload.CameraZoom} Timeline={payload.BannerTimeline} Bg={payload.BannerBg} Expression={payload.Expression}");
            builder.AppendLine($"Head={payload.HeadDirection} Eye={payload.EyeDirection}");
            builder.AppendLine($"Directional light: {payload.DirectionalLightingColor} | Brightness={payload.DirectionalLightingBrightness} | Angles={payload.DirectionalLightingVerticalAngle}/{payload.DirectionalLightingHorizontalAngle}");
            builder.AppendLine($"Ambient light: {payload.AmbientLightingColor} | Brightness={payload.AmbientLightingBrightness}");
        }

        return builder.ToString();
    }

    private static string BuildCharacterInspectReport(
        CharacterInspectResearchSnapshot snapshot,
        InspectPreviewCaptureRecord? currentInspectCapture,
        (float TopTrimFraction, float BottomTrimFraction) cropFractions,
        int scalePercent,
        string poseStatus,
        string barefootStatus,
        string previewSnipStatus)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Footballer CharacterInspect test report");
        builder.AppendLine($"Usage: {GetInspectReportUsageNote(snapshot)}");
        builder.AppendLine();
        builder.AppendLine($"Safe status: {snapshot.SafeStatus}");
        builder.AppendLine($"Next step: {snapshot.NextResearchStep}");
        builder.AppendLine($"Requested entity: {FormatEntityId(snapshot.RequestedEntityId)}");
        builder.AppendLine($"Current entity: {FormatEntityId(snapshot.CurrentEntityId)}");
        builder.AppendLine($"Addon visible: {(snapshot.AddonVisible ? "Y" : "N")}");
        builder.AppendLine(snapshot.AddonAddress == nint.Zero
            ? "Addon position: unavailable"
            : $"Addon position: ({snapshot.AddonX:0.##}, {snapshot.AddonY:0.##})");
        builder.AppendLine($"Preview component: {FormatAddress(snapshot.PreviewComponentAddress)}");
        builder.AppendLine($"Preview node: {FormatAddress(snapshot.PreviewNodeAddress)}");
        builder.AppendLine($"Preview visible: {(snapshot.PreviewNodeVisible ? "Y" : "N")}");
        builder.AppendLine(snapshot.PreviewNodeAddress == nint.Zero
            ? "Preview bounds: unavailable"
            : $"Preview bounds: ({snapshot.PreviewNodeX:0.##}, {snapshot.PreviewNodeY:0.##}) {snapshot.PreviewNodeWidth}x{snapshot.PreviewNodeHeight}");
        builder.AppendLine(snapshot.PreviewNodeAddress == nint.Zero
            ? "Preview node scale: unavailable"
            : $"Preview node scale: X {snapshot.PreviewNodeScaleX:0.###} / Y {snapshot.PreviewNodeScaleY:0.###}");
        builder.AppendLine($"Preview scaling fallback: {scalePercent}%");
        builder.AppendLine($"Zoom ratio: {snapshot.CharaViewZoomRatio:0.###}");
        builder.AppendLine($"Preview character: {FormatAddress(snapshot.PreviewCharacterAddress)}");
        builder.AppendLine($"Preview draw object: {FormatAddress(snapshot.PreviewDrawObjectAddress)}");
        builder.AppendLine($"Preview game position: {snapshot.PreviewGameObjectPosition}");
        builder.AppendLine($"Preview game rotation: {snapshot.PreviewGameObjectRotation}");
        builder.AppendLine($"Preview draw position: {snapshot.PreviewDrawObjectPosition}");
        builder.AppendLine($"Preview draw rotation: {snapshot.PreviewDrawObjectRotation}");
        builder.AppendLine($"Snapshot captured: {snapshot.SnapshotCapturedAt}");
        builder.AppendLine($"Pose preset status: {poseStatus}");
        builder.AppendLine($"Barefoot apply status: {barefootStatus}");
        builder.AppendLine($"Preview snip status: {previewSnipStatus}");
        builder.AppendLine($"Scene camera manager: {FormatAddress(snapshot.CameraManagerAddress)}");
        builder.AppendLine($"Scene manager index: {(snapshot.CameraManagerIndex < 0 ? "-" : snapshot.CameraManagerIndex.ToString())}");
        builder.AppendLine($"Raw scene camera: {FormatAddress(snapshot.RawSceneCameraAddress)}");
        builder.AppendLine($"Manager current camera: {FormatAddress(snapshot.ManagerCurrentCameraAddress)}");
        builder.AppendLine($"Active camera: {FormatAddress(snapshot.CameraAddress)}");
        builder.AppendLine($"Camera position: {snapshot.CameraPosition}");
        builder.AppendLine($"Camera look-at: {snapshot.CameraLookAt}");
        builder.AppendLine($"Derived yaw / pitch: {snapshot.CameraRotation}");
        builder.AppendLine($"Camera distance: {snapshot.CameraDistance}");
        builder.AppendLine($"Camera FoV: {snapshot.CameraFoV}");
        builder.AppendLine($"Camera status: {snapshot.CameraSnapshotStatus}");
        builder.AppendLine($"Stored snip profile: {cropFractions.TopTrimFraction:P0} top / {cropFractions.BottomTrimFraction:P0} bottom");
        builder.AppendLine($"Capture ready: {(snapshot.CaptureReady ? "Y" : "N")}");
        builder.AppendLine($"Capture status: {snapshot.ActiveExportStatus}");
        builder.AppendLine(currentInspectCapture is null
            ? "Latest snip: none"
            : $"Latest snip: {Path.GetFileName(currentInspectCapture.FilePath)} | Rect=({currentInspectCapture.ClientX}, {currentInspectCapture.ClientY}) {currentInspectCapture.Width}x{currentInspectCapture.Height}");

        if (snapshot.ActiveExportPayload is not null)
        {
            var payload = snapshot.ActiveExportPayload;
            builder.AppendLine();
            builder.AppendLine("Read-only payload:");
            builder.AppendLine($"Camera position: {payload.CameraPosition}");
            builder.AppendLine($"Camera target: {payload.CameraTarget}");
            builder.AppendLine($"Zoom={payload.CameraZoom} Timeline={payload.BannerTimeline} Bg={payload.BannerBg} Expression={payload.Expression}");
            builder.AppendLine($"Head={payload.HeadDirection} Eye={payload.EyeDirection}");
            builder.AppendLine($"Directional light: {payload.DirectionalLightingColor} | Brightness={payload.DirectionalLightingBrightness} | Angles={payload.DirectionalLightingVerticalAngle}/{payload.DirectionalLightingHorizontalAngle}");
            builder.AppendLine($"Ambient light: {payload.AmbientLightingColor} | Brightness={payload.AmbientLightingBrightness}");
        }

        return builder.ToString();
    }

    private static string GetTestReportUsageNote(CommendationPortraitResearchSnapshot snapshot)
    {
        var characterCount = snapshot.BannerPartyAgentSnapshot?.Characters.Length ?? 0;
        return characterCount <= 1
            ? "Copy the report after the preview is stable."
            : "Copy a report before and after changing the selected portrait.";
    }

    private static string BuildTestReportClipboardStatus(CommendationPortraitResearchSnapshot snapshot)
    {
        var characterCount = snapshot.BannerPartyAgentSnapshot?.Characters.Length ?? 0;
        return characterCount <= 1
            ? "Copied a concise BannerParty test report to the clipboard. With one real party member, one report is enough for now."
            : "Copied a concise BannerParty test report to the clipboard. Capture one report before and one after changing the portrait-row context, then send both.";
    }

    private static string GetInspectReportUsageNote(CharacterInspectResearchSnapshot snapshot)
        => snapshot.CaptureReady
            ? "Open CharacterInspect and wait for a stable preview before capturing."
            : "Open CharacterInspect and wait for a stable preview before capturing.";

    private static string BuildInspectReportClipboardStatus(CharacterInspectResearchSnapshot snapshot)
        => snapshot.CaptureReady
            ? "Copied a concise CharacterInspect report to the clipboard. Capture another after switching inspect targets if you want a before/after pair."
            : "Copied a concise CharacterInspect report to the clipboard. Open inspect for a live party member if you still need the preview bounds and payload to populate.";

    private static string GetInspectSnipUsageNote(CharacterInspectResearchSnapshot snapshot)
        => snapshot.CaptureReady
            ? "Keep Footballer away from the preview while capturing. The crop profile applies to future captures."
            : "Open CharacterInspect and wait for a stable preview before capturing.";

    private void DrawPreviewScalingSelector(int currentScalePercent, bool disabled = false)
    {
        var measuredToolbar = !plugin.SessionDebugUnlocked;
        if (measuredToolbar)
            ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(ImGui.GetStyle().FramePadding.X,
                Math.Max(0, (FootballerPresentation.ToolbarHeight * MaterialTheme.Metrics.Scale - ImGui.GetTextLineHeight()) * .5f)));
        ImGui.BeginDisabled(disabled);
        ImGui.SetNextItemWidth(100f * MaterialTheme.Metrics.Scale);
        if (ImGui.BeginCombo("##PreviewScaling", UiText.F($"{currentScalePercent}%")))
        {
            foreach (var option in PreviewScalePercents)
            {
                var selected = option == currentScalePercent;
                if (UiGui.Selectable(UiText.F($"{option}%"), selected))
                {
                    plugin.Configuration.InspectPreviewWindowScalePercent = option;
                    plugin.Configuration.Save();
                }

                if (selected)
                    ImGui.SetItemDefaultFocus();
            }

            ImGui.EndCombo();
        }

        ImGui.SameLine(0f, 6f);
        UiGui.TextUnformatted("Scaling");
        ImGui.SameLine(0f, 4f);
        UiGui.TextDisabled("(?)");
        ShowHoverTooltip("Pick Same as character preview window!");
        ImGui.EndDisabled();
        if (measuredToolbar) ImGui.PopStyleVar();
    }

    private static float ClampCropFraction(float value)
        => float.IsFinite(value) ? Math.Clamp(value, 0f, 0.9f) : 0f;

    private static string FormatNodePair(uint baseNodeId, uint? sliderNodeId)
        => UiText.F($"{(baseNodeId == 0 ? "-" : baseNodeId.ToString())}/{sliderNodeId?.ToString() ?? "-"}");

    private static string FormatEntityId(uint entityId)
        => entityId == 0 ? "-" : UiText.F($"0x{entityId:X8}");

    private string GetSafeDisplayName(PartyShowcaseMember member)
        => GetUiDisplayName(member);

    private string GetSafeWorldLabel(PartyShowcaseMember member)
        => plugin.FormatWorldName(member.WorldName);

    private string FormatSafeCharacterName(string? characterName)
        => plugin.FormatCharacterName(characterName);

    private static string FormatAddress(nint address)
        => address == nint.Zero ? "-" : UiText.F($"0x{address.ToInt64():X}");
}
