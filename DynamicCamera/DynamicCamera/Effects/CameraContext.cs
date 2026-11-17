// Ported from CameraOverhaul by Mirsario (https://github.com/Mirsario/Minecraft-CameraOverhaul).
// Copyright 2020-2026 Mirsario & Contributors. Released under the GNU General Public License 3.0.

using DynamicCamera.Utilities;

namespace DynamicCamera.Effects;

internal enum CameraPerspective
{
    FirstPerson,
    ThirdPerson,
    Overhead,
}

/// <summary>
/// Snapshot of everything the camera effects need for one frame.
/// Angles are in degrees and use the original mod's conventions:
/// positive pitch looks down, positive yaw turns right (clockwise seen from above), positive roll tilts clockwise.
/// Velocity is in blocks per 1/20 second, the unit the original's factors were tuned for.
/// </summary>
internal sealed class CameraContext
{
    public bool IsSwimming;
    public bool IsGliding;
    public bool IsSprinting;
    public bool IsRiding;
    public bool IsRidingMount;
    public bool IsRidingVehicle;

    public double VelocityX;
    public double VelocityY;
    public double VelocityZ;

    public CameraPerspective Perspective;

    public double Pitch;
    public double Yaw;

    public double PositionX;
    public double PositionY;
    public double PositionZ;

    /// <summary> Horizontal velocity relative to the view direction: X points left, Z points forward. </summary>
    public void GetForwardRelativeVelocity(out double left, out double forward)
    {
        MathUtils.Rotate(VelocityX, VelocityZ, 360.0 - Yaw, out left, out forward);
    }
}
