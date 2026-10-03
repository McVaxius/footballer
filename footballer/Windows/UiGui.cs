using System.Numerics;
using AethertekUI;
using Dalamud.Bindings.ImGui;

namespace footballer.Windows;

// Native widgets receive their original English labels/IDs. Only their visible label is painted in the selected locale.
// This also preserves English-derived helper IDs and existing saved window identities.
internal static class UiGui
{
    internal static void TextUnformatted(string text) => ImGui.TextUnformatted(UiText.T(text));
    internal static void TextWrapped(string text) => ImGui.TextWrapped(UiText.T(text));
    internal static void TextDisabled(string text)
    {
        ImGui.PushTextWrapPos(0);
        ImGui.TextDisabled(UiText.T(text));
        ImGui.PopTextWrapPos();
    }
    internal static void Text(string text) => ImGui.TextUnformatted(UiText.T(text));
    internal static void BulletText(string text) => ImGui.BulletText(UiText.T(text));
    internal static void TextColored(Vector4 color, string text) => ImGui.TextColored(color, UiText.T(text));

    // Native controls retain the original ID, hit testing, focus and keyboard behavior.
    internal static bool Action(string id, string text, MaterialIcon icon = MaterialIcon.None, bool disabled = false, float height = 66)
    {
        var c = MaterialTheme.Current.Colors;
        var s = MaterialTheme.Metrics.Scale;
        var label = UiText.T(text);
        var width = ImGui.CalcTextSize(label).X + (icon == MaterialIcon.None ? 32 : 64) * s;
        ImGui.BeginDisabled(disabled);
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
        if (icon != MaterialIcon.None) MaterialIcons.Draw(icon, min + new Vector2(16 * s, (height - 26) * .5f * s), 26 * s, foreground);
        dl.AddText(min + new Vector2((icon == MaterialIcon.None ? 16 : 48) * s, (height * s - ImGui.GetTextLineHeight()) * .5f), MaterialCanvas.Color(foreground), label);
        dl.PopClipRect();
        ImGui.EndDisabled();
        return clicked;
    }
    internal static bool Toggle(string original, ref bool value, float height = 66)
    {
        var s = MaterialTheme.Metrics.Scale;
        var c = MaterialTheme.Current.Colors;
        var label = UiText.T(original);
        var width = ImGui.CalcTextSize(label).X + 44 * s + 42 * s;
        var nativeWidth = ImGui.CalcTextSize(original).X;
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
        var fill = value ? c.PrimaryContainer : c.SurfaceContainerHigh;
        if (ImGui.IsItemHovered()) fill = MaterialColor.Layer(fill, c.OnSurface, .08f);
        if (ImGui.IsItemActive()) fill = MaterialColor.Layer(fill, c.OnSurface, .12f);
        dl.AddRectFilled(min, max, MaterialCanvas.Color(fill), 4 * s);
        dl.AddRect(min, max, MaterialCanvas.Color(ImGui.IsItemFocused() || value ? c.Primary : c.OutlineVariant), 4 * s);
        var track = min + new Vector2(width - 58 * s, (height - 26) * .5f * s);
        dl.AddRectFilled(track, track + new Vector2(44, 26) * s, MaterialCanvas.Color(value ? c.Primary : c.Outline), 13 * s);
        dl.AddCircleFilled(track + new Vector2(value ? 31 : 13, 13) * s, 10 * s, MaterialCanvas.Color(c.OnSurface), 24);
        dl.PushClipRect(min, max, true);
        dl.AddText(min + new Vector2(14 * s, (height * s - ImGui.GetTextLineHeight()) * .5f), MaterialCanvas.Color(c.OnSurface), label);
        dl.PopClipRect();
        return changed;
    }

    internal static bool SliderFloat(string label, ref float value, float min, float max, string format)
    {
        var width = FitField(label);
        var changed = ImGui.SliderFloat(label, ref value, min, max, format);
        FieldLabel(label, width);
        return changed;
    }
    internal static bool CollapsingHeader(string label)
    {
        var position = ImGui.GetCursorScreenPos();
        ImGui.PushStyleColor(ImGuiCol.Text, Vector4.Zero);
        var open = ImGui.CollapsingHeader(label);
        ImGui.PopStyleColor();
        var c = MaterialTheme.Current.Colors;
        var dl = ImGui.GetWindowDrawList();
        dl.PushClipRect(ImGui.GetItemRectMin(), ImGui.GetItemRectMax(), true);
        MaterialIcons.Draw(open ? MaterialIcon.ChevronDown : MaterialIcon.ArrowRight, position + ImGui.GetStyle().FramePadding, ImGui.GetFontSize(), c.OnSurface);
        dl.AddText(position + new Vector2(ImGui.GetFontSize() + ImGui.GetStyle().FramePadding.X * 2, ImGui.GetStyle().FramePadding.Y), MaterialCanvas.Color(c.OnSurface), UiText.T(label));
        dl.PopClipRect();
        return open;
    }
    private static void Label(string original,Vector2 position,Vector4 background,Vector4 foreground,Vector2? clip=null,string? display=null)
    {
        var visible=original.Split("##",2)[0];
        var translated=display ?? UiText.T(visible);
        if(translated==visible) return;
        var dl=ImGui.GetWindowDrawList();
        var width=Math.Max(ImGui.CalcTextSize(visible).X,ImGui.CalcTextSize(translated).X);
        if(clip is { } max) dl.PushClipRect(position,max,true);
        dl.AddRectFilled(position,position+new Vector2(width,ImGui.GetTextLineHeight()),ImGui.ColorConvertFloat4ToU32(background));
        foreground.W*=ImGui.GetStyle().Alpha;
        dl.AddText(position,ImGui.ColorConvertFloat4ToU32(foreground),translated);
        if(clip.HasValue) dl.PopClipRect();
    }
    internal static bool Button(string label,string? display=null)
    {
        var translated=display ?? UiText.T(label.Split("##",2)[0]);
        var width=ImGui.CalcTextSize(translated).X+2*ImGui.GetStyle().FramePadding.X;
        if(width>ImGui.GetContentRegionAvail().X && ImGui.GetCursorPosX()>ImGui.GetStyle().WindowPadding.X+1) ImGui.NewLine();
        var foreground=ImGui.GetStyle().Colors[(int)ImGuiCol.Text];
        ImGui.PushStyleColor(ImGuiCol.Text,Vector4.Zero);
        var clicked=ImGui.Button(label,new Vector2(width,0));
        ImGui.PopStyleColor();
        var min=ImGui.GetItemRectMin(); var max=ImGui.GetItemRectMax();
        foreground.W*=ImGui.GetStyle().Alpha;
        ImGui.GetWindowDrawList().PushClipRect(min,max,true);
        ImGui.GetWindowDrawList().AddText(min+(max-min-ImGui.CalcTextSize(translated))*.5f,ImGui.ColorConvertFloat4ToU32(foreground),translated);
        ImGui.GetWindowDrawList().PopClipRect();
        return clicked;
    }
    internal static bool SmallButton(string label,string? display=null)
    {
        // Native small buttons use the same ID and behavior with zero vertical padding.
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding,new Vector2(ImGui.GetStyle().FramePadding.X,0));
        var clicked=Button(label,display);
        ImGui.PopStyleVar();
        return clicked;
    }
    internal static bool Checkbox(string label,ref bool value)
    {
        var visible=label.Split("##",2)[0];
        var translated=UiText.T(visible);
        var foreground=ImGui.GetStyle().Colors[(int)ImGuiCol.Text];
        var gap=ImGui.GetStyle().ItemInnerSpacing;
        // Native Checkbox sizes its hit area from the original label. Adjust that size for the
        // translated ink while keeping the native widget and its original ID.
        ImGui.PushStyleVar(ImGuiStyleVar.ItemInnerSpacing,new Vector2(Math.Max(0,gap.X+ImGui.CalcTextSize(translated).X-ImGui.CalcTextSize(visible).X),gap.Y));
        ImGui.PushStyleColor(ImGuiCol.Text,Vector4.Zero);
        var changed=ImGui.Checkbox(label,ref value);
        ImGui.PopStyleColor();
        ImGui.PopStyleVar();
        var p=ImGui.GetItemRectMin()+new Vector2(ImGui.GetFrameHeight()+gap.X,ImGui.GetStyle().FramePadding.Y);
        foreground.W*=ImGui.GetStyle().Alpha;
        ImGui.GetWindowDrawList().AddText(p,ImGui.ColorConvertFloat4ToU32(foreground),translated);
        return changed;
    }
    internal static bool Selectable(string original,bool selected,string? display=null)
    {
        var translated=display ?? UiText.T(original.Split("##",2)[0]);
        var origin=ImGui.GetCursorScreenPos();
        var width=ImGui.GetContentRegionAvail().X;
        var foreground=ImGui.GetStyle().Colors[(int)ImGuiCol.Text];
        ImGui.PushStyleColor(ImGuiCol.Text,Vector4.Zero);
        var clicked=ImGui.Selectable(original,selected,ImGuiSelectableFlags.None,new Vector2(0,ImGui.GetTextLineHeight()));
        ImGui.PopStyleColor();
        foreground.W*=ImGui.GetStyle().Alpha;
        var dl=ImGui.GetWindowDrawList();
        dl.PushClipRect(origin,origin+new Vector2(width,ImGui.GetTextLineHeight()),true);
        dl.AddText(origin,ImGui.ColorConvertFloat4ToU32(foreground),translated);
        dl.PopClipRect();
        if(ImGui.CalcTextSize(translated).X>width && ImGui.IsItemHovered()) ImGui.SetTooltip(translated);
        return clicked;
    }
    private static float FitField(string label)
    {
        var remaining=ImGui.GetContentRegionAvail().X-ImGui.CalcTextSize(UiText.T(label)).X-ImGui.GetStyle().ItemInnerSpacing.X;
        var width=Math.Max(80,Math.Min(ImGui.CalcItemWidth(),remaining));
        ImGui.SetNextItemWidth(width);
        return width;
    }
    private static void FieldLabel(string label,float width, Vector2? itemMin = null)
    {
        var min=itemMin ?? ImGui.GetItemRectMin();
        var p=min+new Vector2(width+ImGui.GetStyle().ItemInnerSpacing.X,ImGui.GetStyle().FramePadding.Y);
        var background=(ImGuiP.GetCurrentWindow().Flags & ImGuiWindowFlags.ChildWindow)!=0 ? ImGuiCol.ChildBg : ImGuiCol.WindowBg;
        Label(label,p,ImGui.GetStyle().Colors[(int)background],ImGui.GetStyle().Colors[(int)ImGuiCol.Text]);
    }
    internal static bool InputText(string label,ref string value,int length) { var width=FitField(label);var changed=ImGui.InputText(label,ref value,length); FieldLabel(label,width); return changed; }
    internal static bool InputInt(string label,ref int value) { var width=FitField(label);var changed=ImGui.InputInt(label,ref value); FieldLabel(label,width); return changed; }
    internal static bool Combo(string label,ref int value,string[] options,int count)
    {
        var width=FitField(label);
        var origin=ImGui.GetCursorScreenPos();
        var changed=false;
        if(ImGui.BeginCombo(label,value>=0 && value<count?UiText.T(options[value]):""))
        {
            for(var index=0;index<count;index++)
            {
                ImGui.PushID(index);
                if(Selectable(options[index],value==index)) { changed=value!=index;value=index; }
                if(value==index) ImGui.SetItemDefaultFocus();
                ImGui.PopID();
            }
            ImGui.EndCombo();
        }
        FieldLabel(label,width,origin); return changed;
    }
    internal static void Title(string original,string translated)
    {
        var s=ImGui.GetStyle(); var size=ImGui.GetFontSize();var height=ImGui.GetFrameHeight();
        var flags=ImGuiP.GetCurrentWindow().Flags;
        var collapseOnLeft=(flags & (ImGuiWindowFlags.NoCollapse|ImGuiWindowFlags.Modal))==0 && s.WindowMenuButtonPosition==ImGuiDir.Left;
        var position=ImGui.GetWindowPos()+new Vector2(s.FramePadding.X+(collapseOnLeft?size+s.ItemInnerSpacing.X:0),s.FramePadding.Y);
        var originalWidth=ImGui.CalcTextSize(original).X;
        using var font=UiText.Font(UiFontRole.Body);
        var translatedWidth=ImGui.CalcTextSize(translated).X*size/ImGui.GetFontSize();
        var dl=ImGui.GetWindowDrawList();
        dl.PushClipRect(ImGui.GetWindowPos(),ImGui.GetWindowPos()+new Vector2(ImGui.GetWindowSize().X,height),false);
        var bg=s.Colors[(int)(ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows)?ImGuiCol.TitleBgActive:ImGuiCol.TitleBg)];
        dl.AddRectFilled(position,position+new Vector2(Math.Max(originalWidth,translatedWidth),height-s.FramePadding.Y),ImGui.ColorConvertFloat4ToU32(bg));
        dl.AddText(ImGui.GetFont(),size,position,ImGui.ColorConvertFloat4ToU32(s.Colors[(int)ImGuiCol.Text]),translated);
        dl.PopClipRect();
    }
    internal static void TableHeadersRow(float height=0)
    {
        ImGui.TableNextRow(ImGuiTableRowFlags.Headers,height);
        for(var index=0;index<ImGui.TableGetColumnCount();index++)
        {
            if(!ImGui.TableSetColumnIndex(index)) continue;
            var original=ImGui.TableGetColumnName(index);
            var position=ImGui.GetCursorScreenPos();
            var available=ImGui.GetContentRegionAvail().X;
            ImGui.TableHeader(original);
            Label(original,position,ImGui.GetStyle().Colors[(int)ImGuiCol.TableHeaderBg],ImGui.GetStyle().Colors[(int)ImGuiCol.Text], position + new Vector2(Math.Max(1, available), ImGui.GetTextLineHeight()));
            var translated=UiText.T(original);
            if(translated!=original && ImGui.CalcTextSize(translated).X>available-16*AethertekUI.MaterialTheme.Metrics.Scale && ImGui.IsItemHovered())
                ImGui.SetTooltip(translated);
        }
    }
}
