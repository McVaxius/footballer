using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Plugin.Services;
using footballer.Models;

namespace footballer.Services;

public sealed class LodestoneProfileService : IDisposable
{
    private const string LodestoneBaseUrl = "https://na.finalfantasyxiv.com";
    private static readonly TimeSpan FreshCacheDuration = TimeSpan.FromMinutes(30);
    private static readonly Regex SearchEntryRegex = new(
        "<div class=\"entry\"><a href=\"(?<path>/lodestone/character/\\d+/)\" class=\"entry__link\"><div class=\"entry__chara__face\"><img src=\"(?<face>https://img2\\.finalfantasyxiv\\.com/f/[^\"]+)\" alt=\"(?<name>[^\"]*)\"></div><div class=\"entry__box entry__box--world\"><p class=\"entry__name\">(?<displayName>.*?)</p><p class=\"entry__world\">.*?</i>(?<world>[^<\\[]+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
    private static readonly Regex SearchPathRegex = new(
        "href=\"(?<path>/lodestone/character/\\d+/)\"",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex ProfileFaceRegex = new(
        "frame__chara__face\">(?:(?!</div>).)*?<img src=\"(?<face>https://img2\\.finalfantasyxiv\\.com/f/[^\"]+)\"",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
    private static readonly HttpClient HttpClient = CreateHttpClient();

    private readonly IPluginLog log;
    private readonly string faceCacheDirectory;
    private readonly ConcurrentDictionary<string, LodestoneFaceLookupRecord> cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, byte> inFlight = new(StringComparer.OrdinalIgnoreCase);
    private readonly CancellationTokenSource disposeCts = new();

    public LodestoneProfileService(IPluginLog log, string pluginConfigDirectory)
    {
        this.log = log;
        faceCacheDirectory = Path.Combine(pluginConfigDirectory, "FaceCache");
        Directory.CreateDirectory(faceCacheDirectory);
    }

    public void Dispose()
    {
        disposeCts.Cancel();
        disposeCts.Dispose();
    }

    public LodestoneFaceLookupRecord GetRecord(string characterName, string worldName)
    {
        var searchUrl = BuildSearchUrl(characterName, worldName);
        var key = BuildCharacterKey(characterName, worldName);
        if (cache.TryGetValue(key, out var record))
            return record;

        return new LodestoneFaceLookupRecord(
            characterName,
            worldName,
            LodestoneFaceLookupStatus.NotRequested,
            searchUrl,
            null,
            null,
            null,
            string.IsNullOrWhiteSpace(worldName) ? "No home world is available yet." : "Waiting for lookup.",
            DateTimeOffset.MinValue,
            GetExistingFaceCachePath(key));
    }

    public void QueueRefresh(IEnumerable<PartyShowcaseMember> members, bool force)
    {
        foreach (var member in members
                     .Where(member => !string.IsNullOrWhiteSpace(member.Name))
                     .Where(member => !string.IsNullOrWhiteSpace(member.WorldName))
                     .DistinctBy(member => BuildCharacterKey(member.Name, member.WorldName)))
        {
            EnsureLookup(member.Name, member.WorldName, force);
        }
    }

    public void EnsureLookup(string characterName, string worldName, bool force = false)
    {
        if (string.IsNullOrWhiteSpace(characterName) || string.IsNullOrWhiteSpace(worldName))
            return;

        var key = BuildCharacterKey(characterName, worldName);
        var existing = GetRecord(characterName, worldName);
        if (!force &&
            existing.UpdatedAtUtc != DateTimeOffset.MinValue &&
            DateTimeOffset.UtcNow - existing.UpdatedAtUtc < FreshCacheDuration &&
            existing.Status != LodestoneFaceLookupStatus.Error &&
            existing.Status != LodestoneFaceLookupStatus.NotRequested)
        {
            return;
        }

        if (!inFlight.TryAdd(key, 0))
            return;

        cache[key] = new LodestoneFaceLookupRecord(
            characterName,
            worldName,
            LodestoneFaceLookupStatus.Running,
            BuildSearchUrl(characterName, worldName),
            existing.ProfileUrl,
            existing.SearchFaceImageUrl,
            existing.ProfileFaceImageUrl,
            "Refreshing Lodestone search and profile data.",
            DateTimeOffset.UtcNow,
            existing.CachedFaceImagePath);

        _ = Task.Run(() => RefreshLookupAsync(key, characterName, worldName, disposeCts.Token));
    }

    public static string GetStatusLabel(LodestoneFaceLookupStatus status)
        => status switch
        {
            LodestoneFaceLookupStatus.NotRequested => "Not checked",
            LodestoneFaceLookupStatus.Running => "Refreshing",
            LodestoneFaceLookupStatus.FaceAvailable => "Face visible",
            LodestoneFaceLookupStatus.PrivacyHidden => "No face exposed",
            LodestoneFaceLookupStatus.NotFound => "No profile match",
            _ => "Lookup error",
        };

    public static string GetFeetGateLabel(LodestoneFaceLookupRecord record, bool respectLodestonePrivacy)
    {
        if (!respectLodestonePrivacy)
            return "Bypassed";

        return record.Status switch
        {
            LodestoneFaceLookupStatus.FaceAvailable => "Allowed",
            LodestoneFaceLookupStatus.PrivacyHidden => "Hidden",
            _ => "Hold",
        };
    }

    public static string BuildSearchUrl(string characterName, string worldName)
        => $"{LodestoneBaseUrl}/lodestone/character/?q={Uri.EscapeDataString(characterName)}&worldname={Uri.EscapeDataString(worldName)}";

    private static string BuildCharacterKey(string characterName, string worldName)
        => $"{characterName.Trim()}@{worldName.Trim()}";

    private async Task RefreshLookupAsync(string key, string characterName, string worldName, CancellationToken cancellationToken)
    {
        try
        {
            var searchUrl = BuildSearchUrl(characterName, worldName);
            var searchHtml = await HttpClient.GetStringAsync(searchUrl, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            var searchMatch = SearchEntryRegex.Matches(searchHtml)
                .Cast<Match>()
                .FirstOrDefault(match =>
                    string.Equals(WebUtility.HtmlDecode(match.Groups["name"].Value).Trim(), characterName, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(WebUtility.HtmlDecode(match.Groups["world"].Value).Trim(), worldName, StringComparison.OrdinalIgnoreCase));
            searchMatch ??= SearchEntryRegex.Match(searchHtml);

            var profilePath = searchMatch.Success
                ? searchMatch.Groups["path"].Value
                : SearchPathRegex.Match(searchHtml).Groups["path"].Value;
            var searchFaceImageUrl = searchMatch.Success ? searchMatch.Groups["face"].Value : null;

            if (string.IsNullOrWhiteSpace(profilePath))
            {
                cache[key] = new LodestoneFaceLookupRecord(
                    characterName,
                    worldName,
                    LodestoneFaceLookupStatus.NotFound,
                    searchUrl,
                    null,
                    null,
                    null,
                    "No Lodestone profile match was found for the current name/world pair.",
                    DateTimeOffset.UtcNow,
                    GetExistingFaceCachePath(key));
                return;
            }

            var profileUrl = $"{LodestoneBaseUrl}{profilePath}";
            var profileHtml = await HttpClient.GetStringAsync(profileUrl, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            var profileFaceMatch = ProfileFaceRegex.Match(profileHtml);
            if (profileFaceMatch.Success)
            {
                var cachedFaceImagePath = await CacheFaceImageAsync(key, profileFaceMatch.Groups["face"].Value, cancellationToken);
                cache[key] = new LodestoneFaceLookupRecord(
                    characterName,
                    worldName,
                    LodestoneFaceLookupStatus.FaceAvailable,
                    searchUrl,
                    profileUrl,
                    searchFaceImageUrl,
                    profileFaceMatch.Groups["face"].Value,
                    "Profile resolved with a face image.",
                    DateTimeOffset.UtcNow,
                    cachedFaceImagePath);
                return;
            }

            var privacyHidden = profileHtml.Contains("frame__chara__face", StringComparison.OrdinalIgnoreCase);
            cache[key] = new LodestoneFaceLookupRecord(
                characterName,
                worldName,
                privacyHidden ? LodestoneFaceLookupStatus.PrivacyHidden : LodestoneFaceLookupStatus.Error,
                searchUrl,
                profileUrl,
                searchFaceImageUrl,
                null,
                privacyHidden
                    ? "Profile resolved but no face image was exposed."
                    : "Profile page loaded, but the expected face markup was not found.",
                DateTimeOffset.UtcNow,
                GetExistingFaceCachePath(key));
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            log.Warning(ex, "[footballer] Lodestone lookup failed for {CharacterKey}.", KrangleService.KrangleName(key));
            cache[key] = new LodestoneFaceLookupRecord(
                characterName,
                worldName,
                LodestoneFaceLookupStatus.Error,
                BuildSearchUrl(characterName, worldName),
                null,
                null,
                null,
                ex.Message,
                DateTimeOffset.UtcNow,
                GetExistingFaceCachePath(key));
        }
        finally
        {
            inFlight.TryRemove(key, out _);
        }
    }

    private string? GetExistingFaceCachePath(string key)
    {
        if (!Directory.Exists(faceCacheDirectory))
            return null;

        var safeKey = SanitizeFileName(key);
        return Directory.GetFiles(faceCacheDirectory, $"{safeKey}.*")
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    private async Task<string?> CacheFaceImageAsync(string key, string? faceImageUrl, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(faceImageUrl))
            return GetExistingFaceCachePath(key);

        try
        {
            Directory.CreateDirectory(faceCacheDirectory);
            var extension = GetFaceImageExtension(faceImageUrl);
            var safeKey = SanitizeFileName(key);
            var targetPath = Path.Combine(faceCacheDirectory, $"{safeKey}{extension}");
            var imageBytes = await HttpClient.GetByteArrayAsync(faceImageUrl, cancellationToken);
            await File.WriteAllBytesAsync(targetPath, imageBytes, cancellationToken);

            foreach (var stalePath in Directory.GetFiles(faceCacheDirectory, $"{safeKey}.*")
                         .Where(path => !string.Equals(path, targetPath, StringComparison.OrdinalIgnoreCase)))
            {
                File.Delete(stalePath);
            }

            return targetPath;
        }
        catch (OperationCanceledException)
        {
            return GetExistingFaceCachePath(key);
        }
        catch (Exception ex)
        {
            log.Warning(ex, "[footballer] Failed to cache Lodestone face image for {CharacterKey}.", KrangleService.KrangleName(key));
            return GetExistingFaceCachePath(key);
        }
    }

    private static string GetFaceImageExtension(string faceImageUrl)
    {
        try
        {
            var extension = Path.GetExtension(new Uri(faceImageUrl).AbsolutePath);
            return string.IsNullOrWhiteSpace(extension) || extension.Length > 8 ? ".png" : extension;
        }
        catch
        {
            return ".png";
        }
    }

    private static string SanitizeFileName(string input)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = input
            .Select(ch => invalid.Contains(ch) ? '_' : ch)
            .ToArray();
        return new string(chars);
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(20),
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("footballer-research-shell/0.0.0.1");
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en-US,en;q=0.9");
        return client;
    }
}
