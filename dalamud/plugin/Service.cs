using Dalamud.Game;
using Dalamud.Game.Command;
using Dalamud.Game.Gui;
using Dalamud.Game.Gui.Toast;
using Dalamud.IoC;
using Dalamud.Plugin;

namespace HarpHero
{
    internal class Service
    {
        public static Plugin plugin;
        public static Configuration config;
        public static TrackAssistant trackAssistant;

        [PluginService]
        public static DalamudPluginInterface pluginInterface { get; private set; } = null!;

        [PluginService]
        public static CommandManager commandManager { get; private set; } = null!;

        [PluginService]
        public static ToastGui toastGui { get; private set; } = null!;

        [PluginService]
        public static SigScanner sigScanner { get; private set; } = null!;

        [PluginService]
        public static Framework framework { get; private set; } = null!;

        [PluginService]
        public static GameGui gameGui { get; private set; } = null!;
    }
}
