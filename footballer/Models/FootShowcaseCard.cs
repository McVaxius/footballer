namespace footballer.Models;

public sealed record FootShowcaseCard(
    PartyShowcaseMember Member,
    LodestoneFaceLookupRecord Lookup,
    string FeetGateLabel,
    string FootStatusLabel,
    string FootStatusNote,
    string? FootImagePath,
    string FaceStatusLabel,
    string FaceStatusNote,
    string? FaceImagePath,
    string VariantLabel);
