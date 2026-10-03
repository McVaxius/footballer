using System.Numerics;
using AethertekUI;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using footballer.Models;

namespace footballer.Windows;

public sealed partial class MainWindow
{
    private void DrawPresentation(Configuration cfg, int scalePercent, IReadOnlyList<FootShowcaseCard> cards)
    {
        DrawPresentationHeader();
        ImGui.Dummy(new Vector2(0, (FootballerPresentation.Compact ? 8 : 20) * MaterialTheme.Metrics.Scale));
        DrawNormalToolbar(cfg, scalePercent);
        ImGui.Dummy(new Vector2(0, (FootballerPresentation.Compact ? 8 : 20) * MaterialTheme.Metrics.Scale));
        DrawPresentationStatus(cfg, cards);
        ImGui.Dummy(new Vector2(0, FootballerPresentation.RegionGap * MaterialTheme.Metrics.Scale));

        if (cards.Count == 0)
        {
            UiGui.TextWrapped("No local player or party members are available yet, so there is nothing to render in the live showcase.");
            return;
        }

        var s = MaterialTheme.Metrics.Scale;
        var columns = Math.Clamp((int)((ImGui.GetContentRegionAvail().X + FootballerPresentation.CardGap * s) / (FootballerPresentation.CardWidth * s)), 1, 4);
        columns = Math.Min(columns, cards.Count);
        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new Vector2(FootballerPresentation.CardGap * .5f * s, 7 * s));
        if (ImGui.BeginTable("FootballerFootShowcaseTable", columns, ImGuiTableFlags.SizingStretchSame))
        {
            for (var i = 0; i < cards.Count; i++)
            {
                ImGui.TableNextColumn();
                DrawPresentationCard(cards[i]);
            }
            ImGui.EndTable();
        }
        ImGui.PopStyleVar();
    }

    private void DrawPresentationHeader()
    {
        var s = MaterialTheme.Metrics.Scale;
        var start = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var c = MaterialTheme.Current.Colors;
        var compact = FootballerPresentation.Compact;
        var titleX = compact ? 78 : 108;
        FootballerPresentation.Footprint(start + new Vector2(12, 0) * s, (compact ? 60 : 86) * s, c.Primary);
        ImGui.SetCursorScreenPos(start + new Vector2(titleX, 0) * s);
        float titleWidth;
        using (UiText.Font(compact ? UiFontRole.CompactTitle : UiFontRole.Title))
        {
            ImGui.TextUnformatted(PluginInfo.DisplayName);
            titleWidth = ImGui.GetItemRectSize().X;
        }
        ImGui.SetCursorScreenPos(start + new Vector2(titleX * s + titleWidth + 18 * s, (compact ? 18 : 27) * s));
        ImGui.TextColored(c.OnSurfaceVariant, "v" + typeof(Plugin).Assembly.GetName().Version);
        ImGui.SetCursorScreenPos(start + new Vector2(titleX, compact ? 43 : 58) * s);
        using (UiText.Font(compact ? UiFontRole.Body : UiFontRole.PluginName)) UiGui.TextColored(c.OnSurfaceVariant, "Party feet previews, simply.");

        var rightWidth = 716 * s;
        var wideHeader = width >= 1280 * s;
        var controlsY = start.Y + (wideHeader ? 16 : compact ? 78 : 104) * s;
        ImGui.SetCursorScreenPos(new Vector2(wideHeader ? start.X + width - rightWidth : start.X, controlsY));
        var compactPreference = plugin.Configuration.UiCompact;
        if (ImGui.Checkbox("C##CompactMode", ref compactPreference)) { plugin.Configuration.UiCompact = compactPreference; plugin.Configuration.Save(); }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip(UiText.T("Compact mode"));
        ImGui.SameLine(0, 12 * s);
        plugin.DrawAppearanceSelector();
        if (wideHeader) ImGui.SetCursorScreenPos(new Vector2(start.X + width - 250 * s, controlsY));
        else ImGui.SameLine(0, 20 * s);
        using (UiText.Font(UiFontRole.Action))
        {
            if (UiGui.Action("Ko-fi", "Ko-fi", MaterialIcon.ExternalLink, height: 36)) plugin.OpenUrl(PluginInfo.SupportUrl);
            ImGui.SameLine(0, 10 * s);
            if (UiGui.Action("Discord", "Discord", height: 36)) plugin.OpenUrl(PluginInfo.DiscordUrl);
        }
        ImGui.SetCursorScreenPos(start);
        ImGui.Dummy(new Vector2(width, (wideHeader ? FootballerPresentation.HeaderHeight : compact ? 116 : 146) * s));
        ImGui.GetWindowDrawList().AddLine(new Vector2(start.X, ImGui.GetItemRectMax().Y), ImGui.GetItemRectMax(), MaterialCanvas.Color(c.OutlineVariant));
    }

    // Every group wraps in its original order, including the additional footwear preference.
    private void ToolbarNext(string label, bool toggle = false, MaterialIcon icon = MaterialIcon.None)
    {
        var s = MaterialTheme.Metrics.Scale;
        var desired = ImGui.CalcTextSize(UiText.T(label)).X + (toggle ? 86 : icon == MaterialIcon.None ? 32 : 64) * s;
        var right = ImGui.GetCursorScreenPos().X + ImGui.GetContentRegionAvail().X;
        if (ImGui.GetItemRectMax().X + 10 * s + desired <= right) ImGui.SameLine(0, 10 * s);
    }

    private void DrawPresentationStatus(Configuration cfg, IReadOnlyList<FootShowcaseCard> cards)
    {
        var s = MaterialTheme.Metrics.Scale;
        var c = MaterialTheme.Current.Colors;
        var refresh = plugin.PartyFeetRefreshService;
        var start = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var state = UiText.T(refresh.IsActive ? "Party feet workflow: Refreshing" : "Party feet workflow: Ready");
        var previews = cards.Count(card => !string.IsNullOrWhiteSpace(card.FootImagePath));
        var hidden = cards.Count(card => card.FootStatusLabel == "Hidden");
        var counts = UiText.F("{0} previews available · {1} hidden by privacy", previews, hidden);
        var wrapped = ImGui.CalcTextSize(state).X + ImGui.CalcTextSize(counts).X + 80 * s > width;
        var height = Math.Max(FootballerPresentation.StatusHeight * s, (wrapped ? 2 : 1) * ImGui.GetTextLineHeight() + 24 * s);
        FootballerPresentation.Surface(start, start + new Vector2(width, height), true);
        var dot = start + new Vector2(24 * s, (wrapped ? 24 * s : height * .5f));
        ImGui.GetWindowDrawList().AddCircleFilled(dot, 10 * s, MaterialCanvas.Color(refresh.IsActive ? FootballerPresentation.Pending : FootballerPresentation.Ready), 24);
        ImGui.SetCursorScreenPos(dot + new Vector2(24 * s, -ImGui.GetTextLineHeight() * .5f));
        ImGui.TextColored(c.OnSurfaceVariant, state);
        ShowHoverTooltip(refresh.LastStatus);
        ImGui.SetCursorScreenPos(new Vector2(wrapped ? start.X + 48 * s : start.X + width - ImGui.CalcTextSize(counts).X - 18 * s,
            wrapped ? dot.Y + ImGui.GetTextLineHeight() * .5f + 4 * s : dot.Y - ImGui.GetTextLineHeight() * .5f));
        ImGui.TextColored(c.OnSurfaceVariant, counts);
        ImGui.SetCursorScreenPos(start);
        ImGui.Dummy(new Vector2(width, height));
        if (refresh.IsActive) UiGui.TextWrapped(refresh.LastStatus);
        else if (!cfg.PluginEnabled) UiGui.TextDisabled("Footballer is disabled — enable it before refreshing party feet.");
        else if (cards.Count <= 1) UiGui.TextDisabled("No party members detected — join or form a party, then Refresh party.");
        else if (previews == 0) UiGui.TextDisabled("No feet previews yet — match Scaling to CharacterInspect, then Refresh party.");
    }

    private void DrawPresentationCard(FootShowcaseCard card)
    {
        var s = MaterialTheme.Metrics.Scale;
        var c = MaterialTheme.Current.Colors;
        var cardWidth = ImGui.GetContentRegionAvail().X;
        var cardHeight = FootballerPresentation.CardHeight * s;
        // Longer resource strings grow the content; the child scrolls rather than truncating it.
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(FootballerPresentation.Compact ? 12 : 18) * s);
        ImGui.PushStyleVar(ImGuiStyleVar.ChildBorderSize, 0);
        ImGui.PushStyleColor(ImGuiCol.ChildBg, Vector4.Zero);
        var origin = ImGui.GetCursorScreenPos();
        FootballerPresentation.Surface(origin, origin + new Vector2(cardWidth, cardHeight));
        if (ImGui.BeginChild($"FootCard##{card.Member.CharacterKey}", new Vector2(0, cardHeight), false))
        {
            var start = ImGui.GetCursorScreenPos();
            var width = ImGui.GetContentRegionAvail().X;
            var icon = Plugin.TextureProvider.GetFromGameIcon(new GameIconLookup(62100u + card.Member.JobId)).GetWrapOrEmpty();
            if (icon.Handle != nint.Zero) ImGui.Image(icon.Handle, new Vector2(48) * s);
            else ImGui.Dummy(new Vector2(48) * s);
            ImGui.SetCursorScreenPos(start + new Vector2(74, 0) * s);
            using (UiText.Font(UiFontRole.PluginName))
            {
                ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + Math.Max(1, width - 74 * s));
                ImGui.TextUnformatted(GetUiDisplayName(card.Member));
                ImGui.PopTextWrapPos();
            }
            ShowHoverTooltip($"{GetSafeWorldLabel(card.Member)} | {card.Member.JobAbbreviation} {card.Member.Level}\n{card.VariantLabel}");
            UiGui.TextDisabled(UiText.F("Lv {0}  {1}", card.Member.Level, card.Member.JobAbbreviation));
            var headerBottom = Math.Max(start.Y + 54 * s, ImGui.GetItemRectMax().Y);
            var faceTop = new Vector2(start.X, headerBottom + (FootballerPresentation.Compact ? 8 : 16) * s);
            var faceSize = Math.Min(FootballerPresentation.FaceSize * s, width * .5f);
            var faceBox = new Vector2(faceSize);
            DrawImageBox(card.FaceImagePath, faceTop, faceBox, false, null);
            ImGui.SetCursorScreenPos(faceTop + new Vector2(faceSize + 16 * s, 0));
            var privacyHidden = card.FootStatusLabel == "Hidden";
            var label = privacyHidden ? "Face unavailable — feet hidden" : card.FootStatusLabel;
            var statusColor = GetCardStatusColor(card.FootStatusLabel);
            var noteWidth = Math.Max(1, width - faceSize - 16 * s);
            var text = UiText.T(label);
            var lines = ImGui.CalcTextSize(text, false, noteWidth - 18 * s).Y;
            var noteTop = faceTop.Y + Math.Max(0, (faceSize - lines) * .5f);
            if (!privacyHidden) ImGui.GetWindowDrawList().AddCircleFilled(new Vector2(faceTop.X + faceSize + 18 * s, noteTop + ImGui.GetTextLineHeight() * .5f), 8 * s, MaterialCanvas.Color(statusColor), 20);
            ImGui.SetCursorScreenPos(new Vector2(faceTop.X + faceSize + (privacyHidden ? 16 : 34) * s, noteTop));
            ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + Math.Max(1, noteWidth - (privacyHidden ? 0 : 18 * s)));
            ImGui.TextColored(c.OnSurfaceVariant, text);
            ImGui.PopTextWrapPos();
            ShowHoverTooltip(CardGuidance(card));
            var dividerY = Math.Max(faceTop.Y + faceSize, ImGui.GetItemRectMax().Y) + (FootballerPresentation.Compact ? 10 : 18) * s;
            ImGui.GetWindowDrawList().AddLine(new Vector2(start.X, dividerY), new Vector2(start.X + width, dividerY), MaterialCanvas.Color(c.OutlineVariant));
            ImGui.SetCursorScreenPos(new Vector2(start.X, dividerY + (FootballerPresentation.Compact ? 8 : 14) * s));
            UiGui.TextUnformatted("Feet preview");
            var feetTop = new Vector2(start.X, ImGui.GetItemRectMax().Y + 12 * s);
            var feetSize = new Vector2(width, Math.Max(100 * s, origin.Y + cardHeight - 28 * s - feetTop.Y));
            DrawImageBox(card.FootImagePath, feetTop, feetSize, true, privacyHidden ? "Hidden by privacy" : card.FootStatusLabel);
            ShowHoverTooltip(CardGuidance(card));
        }
        ImGui.EndChild();
        ImGui.PopStyleColor();
        ImGui.PopStyleVar(2);
    }

    private string GetUiDisplayName(PartyShowcaseMember member)
        => member.IsLocalPlayer ? UiText.F("{0} (You)", plugin.FormatCharacterName(member.Name)) : plugin.FormatCharacterName(member.Name);

    private static string CardGuidance(FootShowcaseCard card) => card.FootStatusLabel switch
    {
        "Preview captured" => "Last saved CharacterInspect preview. Refresh party to capture a new image.",
        "Inspect-ready" or "Pending capture" => "Match Scaling to CharacterInspect, then Refresh party to capture a preview.",
        "Hidden" => "Feet are hidden when Lodestone does not expose a face.",
        "Hold" => "Waiting for the Lodestone privacy check. Use Refresh Lodestone to retry.",
        "Off" => "The foot showcase toggle is currently off.",
        "Filtered" or "Self hidden" => "Hidden by your display settings.",
        "No live entity" => "This member is not available for inspection in the current game scene.",
        _ => card.FootStatusNote,
    };

    private static void DrawImageBox(string? path, Vector2 min, Vector2 size, bool feet, string? placeholder)
    {
        var c = MaterialTheme.Current.Colors;
        var s = MaterialTheme.Metrics.Scale;
        var dl = ImGui.GetWindowDrawList();
        MaterialCanvas.Surface(min, min + size, c.SurfaceContainerHigh, c.SurfaceContainerLowest, 4 * s);
        dl.AddRect(min, min + size, MaterialCanvas.Color(c.Outline), 4 * s);
        var wrap = string.IsNullOrWhiteSpace(path) || !File.Exists(path) ? default : Plugin.TextureProvider.GetFromFile(path).GetWrapOrEmpty();
        if (wrap is not null && wrap.Handle != nint.Zero && wrap.Width > 0 && wrap.Height > 0)
        {
            var fit = ScaleToFit(wrap.Size, size - new Vector2(2 * s));
            var p = min + (size - fit) * .5f;
            dl.AddImage(wrap.Handle, p, p + fit);
        }
        else if (feet)
        {
            var center = min + size * .5f - new Vector2(0, 20 * s);
            if (placeholder == "Hidden by privacy")
            {
                dl.AddRectFilled(center - new Vector2(26, 2) * s, center + new Vector2(26, 40) * s, MaterialCanvas.Color(c.OnSurfaceVariant), 4 * s);
                dl.AddCircle(center - new Vector2(0, 2) * s, 18 * s, MaterialCanvas.Color(c.OnSurfaceVariant), 24, 7 * s);
                dl.AddCircleFilled(center + new Vector2(0, 16) * s, 5 * s, MaterialCanvas.Color(c.SurfaceContainerLowest), 16);
            }
            else FootballerPresentation.Footprint(center - new Vector2(26, 28) * s, 60 * s, MaterialColor.Alpha(c.OnSurfaceVariant, .5f));
            var text = UiText.T(placeholder ?? "No image yet.");
            dl.AddText(min + new Vector2((size.X - ImGui.CalcTextSize(text).X) * .5f, size.Y * .5f + 40 * s), MaterialCanvas.Color(c.OnSurfaceVariant), text);
        }
        else
        {
            var center = min + size * .5f;
            dl.AddCircleFilled(center - new Vector2(0, 18) * s, size.X * .14f, MaterialCanvas.Color(MaterialColor.Alpha(c.OnSurfaceVariant, .45f)), 24);
            dl.AddRectFilled(center + new Vector2(-size.X * .25f, size.Y * .10f), center + new Vector2(size.X * .25f, size.Y * .35f), MaterialCanvas.Color(MaterialColor.Alpha(c.OnSurfaceVariant, .45f)), size.X * .15f);
        }
        ImGui.SetCursorScreenPos(min);
        ImGui.Dummy(size);
    }
}
