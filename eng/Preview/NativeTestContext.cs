using Dalamud.Bindings.ImGui;

namespace AethertekUI.Tests;

// Minimal offline host for the linked AethertekUI software renderer; no plugin or game services.
internal sealed unsafe class NativeTestContext : IDisposable
{
    private readonly ImGuiContextPtr context;
    private byte* pixels;
    private int width, height;

    public NativeTestContext()
    {
        context = ImGui.CreateContext();
        var io = ImGui.GetIO();
        io.IniFilename = null;
        io.LogFilename = null;
        io.DisplaySize = new(730, 380);
        io.DeltaTime = 1f / 60;
        io.Fonts.AddFontDefault();
        RefreshAtlas();
        io.Fonts.SetTexID(0, new ImTextureID((nint)1));
    }

    public void RefreshAtlas()
    {
        byte* currentPixels = null;
        int currentWidth = 0, currentHeight = 0;
        ImGui.GetIO().Fonts.GetTexDataAsRGBA32(0, &currentPixels, &currentWidth, &currentHeight);
        pixels = currentPixels;
        width = currentWidth;
        height = currentHeight;
    }

    public SoftwarePreview.Texture Texture(ImTextureID id)
        => id.Handle == 1 ? new((nint)pixels, width, height)
            : throw new InvalidOperationException("Unknown preview texture.");

    public void Frame(Action draw)
    {
        ImGui.NewFrame();
        draw();
        ImGui.Render();
    }

    public void Dispose() => ImGui.DestroyContext(context);
}
