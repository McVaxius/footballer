namespace footballer;

internal static class PluginInfo
{
    public const string DisplayName = "Footballer";
    public const string InternalName = "footballer";
    public const string Command = "/footballer";
    public const string Visibility = "Private";
    public const string Summary = "Privacy-respecting party-foot showcase plugin with a saved main-window krangle toggle, cached Lodestone face gating, compact normal-mode character boxes, automatic party feet recapture on Refresh party, a preset inspect pose, preview-only barefoot mode, and hidden CharacterInspect/BannerParty research surfaces behind /footballer debug.";
    public const string SupportUrl = "https://ko-fi.com/mcvaxius";
    public const string DiscordUrl = "https://discord.gg/VsXqydsvpu";
    public const string DiscordFeedbackNote = "Scroll down to \"The Dumpster Fire\" channel to discuss issues / suggestions for specific plugins.";
    public const string ShellStatus =
        "Current build status: core showcase live. The normal window now keeps the party showcase, Inspect flow, preset inspect pose, preview capture flow, stored crop profile, and preview-only multi-seam barefoot apply path active without frame-by-frame research polling. Raw CharacterInspect and BannerParty tables still exist, but only behind /footballer debug.";

    public static readonly string[] CommandAliases =
    {
        "/foot",
    };

    public static readonly string[] Concept =
    {
        "Hide feet when a character's face is unavailable on Lodestone to respect privacy.",
        "Treat portrait-window replacement and the showcase window as explicit experiments with safe toggles and hidden debug surfaces.",
        "Use live CharacterInspect preview capture plus a queued preset inspect yaw and a preview-only multi-seam feet clear and redraw path for the current direct character-derived branch, then defer true foot-only crop/export until that path stays stable.",
        "Use the TTSL Lodestone face approach as the likely path for adjacent identity visuals.",
    };

    public static readonly string[] Services =
    {
        "LodestoneProfileService",
        "PartyShowcaseService",
        "FootShowcaseService",
        "CharacterInspectResearchService",
        "CommendationPortraitResearchService",
        "CharacterInspectPoseService",
        "CharacterInspectFootwearService",
        "PartyFeetRefreshService",
    };

    public static readonly string[] LiveToday =
    {
        "Main window, settings window, and DTR toggle",
        "Current party showcase data model with real or krangled labels from a saved toggle",
        "Cached Lodestone search/profile lookup state with local face thumbnails",
        "Live party foot showcase cards with honest capture status",
        "Inspect buttons plus a queued preset inspect pose and action-driven preview capture",
        "Automatic party feet recapture queue from Refresh party",
        "Preview-only barefoot multi-seam apply path when Without footwear is enabled",
        "Hidden CharacterInspect/BannerParty debug surfaces behind /footballer debug",
        "Stored privacy/default configuration values",
        "Standard /footballer, /foot, /footballer ws, and /footballer j shell controls",
        "Ko-fi and Discord links",
    };

    public static readonly string[] Phases =
    {
        "Shell and docs",
        "Live party showcase and optional krangle toggle",
        "Lodestone face cache + privacy gating",
        "Direct CharacterInspect foot-capture research",
        "BannerParty read-only research",
        "Portrait write-path research",
    };

    public static readonly string[] Tests =
    {
        "Load plugin and open the main window",
        "Check DTR toggle behavior and party snapshot population",
        "Refresh Lodestone lookup state and confirm face thumbnails can appear on the showcase cards",
        "Use Refresh party and confirm the plugin walks the live party snapshot and refreshes feet captures sequentially",
        "Toggle male/female/self/showcase/face settings and confirm the live showcase cards respond immediately",
        "Use Inspect on a party row or showcase card and confirm CharacterInspect opens for that member",
        "Confirm the inspect preview rotates to the stored side-angle pose before capture",
        "Use Capture Current Preview and confirm the saved CharacterInspect image lands on the matching showcase card",
        "Enable Without footwear and confirm Inspect clears the preview feet slot before capture",
        "Type /footballer debug and confirm the hidden inspect and BannerParty research surfaces appear",
        "Use /foot ws and /foot j",
        "Verify the hidden privacy override only appears after /footballer debug",
    };
}
