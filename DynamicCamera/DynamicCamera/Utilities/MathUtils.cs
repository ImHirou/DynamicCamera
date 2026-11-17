// Ported from CameraOverhaul by Mirsario (https://github.com/Mirsario/Minecraft-CameraOverhaul).
// Copyright 2020-2026 Mirsario & Contributors. Released under the GNU General Public License 3.0.

using System;

namespace DynamicCamera.Utilities;

internal static class MathUtils
{
    public const double DegToRad = Math.PI / 180.0;
    public const double RadToDeg = 180.0 / Math.PI;

    public static double Clamp(double value, double min, double max)
    {
        return value < min ? min : (value > max ? max : value);
    }

    public static double Clamp01(double value)
    {
        return value < 0.0 ? 0.0 : (value > 1.0 ? 1.0 : value);
    }

    public static double Lerp(double a, double b, double time)
    {
        return a + (b - a) * Clamp01(time);
    }

    // Framerate-agnostic smoothing.
    // https://www.rorydriscoll.com/2016/03/07/frame-rate-independent-damping-using-lerp
    public static double Damp(double source, double destination, double smoothing, double dt)
    {
        return Lerp(source, destination, DampStep(smoothing, dt));
    }

    /// <summary>
    /// Lerp factor for this frame. <paramref name="smoothing"/> is the fraction (squared) left after one second.
    /// Values outside of [-1, 1] would make the damping diverge, so they are clamped.
    /// </summary>
    public static double DampStep(double smoothing, double dt)
    {
        smoothing = Math.Min(Math.Abs(smoothing), 1.0);
        return 1.0 - Math.Pow(smoothing * smoothing, dt);
    }

    public static double StepTowards(double current, double target, double step)
    {
        if (current < target)
        {
            return Math.Min(current + step, target);
        }

        if (current > target)
        {
            return Math.Max(current - step, target);
        }

        return current;
    }

    /// <summary> Wraps an angle difference (in degrees) into (-180, 180], so that seam crossings don't spike by 360. </summary>
    public static double WrapDegrees(double degrees)
    {
        degrees %= 360.0;

        if (degrees <= -180.0)
        {
            degrees += 360.0;
        }
        else if (degrees > 180.0)
        {
            degrees -= 360.0;
        }

        return degrees;
    }

    // https://easings.net/#easeInOutCubic
    public static double EaseInOutCubic(double x)
    {
        return x < 0.5 ? 4.0 * x * x * x : 1.0 - Math.Pow(-2.0 * x + 2.0, 3.0) / 2.0;
    }

    /// <summary> Rotates a 2D vector counter-clockwise by the given amount of degrees. </summary>
    public static void Rotate(double x, double y, double degrees, out double outX, out double outY)
    {
        double radians = degrees * DegToRad;
        double sin = Math.Sin(radians);
        double cos = Math.Cos(radians);

        outX = cos * x - sin * y;
        outY = sin * x + cos * y;
    }

    /// <summary>
    /// Keeps values up to half of <paramref name="max"/> unchanged and smoothly compresses larger ones, so that the result never exceeds
    /// <paramref name="max"/> in magnitude. A non-positive <paramref name="max"/> disables the limit.
    /// </summary>
    public static double SoftLimit(double value, double max)
    {
        if (!(max > 0.0))
        {
            return value;
        }

        double knee = max * 0.5;
        double magnitude = Math.Abs(value);

        if (magnitude <= knee)
        {
            return value;
        }

        double range = max - knee;
        return Math.Sign(value) * (knee + range * Math.Tanh((magnitude - knee) / range));
    }

    // Maximum of sin(pi * u) * (1 - u) on [0, 1], at u = 0.354.
    private const double ImpulseEnvelopePeak = 0.5792303274;

    /// <summary>
    /// A bump that rises from 0 to 1 in the first third of <paramref name="length"/> seconds and smoothly falls back to 0.
    /// Returns 0 outside of [0, length].
    /// </summary>
    public static double ImpulseEnvelope(double time, double length)
    {
        if (!(length > 0.0) || !(time > 0.0) || time >= length)
        {
            return 0.0;
        }

        double u = time / length;
        return Math.Sin(Math.PI * u) * (1.0 - u) / ImpulseEnvelopePeak;
    }

    public static float Sanitize(float value, float fallback)
    {
        return float.IsFinite(value) ? value : fallback;
    }
}
