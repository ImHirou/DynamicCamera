using System;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.Client.NoObf;
using Vintagestory.Common;

namespace DynamicCamera.Patches;

/// <summary>
/// Makes the local player's block and entity targeting follow the rendered view, so that the crosshair in the
/// center of the screen marks what gets targeted even while the camera lags behind a fast turn or tilts.
/// Only the client-side selection ray is rotated: the server checks interaction range, not the look direction.
/// </summary>
internal static class AimPatches
{
    private static bool errorLogged;

    public static void Apply(Harmony harmony, ILogger logger)
    {
        errorLogged = false;

        var original = AccessTools.Method(typeof(GameMain), nameof(GameMain.RayTraceForSelection),
        [
            typeof(IPlayer), typeof(BlockSelection).MakeByRefType(), typeof(EntitySelection).MakeByRefType(),
            typeof(BlockFilter), typeof(EntityFilter),
        ]);

        if (original == null)
        {
            logger.Warning("Could not find GameMain.RayTraceForSelection, targeting will not follow the camera.");
            return;
        }

        try
        {
            harmony.Patch(original, prefix: new HarmonyMethod(typeof(AimPatches), nameof(RayTraceForSelectionPrefix)));
        }
        catch (Exception e)
        {
            logger.Warning("Failed to patch GameMain.RayTraceForSelection, targeting will not follow the camera: {0}", e);
        }
    }

    // Vanilla: RayTraceForSelection(player.Entity.Pos.XYZ + LocalEyePos, Pos.Pitch, Pos.Yaw, PickingRange, ...).
    private static bool RayTraceForSelectionPrefix(GameMain __instance, IPlayer __0, ref BlockSelection __1, ref EntitySelection __2,
        BlockFilter __3, EntityFilter __4)
    {
        // The single player server runs in the same process and uses this method for every player: leave it alone.
        if (__instance is not ClientMain game
            || !ReferenceEquals(__0, game.player)
            || DynamicCameraModSystem.ClientInstance is not { } mod
            || !mod.Config.General.AimFollowsCamera
            // In the overhead camera the character faces its walking direction, not the camera's.
            || game.player.CameraMode == EnumCameraMode.Overhead)
        {
            return true;
        }

        var effects = mod.Effects;
        var entity = __0.Entity;

        if (effects == null || entity == null || (effects.OffsetPitch == 0.0 && effects.OffsetYaw == 0.0))
        {
            return true;
        }

        try
        {
            var fromPos = entity.Pos.XYZ.Add(entity.LocalEyePos);
            float pitch = (float)CameraPatches.ApplyPitchOffset(entity.Pos.Pitch, effects.OffsetPitch);
            float yaw = (float)CameraPatches.ApplyYawOffset(entity.Pos.Yaw, effects.OffsetYaw);

            game.RayTraceForSelection(fromPos, pitch, yaw, __0.WorldData.PickingRange, ref __1, ref __2, __3, __4);
            return false;
        }
        catch (Exception e)
        {
            if (!errorLogged)
            {
                errorLogged = true;
                mod.Mod.Logger.Error("Camera-following targeting failed, using the vanilla look direction: {0}", e);
            }

            return true;
        }
    }
}
