using System.Numerics;
using System.Xml.Linq;
using AethertekUI;
using AethertekUI.Tests;
using Dalamud.Bindings.ImGui;
using footballer.Windows;

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
if (args is not ["render", var root, var output, var build])
    throw new ArgumentException("Usage: render <repo-root> <output-directory> <build-id> | validate <directory>");
if (build.Length is < 1 or > 48 || build.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '-' and not '.'))
    throw new ArgumentException("Build identifier must be 1-48 ASCII letters, digits, dots or hyphens.");
var version = XDocument.Load(Path.Combine(root, "footballer/footballer.csproj"))
    .Descendants("Version").Single().Value;
var theme = FootballerPresentation.Theme(FootballerPresentation.ReferenceAccent);
theme.Density = MaterialDensity.Compact;
using var host = new NativeTestContext();
void Draw()
{
    using var scope = MaterialTheme.Push(theme);
    ImGui.SetNextWindowPos(Vector2.Zero);
    ImGui.SetNextWindowSize(new(730, 380));
    ImGui.Begin("Footballer preview", ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize |
        ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoScrollbar);
    var c = theme.Colors;
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
Console.WriteLine($"Rendered Footballer v{version}, build {build}: {path} (730 x 380)");
