using System.Numerics;
using AethertekUI;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace footballer.Windows;

// Native widgets receive their original English labels/IDs. Only their visible label is painted in the selected locale.
// This also preserves English-derived helper IDs and existing saved window identities.
internal static class UiGui
{
    internal static void TextUnformatted(string text) => MaterialText.Text(UiText.T(text));
    internal static void TextWrapped(string text)
    {
        var window = ImGuiP.GetCurrentWindow();
        // Horizontal content can expand WorkRect beyond the visible pane on later frames.
        var viewportWidth = window.InnerRect.Max.X - window.WindowPadding.X - ImGui.GetCursorScreenPos().X - window.Scroll.X;
        var width = Math.Max(1, Math.Min(ImGui.GetContentRegionAvail().X, viewportWidth));
        ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + width);
        try { MaterialText.Text(UiText.T(text)); }
        finally { ImGui.PopTextWrapPos(); }
    }
    internal static void TextDisabled(string text)
    {
        ImGui.PushTextWrapPos(0);
        try { MaterialText.TextDisabled(UiText.T(text)); }
        finally { ImGui.PopTextWrapPos(); }
    }
    internal static void Text(string text) => MaterialText.Text(UiText.T(text));
    internal static void BulletText(string text) => MaterialText.BulletText(UiText.T(text));
    internal static void TextColored(Vector4 color, string text) => MaterialText.TextColored(color, UiText.T(text));

    private static float ToolbarTextScale => FootballerPresentation.Compact ? .86f : .875f;

    internal static Vector2 ScaledTextSize(string text, float scale)
    {
        if (!MaterialText.RequiresShaping(text)) return MaterialText.Measure(text) * scale;
        var callerScale = ImGuiP.GetCurrentWindow().FontWindowScale;
        ImGui.SetWindowFontScale(callerScale * scale);
        try { return MaterialText.Measure(text); }
        finally { ImGui.SetWindowFontScale(callerScale); }
    }

    internal static float ActionWidth(string id, string text, MaterialIcon icon)
    {
        var compact = FootballerPresentation.Compact;
        var minimum = id switch
        {
            "Refresh party" or "Refreshing..." => compact ? 164 : 174,
            "Settings" => compact ? 135 : 142,
            "Krangle Names" or "Un-Krangle" => compact ? 176 : 185,
            "Refresh Lodestone" => 201,
            _ => 0,
        };
        return Math.Max(minimum * MaterialTheme.Metrics.Scale, MathF.Ceiling(ScaledTextSize(UiText.T(text), ToolbarTextScale).X)
            + (icon == MaterialIcon.None ? 32 : compact ? 70 : 78) * MaterialTheme.Metrics.Scale);
    }

    private static float ToggleTextScale(string original, bool footprint)
        => original == "Enabled" ? FootballerPresentation.Compact ? 1 : 1.08f : footprint ? FootballerPresentation.Compact ? .81f : .875f : ToolbarTextScale;

    private static float ToggleIconWidth(MaterialIcon icon, bool footprint, bool footwear)
        => icon == MaterialIcon.Power ? FootballerPresentation.Compact ? 50 : 58 : footwear ? 52 : footprint || icon != MaterialIcon.None ? FootballerPresentation.Compact ? 36 : 40 : 0;

    internal static float ToggleWidth(string original, MaterialIcon icon = MaterialIcon.None, bool footprint = false, bool footwear = false)
    {
        var s = MaterialTheme.Metrics.Scale;
        var minimum = original == "Enabled" ? FootballerPresentation.Compact ? 201 : 217 : footprint ? FootballerPresentation.Compact ? 211 : 225 : footwear ? 268 : 0;
        return Math.Max(minimum * s, MathF.Ceiling(ScaledTextSize(UiText.T(original), ToggleTextScale(original, footprint)).X)
            + ((footwear ? 90 : footprint ? 80 : 86) + ToggleIconWidth(icon, footprint, footwear)) * s);
    }

    // Native controls retain the original ID, hit testing, focus and keyboard behavior.
    internal static bool Action(string id, string text, MaterialIcon icon = MaterialIcon.None, bool disabled = false, float height = 0)
    {
        var c = MaterialTheme.Current.Colors;
        var s = MaterialTheme.Metrics.Scale;
        var label = UiText.T(text);
        var compact = FootballerPresentation.Compact;
        var width = ActionWidth(id, text, icon);
        var callerScale = ImGuiP.GetCurrentWindow().FontWindowScale;
        ImGui.SetWindowFontScale(callerScale * ToolbarTextScale);
        ImGui.BeginGroup();
        try
        {
            using var controls = MaterialControls.Push(MaterialControlContext.Toolbar);
            using var lineHeight = MaterialText.PushLineHeight(label);
            var contentHeight = Math.Max(MaterialText.Measure(label).Y, icon == MaterialIcon.None ? 0 : (compact ? 32 : 36) * s);
            height = Math.Max(height, Math.Max(MaterialControls.Metrics.Height, contentHeight + (compact ? 4 : 8) * s) / s);
            ImGui.BeginDisabled(disabled);
            try
            {
            ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, s);
            ImGui.PushStyleColor(ImGuiCol.Button, c.SurfaceContainerHigh);
            ImGui.PushStyleColor(ImGuiCol.Border, c.OutlineVariant);
            ImGui.PushStyleColor(ImGuiCol.Text, Vector4.Zero);
            var clicked = ImGui.Button(id, new Vector2(width, height * s));
            ImGui.PopStyleColor(3);
            ImGui.PopStyleVar();
            var min = ImGui.GetItemRectMin();
            var max = ImGui.GetItemRectMax();
            var foreground = c.OnSurface;
            foreground.W *= ImGui.GetStyle().Alpha;
            var dl = ImGui.GetWindowDrawList();
            dl.PushClipRect(min, max, true);
            try
            {
            var iconSize = compact ? 32 : 36;
            if (icon != MaterialIcon.None) MaterialIcons.Draw(icon, min + new Vector2((compact ? 14 : 16) * s, (height - iconSize) * .5f * s), iconSize * s, foreground);
            MaterialText.AddText(dl,min + new Vector2((icon == MaterialIcon.None ? 16 : compact ? 54 : 62) * s, (height * s - MaterialText.Measure(label).Y) * .5f), MaterialCanvas.Color(foreground), label);
            }
            finally { dl.PopClipRect(); }
            return clicked;
            }
            finally { ImGui.EndDisabled(); }
        }
        finally { ImGui.EndGroup(); ImGui.SetWindowFontScale(callerScale); }
    }
    internal static bool Toggle(string original, ref bool value, float height = 0, MaterialIcon icon = MaterialIcon.None, bool footprint = false, bool footwear = false)
    {
        if (height <= 0) height = FootballerPresentation.ToolbarHeight;
        var s = MaterialTheme.Metrics.Scale;
        var c = MaterialTheme.Current.Colors;
        var label = UiText.T(original);
        var compact = FootballerPresentation.Compact;
        var iconWidth = ToggleIconWidth(icon, footprint, footwear) * s;
        var width = ToggleWidth(original, icon, footprint, footwear);
        var callerScale = ImGuiP.GetCurrentWindow().FontWindowScale;
        ImGui.SetWindowFontScale(callerScale * ToggleTextScale(original, footprint));
        ImGui.BeginGroup();
        try
        {
            if (MaterialText.RequiresShaping(label)) height = Math.Max(height, MaterialText.Measure(label).Y / s + 16);
            var nativeWidth = MaterialText.Measure(original).X;
            ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(0, (height * s - ImGui.GetTextLineHeight()) * .5f));
            ImGui.PushStyleVar(ImGuiStyleVar.ItemInnerSpacing, new Vector2(Math.Max(0, width - ImGui.GetFrameHeight() - nativeWidth), 0));
            ImGui.PushStyleColor(ImGuiCol.Text, Vector4.Zero);
            ImGui.PushStyleColor(ImGuiCol.FrameBg, Vector4.Zero);
            ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, Vector4.Zero);
            ImGui.PushStyleColor(ImGuiCol.FrameBgActive, Vector4.Zero);
            ImGui.PushStyleColor(ImGuiCol.CheckMark, Vector4.Zero);
            var changed = ImGui.Checkbox(original, ref value);
            ImGui.PopStyleColor(5);
            ImGui.PopStyleVar(2);
            var min = ImGui.GetItemRectMin();
            var max = ImGui.GetItemRectMax();
            var dl = ImGui.GetWindowDrawList();
            var emphasized = original == "Enabled" && value;
            var fill = emphasized ? c.PrimaryContainer : c.SurfaceContainerHigh;
            if (ImGui.IsItemHovered()) fill = MaterialColor.Layer(fill, c.OnSurface, .08f);
            if (ImGui.IsItemActive()) fill = MaterialColor.Layer(fill, c.OnSurface, .12f);
            dl.AddRectFilled(min, max, MaterialCanvas.Color(fill), 4 * s);
            dl.AddRect(min, max, MaterialCanvas.Color(ImGui.IsItemFocused() || emphasized ? c.Primary : c.OutlineVariant), 4 * s);
            var track = min + new Vector2(width - 58 * s, (height - 26) * .5f * s);
            dl.AddRectFilled(track, track + new Vector2(44, 26) * s, MaterialCanvas.Color(value ? c.Primary : c.Outline), 13 * s);
            dl.AddCircleFilled(track + new Vector2(value ? 31 : 13, 13) * s, 10 * s, MaterialCanvas.Color(c.OnSurface), 24);
            dl.PushClipRect(min, max, true);
            try
            {
            var iconSize = icon == MaterialIcon.Power ? compact ? 38 : 42 : footwear ? 34 : compact ? 28 : 30;
            var iconPosition = min + new Vector2((icon == MaterialIcon.Power && !compact ? 11 : 14) * s, (height - iconSize) * .5f * s);
            if (icon != MaterialIcon.None) MaterialIcons.Draw(icon, iconPosition, iconSize * s, icon == MaterialIcon.Power ? value ? c.Primary : c.OnSurfaceVariant : c.OnSurface);
            if (footprint) FootballerPresentation.Footprint(iconPosition, iconSize * s, c.OnSurface);
            if (footwear) FootballerPresentation.Shoe(iconPosition, iconSize * s, c.OnSurface);
            MaterialText.AddText(dl,min + new Vector2(14 * s + iconWidth, (height * s - MaterialText.Measure(label).Y) * .5f), MaterialCanvas.Color(c.OnSurface), label);
            }
            finally { dl.PopClipRect(); }
            return changed;
        }
        finally { ImGui.EndGroup(); ImGui.SetWindowFontScale(callerScale); }
    }

    internal static bool SliderFloat(string label, ref float value, float min, float max, string format)
    {
        BeginField(label);
        try { return ImGui.SliderFloat("", ref value, min, max, format); }
        finally { ImGui.PopID(); }
    }
    internal static bool CollapsingHeader(string label)
    {
        using var height = MaterialText.PushLineHeight(UiText.T(label.Split("##", 2)[0]));
        var position = ImGui.GetCursorScreenPos();
        ImGui.PushStyleColor(ImGuiCol.Text, Vector4.Zero);
        var open = ImGui.CollapsingHeader(label);
        ImGui.PopStyleColor();
        var c = MaterialTheme.Current.Colors;
        var dl = ImGui.GetWindowDrawList();
        dl.PushClipRect(ImGui.GetItemRectMin(), ImGui.GetItemRectMax(), true);
        try
        {
        MaterialIcons.Draw(open ? MaterialIcon.ChevronDown : MaterialIcon.ArrowRight, position + ImGui.GetStyle().FramePadding, ImGui.GetFontSize(), c.OnSurface);
        MaterialText.AddText(dl,position + new Vector2(ImGui.GetFontSize() + ImGui.GetStyle().FramePadding.X * 2, ImGui.GetStyle().FramePadding.Y), MaterialCanvas.Color(c.OnSurface), UiText.T(label));
        }
        finally { dl.PopClipRect(); }
        return open;
    }
    private static void Label(string original,Vector2 position,Vector4 background,Vector4 foreground,Vector2? clip=null,string? display=null)
    {
        var visible=original.Split("##",2)[0];
        var translated=display ?? UiText.T(visible);
        if(translated==visible && !MaterialText.RequiresShaping(translated)) return;
        var dl=ImGui.GetWindowDrawList();
        var width=Math.Max(MaterialText.Measure(visible).X,MaterialText.Measure(translated).X);
        if(clip is { } max) dl.PushClipRect(position,max,true);
        try
        {
            dl.AddRectFilled(position,position+new Vector2(width,Math.Max(ImGui.GetTextLineHeight(), MaterialText.Measure(translated).Y)),ImGui.ColorConvertFloat4ToU32(background));
            foreground.W*=ImGui.GetStyle().Alpha;
            MaterialText.AddText(dl,position,ImGui.ColorConvertFloat4ToU32(foreground),translated);
        }
        finally { if(clip.HasValue) dl.PopClipRect(); }
    }
    internal static bool Button(string label,string? display=null)
    {
        var translated=display ?? UiText.T(label.Split("##",2)[0]);
        using var controls = ImGui.GetStyle().FramePadding.Y == 0 || MaterialControls.Context == MaterialControlContext.Dense
            ? default(MaterialControls.ControlScope) : MaterialControls.Push(MaterialControlContext.Toolbar);
        using var height = MaterialText.PushLineHeight(translated);
        var width=MaterialText.Measure(translated).X+2*ImGui.GetStyle().FramePadding.X;
        if(width>ImGui.GetContentRegionAvail().X && ImGui.GetCursorPosX()>ImGui.GetStyle().WindowPadding.X+1) ImGui.NewLine();
        var foreground=ImGui.GetStyle().Colors[(int)ImGuiCol.Text];
        ImGui.PushStyleColor(ImGuiCol.Text,Vector4.Zero);
        var clicked=ImGui.Button(label,new Vector2(width,0));
        ImGui.PopStyleColor();
        var min=ImGui.GetItemRectMin(); var max=ImGui.GetItemRectMax();
        foreground.W*=ImGui.GetStyle().Alpha;
        ImGui.GetWindowDrawList().PushClipRect(min,max,true);
        try { MaterialText.AddText(ImGui.GetWindowDrawList(),min+(max-min-MaterialText.Measure(translated))*.5f,ImGui.ColorConvertFloat4ToU32(foreground),translated); }
        finally { ImGui.GetWindowDrawList().PopClipRect(); }
        return clicked;
    }
    internal static bool SmallButton(string label,string? display=null)
    {
        // Native small buttons use the same ID and behavior with zero vertical padding.
        using var padding = new MaterialStyleScope();
        padding.Style(ImGuiStyleVar.FramePadding,new Vector2(ImGui.GetStyle().FramePadding.X,0));
        return Button(label,display);
    }
    internal static bool Checkbox(string label,ref bool value)
    {
        var visible=label.Split("##",2)[0];
        var translated=UiText.T(visible);
        using var height = MaterialText.PushLineHeight(translated);
        var foreground=ImGui.GetStyle().Colors[(int)ImGuiCol.Text];
        var gap=ImGui.GetStyle().ItemInnerSpacing;
        // Native Checkbox sizes its hit area from the original label. Adjust that size for the
        // translated ink while keeping the native widget and its original ID.
        ImGui.PushStyleVar(ImGuiStyleVar.ItemInnerSpacing,new Vector2(Math.Max(0,gap.X+MaterialText.Measure(translated).X-MaterialText.Measure(visible).X),gap.Y));
        ImGui.PushStyleColor(ImGuiCol.Text,Vector4.Zero);
        var changed=ImGui.Checkbox(label,ref value);
        ImGui.PopStyleColor();
        ImGui.PopStyleVar();
        var p=ImGui.GetItemRectMin()+new Vector2(ImGui.GetFrameHeight()+gap.X,ImGui.GetStyle().FramePadding.Y);
        foreground.W*=ImGui.GetStyle().Alpha;
        MaterialText.AddText(ImGui.GetWindowDrawList(),p,ImGui.ColorConvertFloat4ToU32(foreground),translated);
        return changed;
    }
    internal static bool Selectable(string original,bool selected,string? display=null)
    {
        var translated=display ?? UiText.T(original.Split("##",2)[0]);
        var origin=ImGui.GetCursorScreenPos();
        var width=ImGui.GetContentRegionAvail().X;
        var foreground=ImGui.GetStyle().Colors[(int)ImGuiCol.Text];
        var height=Math.Max(ImGui.GetTextLineHeight(), MaterialText.Measure(translated).Y);
        ImGui.PushStyleColor(ImGuiCol.Text,Vector4.Zero);
        var clicked=ImGui.Selectable(original,selected,ImGuiSelectableFlags.None,new Vector2(0,height));
        ImGui.PopStyleColor();
        foreground.W*=ImGui.GetStyle().Alpha;
        var dl=ImGui.GetWindowDrawList();
        MaterialText.AddText(dl,origin,ImGui.ColorConvertFloat4ToU32(foreground),translated);
        if(MaterialText.Measure(translated).X>width && ImGui.IsItemHovered()) MaterialText.SetTooltip(translated);
        return clicked;
    }
    private static void BeginField(string label)
    {
        var requested = ImGui.CalcItemWidth();
        var minimum = MathF.Ceiling(Math.Max(80 * MaterialTheme.Metrics.Scale,
            MaterialText.Measure("00000000").X + 2 * ImGui.GetStyle().FramePadding.X));
        var visible = label.Split("##", 2)[0];
        if (visible.Length != 0) MaterialText.Text(UiText.T(visible));
        ImGui.SetNextItemWidth(MaterialLayout.FitNextItemWidth(requested, minimum));
        // Remove the hidden label's layout width while retaining the original native widget ID.
        ImGuiP.PushOverrideID(ImGui.GetID(label));
    }
    internal static bool InputText(string label,ref string value,int length)
    { BeginField(label);try { using var height=MaterialText.PushLineHeight(value);return MaterialShapedInput.SingleLine("","",ref value,length); } finally { ImGui.PopID(); } }
    internal static bool InputInt(string label,ref int value)
    { BeginField(label);try { return ImGui.InputInt("",ref value); } finally { ImGui.PopID(); } }
    // Appearance controls retain their native numeric field, format, steps and caption placement.
    internal static bool AppearanceSliderInt(string label, ref int value, int min, int max, string format, ImGuiSliderFlags flags)
    {
        var nativeLabel = UiText.T(label.Split("##", 2)[0]) + label[label.Split("##", 2)[0].Length..];
        if (!MaterialText.RequiresShaping(nativeLabel)) return ImGui.SliderInt(nativeLabel, ref value, min, max, format, flags);
        using var height = MaterialText.PushLineHeight(UiText.T(label.Split("##", 2)[0]));
        var origin = ImGui.GetCursorScreenPos(); var width = ImGui.CalcItemWidth();
        var drawing = ImGui.GetWindowDrawList(); var parent = ImGuiP.GetCurrentWindow(); var previousMax = parent.DC.CursorMaxPos;
        drawing.PushClipRect(new Vector2(origin.X, parent.Pos.Y), new Vector2(origin.X + width, parent.Pos.Y + parent.Size.Y), true);
        bool changed;
        try { changed = ImGui.SliderInt(label, ref value, min, max, format, flags); }
        finally { drawing.PopClipRect(); }
        AppearanceFieldLabel(label, origin, width, drawing, parent, previousMax);
        return changed;
    }
    internal static bool AppearanceInputFloat(string label, ref float value)
    {
        var visible = label.Split("##", 2)[0]; var nativeLabel = UiText.T(visible) + label[visible.Length..];
        if (!MaterialText.RequiresShaping(nativeLabel)) return ImGui.InputFloat(nativeLabel, ref value);
        using var height = MaterialText.PushLineHeight(UiText.T(visible));
        var origin = ImGui.GetCursorScreenPos(); var width = ImGui.CalcItemWidth();
        var drawing = ImGui.GetWindowDrawList(); var parent = ImGuiP.GetCurrentWindow(); var previousMax = parent.DC.CursorMaxPos;
        drawing.PushClipRect(new Vector2(origin.X, parent.Pos.Y), new Vector2(origin.X + width, parent.Pos.Y + parent.Size.Y), true);
        bool changed;
        try { changed = ImGui.InputFloat(label, ref value); }
        finally { drawing.PopClipRect(); }
        AppearanceFieldLabel(label, origin, width, drawing, parent, previousMax);
        return changed;
    }
    private static void AppearanceFieldLabel(string label, Vector2 origin, float width, ImDrawListPtr drawing, ImGuiWindowPtr parent, Vector2 previousMax)
    {
        var translated = UiText.T(label.Split("##", 2)[0]);
        var position = origin + new Vector2(width + ImGui.GetStyle().ItemInnerSpacing.X, ImGui.GetStyle().FramePadding.Y);
        MaterialText.AddText(drawing, position, ImGui.GetColorU32(ImGuiCol.Text), translated);
        var right = position.X + MaterialText.Measure(translated).X;
        parent.DC.CursorMaxPos = new Vector2(Math.Max(previousMax.X, right), parent.DC.CursorMaxPos.Y);
        parent.DC.CursorPosPrevLine = new Vector2(right, parent.DC.CursorPosPrevLine.Y);
    }
    internal static bool Combo(string label,ref int value,string[] options,int count)
    {
        BeginField(label);
        try
        {
        var changed=false;
        if(MaterialText.BeginCombo("",value>=0 && value<count?UiText.T(options[value]):""))
        {
            try
            {
            for(var index=0;index<count;index++)
            {
                ImGui.PushID(index);
                try
                {
                    if(Selectable(options[index],value==index)) { changed=value!=index;value=index; }
                    if(value==index) ImGui.SetItemDefaultFocus();
                }
                finally { ImGui.PopID(); }
            }
            }
            finally { ImGui.EndCombo(); }
        }
        return changed;
        }
        finally { ImGui.PopID(); }
    }
    internal static void Title(string original,string translated)
        => TitleWithButtons(original, translated, null);

    internal static void ReserveTitleSpace(Window owner, string visible, float minimumWidth)
    {
        var style = ImGui.GetStyle();
        var fontSize = ImGui.GetFontSize();
        var collapse = (owner.Flags & (ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.Modal)) == 0
            && style.WindowMenuButtonPosition != ImGuiDir.None;
        var controls = AdditionalTitleButtonWidth(owner, fontSize)
            + ((owner.ShowCloseButton ? 1 : 0) + (collapse ? 1 : 0)) * (fontSize + style.ItemInnerSpacing.X);
        var required = (MaterialText.Measure(visible).X + controls + style.FramePadding.X * 2 + style.ItemInnerSpacing.X)
            / ImGui.GetIO().FontGlobalScale;
        var bounds = owner.SizeConstraints ?? new WindowSizeConstraints();
        bounds.MinimumSize = new(Math.Max(minimumWidth, required), bounds.MinimumSize.Y);
        owner.SizeConstraints = bounds;
    }

    private static float AdditionalTitleButtonWidth(Window? owner, float fontSize)
    {
        if (owner is null) return 0;
        var count = owner.TitleBarButtons.Count(button => !owner.IsClickthrough || button.AvailableClickthrough);
        if (owner.AllowPinning || owner.AllowClickthrough || owner.AllowBackgroundBlur) count++;
        return count * (fontSize + ImGui.GetStyle().ItemInnerSpacing.X);
    }

    internal static void TitleWithButtons(string original,string translated, Window? owner)
    {
        var s=ImGui.GetStyle(); var size=ImGui.GetFontSize();var height=ImGui.GetFrameHeight();
        var flags=ImGuiP.GetCurrentWindow().Flags;
        var collapseOnLeft=(flags & (ImGuiWindowFlags.NoCollapse|ImGuiWindowFlags.Modal))==0 && s.WindowMenuButtonPosition==ImGuiDir.Left;
        var position=ImGui.GetWindowPos()+new Vector2(s.FramePadding.X+(collapseOnLeft?size+s.ItemInnerSpacing.X:0),s.FramePadding.Y);
        var originalWidth=MaterialText.Measure(original).X;
        using var font=UiText.Font(UiFontRole.Body);
        var translatedWidth=ScaledTextSize(translated,size/ImGui.GetFontSize()).X;
        var dl=ImGui.GetWindowDrawList();
        var rightButtons = size + s.FramePadding.X * 2;
        if ((flags & ImGuiWindowFlags.NoCollapse) == 0 && s.WindowMenuButtonPosition == ImGuiDir.Right)
            rightButtons += size + s.ItemInnerSpacing.X;
        rightButtons += AdditionalTitleButtonWidth(owner, size);
        dl.PushClipRect(position,ImGui.GetWindowPos()+new Vector2(Math.Max(0,ImGui.GetWindowSize().X-rightButtons),height),false);
        try
        {
        var bg=s.Colors[(int)(ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows)?ImGuiCol.TitleBgActive:ImGuiCol.TitleBg)];
        dl.AddRectFilled(position,position+new Vector2(Math.Max(originalWidth,translatedWidth),height-s.FramePadding.Y),ImGui.ColorConvertFloat4ToU32(bg));
        MaterialText.AddText(dl,ImGui.GetFont(),size,position,ImGui.ColorConvertFloat4ToU32(s.Colors[(int)ImGuiCol.Text]),translated);
        }
        finally { dl.PopClipRect(); }
    }
    internal static void TableHeadersRow(float height=0)
    {
        for(var column=0;column<ImGui.TableGetColumnCount();column++)
        {
            var translated=UiText.T(ImGui.TableGetColumnName(column));
            if(MaterialText.RequiresShaping(translated)) height=Math.Max(height,MaterialText.Measure(translated).Y+2*ImGui.GetStyle().CellPadding.Y);
        }
        ImGui.TableNextRow(ImGuiTableRowFlags.Headers,height);
        for(var index=0;index<ImGui.TableGetColumnCount();index++)
        {
            if(!ImGui.TableSetColumnIndex(index)) continue;
            var original=ImGui.TableGetColumnName(index);
            var position=ImGui.GetCursorScreenPos();
            var available=ImGui.GetContentRegionAvail().X;
            var foreground=ImGui.GetStyle().Colors[(int)ImGuiCol.Text];
            ImGui.PushStyleColor(ImGuiCol.Text,Vector4.Zero);
            ImGui.TableHeader(original);
            ImGui.PopStyleColor();
            foreground.W*=ImGui.GetStyle().Alpha;
            var dl=ImGui.GetWindowDrawList();
            MaterialText.AddText(dl,position,ImGui.ColorConvertFloat4ToU32(foreground),UiText.T(original));
            var translated=UiText.T(original);
            if(translated!=original && MaterialText.Measure(translated).X>available-16*AethertekUI.MaterialTheme.Metrics.Scale && ImGui.IsItemHovered())
                MaterialText.SetTooltip(translated);
        }
    }
}
