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
        var s = MaterialTheme.Metrics.Scale;
        var start = ImGui.GetCursorScreenPos();
        DrawPresentationHeader();
        ImGui.SetCursorScreenPos(new Vector2(start.X, ImGui.GetItemRectMax().Y + (FootballerPresentation.Compact ? 14 : 18) * s));
        DrawNormalToolbar(cfg, scalePercent);
        ImGui.SetCursorScreenPos(new Vector2(start.X, ImGui.GetItemRectMax().Y + (FootballerPresentation.Compact ? 4 : 20) * s));
        DrawPresentationStatus(cfg, cards);
        var statusBottom = ImGui.GetItemRectMax().Y;

        if (cards.Count == 0)
        {
            ImGui.SetCursorScreenPos(new Vector2(start.X, statusBottom + FootballerPresentation.RegionGap * s));
            UiGui.TextWrapped("No local player or party members are available yet, so there is nothing to render in the live showcase.");
            return;
        }

        var minimumCardWidth = FootballerPresentation.CardWidth * s;
        var callerScale = ImGuiP.GetCurrentWindow().FontWindowScale;
        using (UiText.Font(UiFontRole.PluginName))
        {
            ImGui.SetWindowFontScale(callerScale * FootballerPresentation.CardNameScale);
            try
            {
                minimumCardWidth = Math.Max(minimumCardWidth, cards.Max(card => MaterialText.Measure(GetUiDisplayName(card.Member)).X)
                    + (FootballerPresentation.Compact ? 104 : 110) * s);
            }
            finally { ImGui.SetWindowFontScale(callerScale); }
        }
        var columns = Math.Clamp((int)((ImGui.GetContentRegionAvail().X + FootballerPresentation.CardGap * s) / (minimumCardWidth + FootballerPresentation.CardGap * s)), 1, 4);
        columns = Math.Min(columns, cards.Count);
        ImGui.SetCursorScreenPos(new Vector2(start.X, statusBottom + (FootballerPresentation.RegionGap - 7) * s));
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
        var titleX = compact ? 98 : 108;
        FootballerPresentation.Footprint(start + new Vector2(compact ? 19 : 16, compact ? 0 : -4) * s,
            (compact ? new Vector2(63, 72) : new Vector2(77, 89)) * s, c.Primary);
        ImGui.SetCursorScreenPos(start + new Vector2(titleX, compact ? -2 : -6) * s);
        float titleWidth;
        using (UiText.Font(compact ? UiFontRole.CompactTitle : UiFontRole.Title))
        {
            var callerScale = ImGuiP.GetCurrentWindow().FontWindowScale;
            ImGui.SetWindowFontScale(callerScale * FootballerPresentation.TitleScale);
            try
            {
                MaterialText.Text(PluginInfo.DisplayName);
                titleWidth = ImGui.GetItemRectSize().X;
            }
            finally { ImGui.SetWindowFontScale(callerScale); }
        }
        ImGui.SetCursorScreenPos(start + new Vector2(titleX * s + titleWidth + 18 * s, (compact ? 18 : 27) * s));
        MaterialText.TextColored(c.OnSurfaceVariant, "v" + typeof(Plugin).Assembly.GetName().Version);
        ImGui.SetCursorScreenPos(start + new Vector2(titleX, compact ? 43 : 58) * s);
        using (UiText.Font(compact ? UiFontRole.Body : UiFontRole.PluginName))
        {
            var callerScale = ImGuiP.GetCurrentWindow().FontWindowScale;
            ImGui.SetWindowFontScale(callerScale * FootballerPresentation.SubtitleScale);
            try { UiGui.TextColored(c.OnSurfaceVariant, "Party feet previews, simply."); }
            finally { ImGui.SetWindowFontScale(callerScale); }
        }

        var languageLabel = UiText.Languages.First(language => language.Code == UiText.Current.Language).Name;
        var languageWidth = (compact ? 196 : 210) * s;
        var languageMinimum = MathF.Ceiling(MaterialText.Measure(languageLabel).X + (compact ? 101 : 105) * s);
        var transparencyWidth = ImGui.GetFrameHeight() + ImGui.GetStyle().ItemInnerSpacing.X + MaterialText.Measure(UiText.T("Transparency")).X;
        var rightWidth = (compact ? 225 : 247) * s + transparencyWidth + 40 * s
            + (plugin.Configuration.UiCompactVisibleOnMainWindow ? ImGui.GetFrameHeight() + MaterialText.Measure("C").X + 26 * s : 0)
            + (plugin.Configuration.UiLanguageVisibleOnMainWindow ? Math.Max(languageWidth, languageMinimum) + 20 * s : 0);
        var wideHeader = width >= Math.Max(1280 * s, rightWidth + 560 * s);
        var controlsY = start.Y + (wideHeader ? compact ? 8 : 16 : compact ? 78 : 104) * s;
        ImGui.SetCursorScreenPos(new Vector2(wideHeader ? start.X + width - rightWidth : start.X, controlsY));
        if (plugin.Configuration.UiCompactVisibleOnMainWindow)
        {
            var compactPreference = plugin.Configuration.UiCompact;
            bool compactChanged;
            using (var checkboxStyle = new MaterialStyleScope())
            {
                if (compact)
                {
                    checkboxStyle.Style(ImGuiStyleVar.FramePadding, new Vector2(ImGui.GetStyle().FramePadding.X, 0));
                    checkboxStyle.Style(ImGuiStyleVar.ItemInnerSpacing, new Vector2(15 * s, ImGui.GetStyle().ItemInnerSpacing.Y));
                    ImGui.SetCursorPosY(ImGui.GetCursorPosY() + (47 * s - ImGui.GetFrameHeight()) * .5f);
                }
                compactChanged = ImGui.Checkbox("C##CompactMode", ref compactPreference);
            }
            if (compactChanged) { plugin.Configuration.UiCompact = compactPreference; plugin.Configuration.Save(); }
            if (ImGui.IsItemHovered()) MaterialText.SetTooltip(UiText.T("Compact mode"));
            ImGui.SetCursorScreenPos(new Vector2(ImGui.GetItemRectMax().X + (compact ? 26 : 12) * s, controlsY));
        }
        if (plugin.Configuration.UiLanguageVisibleOnMainWindow)
        {
            plugin.DrawHeaderAppearanceSelector();
            ImGui.SameLine(0, 20 * s);
        }
        plugin.DrawTransparency();
        if (wideHeader) ImGui.SetCursorScreenPos(new Vector2(start.X + width - (compact ? 225 : 247) * s, controlsY));
        else ImGui.SameLine(0, 20 * s);
        var linkStart = ImGui.GetCursorScreenPos();
        ImGui.GetWindowDrawList().AddLine(linkStart + new Vector2(compact ? -23 : -28, 6) * s, linkStart + new Vector2(compact ? -23 : -28, 42) * s, MaterialCanvas.Color(c.OutlineVariant));
        using (UiText.Font(UiFontRole.Action))
        {
            if (DrawHeaderLink("Ko-fi", c.Primary, false)) plugin.OpenUrl(PluginInfo.SupportUrl);
            ImGui.SameLine(0, (compact ? 18 : 34) * s);
            if (DrawHeaderLink("Discord", FootballerPresentation.Rgb(0x928DFF), true)) plugin.OpenUrl(PluginInfo.DiscordUrl);
        }
        ImGui.SetCursorScreenPos(start);
        ImGui.Dummy(new Vector2(width, (wideHeader ? FootballerPresentation.HeaderHeight : compact ? 137 : 165) * s));
        ImGui.GetWindowDrawList().AddLine(new Vector2(start.X, ImGui.GetItemRectMax().Y), ImGui.GetItemRectMax(), MaterialCanvas.Color(c.OutlineVariant));
    }

    private static bool DrawHeaderLink(string id, Vector4 color, bool discord)
    {
        var s = MaterialTheme.Metrics.Scale;
        var label = UiText.T(id);
        var width = MaterialText.Measure(label).X + 50 * s;
        var height = MaterialText.RequiresShaping(label) ? Math.Max(46 * s, MaterialText.Measure(label).Y + 14 * s) : 46 * s;
        ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 0);
        ImGui.PushStyleColor(ImGuiCol.Text, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.Button, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, Vector4.Zero);
        var clicked = ImGui.Button(id, new Vector2(width, height));
        ImGui.PopStyleColor(4);
        ImGui.PopStyleVar();
        var min = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();
        if (ImGui.IsItemHovered()) color = MaterialColor.Layer(color, MaterialTheme.Current.Colors.OnSurface, .12f);
        color.W *= ImGui.GetStyle().Alpha;
        var iconPosition = min + new Vector2(0, 7) * s;
        if (discord) FootballerPresentation.Discord(iconPosition, 32 * s, color);
        else MaterialIcons.Draw(MaterialIcon.Heart, iconPosition, 32 * s, color);
        var dl = ImGui.GetWindowDrawList();
        dl.PushClipRect(min, max, true);
        try { MaterialText.AddText(dl,min + new Vector2(44 * s, (height - MaterialText.Measure(label).Y) * .5f), MaterialCanvas.Color(color), label); }
        finally { dl.PopClipRect(); }
        return clicked;
    }

    // Keep each complete group together when the translated toolbar needs another row.
    private void ToolbarNext(string label, bool toggle = false, MaterialIcon icon = MaterialIcon.None, float measuredPixels = 0)
    {
        var s = MaterialTheme.Metrics.Scale;
        var desired = measuredPixels > 0 ? measuredPixels : MaterialText.Measure(UiText.T(label)).X + (toggle ? icon == MaterialIcon.None ? 86 : 118 : icon == MaterialIcon.None ? 32 : 64) * s;
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
        var wrapped = MaterialText.Measure(state).X + MaterialText.Measure(counts).X + 80 * s > width;
        var height = Math.Max(FootballerPresentation.StatusHeight * s, (wrapped ? 2 : 1) * ImGui.GetTextLineHeight() + (FootballerPresentation.Compact ? 22 : 24) * s);
        FootballerPresentation.Surface(start, start + new Vector2(width, height), true);
        var dot = start + new Vector2(24 * s, (wrapped ? 24 * s : height * .5f));
        ImGui.GetWindowDrawList().AddCircleFilled(dot, 10 * s, MaterialCanvas.Color(refresh.IsActive ? FootballerPresentation.Pending : FootballerPresentation.Ready), 24);
        ImGui.SetCursorScreenPos(dot + new Vector2(24 * s, -ImGui.GetTextLineHeight() * .5f));
        MaterialText.TextColored(c.OnSurfaceVariant, state);
        ShowHoverTooltip(refresh.LastStatus);
        ImGui.SetCursorScreenPos(new Vector2(wrapped ? start.X + 48 * s : start.X + width - MaterialText.Measure(counts).X - 18 * s,
            wrapped ? dot.Y + ImGui.GetTextLineHeight() * .5f + 4 * s : dot.Y - ImGui.GetTextLineHeight() * .5f));
        MaterialText.TextColored(c.OnSurfaceVariant, counts);
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
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(18) * s);
        ImGui.PushStyleVar(ImGuiStyleVar.ChildBorderSize, 0);
        ImGui.PushStyleColor(ImGuiCol.ChildBg, Vector4.Zero);
        var origin = ImGui.GetCursorScreenPos();
        FootballerPresentation.Surface(origin, origin + new Vector2(cardWidth, cardHeight));
        if (ImGui.BeginChild($"FootCard##{card.Member.CharacterKey}", new Vector2(0, cardHeight), false, ImGuiWindowFlags.AlwaysUseWindowPadding))
        {
            var start = ImGui.GetCursorScreenPos();
            var width = ImGui.GetContentRegionAvail().X;
            var compact = FootballerPresentation.Compact;
            var nameX = compact ? 68 : 74;
            var iconSize = compact ? 42 : 48;
            var icon = Plugin.TextureProvider.GetFromGameIcon(new GameIconLookup(62100u + card.Member.JobId)).GetWrapOrEmpty();
            if (icon.Handle != nint.Zero) ImGui.Image(icon.Handle, new Vector2(iconSize) * s);
            else ImGui.Dummy(new Vector2(iconSize) * s);
            ImGui.SetCursorScreenPos(start + new Vector2(nameX, compact ? -8 : -6) * s);
            using (UiText.Font(UiFontRole.PluginName))
            {
                var callerScale = ImGuiP.GetCurrentWindow().FontWindowScale;
                ImGui.SetWindowFontScale(callerScale * FootballerPresentation.CardNameScale);
                try { MaterialText.Text(GetUiDisplayName(card.Member)); }
                finally { ImGui.SetWindowFontScale(callerScale); }
            }
            ShowHoverTooltip($"{GetSafeWorldLabel(card.Member)} | {card.Member.JobAbbreviation} {card.Member.Level}\n{GetUiVariantLabel(card)}", false);
            ImGui.SetCursorScreenPos(start + new Vector2(nameX, compact ? 20 : 29) * s);
            UiGui.TextColored(c.OnSurfaceVariant, UiText.F("Lv {0}  {1}", card.Member.Level, card.Member.JobAbbreviation));
            var headerBottom = Math.Max(start.Y + (compact ? 44 : 54) * s, ImGui.GetItemRectMax().Y);
            var faceTop = new Vector2(start.X, headerBottom + (compact ? 8 : 18) * s);
            var faceSize = Math.Min(FootballerPresentation.FaceSize * s, width * .5f);
            var faceBox = new Vector2(faceSize);
            DrawImageBox(card.FaceImagePath, faceTop, faceBox, false, null);
            ImGui.SetCursorScreenPos(faceTop + new Vector2(faceSize + 16 * s, 0));
            var privacyHidden = card.FootStatusLabel == "Hidden";
            var label = privacyHidden ? "Face unavailable — feet hidden" : card.FootStatusLabel;
            var statusColor = GetCardStatusColor(card.FootStatusLabel);
            var noteWidth = Math.Max(1, width - faceSize - 16 * s);
            var text = UiText.T(label);
            var lines = MaterialText.Measure(text, false, noteWidth - 18 * s).Y;
            var noteTop = faceTop.Y + Math.Max(0, (faceSize - lines) * .5f);
            if (!privacyHidden) ImGui.GetWindowDrawList().AddCircleFilled(new Vector2(faceTop.X + faceSize + 18 * s, noteTop + ImGui.GetTextLineHeight() * .5f), 8 * s, MaterialCanvas.Color(statusColor), 20);
            ImGui.SetCursorScreenPos(new Vector2(faceTop.X + faceSize + (privacyHidden ? 16 : 34) * s, noteTop));
            ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + Math.Max(1, noteWidth - (privacyHidden ? 0 : 18 * s)));
            MaterialText.TextColored(c.OnSurfaceVariant, text);
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
            MaterialText.AddText(dl,min + new Vector2((size.X - MaterialText.Measure(text).X) * .5f, size.Y * .5f + 40 * s), MaterialCanvas.Color(c.OnSurfaceVariant), text);
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
