// Ported from CameraOverhaul by Mirsario (https://github.com/Mirsario/Minecraft-CameraOverhaul).
// Copyright 2020-2026 Mirsario & Contributors. Released under the GNU General Public License 3.0.

using System;
using System.Numerics;
using DynamicCamera.Config;
using DynamicCamera.Utilities;

namespace DynamicCamera.Effects;

/// <summary>
/// Trauma-based screen shakes. Each shake fades out quadratically over its length and,
/// if it has a position, also with the distance to the camera.
/// </summary>
internal sealed class ScreenShakes
{
    public sealed class Slot
    {
        public float Trauma;
        public float Radius;
        public float Frequency;
        public float LengthInSeconds;
        // Infinity means "positionless": the shake is felt everywhere at full strength.
        public double PositionX = double.PositiveInfinity;
        public double PositionY = double.PositiveInfinity;
        public double PositionZ = double.PositiveInfinity;

        internal double StartTime;
        internal int Version = 1;

        public bool HasPosition => double.IsFinite(PositionX) && double.IsFinite(PositionY) && double.IsFinite(PositionZ);

        public void SetPosition(double x, double y, double z)
        {
            PositionX = x;
            PositionY = y;
            PositionZ = z;
        }

        internal void SetDefaults(double time)
        {
            Trauma = 0.5f;
            Radius = 10.0f;
            Frequency = 1.0f;
            LengthInSeconds = 2.0f;
            PositionX = PositionY = PositionZ = double.PositiveInfinity;
            StartTime = time;
        }
    }

    private const int Capacity = 64;

    private readonly Slot dummyInstance = new();
    private readonly Slot?[] instances = new Slot?[Capacity];
    private ulong instanceMask;

    /// <summary> Current time of the camera clock, in seconds. Used as the start time of new shakes. </summary>
    public double Time { get; set; }

    public double OffsetPitch { get; private set; }
    public double OffsetYaw { get; private set; }
    public double OffsetRoll { get; private set; }

    public Slot Get(long handle) => IsHandleValid(handle) ? instances[ExtractIndex(handle)]! : dummyInstance;

    public Slot CreateDirect() => Get(Create());

    /// <summary> Allocates a shake with default values. Returns 0 (an invalid handle) if every slot is taken. </summary>
    public long Create()
    {
        if (instanceMask == ulong.MaxValue)
        {
            return 0L;
        }

        int index = BitOperations.TrailingZeroCount(~instanceMask);
        var instance = instances[index] ??= new Slot();

        instanceMask |= 1UL << index;
        instance.SetDefaults(Time);

        return ConstructHandle(index, instance.Version);
    }

    public long Validate(long handle) => IsHandleValid(handle) ? handle : Create();

    /// <summary> Restarts the shake behind the handle, or allocates a new one if it has already ended. </summary>
    public long Recreate(long handle)
    {
        if (!IsHandleValid(handle))
        {
            return Create();
        }

        Get(handle).SetDefaults(Time);
        return handle;
    }

    public void Clear()
    {
        ulong mask = instanceMask;

        while (mask != 0UL)
        {
            int index = BitOperations.TrailingZeroCount(mask);
            mask &= ~(1UL << index);
            instances[index]!.Version++;
        }

        instanceMask = 0UL;
        OffsetPitch = OffsetYaw = OffsetRoll = 0.0;
    }

    /// <summary> Recomputes the combined shake rotation for a camera at the given position. </summary>
    public void Update(double x, double y, double z, GeneralSettings cfg)
    {
        GetNoiseAtPosition(x, y, z, cfg, out double noiseX, out double noiseY, out double noiseZ);

        double maxIntensity = cfg.ScreenShakesMaxIntensity;
        OffsetPitch = noiseX * maxIntensity;
        OffsetYaw = noiseY * maxIntensity;
        OffsetRoll = noiseZ * maxIntensity;
    }

    private void GetNoiseAtPosition(double x, double y, double z, GeneralSettings cfg, out double noiseX, out double noiseY, out double noiseZ)
    {
        ulong mask = instanceMask;
        double time = Time;
        double sampleBase = time * cfg.ScreenShakesMaxFrequency;
        double total = 0.0;

        noiseX = noiseY = noiseZ = 0.0;

        while (mask != 0UL)
        {
            int index = BitOperations.TrailingZeroCount(mask);
            mask &= ~(1UL << index);

            var shake = instances[index]!;
            double progress = shake.LengthInSeconds > 0f ? MathUtils.Clamp01((time - shake.StartTime) / shake.LengthInSeconds) : 1.0;

            if (progress >= 1.0)
            {
                // Free the stale entry.
                instanceMask &= ~(1UL << index);
                shake.Version++;
                continue;
            }

            double decay = 1.0 - progress;
            double intensity = MathUtils.Clamp01(shake.Trauma) * (decay * decay);

            if (shake.HasPosition)
            {
                double dx = x - shake.PositionX;
                double dy = y - shake.PositionY;
                double dz = z - shake.PositionZ;
                double distance = Math.Sqrt(dx * dx + dy * dy + dz * dz);
                double distanceFactor = 1.0 - Math.Min(1.0, distance / shake.Radius);
                intensity *= distanceFactor * distanceFactor;
            }

            if (intensity <= 0.0 || !double.IsFinite(intensity))
            {
                continue;
            }

            double sampleStep = sampleBase * shake.Frequency;
            noiseX += SimplexNoise.Noise(sampleStep, -69.0) * intensity;
            noiseY += SimplexNoise.Noise(sampleStep, -420.0) * intensity;
            noiseZ += SimplexNoise.Noise(sampleStep, -1337.0) * intensity;
            total += intensity;
        }

        // Don't go overboard.
        if (total > 1.0)
        {
            noiseX /= total;
            noiseY /= total;
            noiseZ /= total;
        }
    }

    private static int ExtractIndex(long handle) => (int)(handle & 0xFFFFFFFFL);

    private static int ExtractVersion(long handle) => (int)(handle >> 32);

    private static long ConstructHandle(int index, int version) => (uint)index | ((long)version << 32);

    private bool IsHandleValid(long handle)
    {
        if (handle == 0L)
        {
            return false;
        }

        int index = ExtractIndex(handle);
        return index is >= 0 and < Capacity && instances[index] is { } instance && instance.Version == ExtractVersion(handle);
    }
}
