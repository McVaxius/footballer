using System.Numerics;
using System.Xml.Linq;
using System.Reflection;
using System.Runtime.Loader;
using AethertekUI;
using AethertekUI.Tests;
using Dalamud.Bindings.ImGui;
using footballer.Windows;

if (args is ["check-appearance", var pluginAssemblyPath])
{
    var hostDirectory = Environment.GetEnvironmentVariable("DalamudLibPath")
        ?? Environment.GetEnvironmentVariable("DALAMUD_HOME")
        ?? throw new InvalidOperationException("The existing Dalamud reference directory is required.");
    Assembly? ResolveHost(AssemblyLoadContext context, AssemblyName name)
    {
        var candidate = Path.Combine(hostDirectory, name.Name + ".dll");
        return File.Exists(candidate) ? context.LoadFromAssemblyPath(candidate) : null;
    }
    AssemblyLoadContext.Default.Resolving += ResolveHost;
    try
    {
        // Instantiate only the data model. Do not construct the plugin, invoke Save or open audio.
        var pluginAssembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(pluginAssemblyPath));
        var configurationType = pluginAssembly.GetType("footballer.Configuration", throwOnError: true)!;
        var jsonAssembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(hostDirectory, "Newtonsoft.Json.dll"));
        var jsonConvert = jsonAssembly.GetType("Newtonsoft.Json.JsonConvert", throwOnError: true)!;
        var deserialize = jsonConvert.GetMethod("DeserializeObject", [typeof(string), typeof(Type)])!;
        var serialize = jsonConvert.GetMethod("SerializeObject", [typeof(object)])!;
        var oldJson = """{"Version":4,"UiLanguage":"ja","UiColors":{"Mode":1,"BackgroundRgb":1193046,"ControlsRgb":5666970,"TextRgb":14544639},"ExistingUnknownOption":"retained"}""";
        var oldConfig = deserialize.Invoke(null, [oldJson, configurationType])!;
        var muted = configurationType.GetProperty("UiLcarsSoundsMuted")!;
        var colours = configurationType.GetProperty("UiColors")!;
        if (muted.GetValue(oldConfig) is not true) throw new InvalidDataException("Legacy configuration must start muted.");
        var picker = (MaterialColorSettings)colours.GetValue(oldConfig)!;
        if (picker.Mode != MaterialColorMode.Picker) throw new InvalidDataException("Legacy Picker mode was changed.");
        colours.SetValue(oldConfig, picker with { Mode = MaterialColorMode.Lcars });
        muted.SetValue(oldConfig, false);
        var saved = (string)serialize.Invoke(null, [oldConfig])!;
        var restored = deserialize.Invoke(null, [saved, configurationType])!;
        var restoredColours = (MaterialColorSettings)colours.GetValue(restored)!;
        if (restoredColours != picker with { Mode = MaterialColorMode.Lcars }
            || muted.GetValue(restored) is not false || !saved.Contains("ExistingUnknownOption", StringComparison.Ordinal)
            || configurationType.GetProperty("UiLanguage")!.GetValue(restored) is not "ja")
            throw new InvalidDataException("Appearance serialization lost values or existing configuration.");
        colours.SetValue(restored, restoredColours with { Mode = MaterialColorMode.Auto });
        var automaticJson = (string)serialize.Invoke(null, [restored])!;
        var automatic = deserialize.Invoke(null, [automaticJson, configurationType])!;
        if ((MaterialColorSettings)colours.GetValue(automatic)! != picker with { Mode = MaterialColorMode.Auto })
            throw new InvalidDataException("Auto transition lost saved Picker RGB values.");
        var keys = new[] { "LCARS", "LCARS panels, bars and controls", "LCARS UI sounds", "UI sounds", "Preview sound",
            "Custom UI tones. The titlebar speaker toggles the same setting.", "Select LCARS to use UI sounds.",
            "Turn on UI sounds to preview.", "Audio output is unavailable.", "UI sounds muted", "UI sounds on" };
        var resources = pluginAssembly.GetManifestResourceNames()
            .Where(name => name.StartsWith("footballer.Localization.Strings_", StringComparison.Ordinal) && name.EndsWith(".resources", StringComparison.Ordinal)).ToArray();
        if (resources.Length != 15) throw new InvalidDataException("Expected fifteen compiled localization resources.");
        foreach (var resource in resources)
        {
            using var reader = new System.Resources.ResourceReader(pluginAssembly.GetManifestResourceStream(resource)!);
            var values = reader.Cast<System.Collections.DictionaryEntry>().ToDictionary(entry => (string)entry.Key, entry => (string)entry.Value!);
            foreach (var key in keys)
                if (!values.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
                    throw new InvalidDataException($"Missing {key} in {resource}.");
        }
        Console.WriteLine("Footballer appearance checks passed: actual Configuration legacy mute default, Newtonsoft LCARS/mute/Auto roundtrips, retained Picker RGB/unknown settings/language, and 165 compiled localized captions.");
    }
    finally { AssemblyLoadContext.Default.Resolving -= ResolveHost; }
    return;
}

if (args is ["validate", var directory])
{
    var paths = Directory.GetFiles(directory, "*.png").Order().ToArray();
    if (paths.Length is < 1 or > 5) throw new InvalidDataException("Expected one to five previews.");
    for (var i = 0; i < paths.Length; i++)
    {
        if (Path.GetFileName(paths[i]) != $"preview-{i + 1}.png")
            throw new InvalidDataException("Previews must be numbered consecutively from preview-1.png.");
        var frame = SoftwarePreview.Read(paths[i]);
        if (frame.Width > 730 || frame.Height > 380)
            throw new InvalidDataException("Preview exceeds 730 x 380.");
        Console.WriteLine($"Validated {Path.GetFileName(paths[i])}: {frame.Width} x {frame.Height}");
    }
    return;
}
if (args is not [var operation, var root, var output, var build]
    || operation is not ("render" or "render-lcars" or "render-lcars-regular"))
    throw new ArgumentException("Usage: render|render-lcars|render-lcars-regular <repo-root> <output-directory> <build-id> | validate <directory>");
if (build.Length is < 1 or > 48 || build.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '-' and not '.'))
    throw new ArgumentException("Build identifier must be 1-48 ASCII letters, digits, dots or hyphens.");
var version = XDocument.Load(Path.Combine(root, "footballer/footballer.csproj"))
    .Descendants("Version").Single().Value;
var lcars = operation != "render";
var theme = FootballerPresentation.Theme(FootballerPresentation.ReferenceAccent,
    new MaterialColorSettings { Mode = lcars ? MaterialColorMode.Lcars : MaterialColorMode.Auto });
theme.Density = operation == "render-lcars-regular" ? MaterialDensity.Standard : MaterialDensity.Compact;
if (theme.IsLcars != lcars) throw new InvalidDataException("Consumer theme lost the selected colour mode.");
using var host = new NativeTestContext();
void Draw()
{
    using var scope = MaterialTheme.Push(theme);
    ImGui.SetNextWindowPos(Vector2.Zero);
    ImGui.SetNextWindowSize(new(730, 380));
    ImGui.Begin("Footballer preview", ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize |
        ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoScrollbar);
    var c = theme.Colors;
    if (lcars)
    {
        ImGui.SetCursorPos(new(24, 12));
        MaterialLcars.Header($"Footballer v{version}");
        ImGui.SetCursorPos(new(24, 92));
        using (MaterialLcars.NativeButtonStyle())
        {
            ImGui.Button("Refresh party", new(144, 32));
            ImGui.SameLine();
            ImGui.Button("Settings", new(110, 32));
        }
        ImGui.SameLine();
        MaterialStatus.Badge("Privacy respected", FootballerPresentation.Ready);
        void Card(Vector2 origin, string name, string status, bool hidden)
        {
            // Exercise the same source-owned card surface used by Main, retaining its inset band.
            FootballerPresentation.Surface(origin, origin + new Vector2(334, 160));
            ImGui.SetCursorScreenPos(origin);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(18, 12));
            ImGui.PushStyleColor(ImGuiCol.ChildBg, Vector4.Zero);
            if (ImGui.BeginChild("PreviewCard##" + name, new Vector2(334, 160), false, ImGuiWindowFlags.AlwaysUseWindowPadding))
            {
                MaterialLcars.PanelHeading(name, 1);
                if (ImGui.GetItemRectMax().X > origin.X + 316.1f)
                    throw new InvalidDataException("LCARS heading escaped its source-shaped card child.");
                ImGui.TextUnformatted(status);
                if (hidden) FootballerPresentation.Shoe(ImGui.GetCursorScreenPos() + new Vector2(16, 10), 70, c.Outline);
                else FootballerPresentation.Footprint(ImGui.GetCursorScreenPos() + new Vector2(16, 10), 60, c.Primary);
            }
            ImGui.EndChild();
            ImGui.PopStyleColor();
            ImGui.PopStyleVar();
        }
        Card(new(24, 143), "Sample adventurer", "Preview capture available", false);
        Card(new(372, 143), "Private adventurer", "Hidden by Lodestone privacy", true);
        ImGui.SetCursorPos(new(24, 322));
        MaterialLcars.Footer($"Build {build} | diagnostic, fabricated data");
        ImGui.End();
        return;
    }
    FootballerPresentation.Footprint(new(24, 20), 54, c.Primary);
    ImGui.SetCursorPos(new(94, 22));
    ImGui.SetWindowFontScale(2);
    ImGui.TextUnformatted($"Footballer v{version}");
    ImGui.SetWindowFontScale(1);
    ImGui.SetCursorPos(new(94, 58));
    ImGui.TextColored(c.OnSurfaceVariant, "Party feet previews, simply.");
    ImGui.SetCursorPos(new(24, 91));
    MaterialButton.Draw("refresh", "Refresh party", MaterialButtonVariant.Filled);
    ImGui.SameLine();
    MaterialButton.Draw("settings", "Settings", MaterialButtonVariant.Tonal);
    ImGui.SameLine();
    MaterialStatus.Badge("Privacy respected", FootballerPresentation.Ready);
    ImGui.SetCursorPos(new(24, 143));
    using (MaterialSurface.Card("sample", new(334, 160), outline: true))
    {
        ImGui.TextUnformatted("Sample adventurer");
        ImGui.TextColored(c.OnSurfaceVariant, "Preview capture available");
        var origin = ImGui.GetCursorScreenPos() + new Vector2(8, 8);
        FootballerPresentation.Footprint(origin, 70, c.Primary);
        FootballerPresentation.Footprint(origin + new Vector2(92, 0), 70, c.Secondary);
    }
    ImGui.SetCursorPos(new(372, 143));
    using (MaterialSurface.Card("privacy", new(334, 160), outline: true))
    {
        ImGui.TextUnformatted("Private adventurer");
        ImGui.TextColored(c.OnSurfaceVariant, "Hidden by Lodestone privacy");
        FootballerPresentation.Shoe(ImGui.GetCursorScreenPos() + new Vector2(16, 12), 88, c.Outline);
    }
    ImGui.SetCursorPos(new(24, 320));
    ImGui.TextUnformatted($"Build {build}");
    ImGui.SetCursorPos(new(24, 346));
    ImGui.TextColored(c.OnSurfaceVariant, "Diagnostic software render / fabricated data / 730 x 380");
    ImGui.End();
}
for (var i = 0; i < 3; i++) host.Frame(Draw);
var path = Path.Combine(output, "preview-1.png");
SoftwarePreview.Save(path, host);
var rendered = SoftwarePreview.Read(path);
if (rendered.Width != 730 || rendered.Height != 380) throw new InvalidDataException("Wrong preview dimensions.");
if (lcars)
{
    int Count(Vector4 colour)
    {
        var red = (byte)MathF.Round(colour.X * 255);
        var green = (byte)MathF.Round(colour.Y * 255);
        var blue = (byte)MathF.Round(colour.Z * 255);
        var count = 0;
        for (var offset = 0; offset < rendered.Rgba.Length; offset += 4)
            if (rendered.Rgba[offset] == red && rendered.Rgba[offset + 1] == green && rendered.Rgba[offset + 2] == blue) count++;
        return count;
    }
    var orange = Count(MaterialLcars.Orange);
    var lilac = Count(MaterialLcars.Lilac);
    var black = Count(MaterialLcars.Background);
    if (orange < 1000 || lilac < 1000 || black < 10000)
        throw new InvalidDataException($"LCARS geometry missing: orange={orange}, lilac={lilac}, black={black}.");
    Console.WriteLine($"LCARS {theme.Density} native surface checks passed: orange={orange}, lilac={lilac}, black={black}.");
}
Console.WriteLine($"Rendered Footballer v{version}, build {build}: {path} (730 x 380)");
