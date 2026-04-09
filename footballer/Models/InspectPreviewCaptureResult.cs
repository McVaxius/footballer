namespace footballer.Models;

public sealed record InspectPreviewCaptureResult(
    bool Success,
    string Status,
    InspectPreviewCaptureRecord? Capture);
