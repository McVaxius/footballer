namespace footballer.Models;

public sealed record CommendationAddonProbe(
    string AddonName,
    int? AgentId,
    string Role,
    bool Visible);

public sealed record PortraitAddonNodeEntry(
    int Index,
    uint NodeId,
    string TypeName,
    ushort RawType,
    bool Visible,
    float X,
    float Y,
    ushort Width,
    ushort Height,
    ushort Priority,
    ushort FlagsRaw,
    string FlagsLabel,
    int EventCount,
    string? FirstEventType,
    bool AppearsInteractive,
    string? Text,
    nint Address);

public sealed record PortraitSlotCandidate(
    int SlotIndex,
    uint BaseNodeId,
    uint? SliderNodeId,
    float X,
    float Y,
    ushort Width,
    ushort Height,
    bool AnyVisible,
    bool AnyInteractive,
    string Note);

public sealed record PortraitAddonNodeSnapshot(
    string AddonName,
    nint Address,
    float X,
    float Y,
    ushort NodeCount,
    string CaptureStatus,
    PortraitAddonNodeEntry[] Nodes,
    PortraitSlotCandidate[] LikelyPortraitSlots);

public sealed record BannerPartyCharacterSnapshot(
    int RowIndex,
    string Name,
    string Job,
    uint WorldId,
    bool CharacterVisible,
    bool CharacterLoaded,
    bool CharacterDataCopied,
    uint CharaViewState,
    short PoseClassJob,
    byte PortraitClassJobId,
    string SelectionHint);

public sealed record PortraitRailRowMapping(
    int RailOrder,
    uint BaseNodeId,
    uint? SliderNodeId,
    float X,
    float Y,
    ushort Width,
    ushort Height,
    bool AnyVisible,
    bool AnyInteractive,
    int? AgentRowIndex,
    string? CharacterName,
    string? Job,
    bool LikelySelected,
    string MappingNote);

public sealed record PortraitExportPayloadSnapshot(
    string CameraPosition,
    string CameraTarget,
    short ImageRotation,
    byte CameraZoom,
    ushort BannerTimeline,
    float AnimationProgress,
    byte Expression,
    string HeadDirection,
    string EyeDirection,
    string DirectionalLightingColor,
    byte DirectionalLightingBrightness,
    short DirectionalLightingVerticalAngle,
    short DirectionalLightingHorizontalAngle,
    string AmbientLightingColor,
    byte AmbientLightingBrightness,
    ushort BannerBg);

public sealed record BannerPartyAgentSnapshot(
    nint Address,
    int CharacterCount,
    string CaptureStatus,
    int? ActiveCharacterRowIndex,
    string? ActiveCharacterName,
    BannerPartyCharacterSnapshot[] Characters,
    PortraitRailRowMapping[] RailMappings,
    PortraitExportPayloadSnapshot? ActiveExportPayload,
    string ActiveExportStatus);

public sealed record CommendationPortraitResearchSnapshot(
    bool ReplaceCommendationPicturesConfigured,
    bool AnyRelevantAddonVisible,
    string SafeStatus,
    string KnownCallbackSeam,
    string NextResearchStep,
    CommendationAddonProbe[] Probes,
    PortraitAddonNodeSnapshot? BannerPartySnapshot,
    BannerPartyAgentSnapshot? BannerPartyAgentSnapshot);
