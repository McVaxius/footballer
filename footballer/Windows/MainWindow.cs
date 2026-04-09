using System;
using System.IO;
using System.Numerics;
using System.Reflection;
using System.Text;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using footballer.Models;
using footballer.Services;

namespace footballer.Windows;

public sealed class MainWindow : PositionedWindow, IDisposable
{
    private static readonly int[] PreviewScalePercents = { 60, 70, 80, 90, 100, 110, 120, 130, 140, 150, 160, 170, 180, 190, 200 };
    private readonly Plugin plugin;

    public MainWindow(Plugin plugin)
        : base($"{PluginInfo.DisplayName}##Main")
    {
        this.plugin = plugin;
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

        ImGui.Text($"{PluginInfo.DisplayName} v{version}");
        ImGui.SameLine();
        if (ImGui.SmallButton("Ko-fi"))
            plugin.OpenUrl(PluginInfo.SupportUrl);
        ImGui.SameLine();
        if (ImGui.SmallButton("Discord"))
            plugin.OpenUrl(PluginInfo.DiscordUrl);

        ImGui.Separator();

        var enabled = cfg.PluginEnabled;
        if (ImGui.Checkbox("Enabled", ref enabled))
            plugin.SetPluginEnabled(enabled, printStatus: true);

        ImGui.SameLine();
        var showcase = cfg.ShowFootShowcase;
        if (ImGui.Checkbox("Foot showcase", ref showcase))
        {
            cfg.ShowFootShowcase = showcase;
            cfg.Save();
        }

        ImGui.SameLine();
        if (ImGui.SmallButton("Settings"))
            plugin.OpenConfigUi();

        ImGui.SameLine();
        var krangleNames = cfg.KrangleNames;
        if (ImGui.SmallButton(krangleNames ? "Un-Krangle" : "Krangle Names"))
            plugin.SetKrangleNames(!krangleNames, printStatus: true);

        ImGui.SameLine();
        if (ImGui.SmallButton("Refresh party"))
            plugin.QueuePartyResearchRefresh(forceLodestone: false, refreshFeetCaptures: true);

        ImGui.SameLine();
        if (ImGui.SmallButton("Refresh Lodestone"))
            plugin.QueuePartyResearchRefresh(forceLodestone: true);

        ImGui.SameLine();
        DrawPreviewScalingSelector(scalePercent);

        if (showDebug)
        {
            ImGui.TextWrapped(PluginInfo.Summary);
            ImGui.TextWrapped(PluginInfo.ShellStatus);
            ImGui.TextWrapped("Privacy rule: feet are hidden when a face is unavailable on Lodestone so the plugin does not bypass that profile privacy choice.");
            ImGui.TextWrapped(PluginInfo.DiscordFeedbackNote);
            ImGui.TextColored(new Vector4(0.98f, 0.73f, 0.40f, 1f), "Session debug mode is active. Hidden research surfaces are visible below.");

            ImGui.Separator();
            ImGui.TextUnformatted("Live Today");
            foreach (var item in PluginInfo.LiveToday)
                ImGui.BulletText(item);

            ImGui.Separator();
            ImGui.TextUnformatted("Current Defaults");
            ImGui.BulletText($"Krangle labels: {(cfg.KrangleNames ? "Yes" : "No")}");
            ImGui.BulletText($"Preview scaling: {scalePercent}%");
            ImGui.BulletText($"Auto refresh party on showcase open: {(cfg.AutoRefreshPartyOnShowcaseOpen ? "Yes" : "No")}");
            ImGui.BulletText($"Show male feet: {(cfg.ShowMaleFeet ? "Yes" : "No")}");
            ImGui.BulletText($"Show female feet: {(cfg.ShowFemaleFeet ? "Yes" : "No")}");
            ImGui.BulletText($"Without footwear: {(cfg.WithoutFootwear ? "Yes" : "No")}");
            ImGui.BulletText($"Show own feet: {(cfg.ShowOwnFeet ? "Yes" : "No")}");
            ImGui.BulletText($"Replace party portrait window pictures: {(cfg.ReplaceCommendationPictures ? "Yes" : "No")}");
            ImGui.BulletText($"Show face next to feet: {(cfg.ShowFaceNextToFeet ? "Yes" : "No")}");
            ImGui.BulletText(plugin.SessionDebugUnlocked
                ? $"Lodestone privacy gate (debug): {(effectiveRespectPrivacy ? "Forced / On" : "Override Off")}"
                : "Lodestone privacy gate: Forced on");

            ImGui.Separator();
            ImGui.TextUnformatted("Live Party Foot Showcase");
            ImGui.TextWrapped("Normal mode is now meant to stay stripped down to the top controls and the mini character boxes. The extra copy, raw tables, and manual inspect/capture actions stay below the fold only when session debug is enabled.");
        }
        else
        {
            ImGui.Separator();
        }

        if (footCards.Count == 0)
        {
            ImGui.TextWrapped("No local player or party members are available yet, so there is nothing to render in the live showcase.");
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
            if (ImGui.CollapsingHeader("Party Showcase Model"))
            {
                ImGui.TextWrapped("Raw party snapshot, Lodestone lookup state, and feet-gate truth. Names and worlds follow the current krangle toggle here too.");

                if (partyMembers.Count == 0)
                {
                    ImGui.TextWrapped("No local player or party members are available yet.");
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
                    ImGui.TableHeadersRow();

                    foreach (var member in partyMembers)
                    {
                        var lookup = plugin.LodestoneProfileService.GetRecord(member.Name, member.WorldName);
                        var feetGateLabel = LodestoneProfileService.GetFeetGateLabel(lookup, effectiveRespectPrivacy);

                        ImGui.TableNextRow();

                        ImGui.TableNextColumn();
                        ImGui.TextUnformatted(member.Slot.ToString());

                        ImGui.TableNextColumn();
                        ImGui.TextUnformatted(GetSafeDisplayName(member));
                        ImGui.TextDisabled(GetSafeWorldLabel(member));

                        ImGui.TableNextColumn();
                        ImGui.TextUnformatted($"{member.JobAbbreviation} {member.Level}");

                        ImGui.TableNextColumn();
                        ImGui.TextUnformatted(string.IsNullOrWhiteSpace(member.ContentId) ? "-" : member.ContentIdShort);

                        ImGui.TableNextColumn();
                        DrawStatusText(LodestoneProfileService.GetStatusLabel(lookup.Status), lookup.Status);
                        if (!string.IsNullOrWhiteSpace(lookup.Note))
                            ImGui.TextWrapped(lookup.Note);

                        ImGui.TableNextColumn();
                        DrawFeetGateText(feetGateLabel);

                        ImGui.TableNextColumn();
                        DrawInspectButton(member, $"PartyTable{member.CharacterKey}");

                        ImGui.SameLine();
                        if (ImGui.SmallButton($"Search##{member.CharacterKey}"))
                            plugin.OpenUrl(lookup.SearchUrl);

                        if (!string.IsNullOrWhiteSpace(lookup.ProfileUrl))
                        {
                            ImGui.SameLine();
                            if (ImGui.SmallButton($"Profile##{member.CharacterKey}"))
                                plugin.OpenUrl(lookup.ProfileUrl!);
                        }

                        ImGui.SameLine();
                        if (ImGui.SmallButton($"Retry##{member.CharacterKey}"))
                            plugin.LodestoneProfileService.EnsureLookup(member.Name, member.WorldName, force: true);
                    }

                    ImGui.EndTable();
                }
            }

            ImGui.Separator();
            ImGui.TextUnformatted("Inspect Capture");
            ImGui.TextWrapped("The normal showcase flow now avoids frame-by-frame research polling. Use Inspect on a party row or showcase card, let CharacterInspect settle, then capture the current preview when you want to refresh a foot card image. Refresh party also runs the sequential inspect/pose/barefoot/capture loop for the live party snapshot, with a 2-second stable hold before each automatic save.");
            ImGui.BulletText($"Party feet refresh: {plugin.PartyFeetRefreshService.LastStatus}");
            ImGui.BulletText($"Pose preset: {plugin.CharacterInspectPoseService.LastStatus}");
            ImGui.BulletText($"Barefoot apply: {plugin.CharacterInspectFootwearService.LastStatus}");
            ImGui.BulletText($"Preview snip: {plugin.CharacterInspectPreviewCaptureService.LastCaptureStatus}");

            if (ImGui.SmallButton("Capture Current Preview"))
            {
                var result = plugin.CaptureCurrentInspectPreview(partyMembers);
                plugin.PrintStatus(result.Status);
            }
            ImGui.SameLine();
            if (ImGui.SmallButton("Capture Folder"))
                plugin.OpenInspectPreviewCaptureFolder();
            ImGui.SameLine();
            ImGui.TextDisabled("Stored crop defaults: 65% top / 20% bottom.");

            DrawDebugResearchSections(partyMembers, cropFractions, scalePercent);

            ImGui.Separator();
            ImGui.TextUnformatted("Research Notes");
            foreach (var item in PluginInfo.Concept)
                ImGui.BulletText(item);

            ImGui.Separator();
            ImGui.TextUnformatted("Active Services");
            foreach (var item in PluginInfo.Services)
                ImGui.BulletText(item);

            ImGui.Separator();
            ImGui.TextUnformatted("Retest Focus");
            foreach (var item in PluginInfo.Tests)
                ImGui.BulletText(item);
        }

        FinalizePendingWindowPlacement();
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
        ImGui.TextUnformatted("Character Inspect Research");
        ImGui.TextWrapped("These raw inspect surfaces are now debug-only and refresh on demand instead of every frame.");

        if (ImGui.SmallButton("Refresh Inspect Debug"))
        {
            inspectSnapshot = plugin.RefreshCharacterInspectDebugSnapshot();
            currentInspectCapture = plugin.GetCurrentInspectPreviewCapture(partyMembers, inspectSnapshot);
        }

        ImGui.SameLine();
        if (ImGui.SmallButton("COPY INSPECT REPORT"))
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
        ImGui.TextDisabled(inspectSnapshot is null
            ? "Refresh inspect debug first if you want the cached raw inspect snapshot and report to populate."
            : GetInspectReportUsageNote(inspectSnapshot));

        var topTrimPercent = cropFractions.TopTrimFraction * 100f;
        if (ImGui.SliderFloat("Snip top trim %", ref topTrimPercent, 0f, 75f, "%.0f%%"))
        {
            plugin.Configuration.InspectPreviewTopTrimFraction = ClampCropFraction(topTrimPercent / 100f);
            plugin.Configuration.Save();
        }

        var bottomTrimPercent = cropFractions.BottomTrimFraction * 100f;
        if (ImGui.SliderFloat("Snip bottom trim %", ref bottomTrimPercent, 0f, 50f, "%.0f%%"))
        {
            plugin.Configuration.InspectPreviewBottomTrimFraction = ClampCropFraction(bottomTrimPercent / 100f);
            plugin.Configuration.Save();
        }

        if (ImGui.SmallButton("Reset snip profile"))
        {
            plugin.Configuration.InspectPreviewTopTrimFraction = Configuration.DefaultInspectPreviewTopTrimFraction;
            plugin.Configuration.InspectPreviewBottomTrimFraction = Configuration.DefaultInspectPreviewBottomTrimFraction;
            plugin.Configuration.Save();
        }
        ImGui.SameLine();
        ImGui.TextDisabled("Saved globally for future targets.");

        if (inspectSnapshot is null)
        {
            ImGui.TextWrapped("No cached CharacterInspect debug snapshot is available yet.");
        }
        else
        {
            ImGui.BulletText(inspectSnapshot.SafeStatus);
            ImGui.BulletText(inspectSnapshot.NextResearchStep);

            if (ImGui.BeginTable("FootballerCharacterInspectTable", 2, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingFixedFit))
            {
                ImGui.TableSetupColumn("Field");
                ImGui.TableSetupColumn("Value", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableHeadersRow();

                DrawInspectRow("Agent available", inspectSnapshot.AgentAvailable ? "Yes" : "No");
                DrawInspectRow("Agent address", FormatAddress(inspectSnapshot.AgentAddress));
                DrawInspectRow("Addon visible", inspectSnapshot.AddonVisible ? "Yes" : "No");
                DrawInspectRow("Addon address", FormatAddress(inspectSnapshot.AddonAddress));
                DrawInspectRow("Addon position", inspectSnapshot.AddonAddress == nint.Zero
                    ? "-"
                    : $"({inspectSnapshot.AddonX:0.##}, {inspectSnapshot.AddonY:0.##})");
                DrawInspectRow("Requested entity", FormatEntityId(inspectSnapshot.RequestedEntityId));
                DrawInspectRow("Current entity", FormatEntityId(inspectSnapshot.CurrentEntityId));
                DrawInspectRow("Fetch status", $"{inspectSnapshot.FetchCharacterDataStatus} / {inspectSnapshot.FetchSearchCommentStatus} / {inspectSnapshot.FetchFreeCompanyStatus}");
                DrawInspectRow("Buddy inspect", inspectSnapshot.IsBuddyInspect ? "Yes" : "No");
                DrawInspectRow("CharaView state", inspectSnapshot.CharaViewState.ToString());
                DrawInspectRow("Client object", $"{inspectSnapshot.CharaViewClientObjectId} / {inspectSnapshot.CharaViewClientObjectIndex}");
                DrawInspectRow("Loaded / copied", $"{(inspectSnapshot.CharaViewCharacterLoaded ? "Y" : "N")} / {(inspectSnapshot.CharaViewCharacterDataCopied ? "Y" : "N")}");
                DrawInspectRow("Zoom ratio", inspectSnapshot.CharaViewZoomRatio.ToString("0.###"));
                DrawInspectRow("Preview character", FormatAddress(inspectSnapshot.PreviewCharacterAddress));
                DrawInspectRow("Preview draw object", FormatAddress(inspectSnapshot.PreviewDrawObjectAddress));
                DrawInspectRow("Preview game position", inspectSnapshot.PreviewGameObjectPosition);
                DrawInspectRow("Preview game rotation", inspectSnapshot.PreviewGameObjectRotation);
                DrawInspectRow("Preview draw position", inspectSnapshot.PreviewDrawObjectPosition);
                DrawInspectRow("Preview draw rotation", inspectSnapshot.PreviewDrawObjectRotation);
                DrawInspectRow("Snapshot captured", inspectSnapshot.SnapshotCapturedAt);
                DrawInspectRow("Scene camera manager", FormatAddress(inspectSnapshot.CameraManagerAddress));
                DrawInspectRow("Scene manager index", inspectSnapshot.CameraManagerIndex < 0 ? "-" : inspectSnapshot.CameraManagerIndex.ToString());
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
                DrawInspectRow("Preview scaling fallback", $"{scalePercent}%");
                DrawInspectRow("Stored snip profile", $"{cropFractions.TopTrimFraction:P0} top / {cropFractions.BottomTrimFraction:P0} bottom");
                DrawInspectRow("Preview visible", inspectSnapshot.PreviewNodeVisible ? "Yes" : "No");
                DrawInspectRow("Collision node", FormatAddress(inspectSnapshot.CollisionNodeAddress));
                DrawInspectRow("Callback base id", inspectSnapshot.PreviewCallbackBaseId.ToString());
                DrawInspectRow("Capture ready", inspectSnapshot.CaptureReady ? "Yes" : "No");
                DrawInspectRow("Capture status", inspectSnapshot.ActiveExportStatus);
                DrawInspectRow("Latest snip", currentInspectCapture is null ? "-" : Path.GetFileName(currentInspectCapture.FilePath));
                DrawInspectRow("Snip rect", currentInspectCapture is null
                    ? "-"
                    : $"({currentInspectCapture.ClientX}, {currentInspectCapture.ClientY}) {currentInspectCapture.Width}x{currentInspectCapture.Height}");
                DrawInspectRow("Snip action", plugin.CharacterInspectPreviewCaptureService.LastCaptureStatus);

                ImGui.EndTable();
            }

            DrawInspectExportPayload(inspectSnapshot, currentInspectCapture);
        }

        ImGui.Separator();
        ImGui.TextUnformatted("Party Portrait Window Research");
        ImGui.TextWrapped("These portrait probes are now debug-only and refresh on demand instead of every frame.");

        if (ImGui.SmallButton("Refresh Portrait Debug"))
            commendationSnapshot = plugin.RefreshCommendationDebugSnapshot();

        ImGui.SameLine();
        if (ImGui.SmallButton("COPY TEST REPORT"))
        {
            commendationSnapshot = plugin.RefreshCommendationDebugSnapshot();
            ImGui.SetClipboardText(BuildPortraitTestReport(commendationSnapshot));
            plugin.PrintStatus(BuildTestReportClipboardStatus(commendationSnapshot));
        }

        ImGui.SameLine();
        ImGui.TextDisabled(commendationSnapshot is null
            ? "Refresh portrait debug first if you want the cached BannerParty probes and report to populate."
            : GetTestReportUsageNote(commendationSnapshot));

        if (commendationSnapshot is null)
        {
            ImGui.TextWrapped("No cached portrait debug snapshot is available yet.");
            return;
        }

        ImGui.BulletText($"Configured portrait replacement toggle: {(commendationSnapshot.ReplaceCommendationPicturesConfigured ? "On" : "Off")}");
        ImGui.BulletText(commendationSnapshot.SafeStatus);
        ImGui.BulletText(commendationSnapshot.KnownCallbackSeam);
        ImGui.BulletText(commendationSnapshot.NextResearchStep);

        if (ImGui.BeginTable("FootballerCommendationProbeTable", 4, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingFixedFit))
        {
            ImGui.TableSetupColumn("Addon");
            ImGui.TableSetupColumn("Agent");
            ImGui.TableSetupColumn("Visible");
            ImGui.TableSetupColumn("Role", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableHeadersRow();

            foreach (var probe in commendationSnapshot.Probes)
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(probe.AddonName);

                ImGui.TableNextColumn();
                ImGui.TextUnformatted(probe.AgentId?.ToString() ?? "-");

                ImGui.TableNextColumn();
                if (probe.Visible)
                    ImGui.TextColored(new Vector4(0.55f, 0.93f, 0.61f, 1f), "Visible");
                else
                    ImGui.TextDisabled("Hidden");

                ImGui.TableNextColumn();
                ImGui.TextWrapped(probe.Role);
            }

            ImGui.EndTable();
        }

        var bannerPartySnapshot = commendationSnapshot.BannerPartySnapshot;
        var bannerPartyAgentSnapshot = commendationSnapshot.BannerPartyAgentSnapshot;
        if (bannerPartySnapshot is null && bannerPartyAgentSnapshot is null)
        {
            ImGui.TextWrapped("No cached BannerParty addon or agent snapshot is available yet.");
            return;
        }

        if (bannerPartyAgentSnapshot is not null)
        {
            ImGui.Separator();
            ImGui.TextUnformatted("Live BannerParty Agent");
            ImGui.TextWrapped(bannerPartyAgentSnapshot.CaptureStatus);
            ImGui.BulletText($"Agent address: {FormatAddress(bannerPartyAgentSnapshot.Address)}");
            ImGui.BulletText($"Character count: {bannerPartyAgentSnapshot.CharacterCount}");
            ImGui.BulletText(
                bannerPartyAgentSnapshot.ActiveCharacterRowIndex is int activeRow
                    ? $"Active row heuristic: {activeRow} ({bannerPartyAgentSnapshot.ActiveCharacterName ?? "Unknown"})"
                    : "Active row heuristic: none yet");
            ImGui.BulletText(bannerPartyAgentSnapshot.ActiveExportStatus);

            DrawBannerPartyCharacterTable(bannerPartyAgentSnapshot);
            DrawRailMappingTable(bannerPartyAgentSnapshot);
            DrawActiveExportPayload(bannerPartyAgentSnapshot);
        }

        if (bannerPartySnapshot is null)
            return;

        ImGui.Separator();
        ImGui.TextUnformatted("Live BannerParty Addon Capture");
        ImGui.TextWrapped(bannerPartySnapshot.CaptureStatus);
        ImGui.BulletText($"Addon address: {FormatAddress(bannerPartySnapshot.Address)}");
        ImGui.BulletText($"Addon position: ({bannerPartySnapshot.X:0.##}, {bannerPartySnapshot.Y:0.##})");
        ImGui.BulletText($"Node count: {bannerPartySnapshot.NodeCount}");

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
            ImGui.TableHeadersRow();

            foreach (var slot in bannerPartySnapshot.LikelyPortraitSlots)
            {
                ImGui.TableNextRow();

                ImGui.TableNextColumn();
                ImGui.TextUnformatted(slot.SlotIndex.ToString());

                ImGui.TableNextColumn();
                ImGui.TextUnformatted(slot.BaseNodeId == 0 ? "-" : slot.BaseNodeId.ToString());

                ImGui.TableNextColumn();
                ImGui.TextUnformatted(slot.SliderNodeId?.ToString() ?? "-");

                ImGui.TableNextColumn();
                ImGui.TextUnformatted($"({slot.X:0.##}, {slot.Y:0.##})");

                ImGui.TableNextColumn();
                ImGui.TextUnformatted($"{slot.Width}x{slot.Height}");

                ImGui.TableNextColumn();
                DrawBoolPill(slot.AnyVisible, "Yes", "No");

                ImGui.TableNextColumn();
                DrawBoolPill(slot.AnyInteractive, "Yes", "No");

                ImGui.TableNextColumn();
                ImGui.TextWrapped(slot.Note);
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
        ImGui.TableHeadersRow();

        foreach (var node in bannerPartySnapshot.Nodes)
        {
            ImGui.TableNextRow();

            ImGui.TableNextColumn();
            ImGui.TextUnformatted(node.Index.ToString());

            ImGui.TableNextColumn();
            ImGui.TextUnformatted(node.NodeId.ToString());

            ImGui.TableNextColumn();
            ImGui.TextUnformatted($"{node.TypeName} ({node.RawType})");

            ImGui.TableNextColumn();
            DrawBoolPill(node.Visible, "Yes", "No");

            ImGui.TableNextColumn();
            ImGui.TextUnformatted($"({node.X:0.##}, {node.Y:0.##})");

            ImGui.TableNextColumn();
            ImGui.TextUnformatted($"{node.Width}x{node.Height}");

            ImGui.TableNextColumn();
            var eventSummary = node.EventCount > 0
                ? $"{node.EventCount} ({node.FirstEventType ?? "?"})"
                : "0";
            var flagSummary = string.IsNullOrWhiteSpace(node.FlagsLabel)
                ? $"0x{node.FlagsRaw:X4}"
                : $"0x{node.FlagsRaw:X4} {node.FlagsLabel}";
            ImGui.TextWrapped($"{eventSummary} | {(node.AppearsInteractive ? "Interactive" : "Passive")} | {flagSummary}");

            ImGui.TableNextColumn();
            ImGui.TextWrapped(node.Text ?? "-");

            ImGui.TableNextColumn();
            ImGui.TextUnformatted(FormatAddress(node.Address));
        }

        ImGui.EndTable();
    }

    private void DrawFootShowcaseCard(FootShowcaseCard card, bool showDebug)
    {
        var cardHeight = showDebug ? 390f : 264f;
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, showDebug ? new Vector2(8f, 8f) : new Vector2(4f, 4f));
        ImGui.BeginChild($"FootCard##{card.Member.CharacterKey}", new Vector2(0f, cardHeight), true);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, showDebug ? new Vector2(6f, 4f) : new Vector2(4f, 3f));

        ImGui.TextUnformatted(GetSafeDisplayName(card.Member));
        ShowHoverTooltip($"{GetSafeWorldLabel(card.Member)} | {card.Member.JobAbbreviation} {card.Member.Level}\n{card.VariantLabel}");

        if (showDebug)
        {
            ImGui.TextDisabled($"{GetSafeWorldLabel(card.Member)} | {card.Member.JobAbbreviation} {card.Member.Level}");
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
            if (ImGui.SmallButton($"Search##FootCard{card.Member.CharacterKey}"))
                plugin.OpenUrl(card.Lookup.SearchUrl);
            ImGui.SameLine();
            DrawInspectButton(card.Member, $"FootCard{card.Member.CharacterKey}");

            if (!string.IsNullOrWhiteSpace(card.Lookup.ProfileUrl))
            {
                ImGui.SameLine();
                if (ImGui.SmallButton($"Profile##FootCard{card.Member.CharacterKey}"))
                    plugin.OpenUrl(card.Lookup.ProfileUrl!);
            }

            ImGui.SameLine();
            if (ImGui.SmallButton($"Retry##FootCard{card.Member.CharacterKey}"))
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
            if (!drewFaceCompact && !drewFootCompact)
                ImGui.TextDisabled("No image yet.");

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
                ImGui.TextWrapped("No live image is currently eligible for this card. Face images come from Lodestone; foot cards need a saved CharacterInspect preview capture before they can draw.");
            else
                ImGui.TextDisabled("No image yet.");
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
        ImGui.TextUnformatted($"{label}:");
        ImGui.SameLine(0f, 4f);
        ImGui.TextColored(GetCardStatusColor(value), value);
        ShowHoverTooltip(tooltip);
    }

    private static void ShowHoverTooltip(string? text)
    {
        if (string.IsNullOrWhiteSpace(text) || !ImGui.IsItemHovered())
            return;

        ImGui.BeginTooltip();
        ImGui.PushTextWrapPos(420f);
        ImGui.TextUnformatted(text);
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
        ImGui.TextUnformatted($"{label}:");
        ImGui.SameLine();
        ImGui.TextColored(GetCardStatusColor(value), value);
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

        ImGui.TextColored(color, label);
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

        ImGui.TextColored(color, label);
    }

    private static void DrawBoolPill(bool value, string trueLabel, string falseLabel)
    {
        var color = value
            ? new Vector4(0.55f, 0.93f, 0.61f, 1f)
            : new Vector4(0.84f, 0.84f, 0.84f, 1f);
        ImGui.TextColored(color, value ? trueLabel : falseLabel);
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
        ImGui.TextUnformatted("CharacterInspect Capture State");
        if (currentInspectCapture is not null)
        {
            ImGui.TextWrapped($"Latest saved preview snip: {Path.GetFileName(currentInspectCapture.FilePath)}");
            if (TryDrawLocalImage(currentInspectCapture.FilePath, new Vector2(280f, 320f)))
                ImGui.Spacing();
        }

        if (snapshot.ActiveExportPayload is null)
        {
            ImGui.TextWrapped(snapshot.ActiveExportStatus);
            return;
        }

        var payload = snapshot.ActiveExportPayload;
        if (!ImGui.BeginTable("FootballerCharacterInspectPayloadTable", 2, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingFixedFit))
            return;

        ImGui.TableSetupColumn("Field");
        ImGui.TableSetupColumn("Value", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableHeadersRow();

        DrawPayloadRow("Camera position", payload.CameraPosition);
        DrawPayloadRow("Camera target", payload.CameraTarget);
        DrawPayloadRow("Image rotation", payload.ImageRotation.ToString());
        DrawPayloadRow("Camera zoom", payload.CameraZoom.ToString());
        DrawPayloadRow("Banner timeline", payload.BannerTimeline.ToString());
        DrawPayloadRow("Animation progress", payload.AnimationProgress.ToString("0.###"));
        DrawPayloadRow("Expression", payload.Expression.ToString());
        DrawPayloadRow("Head direction", payload.HeadDirection);
        DrawPayloadRow("Eye direction", payload.EyeDirection);
        DrawPayloadRow("Directional light color", payload.DirectionalLightingColor);
        DrawPayloadRow("Directional light brightness", payload.DirectionalLightingBrightness.ToString());
        DrawPayloadRow("Directional light angles", $"{payload.DirectionalLightingVerticalAngle} / {payload.DirectionalLightingHorizontalAngle}");
        DrawPayloadRow("Ambient light color", payload.AmbientLightingColor);
        DrawPayloadRow("Ambient light brightness", payload.AmbientLightingBrightness.ToString());
        DrawPayloadRow("Banner background", payload.BannerBg.ToString());

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
        ImGui.TableHeadersRow();

        foreach (var character in snapshot.Characters)
        {
            ImGui.TableNextRow();

            ImGui.TableNextColumn();
            ImGui.TextUnformatted(character.RowIndex.ToString());

            ImGui.TableNextColumn();
            ImGui.TextUnformatted(FormatSafeCharacterName(character.Name));

            ImGui.TableNextColumn();
            ImGui.TextUnformatted(character.Job);

            ImGui.TableNextColumn();
            ImGui.TextUnformatted(character.WorldId == 0 ? "-" : character.WorldId.ToString());

            ImGui.TableNextColumn();
            DrawBoolPill(character.CharacterVisible, "Yes", "No");

            ImGui.TableNextColumn();
            DrawBoolPill(character.CharacterLoaded, "Yes", "No");

            ImGui.TableNextColumn();
            ImGui.TextWrapped($"State {character.CharaViewState} | Pose {character.PoseClassJob} | CJ {character.PortraitClassJobId}");

            ImGui.TableNextColumn();
            ImGui.TextWrapped(character.SelectionHint);
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
        ImGui.TableHeadersRow();

        foreach (var mapping in snapshot.RailMappings)
        {
            ImGui.TableNextRow();

            ImGui.TableNextColumn();
            if (mapping.LikelySelected)
                ImGui.TextColored(new Vector4(0.55f, 0.93f, 0.61f, 1f), mapping.RailOrder.ToString());
            else
                ImGui.TextUnformatted(mapping.RailOrder.ToString());

            ImGui.TableNextColumn();
            ImGui.TextWrapped($"{(mapping.BaseNodeId == 0 ? "-" : mapping.BaseNodeId.ToString())} / {mapping.SliderNodeId?.ToString() ?? "-"}");

            ImGui.TableNextColumn();
            ImGui.TextWrapped($"({mapping.X:0.##}, {mapping.Y:0.##}) {mapping.Width}x{mapping.Height}");

            ImGui.TableNextColumn();
            DrawBoolPill(mapping.AnyVisible, "Yes", "No");

            ImGui.TableNextColumn();
            DrawBoolPill(mapping.AnyInteractive, "Yes", "No");

            ImGui.TableNextColumn();
            ImGui.TextUnformatted(mapping.AgentRowIndex?.ToString() ?? "-");

            ImGui.TableNextColumn();
            ImGui.TextWrapped(mapping.CharacterName is null
                ? "-"
                : $"{FormatSafeCharacterName(mapping.CharacterName)} ({mapping.Job ?? "-"})");

            ImGui.TableNextColumn();
            ImGui.TextWrapped(mapping.MappingNote);
        }

        ImGui.EndTable();
    }

    private static void DrawActiveExportPayload(BannerPartyAgentSnapshot snapshot)
    {
        ImGui.Separator();
        ImGui.TextUnformatted("Read-Only Active Row Payload");

        if (snapshot.ActiveExportPayload is null)
        {
            ImGui.TextWrapped(snapshot.ActiveExportStatus);
            return;
        }

        var payload = snapshot.ActiveExportPayload;
        if (!ImGui.BeginTable("FootballerBannerPartyPayloadTable", 2, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingFixedFit))
            return;

        ImGui.TableSetupColumn("Field");
        ImGui.TableSetupColumn("Value", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableHeadersRow();

        DrawPayloadRow("Camera position", payload.CameraPosition);
        DrawPayloadRow("Camera target", payload.CameraTarget);
        DrawPayloadRow("Image rotation", payload.ImageRotation.ToString());
        DrawPayloadRow("Camera zoom", payload.CameraZoom.ToString());
        DrawPayloadRow("Banner timeline", payload.BannerTimeline.ToString());
        DrawPayloadRow("Animation progress", payload.AnimationProgress.ToString("0.###"));
        DrawPayloadRow("Expression", payload.Expression.ToString());
        DrawPayloadRow("Head direction", payload.HeadDirection);
        DrawPayloadRow("Eye direction", payload.EyeDirection);
        DrawPayloadRow("Directional light color", payload.DirectionalLightingColor);
        DrawPayloadRow("Directional light brightness", payload.DirectionalLightingBrightness.ToString());
        DrawPayloadRow("Directional light angles", $"{payload.DirectionalLightingVerticalAngle} / {payload.DirectionalLightingHorizontalAngle}");
        DrawPayloadRow("Ambient light color", payload.AmbientLightingColor);
        DrawPayloadRow("Ambient light brightness", payload.AmbientLightingBrightness.ToString());
        DrawPayloadRow("Banner background", payload.BannerBg.ToString());

        ImGui.EndTable();
    }

    private static void DrawPayloadRow(string label, string value)
    {
        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        ImGui.TextUnformatted(label);
        ImGui.TableNextColumn();
        ImGui.TextWrapped(value);
    }

    private void DrawInspectButton(PartyShowcaseMember member, string idSuffix)
    {
        var canInspect = member.EntityId != 0;
        if (!canInspect)
            ImGui.BeginDisabled();

        if (ImGui.SmallButton($"Inspect##{idSuffix}"))
            plugin.OpenCharacterInspect(member);

        if (!canInspect)
            ImGui.EndDisabled();
    }

    private static void DrawInspectRow(string label, string value)
    {
        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        ImGui.TextUnformatted(label);
        ImGui.TableNextColumn();
        ImGui.TextWrapped(value);
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
            ? "Use this instead of reading the raw tables. With one real party member, press once and send the report."
            : "Use this instead of reading the raw tables. Press once, change the selected portrait row context, press again, then send both reports.";
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
            ? "Use this after CharacterInspect is stable. Use Capture Current Preview if you want the lower-biased widget image saved, then switch inspect targets and report again if needed."
            : "Open CharacterInspect for a live party member first, then use this instead of reading the raw inspect table.";

    private static string BuildInspectReportClipboardStatus(CharacterInspectResearchSnapshot snapshot)
        => snapshot.CaptureReady
            ? "Copied a concise CharacterInspect report to the clipboard. Capture another after switching inspect targets if you want a before/after pair."
            : "Copied a concise CharacterInspect report to the clipboard. Open inspect for a live party member if you still need the preview bounds and payload to populate.";

    private static string GetInspectSnipUsageNote(CharacterInspectResearchSnapshot snapshot)
        => snapshot.CaptureReady
            ? "Use Capture Current Preview while CharacterInspect is stable and keep the footballer window away from the preview area. The crop profile is saved globally for future targets."
            : "CharacterInspect must be open and stable before preview capture can run.";

    private void DrawPreviewScalingSelector(int currentScalePercent)
    {
        ImGui.SetNextItemWidth(84f);
        if (ImGui.BeginCombo("##PreviewScaling", $"{currentScalePercent}%"))
        {
            foreach (var option in PreviewScalePercents)
            {
                var selected = option == currentScalePercent;
                if (ImGui.Selectable($"{option}%", selected))
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
        ImGui.TextUnformatted("Scaling");
        ImGui.SameLine(0f, 4f);
        ImGui.TextDisabled("(?)");
        ShowHoverTooltip("Pick Same as character preview window!");
    }

    private static float ClampCropFraction(float value)
        => float.IsFinite(value) ? Math.Clamp(value, 0f, 0.9f) : 0f;

    private static string FormatNodePair(uint baseNodeId, uint? sliderNodeId)
        => $"{(baseNodeId == 0 ? "-" : baseNodeId.ToString())}/{sliderNodeId?.ToString() ?? "-"}";

    private static string FormatEntityId(uint entityId)
        => entityId == 0 ? "-" : $"0x{entityId:X8}";

    private string GetSafeDisplayName(PartyShowcaseMember member)
        => plugin.FormatDisplayName(member);

    private string GetSafeWorldLabel(PartyShowcaseMember member)
        => plugin.FormatWorldName(member.WorldName);

    private string FormatSafeCharacterName(string? characterName)
        => plugin.FormatCharacterName(characterName);

    private static string FormatAddress(nint address)
        => address == nint.Zero ? "-" : $"0x{address.ToInt64():X}";
}
