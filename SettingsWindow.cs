using System.Numerics;
using Dalamud.Interface.Windowing;
using ImGuiNET;

namespace DesynthesisTwo; 

public class SettingsWindow : Window {

    private readonly Plugin plugin;

    public SettingsWindow(Plugin plugin, ImGuiWindowFlags flags = ImGuiWindowFlags.None) : base($"Settings###desynthesisTwoSettingsWindow", flags) {
        this.plugin = plugin;

        Flags = ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoTitleBar;

    }

    public override bool DrawConditions() {
        return plugin.DesynthesisWindow.IsOpen && plugin.DesynthesisWindow.IsDrawing && plugin.DesynthesisWindow.DrawConditions() && plugin.DesynthesisWindow.SettingsWindowOpen;
    }

    public override void PreDraw() {
        if (plugin.DesynthesisWindow.Position != null && plugin.DesynthesisWindow.Size != null) {
            PositionCondition = ImGuiCond.Always;
            Position = plugin.DesynthesisWindow.Position + plugin.DesynthesisWindow.Size * Vector2.UnitX;
        }
        base.PreDraw();
    }
    

    public override void Draw() {
        
        
        
        if (ImGui.Checkbox("Show real item icons", ref Plugin.Config.ActualIcons)) {
            Plugin.SaveConfig();
        }
        
        if (ImGui.Checkbox("Show decimals in skill", ref Plugin.Config.ShowDecimals)) {
            Plugin.SaveConfig();
        }
    }

    public override void PostDraw() {
        if (!(IsFocused || plugin.DesynthesisWindow.IsFocused)) {
            IsOpen = false;
            plugin.DesynthesisWindow.SettingsWindowOpen = false;
            return;
        }
        base.PostDraw();
    }

    public override void OnClose() {
        plugin.DesynthesisWindow.SettingsWindowOpen = false;
        base.OnClose();
    }
}
