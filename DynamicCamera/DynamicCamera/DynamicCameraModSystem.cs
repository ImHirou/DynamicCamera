using System;
using DynamicCamera.Config;
using DynamicCamera.Effects;
using DynamicCamera.Gui;
using DynamicCamera.Patches;
using DynamicCamera.Utilities;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.Client.NoObf;

namespace DynamicCamera;

/// <summary>
/// Client-side entry point. Port of CameraOverhaul by Mirsario to Vintage Story.
/// </summary>
public sealed class DynamicCameraModSystem : ModSystem
{
    public const string Domain = "dynamiccamera";

    // Vintage Story stores velocity in blocks per 1/60 s, the original's factors expect blocks per 1/20 s.
    private const double VelocityScale = 3.0;
    private const float MaxFrameTime = 0.5f;

    private readonly CameraContext context = new();
    private ICoreClientAPI? capi;
    private Harmony? harmony;
    private ISettingsUi? settingsUi;
    private long handSwingShakeHandle;

    /// <summary> The running client instance, used by the Harmony patches. </summary>
    internal static DynamicCameraModSystem? ClientInstance { get; private set; }

    internal DynamicCameraConfig Config { get; private set; } = new();

    internal CameraSystem Effects { get; private set; } = null!;

    public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Client;

    public override void StartClientSide(ICoreClientAPI api)
    {
        capi = api;
        ClientInstance = this;
        Config = LoadConfig();
        Effects = new CameraSystem(() => Config);

        harmony = new Harmony(Mod.Info.ModID);
        if (CameraPatches.Apply(harmony, Mod.Logger))
        {
            AimPatches.Apply(harmony, Mod.Logger);
        }

        ShakeSourcePatches.Apply(harmony, Mod.Logger);

        settingsUi = GuiIntegration.TryCreate(this, api);
        RegisterCommands(api);
    }

    public override void Dispose()
    {
        settingsUi?.Dispose();
        settingsUi = null;
        harmony?.UnpatchAll(harmony.Id);
        harmony = null;

        if (ClientInstance == this)
        {
            ClientInstance = null;
        }

        base.Dispose();
    }

    #region Config

    internal DynamicCameraConfig LoadConfig()
    {
        DynamicCameraConfig? loaded;

        try
        {
            loaded = capi!.LoadModConfig<DynamicCameraConfig>(DynamicCameraConfig.FileName);
        }
        catch (Exception e)
        {
            // Keep the broken file around so the user can fix it, and run with defaults meanwhile.
            Mod.Logger.Error("Failed to read ModConfig/{0}, using default settings: {1}", DynamicCameraConfig.FileName, e.Message);
            return new DynamicCameraConfig();
        }

        if (loaded == null)
        {
            loaded = new DynamicCameraConfig();
        }
        else if (loaded.ConfigVersion < DynamicCameraConfig.CurrentVersion)
        {
            Mod.Logger.Notification("Resetting the configuration due to a version change.");
            loaded = new DynamicCameraConfig();
        }

        loaded.Sanitize();
        loaded.ConfigVersion = DynamicCameraConfig.CurrentVersion;
        // Writes back newly added settings.
        SaveConfig(loaded);
        return loaded;
    }

    internal bool SaveConfig() => SaveConfig(Config);

    private bool SaveConfig(DynamicCameraConfig config)
    {
        try
        {
            capi!.StoreModConfig(config, DynamicCameraConfig.FileName);
            return true;
        }
        catch (Exception e)
        {
            Mod.Logger.Error("Failed to save ModConfig/{0}: {1}", DynamicCameraConfig.FileName, e);
            return false;
        }
    }

    internal void ReloadConfig()
    {
        Config = LoadConfig();
        settingsUi?.OnConfigReplaced();
    }

    /// <summary> Replaces the settings with defaults without saving them. </summary>
    internal void ResetConfigToDefaults()
    {
        Config = new DynamicCameraConfig();
    }

    #endregion

    #region Camera

    /// <summary>
    /// Called right before the game builds the view matrices. Returns false when no offsets should be applied this frame.
    /// </summary>
    internal bool UpdateCameraEffects(PlayerCamera camera, float deltaTime)
    {
        var player = capi?.World?.Player;
        var entity = player?.Entity;

        if (player == null || entity == null)
        {
            return false;
        }

        // The cinematic camera (".cam" commands) drives the camera itself.
        if (!camera.UpdateCameraPos)
        {
            Effects.ResetState();
            return false;
        }

        var mount = entity.MountedOn;
        var mountEntity = mount?.Entity;
        var controls = entity.Controls;

        context.IsRiding = mount != null;
        // Animals are agents; boats, rafts and elevators are plain entities.
        context.IsRidingMount = mountEntity is EntityAgent;
        context.IsRidingVehicle = mountEntity != null && mountEntity is not EntityAgent;
        context.IsGliding = controls.Gliding;
        context.IsSwimming = entity.Swimming;
        context.IsSprinting = controls.Sprint && controls.TriesToMove && !controls.Sneak;

        var motion = (mountEntity ?? entity).Pos.Motion;
        context.VelocityX = motion.X * VelocityScale;
        context.VelocityY = motion.Y * VelocityScale;
        context.VelocityZ = motion.Z * VelocityScale;

        context.Perspective = player.CameraMode switch
        {
            EnumCameraMode.ThirdPerson => CameraPerspective.ThirdPerson,
            EnumCameraMode.Overhead => CameraPerspective.Overhead,
            _ => CameraPerspective.FirstPerson,
        };

        // The game's pitch is PI when looking straight ahead and grows downwards, its yaw grows when turning left.
        context.Pitch = (camera.Pitch - Math.PI) * MathUtils.RadToDeg;
        context.Yaw = -camera.Yaw * MathUtils.RadToDeg;

        context.PositionX = entity.Pos.X;
        context.PositionY = entity.Pos.InternalY + entity.LocalEyePos.Y;
        context.PositionZ = entity.Pos.Z;

        Effects.OnCameraUpdate(context, Math.Clamp(deltaTime, 0f, MaxFrameTime));

        return Effects.OffsetPitch != 0.0 || Effects.OffsetYaw != 0.0 || Effects.OffsetRoll != 0.0;
    }

    #endregion

    #region Screen shake sources

    internal void OnExplosion(double x, double y, double z, float soundRange)
    {
        // CreateExplosion plays its sound with a range of 24 * sqrt(destruction radius).
        // The original mod shook the screen within 10x the explosion radius.
        double destructionRadius = Math.Pow(soundRange / 24.0, 2.0);

        var shake = Effects.Shakes.CreateDirect();
        shake.SetPosition(x, y, z);
        shake.Radius = (float)Math.Clamp(destructionRadius * 10.0, 16.0, 160.0);
        shake.Trauma = Config.General.ExplosionTrauma;
        shake.LengthInSeconds = 2f;
    }

    internal void OnLightningStrike(double x, double y, double z)
    {
        var explosion = Effects.Shakes.CreateDirect();
        explosion.SetPosition(x, y, z);
        explosion.Radius = 16f;
        explosion.Trauma = Config.General.ExplosionTrauma;
        explosion.LengthInSeconds = 3f;

        var thunder = Effects.Shakes.CreateDirect();
        thunder.SetPosition(x, y, z);
        thunder.Radius = 192f;
        thunder.Trauma = Config.General.ThunderTrauma;
        thunder.Frequency = 0.5f;
        thunder.LengthInSeconds = 7f;
    }

    internal void OnHandSwing()
    {
        handSwingShakeHandle = Effects.Shakes.Recreate(handSwingShakeHandle);

        var shake = Effects.Shakes.Get(handSwingShakeHandle);
        shake.Trauma = Config.General.HandSwingTrauma;
        shake.Frequency = 0.5f;
        shake.LengthInSeconds = 0.5f;

        Effects.NotifyOfPlayerAction();
    }

    /// <summary> Plays a positionless shake, used by the settings screens to preview trauma values. </summary>
    internal void PreviewShake(float trauma, float frequency, float lengthInSeconds)
    {
        var shake = Effects.Shakes.CreateDirect();
        shake.Trauma = trauma;
        shake.Frequency = frequency;
        shake.LengthInSeconds = lengthInSeconds;
    }

    #endregion

    #region Commands

    private void RegisterCommands(ICoreClientAPI api)
    {
        api.ChatCommands.Create("dcam")
            .WithDescription(Lang.Get($"{Domain}:command-description"))
            .RequiresPrivilege(Privilege.chat)
            .BeginSubCommand("reload")
                .WithDescription(Lang.Get($"{Domain}:command-reload-description"))
                .HandleWith(_ =>
                {
                    ReloadConfig();
                    return TextCommandResult.Success(Lang.Get($"{Domain}:command-reload-done", DynamicCameraConfig.FileName));
                })
            .EndSubCommand()
            .BeginSubCommand("toggle")
                .WithDescription(Lang.Get($"{Domain}:command-toggle-description"))
                .HandleWith(_ =>
                {
                    Config.General.Enabled = !Config.General.Enabled;
                    SaveConfig();
                    settingsUi?.OnConfigReplaced();
                    return TextCommandResult.Success(Lang.Get(Config.General.Enabled ? $"{Domain}:command-toggle-on" : $"{Domain}:command-toggle-off"));
                })
            .EndSubCommand()
            .BeginSubCommand("settings")
                .WithDescription(Lang.Get($"{Domain}:command-settings-description"))
                .HandleWith(_ =>
                {
                    if (settingsUi == null)
                    {
                        return TextCommandResult.Error(Lang.Get($"{Domain}:command-settings-unavailable", DynamicCameraConfig.FileName));
                    }

                    settingsUi.Open();
                    return TextCommandResult.Success();
                })
            .EndSubCommand();
    }

    #endregion
}
