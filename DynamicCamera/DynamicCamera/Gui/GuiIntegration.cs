using System;
using System.Runtime.CompilerServices;
using Vintagestory.API.Client;

namespace DynamicCamera.Gui;

/// <summary> In-game settings screen. Implementations may depend on optional mods. </summary>
internal interface ISettingsUi : IDisposable
{
    void Open();

    /// <summary> The config object was replaced from outside of the UI (reload, toggle command). </summary>
    void OnConfigReplaced();
}

/// <summary>
/// Entry point for the optional GUI mods. This class must not reference any type from them:
/// Vintage Story scans every type of the mod assembly, and the soft dependencies may be missing.
/// </summary>
internal static class GuiIntegration
{
    public const string ImGuiModId = "vsimgui";
    public const string ConfigLibModId = "configlib";

    public static ISettingsUi? TryCreate(DynamicCameraModSystem mod, ICoreClientAPI api)
    {
        if (!api.ModLoader.IsModEnabled(ImGuiModId))
        {
            mod.Mod.Logger.Notification(
                "Dear ImGui (vsimgui) is not installed, in-game settings are unavailable. Edit ModConfig/dynamiccamera.json and run '.dcam reload' instead.");
            return null;
        }

        try
        {
            return CreateImGuiUi(mod, api, api.ModLoader.IsModEnabled(ConfigLibModId));
        }
        catch (Exception e)
        {
            mod.Mod.Logger.Warning("Failed to set up the in-game settings: {0}", e);
            return null;
        }
    }

    // Kept out of line so that the JIT only touches ImGui types when the mod is actually present.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ISettingsUi CreateImGuiUi(DynamicCameraModSystem mod, ICoreClientAPI api, bool withConfigLib)
    {
        return new ImGuiSettingsUi(mod, api, withConfigLib);
    }
}
