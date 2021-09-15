using Dalamud.Configuration;
using Dalamud.Plugin;
using System;

namespace HarpHero
{
    [Serializable]
    public class Configuration : IPluginConfiguration
    {
        public int Version { get; set; } = 0;

        public bool AutoAdjustEndBar { get; set; } = true;
        public bool AutoAdjustBPM { get; set; } = true;
        public float AutoAdjustSpeedThreshold { get; set; } = 2.0f;

        public bool UseMetronomeLink { get; set; } = true;
        public bool UsePlayback { get; set; } = true;
        public int AssistMode { get; set; } = 2;

        public bool UseTrainingMode { get; set; } = true;

        [NonSerialized]
        private DalamudPluginInterface pluginInterface;

        public void Initialize(DalamudPluginInterface pluginInterface)
        {
            this.pluginInterface = pluginInterface;
        }

        public void Save()
        {
            pluginInterface?.SavePluginConfig(this);
        }

        public bool UseAssistNote() => AssistMode == 1;
        public bool UseAssistBind() => AssistMode == 2;
    }
}
