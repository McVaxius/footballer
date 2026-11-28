using AethertekUI;
using AethertekUI.Dalamud;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.ManagedFontAtlas;

namespace footballer.Windows;

internal sealed class FootballerFonts : IDisposable
{
    private readonly IFontAtlas atlas;
    private readonly Dictionary<UiFontRole, IFontHandle> roles = [];
    private readonly List<IFontHandle> handles = [];
    private int generation;
    internal int Generation => System.Threading.Volatile.Read(ref generation);
    internal FootballerFonts(IFontAtlas atlas, ushort[] ranges, string language, string assetRoot,
        bool mainWindow, bool compact)
    {
        this.atlas = atlas;
        var selectedRoles = mainWindow
            ? new[] { UiFontRole.Body, UiFontRole.PluginName, UiFontRole.Action, compact ? UiFontRole.CompactTitle : UiFontRole.Title }
            : [UiFontRole.Body];
        var glyphCount = 0;
        for (var index = 0; index + 1 < ranges.Length; index += 2)
            glyphCount += ranges[index + 1] - ranges[index] + 1;
        var pageSize = glyphCount > 1000 && mainWindow ? 2048 : 1024;
        var faces = new Dictionary<(MaterialFontWeight Weight, float Height), IFontHandle>();
        atlas.BuildStepChange += MaterialFontAtlas.ReleaseRasterizedInputs;
        try
        {
            foreach (var role in selectedRoles)
            {
                var weight = role switch
                {
                    UiFontRole.Body => MaterialFontWeight.Regular,
                    UiFontRole.Title or UiFontRole.CompactTitle => MaterialFontWeight.Bold,
                    _ => MaterialFontWeight.Semibold,
                };
                var height = FootballerPresentation.AtlasHeight(role);
                if (!faces.TryGetValue((weight, height), out var handle))
                {
                    handle = MaterialFontAtlas.CreateFont(atlas, weight, height, ranges, language, assetRoot,
                        pageSize, includeHostLanguage: false);
                    faces.Add((weight, height), handle);
                    handles.Add(handle);
                    handle.ImFontChanged += FontChanged;
                }
                roles.Add(role, handle);
            }
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    internal IEnumerable<float> Sizes => roles.Keys.Select(role => FootballerPresentation.FontSizes[(int)role]).Distinct();
    private void FontChanged(IFontHandle handle,ILockedImFont font) => System.Threading.Interlocked.Increment(ref generation);
    internal bool Ready => handles.All(h=>h.Available && h.LoadException is null);
    internal Exception? LoadException => handles.FirstOrDefault(h=>h.LoadException is not null)?.LoadException;
    internal unsafe void CheckGlyphs(IEnumerable<string> strings)
    {
        foreach (var (role, handle) in roles)
        {
            using var font = handle.Lock();
            foreach(var text in strings)
                foreach(var character in MaterialText.NativeGlyphText(text).Where(c=>!char.IsControl(c)))
                    if(ImGui.FindGlyphNoFallback(font.ImFont,character).Handle==null)
                        throw new InvalidOperationException("Required UI glyph missing: U+"+((int)character).ToString("X4")+" in "+role);
        }
    }
    internal IDisposable Push(UiFontRole role)
    {
        var handle = roles[role];
        if(!handle.Available || handle.LoadException is not null) throw new InvalidOperationException("Footballer fonts are not ready.",handle.LoadException);
        return handle.Push();
    }
    public void Dispose()
    {
        atlas.BuildStepChange -= MaterialFontAtlas.ReleaseRasterizedInputs;
        foreach (var handle in handles)
        {
            handle.ImFontChanged -= FontChanged;
            handle.Dispose();
        }
        atlas.Dispose();
    }
}
