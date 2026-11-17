// Ported from CameraOverhaul by Mirsario (https://github.com/Mirsario/Minecraft-CameraOverhaul).
// Copyright 2020-2026 Mirsario & Contributors. Released under the GNU General Public License 3.0.

using System;
using System.Collections.Generic;
using DynamicCamera.Utilities;
using Newtonsoft.Json;

namespace DynamicCamera.Config;

/// <summary> Settings that are shared by every movement context. </summary>
public sealed class GeneralSettings
{
    public bool Enabled = true;
    public bool EnableInThirdPerson = true;
    /// <summary> Target blocks and entities through the center of the rendered view instead of the vanilla look direction. </summary>
    public bool AimFollowsCamera = true;
    /// <summary> Scales every effect except the camera smoothing. </summary>
    public float EffectsIntensity = 1.0f;
    public float ContextTransitionSmoothing = 0.1f;

    // Soft limits of the velocity-driven pitch and roll, in degrees. 0 disables a limit.
    public float MaxVelocityPitch = 30.0f;
    public float MaxVelocityRoll = 30.0f;

    // Turning (mouselook) roll
    public float TurningRollAccumulation = 1.0f;
    public float TurningRollIntensity = 1.25f;
    public float TurningRollSmoothing = 1.0f;

    // Idle camera sway
    public float CameraSwayIntensity = 0.6f;
    public float CameraSwayFrequency = 0.16f;
    public float CameraSwayFadeInDelay = 0.15f;
    public float CameraSwayFadeInLength = 5.0f;
    public float CameraSwayFadeOutLength = 0.75f;

    // Screen shakes
    public float ScreenShakesMaxIntensity = 2.5f;
    public float ScreenShakesMaxFrequency = 6.0f;
    /// <summary> Scale the screen shakes by the game's "Camera shake" accessibility setting. </summary>
    public bool RespectGameShakeSetting = true;
    public float LightningTrauma = 1.0f;
    public float ThunderTrauma = 0.05f;
    public float HandSwingTrauma = 0.03f;

    // The game's own camera shakes: damage, weapon hits, translocators, temporal storms, server effects.
    public bool ConvertVanillaShakes = true;
    public bool SuppressVanillaShakeJitter = true;
    public float VanillaShakeTraumaScale = 1.0f;

    // Impacts
    public float LandingPitchIntensity = 4.0f;
    public float LandingTrauma = 0.3f;
    /// <summary> Fall speed (blocks per 1/20 s) above which landings dip the camera. 0.5 is a drop of about 2.5 blocks. </summary>
    public float LandingThreshold = 0.5f;
    public float HurtKickIntensity = 3.0f;

    /// <summary> Config version 1 only: read to migrate it into the sound shakes, never written. </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public float? ExplosionTrauma;

    internal void Sanitize(GeneralSettings defaults)
    {
        EffectsIntensity = MathUtils.Sanitize(EffectsIntensity, defaults.EffectsIntensity);
        ContextTransitionSmoothing = MathUtils.Sanitize(ContextTransitionSmoothing, defaults.ContextTransitionSmoothing);
        MaxVelocityPitch = MathUtils.Sanitize(MaxVelocityPitch, defaults.MaxVelocityPitch);
        MaxVelocityRoll = MathUtils.Sanitize(MaxVelocityRoll, defaults.MaxVelocityRoll);
        TurningRollAccumulation = MathUtils.Sanitize(TurningRollAccumulation, defaults.TurningRollAccumulation);
        TurningRollIntensity = MathUtils.Sanitize(TurningRollIntensity, defaults.TurningRollIntensity);
        TurningRollSmoothing = MathUtils.Sanitize(TurningRollSmoothing, defaults.TurningRollSmoothing);
        CameraSwayIntensity = MathUtils.Sanitize(CameraSwayIntensity, defaults.CameraSwayIntensity);
        CameraSwayFrequency = MathUtils.Sanitize(CameraSwayFrequency, defaults.CameraSwayFrequency);
        CameraSwayFadeInDelay = MathUtils.Sanitize(CameraSwayFadeInDelay, defaults.CameraSwayFadeInDelay);
        CameraSwayFadeInLength = MathUtils.Sanitize(CameraSwayFadeInLength, defaults.CameraSwayFadeInLength);
        CameraSwayFadeOutLength = MathUtils.Sanitize(CameraSwayFadeOutLength, defaults.CameraSwayFadeOutLength);
        ScreenShakesMaxIntensity = MathUtils.Sanitize(ScreenShakesMaxIntensity, defaults.ScreenShakesMaxIntensity);
        ScreenShakesMaxFrequency = MathUtils.Sanitize(ScreenShakesMaxFrequency, defaults.ScreenShakesMaxFrequency);
        LightningTrauma = MathUtils.Sanitize(LightningTrauma, defaults.LightningTrauma);
        ThunderTrauma = MathUtils.Sanitize(ThunderTrauma, defaults.ThunderTrauma);
        HandSwingTrauma = MathUtils.Sanitize(HandSwingTrauma, defaults.HandSwingTrauma);
        VanillaShakeTraumaScale = MathUtils.Sanitize(VanillaShakeTraumaScale, defaults.VanillaShakeTraumaScale);
        LandingPitchIntensity = MathUtils.Sanitize(LandingPitchIntensity, defaults.LandingPitchIntensity);
        LandingTrauma = MathUtils.Sanitize(LandingTrauma, defaults.LandingTrauma);
        LandingThreshold = MathUtils.Sanitize(LandingThreshold, defaults.LandingThreshold);
        HurtKickIntensity = MathUtils.Sanitize(HurtKickIntensity, defaults.HurtKickIntensity);
    }
}

/// <summary> Settings that are smoothly blended between movement contexts (walking, swimming, gliding, ...). </summary>
public sealed class ContextSettings
{
    public float StrafingRollFactor = 10.0f;
    public float ForwardVelocityPitchFactor = 7.0f;
    public float VerticalVelocityPitchFactor = 2.5f;
    public float HorizontalVelocitySmoothingFactor = 1.0f;
    public float VerticalVelocitySmoothingFactor = 1.0f;
    public float CameraSmoothing = 0.0f;

    public ContextSettings Clone() => (ContextSettings)MemberwiseClone();

    public void CopyFrom(ContextSettings other)
    {
        StrafingRollFactor = other.StrafingRollFactor;
        ForwardVelocityPitchFactor = other.ForwardVelocityPitchFactor;
        VerticalVelocityPitchFactor = other.VerticalVelocityPitchFactor;
        HorizontalVelocitySmoothingFactor = other.HorizontalVelocitySmoothingFactor;
        VerticalVelocitySmoothingFactor = other.VerticalVelocitySmoothingFactor;
        CameraSmoothing = other.CameraSmoothing;
    }

    /// <summary> Moves every value of this instance towards <paramref name="target"/> by <paramref name="step"/> (0..1). </summary>
    public void LerpTowards(ContextSettings target, double step)
    {
        StrafingRollFactor = (float)MathUtils.Lerp(StrafingRollFactor, target.StrafingRollFactor, step);
        ForwardVelocityPitchFactor = (float)MathUtils.Lerp(ForwardVelocityPitchFactor, target.ForwardVelocityPitchFactor, step);
        VerticalVelocityPitchFactor = (float)MathUtils.Lerp(VerticalVelocityPitchFactor, target.VerticalVelocityPitchFactor, step);
        HorizontalVelocitySmoothingFactor = (float)MathUtils.Lerp(HorizontalVelocitySmoothingFactor, target.HorizontalVelocitySmoothingFactor, step);
        VerticalVelocitySmoothingFactor = (float)MathUtils.Lerp(VerticalVelocitySmoothingFactor, target.VerticalVelocitySmoothingFactor, step);
        CameraSmoothing = (float)MathUtils.Lerp(CameraSmoothing, target.CameraSmoothing, step);
    }

    internal void Sanitize(ContextSettings defaults)
    {
        StrafingRollFactor = MathUtils.Sanitize(StrafingRollFactor, defaults.StrafingRollFactor);
        ForwardVelocityPitchFactor = MathUtils.Sanitize(ForwardVelocityPitchFactor, defaults.ForwardVelocityPitchFactor);
        VerticalVelocityPitchFactor = MathUtils.Sanitize(VerticalVelocityPitchFactor, defaults.VerticalVelocityPitchFactor);
        HorizontalVelocitySmoothingFactor = MathUtils.Sanitize(HorizontalVelocitySmoothingFactor, defaults.HorizontalVelocitySmoothingFactor);
        VerticalVelocitySmoothingFactor = MathUtils.Sanitize(VerticalVelocitySmoothingFactor, defaults.VerticalVelocitySmoothingFactor);
        CameraSmoothing = MathUtils.Sanitize(CameraSmoothing, defaults.CameraSmoothing);
    }
}

/// <summary> A screen shake caused by every sound whose asset path contains <see cref="Sound"/>. </summary>
public sealed class SoundShake
{
    /// <summary> Part of the sound asset path, case-insensitive. Empty entries are ignored. </summary>
    public string Sound = "";
    public float Trauma = 0.3f;
    /// <summary> Distance in blocks at which the shake fades out. Never larger than the range of the sound. </summary>
    public float Radius = 16.0f;
    /// <summary> Derive the radius from the sound range, the way the game's explosions set it. </summary>
    public bool RadiusFromExplosionRange;
    public float LengthInSeconds = 1.0f;
    public float Frequency = 1.0f;

    internal void Sanitize(SoundShake defaults)
    {
        Sound ??= "";
        Trauma = MathUtils.Sanitize(Trauma, defaults.Trauma);
        Radius = MathUtils.Sanitize(Radius, defaults.Radius);
        LengthInSeconds = MathUtils.Sanitize(LengthInSeconds, defaults.LengthInSeconds);
        Frequency = MathUtils.Sanitize(Frequency, defaults.Frequency);
    }
}

public enum MovementContext
{
    Walking,
    Sprinting,
    Swimming,
    Gliding,
    Mounts,
    Vehicles,
    Flying,
    Climbing,
}

/// <summary> Root of <c>ModConfig/dynamiccamera.json</c>. </summary>
public sealed class DynamicCameraConfig
{
    /// <summary> 2: explosions moved into <see cref="SoundShakes"/>, lightning strikes got <see cref="GeneralSettings.LightningTrauma"/>. </summary>
    public const int CurrentVersion = 2;
    public const string FileName = "dynamiccamera.json";

    public int ConfigVersion = CurrentVersion;
    public GeneralSettings General = new();
    public ContextSettings Walking = new();
    public ContextSettings Sprinting = new();
    public ContextSettings Swimming = new();
    /// <summary> Glider flight. Plays the role of the original mod's elytra ("flying") profile. </summary>
    public ContextSettings Gliding = new();
    public ContextSettings Mounts = new();
    public ContextSettings Vehicles = new();
    /// <summary> Creative and spectator flight. </summary>
    public ContextSettings Flying = new();
    /// <summary> Ladders, vines and other climbable blocks. </summary>
    public ContextSettings Climbing = new();

    /// <summary>
    /// Replaced rather than merged when the file is read: by default Newtonsoft would append the file's entries to these defaults on every load.
    /// </summary>
    [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
    public List<SoundShake> SoundShakes = CreateDefaultSoundShakes();

    public DynamicCameraConfig()
    {
        // Same defaults as the original mod.
        // Sprinting
        Sprinting.StrafingRollFactor = 12.5f;
        Sprinting.ForwardVelocityPitchFactor = 9.5f;
        // Gliding (elytra flight in the original): reversed strafing roll banks the camera into turns.
        Gliding.StrafingRollFactor *= -1.0f;
        // Swimming
        Swimming.StrafingRollFactor *= -3.0f;
        Swimming.ForwardVelocityPitchFactor *= 3.0f;
        Swimming.VerticalVelocityPitchFactor *= 3.0f;
        // Mounts
        Mounts.StrafingRollFactor *= 2.0f;
        Mounts.ForwardVelocityPitchFactor *= 0.5f;
        // Vehicles (boats, rafts, ...)
        Vehicles.StrafingRollFactor *= 0.5f;
        Vehicles.ForwardVelocityPitchFactor *= 0.5f;
        Vehicles.VerticalVelocityPitchFactor *= 2.0f;
        // Not in the original. Creative flight is fast, so it tilts the camera less.
        Flying.StrafingRollFactor *= 0.5f;
        Flying.ForwardVelocityPitchFactor *= 0.3f;
        Flying.VerticalVelocityPitchFactor *= 0.5f;
        // Climbing moves into the ladder, so the forward velocity is meaningless.
        Climbing.StrafingRollFactor *= 0.5f;
        Climbing.ForwardVelocityPitchFactor = 0.0f;
        Climbing.VerticalVelocityPitchFactor *= 0.5f;
        // Camera smoothing
        Sprinting.CameraSmoothing = 0.6f;
        Swimming.CameraSmoothing = 1.5f;
        Gliding.CameraSmoothing = 1.0f;
        Mounts.CameraSmoothing = 1.0f;
        Vehicles.CameraSmoothing = 0.0f;
        Flying.CameraSmoothing = 0.5f;
    }

    public static List<SoundShake> CreateDefaultSoundShakes() =>
    [
        // ServerMain.CreateExplosion: effect/smallexplosion, mediumexplosion and largeexplosion.
        new SoundShake { Sound = "explosion", Trauma = 1.0f, RadiusFromExplosionRange = true, LengthInSeconds = 2.0f, Frequency = 1.0f },
        // Collapsing unstable rock.
        new SoundShake { Sound = "effect/rockslide", Trauma = 0.35f, Radius = 32.0f, LengthInSeconds = 3.0f, Frequency = 0.6f },
        new SoundShake { Sound = "effect/treefell", Trauma = 0.15f, Radius = 20.0f, LengthInSeconds = 1.5f, Frequency = 0.7f },
        // Pulverizer.
        new SoundShake { Sound = "effect/crusher-impact", Trauma = 0.04f, Radius = 8.0f, LengthInSeconds = 0.3f, Frequency = 1.5f },
    ];

    public ContextSettings Get(MovementContext context) => context switch
    {
        MovementContext.Sprinting => Sprinting,
        MovementContext.Swimming => Swimming,
        MovementContext.Gliding => Gliding,
        MovementContext.Mounts => Mounts,
        MovementContext.Vehicles => Vehicles,
        MovementContext.Flying => Flying,
        MovementContext.Climbing => Climbing,
        _ => Walking,
    };

    /// <summary> Upgrades the settings of an older config file. Runs before <see cref="Sanitize"/>, so sections may be missing. </summary>
    internal void Migrate(int fromVersion)
    {
        if (fromVersion < 2 && General?.ExplosionTrauma is { } explosionTrauma && float.IsFinite(explosionTrauma))
        {
            // Version 1 used one trauma for explosions and lightning strikes.
            General.LightningTrauma = explosionTrauma;

            foreach (var shake in SoundShakes ?? [])
            {
                if (shake is { RadiusFromExplosionRange: true })
                {
                    shake.Trauma = explosionTrauma;
                }
            }
        }

        ConfigVersion = CurrentVersion;
    }

    /// <summary> Replaces missing sections and non-finite numbers with defaults. </summary>
    internal void Sanitize()
    {
        var defaults = new DynamicCameraConfig();

        General ??= defaults.General;
        Walking ??= defaults.Walking;
        Sprinting ??= defaults.Sprinting;
        Swimming ??= defaults.Swimming;
        Gliding ??= defaults.Gliding;
        Mounts ??= defaults.Mounts;
        Vehicles ??= defaults.Vehicles;
        Flying ??= defaults.Flying;
        Climbing ??= defaults.Climbing;
        SoundShakes ??= defaults.SoundShakes;

        General.Sanitize(defaults.General);
        General.ExplosionTrauma = null;

        foreach (var context in Enum.GetValues<MovementContext>())
        {
            Get(context).Sanitize(defaults.Get(context));
        }

        SoundShakes.RemoveAll(shake => shake == null);
        var shakeDefaults = new SoundShake();

        foreach (var shake in SoundShakes)
        {
            shake.Sanitize(shakeDefaults);
        }
    }
}
