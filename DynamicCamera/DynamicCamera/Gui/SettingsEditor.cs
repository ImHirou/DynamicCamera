using System;
using System.Globalization;
using System.Numerics;
using DynamicCamera.Config;
using ImGuiNET;
using Vintagestory.API.Config;

namespace DynamicCamera.Gui;

/// <summary>
/// Draws the settings with Dear ImGui. Edits the live config object, so every change is visible immediately.
/// Requires the vsimgui mod: only construct it through <see cref="GuiIntegration"/>.
/// </summary>
internal sealed class SettingsEditor
{
    private const string L = DynamicCameraModSystem.Domain + ":";

    private static readonly Vector4 UnsavedColor = new(1f, 0.78f, 0.35f, 1f);
    private static readonly MovementContext[] Contexts = Enum.GetValues<MovementContext>();
    private static readonly string[] ContextNames = Array.ConvertAll(Contexts, context => context.ToString().ToLowerInvariant());

    private readonly DynamicCameraModSystem mod;
    private readonly DynamicCameraConfig defaults = new();

    public SettingsEditor(DynamicCameraModSystem mod)
    {
        this.mod = mod;
    }

    public bool HasUnsavedChanges { get; set; }

    public void Save()
    {
        if (mod.SaveConfig())
        {
            HasUnsavedChanges = false;
        }
    }

    public void SaveIfChanged()
    {
        if (HasUnsavedChanges)
        {
            Save();
        }
    }

    /// <summary> Discards unsaved changes by reading the config file again. </summary>
    public void Revert()
    {
        mod.ReloadConfig();
        HasUnsavedChanges = false;
    }

    public void ResetToDefaults()
    {
        mod.ResetConfigToDefaults();
        HasUnsavedChanges = true;
    }

    public void Draw(bool showFileButtons)
    {
        var cfg = mod.Config;
        var general = cfg.General;
        var generalDefaults = defaults.General;

        ImGui.PushID("dynamiccamera-settings");
        ImGui.PushItemWidth(ImGui.GetContentRegionAvail().X * 0.45f);

        if (showFileButtons)
        {
            DrawFileButtons();
            ImGui.Separator();
        }
        else if (HasUnsavedChanges)
        {
            ColoredText(Lang.Get(L + "gui-unsaved"), UnsavedColor);
        }

        WrappedDisabledText(Lang.Get(L + "gui-hint"));

        Checkbox("enabled", ref general.Enabled, generalDefaults.Enabled);
        Checkbox("enableinthirdperson", ref general.EnableInThirdPerson, generalDefaults.EnableInThirdPerson);
        Checkbox("aimfollowscamera", ref general.AimFollowsCamera, generalDefaults.AimFollowsCamera);
        Slider("contexttransitionsmoothing", ref general.ContextTransitionSmoothing, generalDefaults.ContextTransitionSmoothing, 0f, 0.95f, "%.2f");

        if (ImGui.CollapsingHeader(Lang.Get(L + "gui-section-turning") + "##turning"))
        {
            Slider("turningrollaccumulation", ref general.TurningRollAccumulation, generalDefaults.TurningRollAccumulation, 0f, 5f, "%.2f");
            Slider("turningrollintensity", ref general.TurningRollIntensity, generalDefaults.TurningRollIntensity, 0f, 10f, "%.2f");
            Slider("turningrollsmoothing", ref general.TurningRollSmoothing, generalDefaults.TurningRollSmoothing, 0f, 10f, "%.2f");
        }

        if (ImGui.CollapsingHeader(Lang.Get(L + "gui-section-sway") + "##sway"))
        {
            // The unit is appended to an ImGui printf format, so a stray '%' must not reach it.
            string secondsFormat = "%.2f " + Lang.Get(L + "gui-unit-seconds").Replace("%", "%%");

            Slider("cameraswayintensity", ref general.CameraSwayIntensity, generalDefaults.CameraSwayIntensity, 0f, 5f, "%.2f");
            Slider("cameraswayfrequency", ref general.CameraSwayFrequency, generalDefaults.CameraSwayFrequency, 0f, 2f, "%.3f");
            Slider("cameraswayfadeindelay", ref general.CameraSwayFadeInDelay, generalDefaults.CameraSwayFadeInDelay, 0f, 30f, secondsFormat);
            Slider("cameraswayfadeinlength", ref general.CameraSwayFadeInLength, generalDefaults.CameraSwayFadeInLength, 0f, 30f, secondsFormat);
            Slider("cameraswayfadeoutlength", ref general.CameraSwayFadeOutLength, generalDefaults.CameraSwayFadeOutLength, 0f, 10f, secondsFormat);
        }

        if (ImGui.CollapsingHeader(Lang.Get(L + "gui-section-shakes") + "##shakes", ImGuiTreeNodeFlags.DefaultOpen))
        {
            Slider("screenshakesmaxintensity", ref general.ScreenShakesMaxIntensity, generalDefaults.ScreenShakesMaxIntensity, 0f, 10f, "%.2f");
            Slider("screenshakesmaxfrequency", ref general.ScreenShakesMaxFrequency, generalDefaults.ScreenShakesMaxFrequency, 0f, 20f, "%.2f");
            Slider("explosiontrauma", ref general.ExplosionTrauma, generalDefaults.ExplosionTrauma, 0f, 1f, "%.3f");
            Slider("thundertrauma", ref general.ThunderTrauma, generalDefaults.ThunderTrauma, 0f, 1f, "%.3f");
            Slider("handswingtrauma", ref general.HandSwingTrauma, generalDefaults.HandSwingTrauma, 0f, 1f, "%.3f");
            DrawShakePreviewButtons(general);
        }

        if (ImGui.CollapsingHeader(Lang.Get(L + "gui-section-contexts") + "##contexts", ImGuiTreeNodeFlags.DefaultOpen))
        {
            WrappedDisabledText(Lang.Get(L + "gui-contexts-hint"));

            if (ImGui.BeginTabBar("contexts"))
            {
                for (int i = 0; i < Contexts.Length; i++)
                {
                    string name = ContextNames[i];

                    if (ImGui.BeginTabItem(Lang.Get(L + "gui-context-" + name) + "##" + name))
                    {
                        ImGui.PushID(name);
                        DrawContext(cfg.Get(Contexts[i]), defaults.Get(Contexts[i]));
                        ImGui.PopID();
                        ImGui.EndTabItem();
                    }
                }

                ImGui.EndTabBar();
            }
        }

        ImGui.PopItemWidth();
        ImGui.PopID();
    }

    private void DrawFileButtons()
    {
        if (ImGui.Button(Lang.Get(L + "gui-save") + "##save"))
        {
            Save();
        }

        ImGui.SameLine();

        if (ImGui.Button(Lang.Get(L + "gui-revert") + "##revert"))
        {
            Revert();
        }

        ItemTooltip(Lang.Get(L + "gui-revert-desc"));
        ImGui.SameLine();

        if (ImGui.Button(Lang.Get(L + "gui-defaults") + "##defaults"))
        {
            ResetToDefaults();
        }

        ItemTooltip(Lang.Get(L + "gui-defaults-desc"));

        if (HasUnsavedChanges)
        {
            ImGui.SameLine();
            ColoredText(Lang.Get(L + "gui-unsaved"), UnsavedColor);
        }
    }

    private void DrawShakePreviewButtons(GeneralSettings general)
    {
        ImGui.TextUnformatted(Lang.Get(L + "gui-preview"));
        ImGui.SameLine();

        // Same parameters as the real sources, at point-blank range.
        if (ImGui.SmallButton(Lang.Get(L + "gui-preview-explosion") + "##preview-explosion"))
        {
            mod.PreviewShake(general.ExplosionTrauma, 1f, 2f);
        }

        ImGui.SameLine();

        if (ImGui.SmallButton(Lang.Get(L + "gui-preview-thunder") + "##preview-thunder"))
        {
            mod.PreviewShake(general.ThunderTrauma, 0.5f, 7f);
        }

        ImGui.SameLine();

        if (ImGui.SmallButton(Lang.Get(L + "gui-preview-handswing") + "##preview-handswing"))
        {
            mod.OnHandSwing();
        }
    }

    private void DrawContext(ContextSettings settings, ContextSettings contextDefaults)
    {
        Slider("strafingrollfactor", ref settings.StrafingRollFactor, contextDefaults.StrafingRollFactor, -50f, 50f, "%.2f");
        Slider("forwardvelocitypitchfactor", ref settings.ForwardVelocityPitchFactor, contextDefaults.ForwardVelocityPitchFactor, -50f, 50f, "%.2f");
        Slider("verticalvelocitypitchfactor", ref settings.VerticalVelocityPitchFactor, contextDefaults.VerticalVelocityPitchFactor, -20f, 20f, "%.2f");
        // The base smoothing values are tiny, so useful factors span several orders of magnitude.
        Slider("horizontalvelocitysmoothingfactor", ref settings.HorizontalVelocitySmoothingFactor, contextDefaults.HorizontalVelocitySmoothingFactor, 0.01f, 100f, "%.3g", ImGuiSliderFlags.Logarithmic);
        Slider("verticalvelocitysmoothingfactor", ref settings.VerticalVelocitySmoothingFactor, contextDefaults.VerticalVelocitySmoothingFactor, 0.01f, 20000f, "%.3g", ImGuiSliderFlags.Logarithmic);
        Slider("camerasmoothing", ref settings.CameraSmoothing, contextDefaults.CameraSmoothing, 0f, 5f, "%.2f");

        if (ImGui.SmallButton(Lang.Get(L + "gui-reset-profile") + "##reset-profile"))
        {
            settings.CopyFrom(contextDefaults);
            HasUnsavedChanges = true;
        }
    }

    private void Checkbox(string key, ref bool value, bool defaultValue)
    {
        bool changed = ImGui.Checkbox(Lang.Get(L + "setting-" + key) + "##" + key, ref value);

        if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
        {
            value = defaultValue;
            changed = true;
        }

        SettingTooltip(key, Lang.Get(defaultValue ? L + "gui-on" : L + "gui-off"));
        HasUnsavedChanges |= changed;
    }

    private void Slider(string key, ref float value, float defaultValue, float min, float max, string format,
        ImGuiSliderFlags flags = ImGuiSliderFlags.None)
    {
        // No AlwaysClamp: Ctrl+Click allows typing values outside of the slider range, like the original's text fields.
        bool changed = ImGui.SliderFloat(Lang.Get(L + "setting-" + key) + "##" + key, ref value, min, max, format, flags);

        if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
        {
            value = defaultValue;
            changed = true;
        }

        if (changed && !float.IsFinite(value))
        {
            value = defaultValue;
        }

        SettingTooltip(key, defaultValue.ToString("0.#####", CultureInfo.InvariantCulture));
        HasUnsavedChanges |= changed;
    }

    private static void SettingTooltip(string key, string defaultText)
    {
        if (!ImGui.BeginItemTooltip())
        {
            return;
        }

        ImGui.PushTextWrapPos(ImGui.GetFontSize() * 28f);
        ImGui.TextUnformatted(Lang.Get(L + "setting-" + key + "-desc"));
        ImGui.Spacing();
        DisabledText(Lang.Get(L + "gui-default", defaultText));
        DisabledText(Lang.Get(L + "gui-rightclick-reset"));
        ImGui.PopTextWrapPos();
        ImGui.EndTooltip();
    }

    private static void ItemTooltip(string text)
    {
        if (!ImGui.BeginItemTooltip())
        {
            return;
        }

        ImGui.PushTextWrapPos(ImGui.GetFontSize() * 28f);
        ImGui.TextUnformatted(text);
        ImGui.PopTextWrapPos();
        ImGui.EndTooltip();
    }

    // ImGui's Text* and SetTooltip functions take printf formats; translated strings must never be interpreted as one.
    private static void ColoredText(string text, Vector4 color)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, color);
        ImGui.TextUnformatted(text);
        ImGui.PopStyleColor();
    }

    private static void DisabledText(string text)
    {
        ColoredText(text, ImGui.GetStyle().Colors[(int)ImGuiCol.TextDisabled]);
    }

    private static void WrappedDisabledText(string text)
    {
        ImGui.PushTextWrapPos(0f);
        DisabledText(text);
        ImGui.PopTextWrapPos();
    }
}
