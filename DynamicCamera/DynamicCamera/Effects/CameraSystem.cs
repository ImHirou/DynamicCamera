// Ported from CameraOverhaul by Mirsario (https://github.com/Mirsario/Minecraft-CameraOverhaul).
// Copyright 2020-2026 Mirsario & Contributors. Released under the GNU General Public License 3.0.

using System;
using DynamicCamera.Config;
using DynamicCamera.Utilities;

namespace DynamicCamera.Effects;

/// <summary>
/// Computes the visual-only camera rotation offsets (pitch, yaw, roll in degrees) for every rendered frame.
/// </summary>
internal sealed class CameraSystem
{
    // Movement below these thresholds does not count as a player action, so that the idle sway can start
    // even though Vintage Story's velocities and smoothed mouse angles converge to rest asymptotically.
    private const double ActionVelocityThreshold = 0.002;
    private const double ActionRotationThreshold = 0.001;
    // Moving faster than this (about 0.2 blocks per second) always counts as an action, even at a constant velocity.
    private const double ActionSpeedThreshold = 0.01;

    /// <summary> Damage at which the hurt kick reaches its full strength. </summary>
    public const double HurtKickFullDamage = 6.0;

    private readonly Func<DynamicCameraConfig> configProvider;

    private DynamicCameraConfig cfg = null!;
    private ContextSettings? ctxCfg;
    private bool hasPreviousFrame;
    private double prevPitch;
    private double prevYaw;
    private double prevVelocityX;
    private double prevVelocityY;
    private double prevVelocityZ;
    private CameraPerspective? prevPerspective;
    private double lastActionTime;

    public CameraSystem(Func<DynamicCameraConfig> configProvider)
    {
        this.configProvider = configProvider;
    }

    public ScreenShakes Shakes { get; } = new();

    /// <summary> Seconds of unpaused rendering since the system was created. </summary>
    public double Time { get; private set; }

    /// <summary> False while the effects are off, because of the settings or the current perspective. </summary>
    public bool IsActive { get; private set; }

    public double OffsetPitch { get; private set; }
    public double OffsetYaw { get; private set; }
    public double OffsetRoll { get; private set; }

    public void NotifyOfPlayerAction()
    {
        lastActionTime = Time;
    }

    /// <summary> Forgets all frame-to-frame state. Use after the camera was taken over or effects were off. </summary>
    public void ResetState()
    {
        IsActive = false;
        hasPreviousFrame = false;
        prevPerspective = null;
        msInit = false;
        prevVerticalVelocityPitchOffset = 0.0;
        prevForwardVelocityPitchOffset = 0.0;
        turningRollTargetOffset = 0.0;
        prevStrafingRollOffset = 0.0;
        cameraSwayFactor = 0.0;
        cameraSwayFactorTarget = 0.0;
        wasAirborne = false;
        landingStartTime = double.NegativeInfinity;
        hurtStartTime = double.NegativeInfinity;
        OffsetPitch = OffsetYaw = OffsetRoll = 0.0;
        lastActionTime = Time;
    }

    public void OnCameraUpdate(CameraContext context, double deltaTime)
    {
        Time += deltaTime;
        Shakes.Time = Time;
        cfg = configProvider();
        var general = cfg.General;

        UpdateContext(context, deltaTime);

        // Always sample the shakes, so that finished ones are freed even while the effects are disabled.
        Shakes.Update(context.PositionX, context.PositionY, context.PositionZ, general, context.ShakeScale, deltaTime);

        if (!general.Enabled || (!general.EnableInThirdPerson && context.Perspective != CameraPerspective.FirstPerson))
        {
            ResetState();
            return;
        }

        IsActive = true;

        if (IsPlayerAction(context))
        {
            NotifyOfPlayerAction();
        }

        UpdateLanding(context);

        // The camera smoothing only delays the view, so the effects intensity does not apply to it.
        double smoothPitch = 0.0, smoothYaw = 0.0;
        CameraSmoothingOffset(context, deltaTime, ref smoothPitch, ref smoothYaw);

        double pitch = 0.0, yaw = 0.0, roll = 0.0;

        // Pitch & yaw
        NoiseOffset(deltaTime, ref pitch, ref yaw);
        // Pitch
        pitch += VerticalVelocityPitchOffset(context, deltaTime);
        pitch += ForwardVelocityPitchOffset(context, deltaTime);
        // Roll
        roll += TurningRollOffset(context, deltaTime);
        roll += StrafingRollOffset(context, deltaTime);
        // Landings and hits
        ImpulseOffsets(ref pitch, ref roll);

        double intensity = Math.Max(0.0, general.EffectsIntensity);
        OffsetPitch = smoothPitch + (pitch + Shakes.OffsetPitch) * intensity;
        OffsetYaw = smoothYaw + (yaw + Shakes.OffsetYaw) * intensity;
        OffsetRoll = (roll + Shakes.OffsetRoll) * intensity;

        prevVelocityX = context.VelocityX;
        prevVelocityY = context.VelocityY;
        prevVelocityZ = context.VelocityZ;
        prevPitch = context.Pitch;
        prevYaw = context.Yaw;
        prevPerspective = context.Perspective;
        hasPreviousFrame = true;
    }

    private bool IsPlayerAction(CameraContext context)
    {
        if (context.HasMovementInput
            || context.VelocityX * context.VelocityX + context.VelocityZ * context.VelocityZ > ActionSpeedThreshold * ActionSpeedThreshold)
        {
            return true;
        }

        return hasPreviousFrame
            && (Math.Abs(context.VelocityX - prevVelocityX) > ActionVelocityThreshold
                || Math.Abs(context.VelocityY - prevVelocityY) > ActionVelocityThreshold
                || Math.Abs(context.VelocityZ - prevVelocityZ) > ActionVelocityThreshold
                || Math.Abs(MathUtils.WrapDegrees(context.Pitch - prevPitch)) > ActionRotationThreshold
                || Math.Abs(MathUtils.WrapDegrees(context.Yaw - prevYaw)) > ActionRotationThreshold);
    }

    private void UpdateContext(CameraContext context, double deltaTime)
    {
        ContextSettings target;
        if (context.IsRidingVehicle) target = cfg.Vehicles;
        else if (context.IsRidingMount) target = cfg.Mounts;
        else if (context.IsSwimming) target = cfg.Swimming;
        else if (context.IsGliding) target = cfg.Gliding;
        else if (context.IsFlying) target = cfg.Flying;
        else if (context.IsClimbing) target = cfg.Climbing;
        else if (context.IsSprinting) target = cfg.Sprinting;
        else target = cfg.Walking;

        if (ctxCfg == null)
        {
            ctxCfg = target.Clone();
            return;
        }

        // The original applies the damping twice, which freezes the blend when the smoothing is set to 0.
        double smoothing = cfg.General.ContextTransitionSmoothing;
        double step = smoothing > 0.0 ? MathUtils.DampStep(smoothing, deltaTime) : 1.0;
        ctxCfg.LerpTowards(target, step);
    }

    private const double BaseVerticalPitchSmoothing = 0.00004;
    private const double VerticalPitchThreshold = 0.4;
    private double prevVerticalVelocityPitchOffset;

    private double VerticalVelocityPitchOffset(CameraContext context, double deltaTime)
    {
        double multiplier = ctxCfg!.VerticalVelocityPitchFactor;
        double smoothing = BaseVerticalPitchSmoothing * ctxCfg.VerticalVelocitySmoothingFactor;

        double targetOffset = context.VelocityY * multiplier;
        // Threshold on the target, so that tiny bounces don't shake the camera.
        if (Math.Abs(targetOffset) < VerticalPitchThreshold)
        {
            targetOffset = 0.0;
        }

        targetOffset = MathUtils.SoftLimit(targetOffset, cfg.General.MaxVelocityPitch);

        double currentOffset = MathUtils.Damp(prevVerticalVelocityPitchOffset, targetOffset, smoothing, deltaTime);
        prevVerticalVelocityPitchOffset = currentOffset;
        return currentOffset;
    }

    private const double BaseForwardPitchSmoothing = 0.008;
    private double prevForwardVelocityPitchOffset;

    private double ForwardVelocityPitchOffset(CameraContext context, double deltaTime)
    {
        double multiplier = ctxCfg!.ForwardVelocityPitchFactor;
        double smoothing = BaseForwardPitchSmoothing * ctxCfg.HorizontalVelocitySmoothingFactor;

        context.GetForwardRelativeVelocity(out _, out double forward);
        double targetOffset = MathUtils.SoftLimit(forward * multiplier, cfg.General.MaxVelocityPitch);
        double currentOffset = MathUtils.Damp(prevForwardVelocityPitchOffset, targetOffset, smoothing, deltaTime);

        prevForwardVelocityPitchOffset = currentOffset;
        return currentOffset;
    }

    private const double BaseTurningRollAccumulation = 0.0048;
    private const double BaseTurningRollIntensity = 1.25;
    private const double BaseTurningRollSmoothing = 0.0825;
    private double turningRollTargetOffset;

    private double TurningRollOffset(CameraContext context, double deltaTime)
    {
        double decaySmoothing = BaseTurningRollSmoothing * cfg.General.TurningRollSmoothing;
        double intensity = BaseTurningRollIntensity * cfg.General.TurningRollIntensity;
        double accumulation = BaseTurningRollAccumulation * cfg.General.TurningRollAccumulation;
        double yawDelta = hasPreviousFrame ? MathUtils.WrapDegrees(prevYaw - context.Yaw) : 0.0;

        // Don't spazz out when switching perspectives.
        if (context.Perspective != prevPerspective)
        {
            yawDelta = 0.0;
        }

        // Decay
        turningRollTargetOffset = MathUtils.Damp(turningRollTargetOffset, 0.0, decaySmoothing, deltaTime);
        // Accumulation
        turningRollTargetOffset = MathUtils.Clamp(turningRollTargetOffset + yawDelta * accumulation, -1.0, 1.0);
        // Apply
        return MathUtils.Clamp01(MathUtils.EaseInOutCubic(Math.Abs(turningRollTargetOffset))) * intensity * Math.Sign(turningRollTargetOffset);
    }

    private const double BaseStrafingRollSmoothing = 0.008;
    private double prevStrafingRollOffset;

    private double StrafingRollOffset(CameraContext context, double deltaTime)
    {
        double multiplier = ctxCfg!.StrafingRollFactor;
        double smoothing = BaseStrafingRollSmoothing * ctxCfg.HorizontalVelocitySmoothingFactor;

        context.GetForwardRelativeVelocity(out double left, out _);
        double target = MathUtils.SoftLimit(-left * multiplier, cfg.General.MaxVelocityRoll);
        double offset = MathUtils.Damp(prevStrafingRollOffset, target, smoothing, deltaTime);

        prevStrafingRollOffset = offset;
        return offset;
    }

    private const double CameraSwayFadingSmoothness = 3.0;
    private double cameraSwayFactor;
    private double cameraSwayFactorTarget;

    private void NoiseOffset(double deltaTime, ref double pitch, ref double yaw)
    {
        var general = cfg.General;
        double time = Time;
        double noiseX = time * general.CameraSwayFrequency;

        // Fade out if the player turns, moves, or does an interaction.
        if (time - lastActionTime < general.CameraSwayFadeInDelay)
        {
            cameraSwayFactorTarget = 0.0; // Fade-out
        }
        // Only start a fade-in after the last fade-out has ended.
        else if (cameraSwayFactor == cameraSwayFactorTarget)
        {
            cameraSwayFactorTarget = 1.0; // Fade-in
        }

        double fadeLength = cameraSwayFactorTarget > 0.0 ? general.CameraSwayFadeInLength : general.CameraSwayFadeOutLength;
        double fadeStep = fadeLength > 0.0 ? deltaTime / fadeLength : 1.0;
        cameraSwayFactor = MathUtils.StepTowards(cameraSwayFactor, cameraSwayFactorTarget, fadeStep);

        double scaledIntensity = general.CameraSwayIntensity * Math.Pow(cameraSwayFactor, CameraSwayFadingSmoothness);

        pitch += SimplexNoise.Noise(noiseX, 420.0) * scaledIntensity;
        yaw += SimplexNoise.Noise(noiseX, 1337.0) * scaledIntensity;
    }

    private const double BaseMouseSmoothing = 16.0;
    private const double MouseSmoothingThreshold = 0.001;

    private bool msInit;
    private double prevYawNorm, prevPitchNorm; // Last frame's raw angles.
    private double contYaw, contPitch; // Continuous, unwrapped stream.
    private double smYaw, smPitch; // Smoothed angles.

    private void CameraSmoothingOffset(CameraContext context, double deltaTime, ref double pitch, ref double yaw)
    {
        double cameraSmoothingValue = Math.Max(0.0, ctxCfg!.CameraSmoothing);
        double yawNow = context.Yaw;
        double pitchNow = context.Pitch;

        // Don't spazz out.
        if (!msInit || context.Perspective != prevPerspective)
        {
            prevYawNorm = contYaw = smYaw = yawNow;
            prevPitchNorm = contPitch = smPitch = pitchNow;
            msInit = true;
            return;
        }

        // Preserve real mouse movement.
        contYaw += MathUtils.WrapDegrees(yawNow - prevYawNorm);
        contPitch += MathUtils.WrapDegrees(pitchNow - prevPitchNorm);
        prevYawNorm = yawNow;
        prevPitchNorm = pitchNow;

        if (cameraSmoothingValue <= MouseSmoothingThreshold)
        {
            smYaw = contYaw;
            smPitch = contPitch;
            return;
        }

        double k = BaseMouseSmoothing / cameraSmoothingValue;
        double step = 1.0 - Math.Exp(-k * Math.Max(0.0, deltaTime));

        smYaw += (contYaw - smYaw) * step;
        smPitch += (contPitch - smPitch) * step;

        // Offset so that base + offset == smoothed angles this frame.
        yaw += MathUtils.WrapDegrees(smYaw - yawNow);
        pitch += MathUtils.WrapDegrees(smPitch - pitchNow);
    }

    #region Impacts

    private const double LandingPitchLength = 0.35;
    private const float LandingShakeLength = 0.4f;
    private const float LandingShakeFrequency = 0.8f;
    private const double HurtKickLength = 0.3;

    private bool wasAirborne;
    private double airborneMinVelocityY;
    private double landingStartTime = double.NegativeInfinity;
    private double landingPitch;
    private double hurtStartTime = double.NegativeInfinity;
    private double hurtPitch;
    private double hurtRoll;

    /// <summary> Dips the camera like after a fall. <paramref name="strength"/> is 0..1. </summary>
    public void TriggerLanding(double strength)
    {
        if (!IsActive || !(strength > 0.0))
        {
            return;
        }

        var general = cfg.General;
        landingStartTime = Time;
        landingPitch = general.LandingPitchIntensity * strength;

        if (general.LandingTrauma > 0f)
        {
            var shake = Shakes.CreateDirect();
            shake.Trauma = (float)(general.LandingTrauma * strength);
            shake.Frequency = LandingShakeFrequency;
            shake.LengthInSeconds = LandingShakeLength;
        }

        NotifyOfPlayerAction();
    }

    /// <summary>
    /// Kicks the camera away from a hit. <paramref name="right"/> and <paramref name="forward"/> are the knockback direction
    /// projected on the view's right and forward axes (-1..1).
    /// </summary>
    public void OnPlayerHurt(double damage, double right, double forward)
    {
        if (!IsActive)
        {
            return;
        }

        double strength = MathUtils.Clamp01(damage / HurtKickFullDamage) * cfg.General.HurtKickIntensity;

        if (strength == 0.0 || !double.IsFinite(strength))
        {
            return;
        }

        // Positive roll tilts clockwise and positive pitch looks down:
        // a hit from the left tilts the head to the right, a hit from the front throws it back.
        hurtStartTime = Time;
        hurtRoll = strength * right;
        hurtPitch = strength * 0.5 * forward;
        NotifyOfPlayerAction();
    }

    private void UpdateLanding(CameraContext context)
    {
        if (context.IsSwimming || context.IsGliding || context.IsFlying || context.IsClimbing || context.IsRiding)
        {
            wasAirborne = false;
            return;
        }

        if (!context.IsOnGround)
        {
            airborneMinVelocityY = wasAirborne ? Math.Min(airborneMinVelocityY, context.VelocityY) : context.VelocityY;
            wasAirborne = true;
            return;
        }

        if (!wasAirborne)
        {
            return;
        }

        wasAirborne = false;

        // Full strength at twice the threshold speed.
        double threshold = Math.Max(cfg.General.LandingThreshold, 0.01);
        TriggerLanding(MathUtils.Clamp01((-airborneMinVelocityY - threshold) / threshold));
    }

    private void ImpulseOffsets(ref double pitch, ref double roll)
    {
        pitch += landingPitch * MathUtils.ImpulseEnvelope(Time - landingStartTime, LandingPitchLength);

        double hurt = MathUtils.ImpulseEnvelope(Time - hurtStartTime, HurtKickLength);
        pitch += hurtPitch * hurt;
        roll += hurtRoll * hurt;
    }

    #endregion
}
