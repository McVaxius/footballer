using Dalamud.Configuration;
using System;

namespace footballer;

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    public const int DefaultInspectPreviewWindowScalePercent = 100;
    public const float DefaultInspectPreviewTopTrimFraction = 0.65f;
    public const float DefaultInspectPreviewBottomTrimFraction = 0.20f;

    public int Version { get; set; } = 4;
    public bool PluginEnabled { get; set; } = false;
    public bool DtrBarEnabled { get; set; } = true;
    public int DtrBarMode { get; set; } = 1;
    public string DtrIconEnabled { get; set; } = "\uE044";
    public string DtrIconDisabled { get; set; } = "\uE04C";
    public bool KrangleNames { get; set; } = false;
    public bool ShowMaleFeet { get; set; } = true;
    public bool ShowFemaleFeet { get; set; } = true;
    public bool WithoutFootwear { get; set; } = false;
    public bool ShowOwnFeet { get; set; } = true;
    public bool ReplaceCommendationPictures { get; set; } = true;
    public bool ShowFootShowcase { get; set; } = true;
    public bool ShowFaceNextToFeet { get; set; } = true;
    public bool RespectLodestonePrivacy { get; set; } = true;
    public bool OpenMainWindowOnLoad { get; set; } = false;
    public bool AutoRefreshPartyOnShowcaseOpen { get; set; } = false;
    public int InspectPreviewWindowScalePercent { get; set; } = DefaultInspectPreviewWindowScalePercent;
    public float InspectPreviewTopTrimFraction { get; set; } = DefaultInspectPreviewTopTrimFraction;
    public float InspectPreviewBottomTrimFraction { get; set; } = DefaultInspectPreviewBottomTrimFraction;

    public void Save() => Plugin.PluginInterface.SavePluginConfig(this);
}
