using System;
using System.Collections.Generic;
using footballer.Models;

namespace footballer.Services;

public sealed class FootShowcaseService
{
    public IReadOnlyList<FootShowcaseCard> BuildCards(
        IReadOnlyList<PartyShowcaseMember> members,
        LodestoneProfileService lodestoneProfileService,
        Configuration configuration,
        bool effectiveRespectPrivacy,
        CharacterInspectPreviewCaptureService inspectPreviewCaptureService)
    {
        var cards = new List<FootShowcaseCard>(members.Count);

        foreach (var member in members)
        {
            var lookup = lodestoneProfileService.GetRecord(member.Name, member.WorldName);
            var feetGateLabel = LodestoneProfileService.GetFeetGateLabel(lookup, effectiveRespectPrivacy);
            var blockedByOwnSetting = member.IsLocalPlayer && !configuration.ShowOwnFeet;
            var blockedBySexSetting = member.IsMale
                ? !configuration.ShowMaleFeet
                : member.IsFemale
                    ? !configuration.ShowFemaleFeet
                    : !(configuration.ShowMaleFeet || configuration.ShowFemaleFeet);
            var blockedByFeetGate = feetGateLabel is not ("Allowed" or "Bypassed");
            var showFootAsset = configuration.ShowFootShowcase &&
                                !blockedByOwnSetting &&
                                !blockedBySexSetting &&
                                !blockedByFeetGate &&
                                member.EntityId != 0;
            var previewCapture = inspectPreviewCaptureService.GetLatestCapture(member.CharacterKey);
            var previewSnippedForMember = showFootAsset &&
                                          previewCapture is not null &&
                                          File.Exists(previewCapture.FilePath);

            var footStatusLabel = showFootAsset
                ? previewSnippedForMember
                    ? "Preview captured"
                    : "Inspect-ready"
                : !configuration.ShowFootShowcase
                    ? "Off"
                    : blockedByOwnSetting
                        ? "Self hidden"
                        : blockedBySexSetting
                            ? "Filtered"
                        : blockedByFeetGate
                            ? feetGateLabel
                            : member.EntityId == 0
                                ? "No live entity"
                                : "Pending capture";
            var footStatusNote = showFootAsset
                ? previewSnippedForMember
                    ? BuildPreviewSnippedNote(configuration, previewCapture!)
                    : configuration.WithoutFootwear
                        ? "Barefoot preview mode is enabled. Use Inspect on this card or in the party table, wait for CharacterInspect to settle, then use Capture current preview from the main window."
                        : "Direct inspect-derived foot capture is the intended path here. Use Inspect on this card or in the party table, wait for CharacterInspect to settle, then use Capture current preview from the main window."
                : !configuration.ShowFootShowcase
                    ? "The foot showcase toggle is currently off."
                    : blockedByOwnSetting
                        ? "Your own feet are hidden because Show own feet is off."
                    : blockedBySexSetting
                        ? $"This card is filtered by the {member.SexLabel} feet toggle."
                    : blockedByFeetGate
                        ? lookup.Note ?? "Feet are blocked by the current Lodestone privacy gate state."
                        : member.EntityId == 0
                            ? "This party member is not currently backed by a live entity id, so CharacterInspect cannot be requested yet."
                            : "The live CharacterInspect seam is available, but you still need to open inspect and use Capture current preview from the main window to save an image for this card.";
            var footImagePath = previewSnippedForMember
                ? previewCapture!.FilePath
                : null;

            var faceImagePath = configuration.ShowFaceNextToFeet &&
                                !string.IsNullOrWhiteSpace(lookup.CachedFaceImagePath) &&
                                System.IO.File.Exists(lookup.CachedFaceImagePath)
                ? lookup.CachedFaceImagePath
                : null;
            var faceStatusLabel = faceImagePath is not null
                ? "Visible"
                : !configuration.ShowFaceNextToFeet
                    ? "Off"
                    : LodestoneProfileService.GetStatusLabel(lookup.Status);
            var faceStatusNote = faceImagePath is not null
                ? "Showing the cached Lodestone face image next to the foot showcase card."
                : !configuration.ShowFaceNextToFeet
                    ? "The face-next-to-feet toggle is currently off."
                    : lookup.Note ?? "Use Refresh Lodestone to resolve the current face state.";

            cards.Add(new FootShowcaseCard(
                member,
                lookup,
                feetGateLabel,
                footStatusLabel,
                footStatusNote,
                footImagePath,
                faceStatusLabel,
                faceStatusNote,
                faceImagePath,
                member.HasCustomizeData
                    ? $"{member.RaceLabel} / {member.TribeLabel} / {member.SexLabel}"
                    : $"{member.SexLabel} / local customize data unavailable"));
        }

        return cards;
    }

    private static string BuildPreviewSnippedNote(
        Configuration configuration,
        InspectPreviewCaptureRecord capture)
    {
        var branchNote = configuration.WithoutFootwear
            ? " Barefoot preview mode was enabled when this preview-capture flow ran."
            : string.Empty;
        return $"Showing the last saved lower-biased CharacterInspect preview capture for this member. Capture rect: ({capture.ClientX}, {capture.ClientY}) {capture.Width}x{capture.Height}.{branchNote}";
    }
}
