using Dalamud.Data;
using Dalamud.Game.Command;
using Dalamud.Plugin;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Game;
using Dalamud.Game.ClientState;

namespace DesynthesisTwo; 

public sealed class Plugin : IDalamudPlugin
{
    public string Name => "Desynthesis 2";

    private const string CommandName = "/desynthesis";
    private readonly string[] commandAlias = { "/desynthesistwo", "/desynthesis2", "/desynth2", "/desynth"};

    [PluginService] public static DalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] public static CommandManager CommandManager { get; private set; } = null!;
    [PluginService] public static DataManager Data { get; private set; } = null!;
    [PluginService] public static SigScanner SigScanner { get; private set; } = null!;
    [PluginService] public static ClientState ClientState { get; private set; } = null!;
    
    public static Configuration Config { get; private set; } = null!;

    public static WindowSystem WindowSystem { get; private set; } = null!;

    public readonly DesynthesisWindow DesynthesisWindow;
    public readonly SettingsWindow SettingsWindow;

    public static IconManager IconManager { get; } = new();


    public Plugin() {
            
        Config = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        
        
        WindowSystem = new WindowSystem(Name);
            
        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand) {
            HelpMessage = $"Open the {Name} window.",
            ShowInHelp = true
        });
        foreach (var a in commandAlias) {
            CommandManager.AddHandler(a, new CommandInfo(OnCommand) {
                HelpMessage = $"Open the {Name} window.",
                ShowInHelp = false
            });
        }
        
        WindowSystem.AddWindow(DesynthesisWindow = new DesynthesisWindow(this));
        WindowSystem.AddWindow(SettingsWindow = new SettingsWindow(this));
        
        
        #if DEBUG
        DesynthesisWindow.IsOpen = true;
        #endif



        PluginInterface.UiBuilder.Draw += WindowSystem.Draw;
    }

    public static void SaveConfig() {
        PluginInterface.SavePluginConfig(Config);
    }

    public void Dispose() {
        PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;
        WindowSystem.RemoveAllWindows();
        CommandManager.RemoveHandler(CommandName);
        foreach (var a in commandAlias) CommandManager.RemoveHandler(a);
        IconManager.Dispose();
    }
        

        
    private void OnCommand(string command, string args) {
        DesynthesisWindow.IsOpen = !DesynthesisWindow.IsOpen;
    }
    
}