using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dalamud.Plugin.Services;
using footballer.Models;

namespace footballer.Services;

public sealed class CharacterInspectPreviewCaptureService
{
    private const int MinimumPreferredCaptureHeight = 72;
    private const float DefaultTopTrimFraction = Configuration.DefaultInspectPreviewTopTrimFraction;
    private const float DefaultBottomTrimFraction = Configuration.DefaultInspectPreviewBottomTrimFraction;
    private const float MaximumCombinedTrimFraction = 0.85f;
    private const int ExpandedCaptureHeightMultiplier = 2;

    private readonly IPluginLog log;
    private readonly Configuration configuration;
    private readonly string captureDirectory;
    private readonly string indexPath;
    private readonly Dictionary<string, CaptureIndexEntry> capturesByCharacterKey = new(StringComparer.OrdinalIgnoreCase);

    public CharacterInspectPreviewCaptureService(IPluginLog log, Configuration configuration, string pluginConfigDirectory)
    {
        this.log = log;
        this.configuration = configuration;
        captureDirectory = Path.Combine(pluginConfigDirectory, "inspect-preview-captures");
        indexPath = Path.Combine(captureDirectory, "capture-index.json");
        Directory.CreateDirectory(captureDirectory);
        LoadIndex();
    }

    public string CaptureDirectory => captureDirectory;

    public string LastCaptureStatus { get; private set; } =
        "No CharacterInspect preview capture has been saved yet.";

    public InspectPreviewCaptureRecord? LastCapture { get; private set; }

    public string? GetLatestCapturePath(string characterKey)
        => GetLatestCapture(characterKey)?.FilePath;

    public InspectPreviewCaptureRecord? GetLatestCapture(string characterKey)
    {
        if (string.IsNullOrWhiteSpace(characterKey))
            return null;

        if (!capturesByCharacterKey.TryGetValue(characterKey, out var entry))
            return null;

        var filePath = Path.Combine(captureDirectory, entry.FileName);
        if (!File.Exists(filePath))
        {
            capturesByCharacterKey.Remove(characterKey);
            SaveIndex();
            return null;
        }

        var capture = new InspectPreviewCaptureRecord(
            characterKey,
            entry.EntityId,
            filePath,
            entry.ClientX,
            entry.ClientY,
            entry.Width,
            entry.Height,
            DateTimeOffset.FromUnixTimeMilliseconds(entry.CapturedAtUnixMilliseconds));

        LastCapture ??= capture;
        return capture;
    }

    public InspectPreviewCaptureResult Capture(
        CharacterInspectResearchSnapshot snapshot,
        string characterKey,
        uint entityId)
    {
        if (!snapshot.CaptureReady)
            return Fail("CharacterInspect is not ready for preview capture yet. Keep the inspect preview open until the widget is visible and stable.");

        if (snapshot.PreviewNodeAddress == nint.Zero || snapshot.AddonAddress == nint.Zero)
            return Fail("CharacterInspect preview bounds are unavailable, so there is nothing safe to capture yet.");

        var windowHandle = GetGameWindowHandle();
        if (windowHandle == nint.Zero)
            return Fail("Could not resolve the game window handle for CharacterInspect preview capture.");

        if (!GetClientRect(windowHandle, out var clientRect))
            return Fail("Could not read the game client bounds for CharacterInspect preview capture.");

        var clientRectWidth = Math.Max(0, clientRect.Right - clientRect.Left);
        var clientRectHeight = Math.Max(0, clientRect.Bottom - clientRect.Top);
        var cropProfile = BuildCropProfile();
        var requestedCaptureRect = BuildPreferredCaptureRect(snapshot, cropProfile);

        if (!TryClampRect(
                requestedCaptureRect.X,
                requestedCaptureRect.Y,
                requestedCaptureRect.Width,
                requestedCaptureRect.Height,
                clientRectWidth,
                clientRectHeight,
                out var clampedRect))
        {
            return Fail("CharacterInspect preview crop landed outside the game client area, so the capture was skipped.");
        }

        var screenPoint = new POINT
        {
            X = clampedRect.X,
            Y = clampedRect.Y,
        };

        if (!ClientToScreen(windowHandle, ref screenPoint))
            return Fail("Could not translate CharacterInspect preview bounds into screen coordinates.");

        Directory.CreateDirectory(captureDirectory);

        var timestampUtc = DateTimeOffset.UtcNow;
        var captureId = BuildCaptureId(characterKey);
        var fileName = $"inspect-preview-{captureId}-{timestampUtc:yyyyMMdd-HHmmssfff}.png";
        var filePath = Path.Combine(captureDirectory, fileName);

        try
        {
            using var bitmap = new Bitmap(clampedRect.Width, clampedRect.Height);
            using var graphics = Graphics.FromImage(bitmap);
            graphics.CopyFromScreen(
                screenPoint.X,
                screenPoint.Y,
                0,
                0,
                new Size(clampedRect.Width, clampedRect.Height),
                CopyPixelOperation.SourceCopy);
            bitmap.Save(filePath, ImageFormat.Png);
        }
        catch (Exception ex)
        {
            log.Warning(ex, "[footballer] Failed to capture CharacterInspect preview.");
            return Fail($"CharacterInspect preview capture failed: {ex.GetType().Name}: {ex.Message}");
        }

        capturesByCharacterKey.TryGetValue(characterKey, out var previousEntry);
        capturesByCharacterKey[characterKey] = new CaptureIndexEntry(
            fileName,
            entityId,
            timestampUtc.ToUnixTimeMilliseconds(),
            clampedRect.X,
            clampedRect.Y,
            clampedRect.Width,
            clampedRect.Height);
        SaveIndex();

        if (previousEntry is not null && !string.Equals(previousEntry.FileName, fileName, StringComparison.Ordinal))
            TryDeleteOldCapture(previousEntry.FileName);

        var capture = new InspectPreviewCaptureRecord(
            characterKey,
            entityId,
            filePath,
            clampedRect.X,
            clampedRect.Y,
            clampedRect.Width,
            clampedRect.Height,
            timestampUtc);

        LastCapture = capture;
        LastCaptureStatus =
            $"Saved a lower-biased CharacterInspect preview capture for entity {FormatEntityId(entityId)} at {clampedRect.Width}x{clampedRect.Height}. Source height: {requestedCaptureRect.SourceHeight}px. Crop profile: top {cropProfile.TopTrimFraction:P0}, bottom {cropProfile.BottomTrimFraction:P0}.";
        return new InspectPreviewCaptureResult(true, LastCaptureStatus, capture);
    }

    private InspectPreviewCaptureResult Fail(string status)
    {
        LastCaptureStatus = status;
        return new InspectPreviewCaptureResult(false, status, null);
    }

    private void LoadIndex()
    {
        if (!File.Exists(indexPath))
            return;

        try
        {
            var json = File.ReadAllText(indexPath);
            var loaded = JsonSerializer.Deserialize<Dictionary<string, CaptureIndexEntry>>(json);
            if (loaded is null)
                return;

            foreach (var pair in loaded)
            {
                var filePath = Path.Combine(captureDirectory, pair.Value.FileName);
                if (!File.Exists(filePath))
                    continue;

                capturesByCharacterKey[pair.Key] = pair.Value;
            }
        }
        catch (Exception ex)
        {
            log.Warning(ex, "[footballer] Failed to load CharacterInspect preview capture index.");
        }
    }

    private void SaveIndex()
    {
        try
        {
            Directory.CreateDirectory(captureDirectory);
            var json = JsonSerializer.Serialize(capturesByCharacterKey, new JsonSerializerOptions
            {
                WriteIndented = true,
            });
            File.WriteAllText(indexPath, json);
        }
        catch (Exception ex)
        {
            log.Warning(ex, "[footballer] Failed to save CharacterInspect preview capture index.");
        }
    }

    private void TryDeleteOldCapture(string fileName)
    {
        try
        {
            var filePath = Path.Combine(captureDirectory, fileName);
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
        catch (Exception ex)
        {
            log.Warning(ex, "[footballer] Failed to delete an older CharacterInspect preview capture.");
        }
    }

    private static nint GetGameWindowHandle()
    {
        var handle = Process.GetCurrentProcess().MainWindowHandle;
        if (handle != nint.Zero)
            return handle;

        var currentProcessId = (uint)Environment.ProcessId;
        nint discoveredHandle = nint.Zero;
        EnumWindows(
            (windowHandle, parameter) =>
            {
                if (!IsWindowVisible(windowHandle))
                    return true;

                GetWindowThreadProcessId(windowHandle, out var processId);
                if (processId != (uint)parameter)
                    return true;

                discoveredHandle = windowHandle;
                return false;
            },
            (nint)currentProcessId);
        return discoveredHandle;
    }

    private static string BuildCaptureId(string characterKey)
    {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(characterKey));
        return Convert.ToHexString(hashBytes[..6]).ToLowerInvariant();
    }

    private static int RoundToInt(float value)
        => (int)MathF.Round(value, MidpointRounding.AwayFromZero);

    public (float TopTrimFraction, float BottomTrimFraction) GetConfiguredCropFractions()
    {
        var profile = BuildCropProfile();
        return (profile.TopTrimFraction, profile.BottomTrimFraction);
    }

    private CropProfile BuildCropProfile()
    {
        var topTrimFraction = float.IsFinite(configuration.InspectPreviewTopTrimFraction)
            ? configuration.InspectPreviewTopTrimFraction
            : DefaultTopTrimFraction;
        var bottomTrimFraction = float.IsFinite(configuration.InspectPreviewBottomTrimFraction)
            ? configuration.InspectPreviewBottomTrimFraction
            : DefaultBottomTrimFraction;

        topTrimFraction = Math.Clamp(topTrimFraction, 0f, 0.75f);
        bottomTrimFraction = Math.Clamp(bottomTrimFraction, 0f, 0.5f);

        var combined = topTrimFraction + bottomTrimFraction;
        if (combined > MaximumCombinedTrimFraction)
        {
            var overflow = combined - MaximumCombinedTrimFraction;
            if (bottomTrimFraction >= overflow)
                bottomTrimFraction -= overflow;
            else
            {
                topTrimFraction = Math.Max(0f, topTrimFraction - (overflow - bottomTrimFraction));
                bottomTrimFraction = 0f;
            }
        }

        return new CropProfile(topTrimFraction, bottomTrimFraction);
    }

    private static CaptureRect BuildPreferredCaptureRect(
        CharacterInspectResearchSnapshot snapshot,
        CropProfile cropProfile)
    {
        var requestedClientX = RoundToInt(snapshot.AddonX + snapshot.PreviewNodeX);
        var requestedClientY = RoundToInt(snapshot.AddonY + snapshot.PreviewNodeY);
        var captureWidth = snapshot.PreviewNodeWidth;
        var captureHeight = Math.Max(
            snapshot.PreviewNodeHeight,
            snapshot.PreviewNodeHeight * ExpandedCaptureHeightMultiplier);

        if (captureHeight < MinimumPreferredCaptureHeight)
            return new CaptureRect(requestedClientX, requestedClientY, captureWidth, captureHeight, captureHeight);

        var topTrim = RoundToInt(captureHeight * cropProfile.TopTrimFraction);
        var bottomTrim = RoundToInt(captureHeight * cropProfile.BottomTrimFraction);
        var preferredY = requestedClientY + topTrim;
        var preferredHeight = captureHeight - topTrim - bottomTrim;

        if (preferredHeight < MinimumPreferredCaptureHeight)
        {
            topTrim = RoundToInt(captureHeight / 8f);
            bottomTrim = 0;
            preferredY = requestedClientY + topTrim;
            preferredHeight = captureHeight - topTrim - bottomTrim;
        }

        if (preferredHeight < MinimumPreferredCaptureHeight)
            return new CaptureRect(requestedClientX, requestedClientY, captureWidth, captureHeight, captureHeight);

        return new CaptureRect(requestedClientX, preferredY, captureWidth, preferredHeight, captureHeight);
    }

    private static bool TryClampRect(
        int requestedX,
        int requestedY,
        int requestedWidth,
        int requestedHeight,
        int clientWidth,
        int clientHeight,
        out CaptureRect rect)
    {
        var x = Math.Clamp(requestedX, 0, Math.Max(0, clientWidth - 1));
        var y = Math.Clamp(requestedY, 0, Math.Max(0, clientHeight - 1));
        var width = Math.Min(requestedWidth, clientWidth - x);
        var height = Math.Min(requestedHeight, clientHeight - y);

        if (width <= 0 || height <= 0)
        {
            rect = default;
            return false;
        }

        rect = new CaptureRect(x, y, width, height, requestedHeight);
        return true;
    }

    private static string FormatEntityId(uint entityId)
        => entityId == 0 ? "-" : $"0x{entityId:X8}";

    private sealed record CaptureIndexEntry(
        string FileName,
        uint EntityId,
        long CapturedAtUnixMilliseconds,
        int ClientX,
        int ClientY,
        int Width,
        int Height);

    private readonly record struct CropProfile(float TopTrimFraction, float BottomTrimFraction);
    private readonly record struct CaptureRect(int X, int Y, int Width, int Height, int SourceHeight);

    private delegate bool EnumWindowsProc(nint hWnd, nint lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc callback, nint lParam);

    [DllImport("user32.dll")]
    private static extern bool ClientToScreen(nint hWnd, ref POINT point);

    [DllImport("user32.dll")]
    private static extern bool GetClientRect(nint hWnd, out RECT rect);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(nint hWnd);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint hWnd, out uint processId);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
