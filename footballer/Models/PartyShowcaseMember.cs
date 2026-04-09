namespace footballer.Models;

public sealed record PartyShowcaseMember(
    int Slot,
    string Name,
    string WorldName,
    string JobAbbreviation,
    uint JobId,
    int Level,
    string ContentId,
    uint EntityId,
    bool IsLocalPlayer,
    string SexLabel,
    string RaceLabel,
    string TribeLabel,
    bool HasCustomizeData)
{
    public string CharacterKey => string.IsNullOrWhiteSpace(WorldName) ? Name : $"{Name}@{WorldName}";

    public string DisplayName => IsLocalPlayer ? $"{Name} (You)" : Name;

    public string ContentIdShort => ContentId.Length <= 8 ? ContentId : ContentId[..8];

    public bool IsFemale => string.Equals(SexLabel, "Female", StringComparison.OrdinalIgnoreCase);

    public bool IsMale => string.Equals(SexLabel, "Male", StringComparison.OrdinalIgnoreCase);
}
