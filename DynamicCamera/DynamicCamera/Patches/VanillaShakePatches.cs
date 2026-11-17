using System;
using DynamicCamera.Effects;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.Client.NoObf;

namespace DynamicCamera.Patches;

/// <summary>
/// Routes the game's own camera shake into the trauma-based screen shakes. The vanilla shake jitters the camera position
/// (<c>PlayerCamera.CameraShakeStrength</c>); while the camera effects are active it can be suppressed entirely.
/// It is used by damage, weapon hits, spear throws, translocators, temporal storms, low health, the firestarter
/// and by server effects (<c>ScreenshakePacket</c>).
/// </summary>
internal static class VanillaShakePatches
{
    public static void Apply(Harmony harmony, ILogger logger)
    {
        TryPatch(harmony, logger, nameof(ClientMain.AddCameraShake), nameof(AddCameraShakePrefix));
        TryPatch(harmony, logger, nameof(ClientMain.SetCameraShake), nameof(SetCameraShakePrefix));
        TryPatch(harmony, logger, nameof(ClientMain.ReduceCameraShake), nameof(ReduceCameraShakePrefix));
    }

    private static void TryPatch(Harmony harmony, ILogger logger, string method, string prefix)
    {
        var original = AccessTools.Method(typeof(ClientMain), method, [typeof(float)]);

        if (original == null)
        {
            logger.Warning("Could not find ClientMain.{0}, the game's camera shakes stay unchanged.", method);
            return;
        }

        try
        {
            harmony.Patch(original, prefix: new HarmonyMethod(typeof(VanillaShakePatches), prefix));
        }
        catch (Exception e)
        {
            logger.Warning("Failed to patch ClientMain.{0}, the game's camera shakes stay unchanged: {1}", method, e);
        }
    }

    /// <summary>
    /// The shakes that take over the vanilla shake, or null if it should play unchanged: when the conversion is off,
    /// or when the camera effects are off, since then nothing would be shown.
    /// </summary>
    private static ScreenShakes? GetTarget(out bool runVanilla)
    {
        runVanilla = true;

        if (DynamicCameraModSystem.ClientInstance is not { } mod)
        {
            return null;
        }

        var general = mod.Config.General;

        if (!general.ConvertVanillaShakes || !mod.Effects.IsActive)
        {
            return null;
        }

        runVanilla = !general.SuppressVanillaShakeJitter;
        return mod.Effects.Shakes;
    }

    // The vanilla methods multiply by ClientSettings.CameraShakeStrength. The raw value is forwarded instead,
    // since the screen shakes apply that setting themselves (GeneralSettings.RespectGameShakeSetting).
    private static bool AddCameraShakePrefix(float __0)
    {
        GetTarget(out bool runVanilla)?.AddExternal(__0);
        return runVanilla;
    }

    private static bool SetCameraShakePrefix(float __0)
    {
        GetTarget(out bool runVanilla)?.SetExternal(__0);
        return runVanilla;
    }

    private static bool ReduceCameraShakePrefix(float __0)
    {
        GetTarget(out bool runVanilla)?.ReduceExternal(__0);
        return runVanilla;
    }
}
