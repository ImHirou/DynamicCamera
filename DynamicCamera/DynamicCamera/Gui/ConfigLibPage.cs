using System;
using ConfigLib;
using Vintagestory.API.Client;

namespace DynamicCamera.Gui;

internal sealed class ConfigLibPage : IDisposable
{
    private readonly ConfigLibModSystem configLib;
    private readonly SettingsEditor editor;

    private ConfigLibPage(ConfigLibModSystem configLib, SettingsEditor editor)
    {
        this.configLib = configLib;
        this.editor = editor;
    }

    public static ConfigLibPage? Register(ICoreClientAPI api, SettingsEditor editor)
    {
        var configLib = api.ModLoader.GetModSystem<ConfigLibModSystem>();

        if (configLib == null)
        {
            return null;
        }

        var page = new ConfigLibPage(configLib, editor);
        configLib.RegisterCustomConfig(DynamicCameraModSystem.Domain, new Func<string, ControlButtons, ControlButtons>(page.Draw));
        configLib.ConfigWindowClosed += page.OnConfigWindowClosed;
        return page;
    }

    public void Dispose()
    {
        configLib.ConfigWindowClosed -= OnConfigWindowClosed;
    }

    private ControlButtons Draw(string id, ControlButtons pressed)
    {
        if (pressed.Save)
        {
            editor.Save();
        }

        if (pressed.Restore)
        {
            editor.Revert();
        }

        if (pressed.Defaults)
        {
            editor.ResetToDefaults();
        }

        editor.Draw(showFileButtons: false);

        // Settings apply live, so "Reload" has nothing to do.
        return new ControlButtons { Save = true, Restore = true, Defaults = true, Reload = false };
    }

    private void OnConfigWindowClosed()
    {
        editor.SaveIfChanged();
    }
}
