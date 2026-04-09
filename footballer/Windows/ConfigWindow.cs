using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace footballer.Windows;

public sealed class ConfigWindow : PositionedWindow, IDisposable
{
    private static readonly string[] DtrModes = { "Text only", "Icon + text", "Icon only" };
    private readonly Plugin plugin;

    public ConfigWindow(Plugin plugin)
        : base($"{PluginInfo.DisplayName} Settings##Config")
    {
        this.plugin = plugin;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(660f, 520f),
            MaximumSize = new Vector2(1500f, 1300f),
        };
    }

    public void Dispose()
    {
    }

    public override void Draw()
    {
        var cfg = plugin.Configuration;

        ImGui.TextWrapped("This build now has a live party foot showcase surface, Lodestone face caching/privacy gating, a stored CharacterInspect capture profile, preview-only barefoot mode, and hidden research surfaces behind /footballer debug. The normal window keeps the showcase flow active without continuously polling the raw inspect and portrait research surfaces.");
        ImGui.Separator();
        ImGui.TextUnformatted("Live Shell Controls");

        var enabled = cfg.PluginEnabled;
        if (ImGui.Checkbox("Plugin enabled", ref enabled))
            plugin.SetPluginEnabled(enabled, printStatus: true);

        var dtr = cfg.DtrBarEnabled;
        if (ImGui.Checkbox("Show DTR bar entry", ref dtr))
        {
            cfg.DtrBarEnabled = dtr;
            cfg.Save();
            plugin.UpdateDtrBar();
        }

        var mode = cfg.DtrBarMode;
        if (ImGui.Combo("DTR mode", ref mode, DtrModes, DtrModes.Length))
        {
            cfg.DtrBarMode = mode;
            cfg.Save();
            plugin.UpdateDtrBar();
        }

        var onIcon = cfg.DtrIconEnabled;
        if (ImGui.InputText("DTR enabled glyph", ref onIcon, 8))
        {
            cfg.DtrIconEnabled = onIcon.Length <= 3 ? onIcon : onIcon[..3];
            cfg.Save();
            plugin.UpdateDtrBar();
        }

        var offIcon = cfg.DtrIconDisabled;
        if (ImGui.InputText("DTR disabled glyph", ref offIcon, 8))
        {
            cfg.DtrIconDisabled = offIcon.Length <= 3 ? offIcon : offIcon[..3];
            cfg.Save();
            plugin.UpdateDtrBar();
        }

        ImGui.Separator();
        ImGui.TextUnformatted("Stored Display Defaults");

        var krangleNames = cfg.KrangleNames;
        if (ImGui.Checkbox("Default: Krangle labels", ref krangleNames))
            plugin.SetKrangleNames(krangleNames);

        var showMaleFeet = cfg.ShowMaleFeet;
        if (ImGui.Checkbox("Default: Show male feet", ref showMaleFeet))
        {
            cfg.ShowMaleFeet = showMaleFeet;
            cfg.Save();
        }

        var showFemaleFeet = cfg.ShowFemaleFeet;
        if (ImGui.Checkbox("Default: Show female feet", ref showFemaleFeet))
        {
            cfg.ShowFemaleFeet = showFemaleFeet;
            cfg.Save();
        }

        var withoutFootwear = cfg.WithoutFootwear;
        if (ImGui.Checkbox("Default: Without footwear", ref withoutFootwear))
        {
            cfg.WithoutFootwear = withoutFootwear;
            cfg.Save();
            plugin.HandleWithoutFootwearChanged();
        }
        ImGui.TextWrapped("Current note: Without footwear now drives a preview-only CharacterInspect multi-seam feet clear plus redraw pass. Open Inspect from a party row or showcase card, let the preview settle, then use Capture current preview from the main window.");

        var showOwnFeet = cfg.ShowOwnFeet;
        if (ImGui.Checkbox("Default: Show own feet", ref showOwnFeet))
        {
            cfg.ShowOwnFeet = showOwnFeet;
            cfg.Save();
        }

        var replaceCommendationPictures = cfg.ReplaceCommendationPictures;
        if (ImGui.Checkbox("Default: Replace party portrait window pictures", ref replaceCommendationPictures))
        {
            cfg.ReplaceCommendationPictures = replaceCommendationPictures;
            cfg.Save();
        }

        var showFootShowcase = cfg.ShowFootShowcase;
        if (ImGui.Checkbox("Default: Show foot showcase", ref showFootShowcase))
        {
            cfg.ShowFootShowcase = showFootShowcase;
            cfg.Save();
        }

        var showFaceNextToFeet = cfg.ShowFaceNextToFeet;
        if (ImGui.Checkbox("Default: Show face next to feet", ref showFaceNextToFeet))
        {
            cfg.ShowFaceNextToFeet = showFaceNextToFeet;
            cfg.Save();
        }

        if (plugin.SessionDebugUnlocked)
        {
            ImGui.TextColored(new Vector4(0.98f, 0.73f, 0.40f, 1f), "Session debug controls are visible. Privacy override is experimental.");

            var respectPrivacy = cfg.RespectLodestonePrivacy;
            if (ImGui.Checkbox("Debug: Respect Lodestone privacy", ref respectPrivacy))
            {
                cfg.RespectLodestonePrivacy = respectPrivacy;
                cfg.Save();
            }
        }
        else
        {
            ImGui.TextWrapped("Lodestone privacy respect is forced on in normal use. Type /footballer debug to expose the experimental override for this session.");
        }

        var openOnLoad = cfg.OpenMainWindowOnLoad;
        ImGui.Separator();
        ImGui.TextUnformatted("Window Behavior");
        if (ImGui.Checkbox("Open main window on load", ref openOnLoad))
        {
            cfg.OpenMainWindowOnLoad = openOnLoad;
            cfg.Save();
        }

        ImGui.Separator();
        ImGui.TextUnformatted("Rollout Phases");
        foreach (var phase in PluginInfo.Phases)
            ImGui.BulletText(phase);

        FinalizePendingWindowPlacement();
    }
}
