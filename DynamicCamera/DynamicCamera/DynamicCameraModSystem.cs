using System;
using System.IO;
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
using GameCamera = Vintagestory.Client.NoObf.Camera;

namespace DynamicCamera;

/// <summary>
/// Client-side entry point. Port of CameraOverhaul by Mirsario to Vintage Story.
/// </summary>
public sealed class DynamicCameraModSystem : ModSystem
{
    public const string Domain = "dynamiccamera";
    public const string ToggleHotkeyCode = "dynamiccamera-toggle";
    public const string BrokenConfigFileName = DynamicCameraConfig.FileName + ".broken";

    // Vintage Story stores velocity in blocks per 1/60 s, the original's factors expect blocks per 1/20 s.
    private const double VelocityScale = 3.0;
    private const float MaxFrameTime = 0.5f;
    // Entity.ReceiveDamage stores the yaw of the knockback direction here, or -999 if the damage had no source position.
    private const string HurtDirectionAttribute = "onHurtDir";
    private const float NoHurtDirection = -999f;

    private readonly CameraContext context = new();
    private readonly Random random = new();
    private ICoreClientAPI? capi;
    private Harmony? harmony;
    private ISettingsUi? settingsUi;
    private long handSwingShakeHandle;
    private float pendingHurtDamage;
    // The camera mode the player picked. IClientPlayer.CameraMode also reports the first-person
    // fallback the game switches to while the third-person camera is pushed into a wall.
    private AccessTools.FieldRef<GameCamera, EnumCameraMode>? selectedCameraMode;

    /// <summary> The running client instance, used by the Harmony patches. </summary>
    internal static DynamicCameraModSystem? ClientInstance { get; private set; }

    internal DynamicCameraConfig Config { get; private set; } = new();

    internal CameraSystem Effects { get; private set; } = null!;

    public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Client;

    public override void StartClientSide(ICoreClientAPI api)
    {
        capi = api;
        ClientInstance = this;

        if (!TryLoadConfig(out var config))
        {
            api.Event.LevelFinalize += () => api.ShowChatMessage(Lang.Get($"{Domain}:config-load-failed", DynamicCameraConfig.FileName, BrokenConfigFileName));
        }

        Config = config;
        Effects = new CameraSystem(() => Config);
        selectedCameraMode = TryGetCameraModeField();

        harmony = new Harmony(Mod.Info.ModID);
        if (CameraPatches.Apply(harmony, Mod.Logger))
        {
            AimPatches.Apply(harmony, Mod.Logger);
        }

        ShakeSourcePatches.Apply(harmony, Mod.Logger);
        VanillaShakePatches.Apply(harmony, Mod.Logger);

        settingsUi = GuiIntegration.TryCreate(this, api);
        RegisterHotkeys(api);
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

    private AccessTools.FieldRef<GameCamera, EnumCameraMode>? TryGetCameraModeField()
    {
        try
        {
            return AccessTools.FieldRefAccess<GameCamera, EnumCameraMode>("CameraMode");
        }
        catch (Exception e)
        {
            Mod.Logger.Warning("Could not find Camera.CameraMode, the perspective is read from the player instead: {0}", e.Message);
            return null;
        }
    }

    #region Config

    /// <summary>
    /// Reads the config file, upgrading older versions. If the file cannot be read, a copy of it is kept as
    /// <see cref="BrokenConfigFileName"/> and false is returned along with the default settings.
    /// </summary>
    private bool TryLoadConfig(out DynamicCameraConfig config)
    {
        DynamicCameraConfig? loaded;

        try
        {
            loaded = capi!.LoadModConfig<DynamicCameraConfig>(DynamicCameraConfig.FileName);
        }
        catch (Exception e)
        {
            Mod.Logger.Error("Failed to read ModConfig/{0}, a copy is kept as {1}: {2}", DynamicCameraConfig.FileName, BrokenConfigFileName, e.Message);
            // Saving the settings later overwrites the file, so keep the user's edits around.
            BackupConfigFile(BrokenConfigFileName);
            config = new DynamicCameraConfig();
            return false;
        }

        if (loaded == null)
        {
            loaded = new DynamicCameraConfig();
        }
        else if (loaded.ConfigVersion != DynamicCameraConfig.CurrentVersion)
        {
            string backup = $"{DynamicCameraConfig.FileName}.v{loaded.ConfigVersion}.bak";
            Mod.Logger.Notification("Converting ModConfig/{0} from version {1}, a copy is kept as {2}.", DynamicCameraConfig.FileName, loaded.ConfigVersion, backup);
            BackupConfigFile(backup);
            loaded.Migrate(loaded.ConfigVersion);
        }

        loaded.Sanitize();
        loaded.ConfigVersion = DynamicCameraConfig.CurrentVersion;
        // Writes back newly added settings.
        SaveConfig(loaded);
        config = loaded;
        return true;
    }

    private void BackupConfigFile(string backupFileName)
    {
        try
        {
            string path = Path.Combine(GamePaths.ModConfig, DynamicCameraConfig.FileName);

            if (File.Exists(path))
            {
                File.Copy(path, Path.Combine(GamePaths.ModConfig, backupFileName), overwrite: true);
            }
        }
        catch (Exception e)
        {
            Mod.Logger.Warning("Failed to copy ModConfig/{0} to {1}: {2}", DynamicCameraConfig.FileName, backupFileName, e.Message);
        }
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

    /// <summary> Reads the config file again. Returns false and keeps the current settings if the file is broken. </summary>
    internal bool ReloadConfig()
    {
        if (!TryLoadConfig(out var config))
        {
            return false;
        }

        Config = config;
        settingsUi?.OnConfigReplaced();
        return true;
    }

    /// <summary> Replaces the settings with defaults without saving them. </summary>
    internal void ResetConfigToDefaults()
    {
        Config = new DynamicCameraConfig();
    }

    internal void ToggleEnabled()
    {
        Config.General.Enabled = !Config.General.Enabled;
        SaveConfig();
        settingsUi?.OnConfigReplaced();
    }

    private string ToggleMessage() => Lang.Get(Config.General.Enabled ? $"{Domain}:command-toggle-on" : $"{Domain}:command-toggle-off");

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
            pendingHurtDamage = 0f;
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
        context.IsFlying = controls.IsFlying;
        context.IsClimbing = controls.IsClimbing;
        context.IsSwimming = entity.Swimming;
        context.IsOnGround = entity.OnGround;
        context.IsSprinting = controls.Sprint && controls.TriesToMove && !controls.Sneak;
        context.HasMovementInput = controls.TriesToMove;

        var motion = (mountEntity ?? entity).Pos.Motion;
        context.VelocityX = motion.X * VelocityScale;
        context.VelocityY = motion.Y * VelocityScale;
        context.VelocityZ = motion.Z * VelocityScale;

        var cameraMode = selectedCameraMode != null ? selectedCameraMode(camera) : player.CameraMode;
        context.Perspective = cameraMode switch
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

        context.ShakeScale = Config.General.RespectGameShakeSetting ? Math.Clamp(ClientSettings.CameraShakeStrength, 0f, 1f) : 1.0;

        if (pendingHurtDamage > 0f)
        {
            ApplyHurtKick(entity, pendingHurtDamage);
            pendingHurtDamage = 0f;
        }

        Effects.OnCameraUpdate(context, Math.Clamp(deltaTime, 0f, MaxFrameTime));

        return Effects.OffsetPitch != 0.0 || Effects.OffsetYaw != 0.0 || Effects.OffsetRoll != 0.0;
    }

    #endregion

    #region Screen shake sources

    // CreateExplosion plays its sound with a range of 24 * sqrt(destruction radius).
    // The original mod shook the screen within 10x the explosion radius.
    private static float ExplosionShakeRadius(float soundRange)
    {
        double destructionRadius = Math.Pow(soundRange / 24.0, 2.0);
        return (float)Math.Clamp(destructionRadius * 10.0, 16.0, 160.0);
    }

    /// <summary> Called for every sound the client plays. <paramref name="path"/> is the sound's asset path. </summary>
    internal void OnSoundPlayed(string path, double x, double y, double z, float range)
    {
        if (!Config.General.Enabled || FindSoundShake(path) is not { } entry)
        {
            return;
        }

        var shake = Effects.Shakes.CreateDirect();

        // The game plays sounds at (0, 0, 0) relative to the listener.
        if (x != 0.0 || y != 0.0 || z != 0.0)
        {
            shake.SetPosition(x, y, z);
        }

        shake.Radius = entry.RadiusFromExplosionRange ? ExplosionShakeRadius(range) : Math.Min(entry.Radius, Math.Max(range, 1f));
        shake.Trauma = entry.Trauma;
        shake.Frequency = entry.Frequency;
        shake.LengthInSeconds = entry.LengthInSeconds;
    }

    private SoundShake? FindSoundShake(string path)
    {
        foreach (var entry in Config.SoundShakes)
        {
            // An empty pattern would match every sound.
            if (!string.IsNullOrWhiteSpace(entry.Sound) && entry.Trauma != 0f
                && path.Contains(entry.Sound, StringComparison.OrdinalIgnoreCase))
            {
                return entry;
            }
        }

        return null;
    }

    internal void OnLightningStrike(double x, double y, double z)
    {
        var strike = Effects.Shakes.CreateDirect();
        strike.SetPosition(x, y, z);
        strike.Radius = 16f;
        strike.Trauma = Config.General.LightningTrauma;
        strike.LengthInSeconds = 3f;

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

    internal void OnPlayerHurt(EntityPlayer player, float damage)
    {
        if (player.EntityId != capi?.World?.Player?.Entity?.EntityId)
        {
            return;
        }

        // The direction attribute may arrive after the damage one, so it is read on the next frame.
        pendingHurtDamage = Math.Max(pendingHurtDamage, damage);
    }

    private void ApplyHurtKick(EntityPlayer entity, float damage)
    {
        float direction = entity.WatchedAttributes.GetFloat(HurtDirectionAttribute, NoHurtDirection);

        if (direction <= NoHurtDirection + 1f)
        {
            // No source position (falling, drowning, ...): flinch to a random side.
            Effects.OnPlayerHurt(damage, random.Next(2) == 0 ? -1.0 : 1.0, -1.0);
            return;
        }

        // The direction is the yaw of the knockback vector (x = sin, z = cos), like the player's own yaw.
        double relative = entity.Pos.Yaw - direction;
        Effects.OnPlayerHurt(damage, Math.Sin(relative), Math.Cos(relative));
    }

    /// <summary> Plays a positionless shake, used by the settings screens to preview trauma values. </summary>
    internal void PreviewShake(float trauma, float frequency, float lengthInSeconds)
    {
        var shake = Effects.Shakes.CreateDirect();
        shake.Trauma = trauma;
        shake.Frequency = frequency;
        shake.LengthInSeconds = lengthInSeconds;
    }

    internal void PreviewLanding() => Effects.TriggerLanding(1.0);

    internal void PreviewHurt() => Effects.OnPlayerHurt(CameraSystem.HurtKickFullDamage, random.Next(2) == 0 ? -1.0 : 1.0, -1.0);

    #endregion

    #region Commands

    private void RegisterHotkeys(ICoreClientAPI api)
    {
        api.Input.RegisterHotKey(ToggleHotkeyCode, Lang.Get($"{Domain}:hotkey-toggle"), GlKeys.K,
            HotkeyType.GUIOrOtherControls, ctrlPressed: true, shiftPressed: true);
        api.Input.SetHotKeyHandler(ToggleHotkeyCode, _ =>
        {
            ToggleEnabled();
            api.ShowChatMessage(ToggleMessage());
            return true;
        });
    }

    private void RegisterCommands(ICoreClientAPI api)
    {
        api.ChatCommands.Create("dcam")
            .WithDescription(Lang.Get($"{Domain}:command-description"))
            .RequiresPrivilege(Privilege.chat)
            .BeginSubCommand("reload")
                .WithDescription(Lang.Get($"{Domain}:command-reload-description"))
                .HandleWith(_ => ReloadConfig()
                    ? TextCommandResult.Success(Lang.Get($"{Domain}:command-reload-done", DynamicCameraConfig.FileName))
                    : TextCommandResult.Error(Lang.Get($"{Domain}:command-reload-failed", DynamicCameraConfig.FileName, BrokenConfigFileName)))
            .EndSubCommand()
            .BeginSubCommand("toggle")
                .WithDescription(Lang.Get($"{Domain}:command-toggle-description"))
                .HandleWith(_ =>
                {
                    ToggleEnabled();
                    return TextCommandResult.Success(ToggleMessage());
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
