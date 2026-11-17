using System;
using DynamicCamera.Utilities;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.Client.NoObf;
using GameCamera = Vintagestory.Client.NoObf.Camera;

namespace DynamicCamera.Patches;

/// <summary>
/// Applies the camera effects around <see cref="GameCamera.Update"/>, which builds the view matrices every frame.
/// Pitch and yaw offsets are added to the camera angles only for the duration of the update, so they orbit the
/// player in third person and never touch the player's real look direction. Roll is applied afterwards in view space.
/// </summary>
internal static class CameraPatches
{
    // Same limits the game uses for the player's pitch; going past them flips the look-at matrix.
    private const double MinPitch = 1.5857964f;
    private const double MaxPitch = 4.697389f;

    private static readonly double[] RollMatrix = Mat4d.Create();
    private static bool errorLogged;

    /// <summary> Game pitch (radians) as rendered with the given pitch offset (degrees, positive looks down). </summary>
    internal static double ApplyPitchOffset(double pitch, double offsetDegrees)
    {
        return Math.Clamp(pitch + offsetDegrees * MathUtils.DegToRad, MinPitch, MaxPitch);
    }

    /// <summary> Game yaw (radians) as rendered with the given yaw offset (degrees, positive turns right). </summary>
    internal static double ApplyYawOffset(double yaw, double offsetDegrees)
    {
        // The original's yaw grows when turning right, the game's yaw grows when turning left.
        return yaw - offsetDegrees * MathUtils.DegToRad;
    }

    public struct UpdateState
    {
        public bool Applied;
        public double Yaw;
        public double Pitch;
    }

    public static bool Apply(Harmony harmony, ILogger logger)
    {
        errorLogged = false;

        try
        {
            var original = AccessTools.Method(typeof(GameCamera), nameof(GameCamera.Update));

            if (original == null)
            {
                logger.Error("Could not find Camera.Update, camera effects are disabled.");
                return false;
            }

            harmony.Patch(original,
                prefix: new HarmonyMethod(typeof(CameraPatches), nameof(UpdatePrefix)),
                postfix: new HarmonyMethod(typeof(CameraPatches), nameof(UpdatePostfix)));
            return true;
        }
        catch (Exception e)
        {
            logger.Error("Failed to patch Camera.Update, camera effects are disabled: {0}", e);
            return false;
        }
    }

    private static void UpdatePrefix(GameCamera __instance, float __0, out UpdateState __state)
    {
        __state = default;

        if (__instance is not PlayerCamera camera || DynamicCameraModSystem.ClientInstance is not { } mod)
        {
            return;
        }

        try
        {
            if (!mod.UpdateCameraEffects(camera, __0))
            {
                return;
            }

            __state.Applied = true;
            __state.Yaw = camera.Yaw;
            __state.Pitch = camera.Pitch;

            var effects = mod.Effects;
            camera.Pitch = ApplyPitchOffset(camera.Pitch, effects.OffsetPitch);
            camera.Yaw = ApplyYawOffset(camera.Yaw, effects.OffsetYaw);
        }
        catch (Exception e)
        {
            LogErrorOnce(mod, e);
        }
    }

    private static void UpdatePostfix(GameCamera __instance, UpdateState __state)
    {
        if (!__state.Applied || __instance is not PlayerCamera camera || DynamicCameraModSystem.ClientInstance is not { } mod)
        {
            return;
        }

        camera.Yaw = __state.Yaw;
        camera.Pitch = __state.Pitch;

        try
        {
            double roll = mod.Effects.OffsetRoll;

            if (roll == 0.0)
            {
                return;
            }

            // Pre-multiplying rotates the view around its own forward axis. Both matrices are rolled,
            // so frustum culling (CameraMatrix) keeps matching what is rendered (CameraMatrixOrigin).
            Mat4d.Identity(RollMatrix);
            Mat4d.RotateZ(RollMatrix, RollMatrix, roll * MathUtils.DegToRad);
            Mat4d.Multiply(camera.CameraMatrix, RollMatrix, camera.CameraMatrix);
            Mat4d.Multiply(camera.CameraMatrixOrigin, RollMatrix, camera.CameraMatrixOrigin);

            double[] origin = camera.CameraMatrixOrigin;
            float[] originf = camera.CameraMatrixOriginf;

            for (int i = 0; i < 16; i++)
            {
                originf[i] = (float)origin[i];
            }
        }
        catch (Exception e)
        {
            LogErrorOnce(mod, e);
        }
    }

    private static void LogErrorOnce(DynamicCameraModSystem mod, Exception e)
    {
        if (errorLogged)
        {
            return;
        }

        errorLogged = true;
        mod.Mod.Logger.Error("Camera effects failed, further errors will not be logged: {0}", e);
    }
}
