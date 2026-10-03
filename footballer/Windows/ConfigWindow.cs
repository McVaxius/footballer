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
        UiGui.Title("Footballer Settings", UiText.T("Footballer Settings"));
        var compact = cfg.UiCompact;
        if (UiGui.Checkbox("Compact mode", ref compact)) { cfg.UiCompact = compact; cfg.Save(); }
        plugin.DrawAppearanceSelector();
        ImGui.Separator();

        UiGui.TextWrapped("Choose which party previews to show. Lodestone privacy is respected in normal use.");
        ImGui.Separator();
        UiGui.TextUnformatted("Live Shell Controls");

        var enabled = cfg.PluginEnabled;
        if (UiGui.Checkbox("Plugin enabled", ref enabled))
            plugin.SetPluginEnabled(enabled, printStatus: true);

        var dtr = cfg.DtrBarEnabled;
        if (UiGui.Checkbox("Show DTR bar entry", ref dtr))
        {
            cfg.DtrBarEnabled = dtr;
            cfg.Save();
            plugin.UpdateDtrBar();
        }

        var mode = cfg.DtrBarMode;
        if (UiGui.Combo("DTR mode", ref mode, DtrModes, DtrModes.Length))
        {
            cfg.DtrBarMode = mode;
            cfg.Save();
            plugin.UpdateDtrBar();
        }

        var onIcon = cfg.DtrIconEnabled;
        if (UiGui.InputText("DTR enabled glyph", ref onIcon, 8))
        {
            cfg.DtrIconEnabled = onIcon.Length <= 3 ? onIcon : onIcon[..3];
            cfg.Save();
            plugin.UpdateDtrBar();
        }

        var offIcon = cfg.DtrIconDisabled;
        if (UiGui.InputText("DTR disabled glyph", ref offIcon, 8))
        {
            cfg.DtrIconDisabled = offIcon.Length <= 3 ? offIcon : offIcon[..3];
            cfg.Save();
            plugin.UpdateDtrBar();
        }

        ImGui.Separator();
        UiGui.TextUnformatted("Stored Display Defaults");

        var krangleNames = cfg.KrangleNames;
        if (UiGui.Checkbox("Default: Krangle labels", ref krangleNames))
            plugin.SetKrangleNames(krangleNames);

        var showMaleFeet = cfg.ShowMaleFeet;
        if (UiGui.Checkbox("Default: Show male feet", ref showMaleFeet))
        {
            cfg.ShowMaleFeet = showMaleFeet;
            cfg.Save();
        }

        var showFemaleFeet = cfg.ShowFemaleFeet;
        if (UiGui.Checkbox("Default: Show female feet", ref showFemaleFeet))
        {
            cfg.ShowFemaleFeet = showFemaleFeet;
            cfg.Save();
        }

        var withoutFootwear = cfg.WithoutFootwear;
        if (UiGui.Checkbox("Default: Without footwear", ref withoutFootwear))
        {
            cfg.WithoutFootwear = withoutFootwear;
            cfg.Save();
            plugin.HandleWithoutFootwearChanged();
        }
        UiGui.TextWrapped("Without footwear removes shoes in the inspect preview only. Refresh party to save new previews.");

        var showOwnFeet = cfg.ShowOwnFeet;
        if (UiGui.Checkbox("Default: Show own feet", ref showOwnFeet))
        {
            cfg.ShowOwnFeet = showOwnFeet;
            cfg.Save();
        }

        var replaceCommendationPictures = cfg.ReplaceCommendationPictures;
        if (UiGui.Checkbox("Default: Replace party portrait window pictures", ref replaceCommendationPictures))
        {
            cfg.ReplaceCommendationPictures = replaceCommendationPictures;
            cfg.Save();
        }

        var showFootShowcase = cfg.ShowFootShowcase;
        if (UiGui.Checkbox("Default: Show foot showcase", ref showFootShowcase))
        {
            cfg.ShowFootShowcase = showFootShowcase;
            cfg.Save();
        }

        var showFaceNextToFeet = cfg.ShowFaceNextToFeet;
        if (UiGui.Checkbox("Default: Show face next to feet", ref showFaceNextToFeet))
        {
            cfg.ShowFaceNextToFeet = showFaceNextToFeet;
            cfg.Save();
        }

        if (plugin.SessionDebugUnlocked)
        {
            UiGui.TextColored(new Vector4(0.98f, 0.73f, 0.40f, 1f), "Session debug controls are visible. Privacy override is experimental.");

            var respectPrivacy = cfg.RespectLodestonePrivacy;
            if (UiGui.Checkbox("Debug: Respect Lodestone privacy", ref respectPrivacy))
            {
                cfg.RespectLodestonePrivacy = respectPrivacy;
                cfg.Save();
            }
        }
        else
        {
        UiGui.TextWrapped("Privacy is always on. /footballer debug exposes the session-only override.");
        }

        var openOnLoad = cfg.OpenMainWindowOnLoad;
        ImGui.Separator();
        UiGui.TextUnformatted("Window Behavior");
        if (UiGui.Checkbox("Open main window on load", ref openOnLoad))
        {
            cfg.OpenMainWindowOnLoad = openOnLoad;
            cfg.Save();
        }

        var autoRefreshOnOpen = cfg.AutoRefreshPartyOnShowcaseOpen;
        if (UiGui.Checkbox("Automatically refresh party once when showcase opens", ref autoRefreshOnOpen))
        {
            cfg.AutoRefreshPartyOnShowcaseOpen = autoRefreshOnOpen;
            cfg.Save();
        }

        UiGui.TextWrapped("Match Scaling to CharacterInspect (60–200%) before capturing.");

        ImGui.Separator();
        UiGui.TextUnformatted("Rollout Phases");
        foreach (var phase in PluginInfo.Phases)
            UiGui.BulletText(phase);

        FinalizePendingWindowPlacement();
    }
}
