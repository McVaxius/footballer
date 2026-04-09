using System;

namespace footballer.Models;

public sealed record LodestoneFaceLookupRecord(
    string CharacterName,
    string WorldName,
    LodestoneFaceLookupStatus Status,
    string SearchUrl,
    string? ProfileUrl,
    string? SearchFaceImageUrl,
    string? ProfileFaceImageUrl,
    string? Note,
    DateTimeOffset UpdatedAtUtc,
    string? CachedFaceImagePath)
{
    public string CharacterKey => string.IsNullOrWhiteSpace(WorldName) ? CharacterName : $"{CharacterName}@{WorldName}";

    public string? EffectiveFaceImageUrl => string.IsNullOrWhiteSpace(ProfileFaceImageUrl) ? SearchFaceImageUrl : ProfileFaceImageUrl;
}
