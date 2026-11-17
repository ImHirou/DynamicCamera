using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using ImGuiNET;
using Vintagestory.API.Client;
using Vintagestory.API.Config;
using VSImGui;
using VSImGui.API;

namespace DynamicCamera.Gui;

/// <summary>
/// Standalone Dear ImGui settings window (hotkey or <c>.dcam settings</c>), plus the Config lib page when that mod is present.
/// Requires the vsimgui mod: only construct it through <see cref="GuiIntegration"/>.
/// </summary>
internal sealed class ImGuiSettingsUi : ISettingsUi
{
    public const string HotkeyCode = "dynamiccamera-settings";

    private readonly ImGuiModSystem imgui;
    private readonly SettingsEditor editor;
    private readonly IDisposable? configLibPage;
    private bool open;

    public ImGuiSettingsUi(DynamicCameraModSystem mod, ICoreClientAPI api, bool withConfigLib)
    {
        editor = new SettingsEditor(mod);
        imgui = api.ModLoader.GetModSystem<ImGuiModSystem>() ?? throw new InvalidOperationException("ImGuiModSystem is not loaded.");
        imgui.Draw += DrawWindow;
        imgui.Closed += OnAllWindowsClosed;

        api.Input.RegisterHotKey(HotkeyCode, Lang.Get(DynamicCameraModSystem.Domain + ":hotkey-settings"), GlKeys.O,
            HotkeyType.GUIOrOtherControls, ctrlPressed: true, shiftPressed: true);
        api.Input.SetHotKeyHandler(HotkeyCode, OnHotkey);

        if (withConfigLib)
        {
            configLibPage = RegisterConfigLibPage(mod, api, editor);
        }
    }

    public void Open()
    {
        open = true;
        imgui.Show();
    }

    public void OnConfigReplaced()
    {
        editor.HasUnsavedChanges = false;
    }

    public void Dispose()
    {
        imgui.Draw -= DrawWindow;
        imgui.Closed -= OnAllWindowsClosed;
        configLibPage?.Dispose();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static IDisposable? RegisterConfigLibPage(DynamicCameraModSystem mod, ICoreClientAPI api, SettingsEditor editor)
    {
        try
        {
            return ConfigLibPage.Register(api, editor);
        }
        catch (Exception e)
        {
            mod.Mod.Logger.Warning("Failed to register the Config lib page: {0}", e);
            return null;
        }
    }

    private bool OnHotkey(KeyCombination keys)
    {
        if (open)
        {
            Close();
        }
        else
        {
            Open();
        }

        return true;
    }

    private void Close()
    {
        open = false;
        editor.SaveIfChanged();
    }

    private void OnAllWindowsClosed()
    {
        if (open)
        {
            Close();
        }
    }

    private CallbackGUIStatus DrawWindow(float deltaSeconds)
    {
        if (!open)
        {
            return CallbackGUIStatus.Closed;
        }

        var flags = editor.HasUnsavedChanges ? ImGuiWindowFlags.UnsavedDocument : ImGuiWindowFlags.None;
        ImGui.SetNextWindowSize(new Vector2(560f, 680f), ImGuiCond.FirstUseEver);

        // "###" keeps the window identity stable regardless of the translated title.
        if (ImGui.Begin(Lang.Get(DynamicCameraModSystem.Domain + ":gui-title") + "###dynamiccamera-settings", ref open, flags))
        {
            editor.Draw(showFileButtons: true);
        }

        ImGui.End();

        if (!open)
        {
            Close();
            return CallbackGUIStatus.Closed;
        }

        return CallbackGUIStatus.GrabMouse;
    }
}
