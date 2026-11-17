using System;
using System.Reflection;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.Client.NoObf;
using Vintagestory.Common;
using Vintagestory.GameContent;

namespace DynamicCamera.Patches;

/// <summary>
/// Client-side hooks for the events that cause screen shakes. Every hook is optional:
/// if the game changes and a method disappears, only that shake source stops working.
/// </summary>
internal static class ShakeSourcePatches
{
    public static void Apply(Harmony harmony, ILogger logger)
    {
        // Explosions are only visible to clients as a sound packet ("effect/smallexplosion" etc.),
        // which is what ServerMain.CreateExplosion sends to everyone in hearing range.
        TryPatch(harmony, logger, "explosions",
            AccessTools.Method(typeof(GeneralPacketHandler), "HandleSound"), nameof(HandleSoundPostfix));
        // Lightning bolts (not the ambient sky flashes).
        TryPatch(harmony, logger, "lightning",
            AccessTools.Method(typeof(LightningFlash), nameof(LightningFlash.ClientInit)), nameof(LightningClientInitPostfix));
        // Attacking, breaking blocks and swinging at air.
        TryPatch(harmony, logger, "hand swings",
            AccessTools.Method(typeof(SystemMouseInWorldInteractions), "StartAttackAnimation"), nameof(AttackSwingPostfix));
        // Placing blocks.
        TryPatch(harmony, logger, "block placement swings",
            AccessTools.Method(typeof(SystemMouseInWorldInteractions), "OnBlockBuild"), nameof(BlockBuildPostfix));
    }

    private static void TryPatch(Harmony harmony, ILogger logger, string feature, MethodInfo? original, string postfix)
    {
        if (original == null)
        {
            logger.Warning("Could not find the game method for {0} screen shakes, they are disabled.", feature);
            return;
        }

        try
        {
            harmony.Patch(original, postfix: new HarmonyMethod(typeof(ShakeSourcePatches), postfix));
        }
        catch (Exception e)
        {
            logger.Warning("Failed to hook {0} screen shakes, they are disabled: {1}", feature, e);
        }
    }

    private static void HandleSoundPostfix(Packet_Server __0)
    {
        var sound = __0?.Sound;

        if (sound?.Name == null || DynamicCameraModSystem.ClientInstance is not { } mod)
        {
            return;
        }

        if (sound.Name.Contains("explosion", StringComparison.OrdinalIgnoreCase))
        {
            mod.OnExplosion(
                CollectibleNet.DeserializeFloat(sound.X),
                CollectibleNet.DeserializeFloat(sound.Y),
                CollectibleNet.DeserializeFloat(sound.Z),
                CollectibleNet.DeserializeFloat(sound.Range));
        }
    }

    private static void LightningClientInitPostfix(LightningFlash __instance)
    {
        if (DynamicCameraModSystem.ClientInstance is not { } mod || __instance.origin == null)
        {
            return;
        }

        // Same impact point the game uses for the thunder sound.
        var points = __instance.points;
        var impact = points is { Count: > 0 } ? __instance.origin + points[^1] : __instance.origin;
        mod.OnLightningStrike(impact.X, impact.Y, impact.Z);
    }

    private static void AttackSwingPostfix()
    {
        DynamicCameraModSystem.ClientInstance?.OnHandSwing();
    }

    private static void BlockBuildPostfix(bool __result)
    {
        if (__result)
        {
            DynamicCameraModSystem.ClientInstance?.OnHandSwing();
        }
    }
}
