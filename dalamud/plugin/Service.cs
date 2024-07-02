using Dalamud.Game;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace HarpHero
{
    internal class Service
    {
        public static Plugin plugin = null!;
        public static IDalamudPluginInterface pluginInterface = null!;
        public static Configuration config = null!;
        public static TrackAssistant trackAssistant = null!;

        [PluginService]
        public static ICommandManager commandManager { get; private set; } = null!;

        [PluginService]
        public static IToastGui toastGui { get; private set; } = null!;

        [PluginService]
        public static IGameInteropProvider interOp { get; private set; } = null!;

        [PluginService]
        public static ISigScanner sigScanner { get; private set; } = null!;

        [PluginService]
        public static IFramework framework { get; private set; } = null!;

        [PluginService]
        public static IGameGui gameGui { get; private set; } = null!;

        [PluginService]
        public static IPluginLog logger { get; private set; } = null!;

        [PluginService]
        public static IGameConfig gameConfig { get; private set; } = null!;
    }
}
