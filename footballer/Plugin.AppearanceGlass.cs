using System;
using System.Linq;
using AethertekUI;
using AethertekUI.Dalamud;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Utility;
using footballer.Windows;

namespace footballer;

public sealed partial class Plugin
{
    private MaterialGlassRenderer? backgroundGlass;
    private (MaterialColorMode? Style, bool Blur, bool Image, string? Path)? activeAppearance;
    internal bool BackgroundRequested => Configuration.UiStylingEnabled && Configuration.UiColors is { } choices && (choices.HasBackgroundBlur(choices.Mode) || choices.Mode == MaterialColorMode.Glassmorphism && choices.GlassBackground == MaterialGlassBackground.Image);
    private readonly FileDialogManager glassImagePicker = new();

    private void DrawUi()
    {
        if (!mainWindow.IsOpen && !configWindow.IsOpen)
        {
            if (uiFonts is not null) ReleaseWindowResources();
            return;
        }

        ApplyAppearance();
        using (MaterialTheme.Push(uiTheme, ImGuiHelpers.GlobalScale, Configuration.UiStylingEnabled ? MaterialStyleMode.ColorsOnly : MaterialStyleMode.Native))
            glassImagePicker.Draw();

        var choices = Configuration.UiColors ?? new();
        var style = Configuration.UiStylingEnabled ? choices.Mode : (MaterialColorMode?)null;
        var blur = style is { } selected && choices.HasBackgroundBlur(selected);
        var image = style == MaterialColorMode.Glassmorphism && choices.GlassBackground == MaterialGlassBackground.Image;
        var appearance = (style, blur, image, image ? choices.GlassImagePath : null);
        if (appearance != activeAppearance)
        {
            activeAppearance = appearance;
            if (!blur && !image) DisposeBackgroundGlass();
            else if (!blur) backgroundGlass?.ReleaseResources();
        }
        // The font status window uses ordinary paint. It never needs backdrop resources.
        if ((blur || image) && uiFonts.Ready)
        {
            backgroundGlass ??= new(PluginInterface, Log);
            backgroundGlass.BeginFrame(blur, image);
            backgroundGlass.ConfigureImage(TextureProvider, image, choices.GlassImagePath);
        }

        var completed = false;
        try
        {
            using var background = MaterialSkin.PushBackgroundRenderer(backgroundGlass, blur, choices.BackgroundBlurPercent / 100f);
            DrawUiContent();
            completed = true;
        }
        finally { backgroundGlass?.EndFrame(completed); }
    }

    private void ReleaseGlassResources() => backgroundGlass?.ReleaseResources();

    private void DisposeBackgroundGlass()
    {
        if (backgroundGlass is null) return;
        backgroundGlass.Dispose();
        backgroundGlass = null;
    }

    private void ReleaseUiSounds()
    {
        uiSoundPlayer?.Dispose();
        uiSoundPlayer = null;
    }

    private void ReleaseWindowResources()
    {
        glassImagePicker.Reset();
        DisposeBackgroundGlass();
        ReleaseUiSounds();
        uiFonts?.Dispose();
        uiFonts = null!;
        shapedText?.Dispose();
        shapedText = null;
        uiText?.Dispose();
        uiText = null!;
        uiTheme = null!;
        languageOptions = null!;
        appliedLanguage = string.Empty;
        activeAppearance = null;
    }
    private void ToggleUiStyling() { Configuration.UiStylingEnabled = !Configuration.UiStylingEnabled; Configuration.Save(); }
    private void ToggleWindowLock() { Configuration.UiWindowsLocked = !Configuration.UiWindowsLocked; Configuration.Save(); }

    internal void PrepareWindowBackground(MaterialWindowOpacity opacity)
    {
        if (backgroundGlass != null) backgroundGlass.Opacity = Configuration.UiTransparencyEnabled
            ? ImGui.IsWindowAppearing() ? Math.Clamp(Configuration.UiWindowOpacityPercent, 10, 100) / 100f
                : opacity.CurrentOpacity
            : 1;
    }

    private void DrawUiWindowControls()
    {
        var styled = Configuration.UiStylingEnabled;
        if (UiGui.Checkbox("UI styling", ref styled)) { Configuration.UiStylingEnabled = styled; Configuration.Save(); }
        var locked = Configuration.UiWindowsLocked;
        if (UiGui.Checkbox("Lock window movement and resizing", ref locked)) { Configuration.UiWindowsLocked = locked; Configuration.Save(); }
    }

    private void BrowseBackgroundImage() => glassImagePicker.OpenFileDialog(
        UiText.T("Choose background image") + "###FootballerBackgroundImagePicker",
        UiText.T("Images") + "{.png,.jpg,.jpeg,.bmp,.gif,.webp,.tif,.tiff,.dds,.tga},.*",
        (accepted, path) =>
        {
            if (!accepted) return;
            colorDraft = colorDraft with { GlassImagePath = path };
            Configuration.UiColors = colorDraft;
            Configuration.Save();
        });

    private void DrawBackgroundSettings()
    {
        var choices = Configuration.UiColors ?? new();
        var blur = choices.HasBackgroundBlur(choices.Mode);
        var changed = UiGui.Checkbox("Background blur", ref blur);
        if (changed) choices = choices.WithBackgroundBlur(choices.Mode, blur);
        var frost = choices.BackgroundBlurPercent;
        MaterialText.Text(UiText.T("Blur (%)"));
        ImGui.SetNextItemWidth(MaterialLayout.FitNextItemWidth(160 * MaterialTheme.Metrics.Scale, 80 * MaterialTheme.Metrics.Scale));
        using (MaterialSkin.NativeFieldStyle())
            if (ImGui.InputInt("##BackgroundFrost", ref frost)) { choices = choices with { BackgroundBlurPercent = frost }; changed = true; }
        if (changed) { Configuration.UiColors = colorDraft = choices; Configuration.Save(); }
        DrawGlassStatusAndScreenshots();
    }

    private void DrawGlassStatusAndScreenshots()
    {
        if (colorDraft.Mode == MaterialColorMode.Glassmorphism && colorDraft.GlassBackground == MaterialGlassBackground.Image)
        {
            MaterialText.TextWrapped(UiText.T(backgroundGlass?.ImageProblem ?? (backgroundGlass?.ImageAvailable == true
                ? "Image backgrounds remain visible to screenshots." : string.IsNullOrWhiteSpace(colorDraft.GlassImagePath) ? "Choose an image file." : "Loading image...")));
            return;
        }
        var paused = backgroundGlass?.ScreenshotsPaused == true;
        ImGui.BeginDisabled(!paused && backgroundGlass?.CanPauseForScreenshots != true);
        if (UiGui.Checkbox("Pause backdrop for screenshots", ref paused)) backgroundGlass?.TrySetScreenshotsPaused(paused);
        ImGui.EndDisabled();
    }
}
