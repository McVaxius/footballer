namespace footballer.Models;

public sealed record InspectPreviewCaptureRecord(
    string CharacterKey,
    uint EntityId,
    string FilePath,
    int ClientX,
    int ClientY,
    int Width,
    int Height,
    DateTimeOffset CapturedAtUtc);
