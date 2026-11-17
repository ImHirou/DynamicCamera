using System;
using System.Reflection;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.Client.NoObf;
using Vintagestory.Common;
using Vintagestory.GameContent;

namespace DynamicCamera.Patches;

/// <summary>
/// Client-side hooks for the events that cause screen shakes and camera kicks. Every hook is optional:
/// if the game changes and a method disappears, only that source stops working.
/// </summary>
internal static class ShakeSourcePatches
{
    public static void Apply(Harmony harmony, ILogger logger)
    {
        // Every sound the client plays goes through this method, the ones the server sends included
        // (explosions are only visible to clients as the sound packet that ServerMain.CreateExplosion sends).
        var playSound = AccessTools.Method(typeof(ClientMain), "PlaySoundAtInternal");

        if (playSound != null)
        {
            TryPatch(harmony, logger, "sound", playSound, nameof(PlaySoundPostfix));
        }
        else
        {
            logger.Notification("Could not find ClientMain.PlaySoundAtInternal, only sounds sent by the server cause screen shakes.");
            TryPatch(harmony, logger, "sound",
                AccessTools.Method(typeof(GeneralPacketHandler), "HandleSound"), nameof(HandleSoundPostfix));
        }

        // Lightning bolts (not the ambient sky flashes).
        TryPatch(harmony, logger, "lightning",
            AccessTools.Method(typeof(LightningFlash), nameof(LightningFlash.ClientInit)), nameof(LightningClientInitPostfix));
        // Attacking, breaking blocks and swinging at air.
        TryPatch(harmony, logger, "hand swings",
            AccessTools.Method(typeof(SystemMouseInWorldInteractions), "StartAttackAnimation"), nameof(AttackSwingPostfix));
        // Placing blocks.
        TryPatch(harmony, logger, "block placement swings",
            AccessTools.Method(typeof(SystemMouseInWorldInteractions), "OnBlockBuild"), nameof(BlockBuildPostfix));
        // Taking damage. On the client the game calls this when the "onHurt" entity attribute changes.
        TryPatch(harmony, logger, "damage",
            AccessTools.Method(typeof(EntityPlayer), nameof(EntityPlayer.OnHurt)), nameof(PlayerHurtPostfix));
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

    // PlaySoundAtInternal(AssetLocation location, double x, double y, double z, float volume, float pitch, float range, EnumSoundType soundType).
    // A postfix runs even when the game skips the sound (muted, too many sounds), so the shakes don't depend on the sound settings.
    private static void PlaySoundPostfix(AssetLocation? __0, double __1, double __2, double __3, float __6)
    {
        if (__0?.Path != null)
        {
            DynamicCameraModSystem.ClientInstance?.OnSoundPlayed(__0.Path, __1, __2, __3, __6);
        }
    }

    private static void HandleSoundPostfix(Packet_Server __0)
    {
        var sound = __0?.Sound;

        if (sound?.Name == null || DynamicCameraModSystem.ClientInstance is not { } mod)
        {
            return;
        }

        mod.OnSoundPlayed(sound.Name,
            CollectibleNet.DeserializeFloat(sound.X),
            CollectibleNet.DeserializeFloat(sound.Y),
            CollectibleNet.DeserializeFloat(sound.Z),
            CollectibleNet.DeserializeFloat(sound.Range));
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

    // OnHurt(DamageSource damageSource, float damage). The server calls it too in single player.
    private static void PlayerHurtPostfix(EntityPlayer __instance, float __1)
    {
        if (__1 > 0f && __instance.World?.Side == EnumAppSide.Client)
        {
            DynamicCameraModSystem.ClientInstance?.OnPlayerHurt(__instance, __1);
        }
    }
}
