// Ported from CameraOverhaul by Mirsario (https://github.com/Mirsario/Minecraft-CameraOverhaul).
// Copyright 2020-2026 Mirsario & Contributors. Released under the GNU General Public License 3.0.

using DynamicCamera.Utilities;

namespace DynamicCamera.Config;

/// <summary> Settings that are shared by every movement context. </summary>
public sealed class GeneralSettings
{
    public bool Enabled = true;
    public bool EnableInThirdPerson = true;
    /// <summary> Target blocks and entities through the center of the rendered view instead of the vanilla look direction. </summary>
    public bool AimFollowsCamera = true;
    public float ContextTransitionSmoothing = 0.1f;

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
    public float ExplosionTrauma = 1.0f;
    public float ThunderTrauma = 0.05f;
    public float HandSwingTrauma = 0.03f;

    public GeneralSettings Clone() => (GeneralSettings)MemberwiseClone();

    internal void Sanitize(GeneralSettings defaults)
    {
        ContextTransitionSmoothing = MathUtils.Sanitize(ContextTransitionSmoothing, defaults.ContextTransitionSmoothing);
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
        ExplosionTrauma = MathUtils.Sanitize(ExplosionTrauma, defaults.ExplosionTrauma);
        ThunderTrauma = MathUtils.Sanitize(ThunderTrauma, defaults.ThunderTrauma);
        HandSwingTrauma = MathUtils.Sanitize(HandSwingTrauma, defaults.HandSwingTrauma);
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

public enum MovementContext
{
    Walking,
    Sprinting,
    Swimming,
    Gliding,
    Mounts,
    Vehicles,
}

/// <summary> Root of <c>ModConfig/dynamiccamera.json</c>. </summary>
public sealed class DynamicCameraConfig
{
    public const int CurrentVersion = 1;
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
        // Camera smoothing
        Sprinting.CameraSmoothing = 0.6f;
        Swimming.CameraSmoothing = 1.5f;
        Gliding.CameraSmoothing = 1.0f;
        Mounts.CameraSmoothing = 1.0f;
        Vehicles.CameraSmoothing = 0.0f;
    }

    public ContextSettings Get(MovementContext context) => context switch
    {
        MovementContext.Sprinting => Sprinting,
        MovementContext.Swimming => Swimming,
        MovementContext.Gliding => Gliding,
        MovementContext.Mounts => Mounts,
        MovementContext.Vehicles => Vehicles,
        _ => Walking,
    };

    public DynamicCameraConfig Clone()
    {
        return new DynamicCameraConfig
        {
            ConfigVersion = ConfigVersion,
            General = General.Clone(),
            Walking = Walking.Clone(),
            Sprinting = Sprinting.Clone(),
            Swimming = Swimming.Clone(),
            Gliding = Gliding.Clone(),
            Mounts = Mounts.Clone(),
            Vehicles = Vehicles.Clone(),
        };
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

        General.Sanitize(defaults.General);

        foreach (var context in System.Enum.GetValues<MovementContext>())
        {
            Get(context).Sanitize(defaults.Get(context));
        }
    }
}
