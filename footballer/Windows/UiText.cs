using System.Collections;
using System.Globalization;
using System.Numerics;
using System.Resources;
using System.Text.RegularExpressions;
using AethertekUI;
using Dalamud.Bindings.ImGui;

namespace footballer.Windows;

internal sealed class UiText : IDisposable
{
    [ThreadStatic] private static UiText? current;
    internal static UiText Current => current ?? throw new InvalidOperationException("Enter the Footballer UI frame before drawing.");
    internal static readonly (string Code,string Name)[] Languages=[("en","English"),("de","Deutsch"),("fr","Français"),
        ("es","Español"),("it","Italiano"),("ru","Русский"),("ja","日本語"),("ko","한국어"),("zh-Hans","简体中文"),
        ("vi","Tiếng Việt"),("pt-BR","Português (Brasil)"),("id","Bahasa Indonesia"),("pl","Polski"),("tr","Türkçe"),("hi","हिन्दी")];
    internal static IEnumerable<string> CjkLanguages(string selected) => new[]{"ja","ko","zh-Hans"}.OrderBy(code=>code==selected?0:1);
    private readonly ResourceManager manager;
    internal ResourceSet Resources { get; }
    internal IReadOnlyList<string> RequiredText { get; }
    internal CultureInfo Culture { get; }
    internal string Language { get; }
    private readonly Func<UiFontRole,IDisposable> pushFont;
    private readonly (Regex Pattern, string Key, int[] ArgumentIndexes, int ArgumentCount)[] messageTemplates;
    internal UiText(string language, Func<UiFontRole,IDisposable> pushFont)
    {
        Language=Languages.Any(l=>l.Code==language)?language:"en";
        Culture=CultureInfo.GetCultureInfo(Language);
        manager=new ResourceManager("footballer.Localization.Strings_"+Language.Replace('-','_'),typeof(UiText).Assembly);
        Resources=manager.GetResourceSet(CultureInfo.InvariantCulture,true,false) ?? throw new MissingManifestResourceException(Language);
        this.pushFont=pushFont;
        var english=new ResourceManager("footballer.Localization.Strings_en",typeof(UiText).Assembly);
        try
        {
            var fallback=english.GetResourceSet(CultureInfo.InvariantCulture,true,false) ?? throw new MissingManifestResourceException("en");
            RequiredText=Values(Resources).Concat(Values(fallback)).Concat(Languages.Where(l=>l.Code!="hi").Select(l=>l.Name)).Append("\u2661").Distinct().ToArray();
        }
        finally { english.ReleaseAllResources(); }
        // Service messages remain English in logs; only their UI copies are localized.
        var parameter = new Regex(@"\{(\d+)(?::([^}]+))?\}");
        messageTemplates = Resources.Cast<DictionaryEntry>().Select(entry => (string)entry.Key)
            .Where(key => parameter.IsMatch(key)).OrderByDescending(key => parameter.Replace(key, "").Length).Select(key =>
            {
                var argumentIndexes = new List<int>();
                var pattern = @"\A";
                var offset = 0;
                foreach (Match hole in parameter.Matches(key))
                {
                    var argumentIndex = int.Parse(hole.Groups[1].Value, CultureInfo.InvariantCulture);
                    // PartyFeetRefreshService emits integer ordinals here; '(You)' belongs to its name slot.
                    var capture = key.Contains("({1}/{2})", StringComparison.Ordinal) && argumentIndex is 1 or 2 ? "([0-9]+)" : "(.*?)";
                    pattern += Regex.Escape(key[offset..hole.Index]) + capture;
                    argumentIndexes.Add(argumentIndex);
                    offset = hole.Index + hole.Length;
                }
                pattern += Regex.Escape(key[offset..]) + @"\z";
                // This runtime's non-backtracking matcher loses captures at a final LF with \z.
                return (new Regex(pattern, RegexOptions.CultureInvariant | RegexOptions.Singleline, TimeSpan.FromMilliseconds(20)), key, argumentIndexes.ToArray(), argumentIndexes.Max() + 1);
            }).ToArray();
    }
    internal static string T(string english)
    {
        if (Current.Resources.GetString(english, false) is { } exact) return exact;
        if (Current.Language == "en") return english;
        const string activeRail = " This is the current active rail candidate.";
        if (english.StartsWith("Ordered by ", StringComparison.Ordinal) && english.EndsWith(activeRail, StringComparison.Ordinal))
            return T(english[..^activeRail.Length]) + T(activeRail);
        foreach (var template in Current.messageTemplates)
        {
            var match = template.Pattern.Match(english);
            if (!match.Success) continue;
            // Service values are already formatted. Names, paths, exceptions and padded numbers stay raw.
            var args = Enumerable.Repeat<object>(string.Empty, template.ArgumentCount).ToArray();
            for (var index = 0; index < template.ArgumentIndexes.Length; index++)
                args[template.ArgumentIndexes[index]] = match.Groups[index + 1].Value;
            return string.Format(Current.Culture, Current.Resources.GetString(template.Key, false)!, LocalizeArguments(template.Key, args));
        }
        return english; // Names, game data and raw diagnostic values are consumer data.
    }
    private static object?[] LocalizeArguments(string key, object?[] args)
    {
        var localized = args.ToArray();
        // These member slots come from FormatDisplayName or the inspect services' entity fallback.
        if (key is "Waiting for AgentInspect before refreshing {0} ({1}/{2})."
            or "Waiting for CharacterInspect to switch to {0} ({1}/{2})."
            or "CharacterInspect is still settling on {0} ({1}/{2})."
            or "Waiting for the inspect preview to become capture-ready for {0} ({1}/{2})."
            or "Waiting for the preset inspect pose on {0} ({1}/{2})."
            or "Waiting for the barefoot preview apply path on {0} ({1}/{2})."
            or "Holding {0} steady for capture ({1}/{2}). Waiting {3:0.0}s more before saving the feet preview."
            or "Waiting for AgentInspect before starting {0} ({1}/{2})."
            or "Refreshing feet preview for {0} ({1}/{2}). Requested CharacterInspect."
            or "Timed out while refreshing {0}." or "Updated the feet preview for {0} ({1}/{2})."
            or "Capture failed for {0}: {1}"
            or "Queued the inspect pose preset for {0}. Waiting for CharacterInspect to finish loading that target."
            or "Waiting for CharacterInspect to switch to {0} before applying the inspect pose preset."
            or "CharacterInspect is open for {0}, but the preview model is not ready for the inspect pose preset yet."
            or "CharacterInspect is open for {0}, but the preview character is still unavailable."
            or "Inspect pose preset timed out for {0}. Re-open Inspect and try again."
            or "Inspect pose preset is ready for {0}. {1}" or "Applied the inspect pose preset for {0}. {1}"
            or "Inspect pose preset is still applying for {0}. {1}"
            or "Queued a barefoot preview update for {0}. Waiting for CharacterInspect to finish loading that target."
            or "Barefoot preview update timed out for {0}. Re-open Inspect and try again."
            or "Waiting for CharacterInspect to switch to {0} before clearing the feet slot."
            or "CharacterInspect is open for {0}, but the preview model is not ready for a feet-slot clear yet."
            or "CharacterInspect is open for {0}, but the preview model is not ready to expose a feet slot yet."
            or "Barefoot preview is ready for {0}. {1}" or "Barefoot preview applied for {0}. {1}")
            if (args[0] is string member) localized[0] = LocalizeMemberLabel(member);
        if (key == "Barefoot preview update attempt {0} is still applying for {1}. {2}" && args[1] is string retryMember)
            localized[1] = LocalizeMemberLabel(retryMember);
        if (key is "Krangle labels: {0}" or "Auto refresh party on showcase open: {0}"
            or "Show male feet: {0}" or "Show female feet: {0}" or "Without footwear: {0}"
            or "Show own feet: {0}" or "Replace party portrait window pictures: {0}"
            or "Show face next to feet: {0}" or "Lodestone privacy gate (debug): {0}"
            or "Configured portrait replacement toggle: {0}" or "Foot showcase: {0}" or "{0} / {1}")
            for (var index = 0; index < localized.Length; index++)
                if (localized[index] is string s && s is "Yes" or "No" or "Y" or "N" or "On" or "Off" or "Forced / On" or "Override Off" or "Interactive" or "Passive")
                    localized[index] = T(s);
        if (key == "{0} | {1} | {2}" && args[1] is string interaction && interaction is "Interactive" or "Passive")
            localized[1] = T(interaction);
        if (key is "Party feet refresh: {0}" or "Pose preset: {0}" or "Barefoot apply: {0}" or "Preview snip: {0}" or "Feet: {0}")
            localized[0] = args[0] is string message ? T(message) : args[0];
        if (key is "Inspect pose preset is ready for {0}. {1}" or "Applied the inspect pose preset for {0}. {1}"
            or "Inspect pose preset is still applying for {0}. {1}" or "Barefoot preview is ready for {0}. {1}"
            or "Barefoot preview applied for {0}. {1}")
            localized[1] = args[1] is string detail ? T(detail) : args[1];
        if (key == "Barefoot preview update attempt {0} is still applying for {1}. {2}")
            localized[2] = args[2] is string detail ? T(detail) : args[2];
        // This slot comes from captureResult.Status, while its nested exception arguments remain raw.
        if (key == "Capture failed for {0}: {1}" && args[1] is string failure
            && (failure.StartsWith("CharacterInspect ", StringComparison.Ordinal) || failure.StartsWith("Could not ", StringComparison.Ordinal)))
            localized[1] = T(failure);
        if (key == "Automatic party feet refresh finished. Captured {0}/{1} member(s){2}." && args[2] is string suffix)
        {
            var match = Regex.Match(suffix, @"^(?:, failed (?<failed>\d+))?(?:, skipped (?<skipped>\d+) without live entity ids)?$", RegexOptions.CultureInvariant);
            if (match.Success)
                localized[2] = (match.Groups["failed"].Success ? F(", failed {0}", match.Groups["failed"].Value) : "")
                    + (match.Groups["skipped"].Success ? F(", skipped {0} without live entity ids", match.Groups["skipped"].Value) : "");
        }
        if (key == "Showing the last saved lower-biased CharacterInspect preview capture for this member. Capture rect: ({0}, {1}) {2}x{3}.{4}"
            && args[4] is string branch && branch == " Barefoot preview mode was enabled when this preview-capture flow ran.")
            localized[4] = T(branch);
        if (key == "Saved a lower-biased CharacterInspect preview capture for entity {0} at {1}x{2}. Effective preview scale: X {3:0.#}% / Y {4:0.#}% from {5}. Config fallback: {6}%. Source height: {7}px. Crop profile: top {8:P0}, bottom {9:P0}."
            && args[5] is string scaleSource && scaleSource is "configured scaling fallback" or "live preview node scale" or "live preview node X scale" or "live preview node Y scale")
            localized[5] = T(scaleSource);
        if (key.StartsWith("Ordered by {0}; {1};", StringComparison.Ordinal))
            for (var index = 0; index < 2; index++)
                if (args[index] is string authored && authored is "node order" or "top-to-bottom Y order" or "left-to-right X order"
                    or "preferring live visible rails before hidden placeholder scaffolds" or "using the full ordered rail list because visible rails were insufficient")
                    localized[index] = T(authored);
        if (key == "This card is filtered by the {0} feet toggle." && args[0] is string sex && sex is "Male" or "Female")
            localized[0] = T(sex);
        if (key == "CharacterInspect is open for entity 0x{0:X8}. Inspect-side CharaView state is {1}, loaded={2}, copied={3}.")
            for (var index = 2; index < 4; index++)
                if (args[index] is string flag && flag is "Y" or "N") localized[index] = T(flag);
        return localized;
    }
    private static string LocalizeMemberLabel(string member)
    {
        const string self = " (You)";
        if (member.EndsWith(self, StringComparison.Ordinal)) return F("{0} (You)", member[..^self.Length]);
        if (Regex.IsMatch(member, @"\Aentity 0x[0-9A-F]{8}\z", RegexOptions.CultureInvariant))
            return F("entity {0}", member[7..]);
        return member;
    }
    internal static string F(string english,params object?[] args) => string.Format(Current.Culture,T(english),LocalizeArguments(english,args));
    internal static string F(FormattableString text) => string.Format(Current.Culture,T(text.Format),LocalizeArguments(text.Format,text.GetArguments()));
    internal string Format(string key, params object?[] args) => string.Format(Culture, Resources.GetString(key, false) ?? key, args);
    internal string Label(string key) => Resources.GetString(key, false) ?? key;
    internal static IDisposable Font(UiFontRole role) => Current.pushFont(role);
    internal Scope Enter() => new(this);
    internal readonly struct Scope : IDisposable
    {
        private readonly UiText? previous;
        internal Scope(UiText value) { previous=current; current=value; }
        public void Dispose() => current=previous;
    }
    internal static string Date(DateTimeOffset? date) => date?.ToLocalTime().ToString("g",Current.Culture) ?? T("Never");
    internal ushort[] GlyphRanges()
    {
        var chars=RequiredText.SelectMany(MaterialText.NativeGlyphText).Where(c=>!char.IsControl(c))
            .Concat(Enumerable.Range(0x20,0x024F-0x20+1).Select(i=>(char)i))
            .Concat(Enumerable.Range(0x0400,0x052F-0x0400+1).Select(i=>(char)i)).Concat("—").Distinct().Order().ToArray();
        var result=new List<ushort>();
        for(var index=0;index<chars.Length;index++)
        {
            var first=chars[index]; var last=first;
            while(index+1<chars.Length && chars[index+1]==last+1) last=chars[++index];
            result.Add(first); result.Add(last);
        }
        result.Add(0); return result.ToArray();
    }
    internal static IEnumerable<string> Values(ResourceSet set) => set.Cast<DictionaryEntry>().Select(e=>(string)e.Value!);
    public void Dispose() => manager.ReleaseAllResources();
}
