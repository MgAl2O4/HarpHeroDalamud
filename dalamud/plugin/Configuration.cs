﻿using Dalamud.Configuration;
using Dalamud.Game.ClientState.Keys;
using MgAl2O4.Utils;
using System;
using System.Collections.Generic;

namespace HarpHero
{
    [Serializable]
    public class Configuration : IPluginConfiguration
    {
        public int Version { get; set; } = 0;

        public bool AutoAdjustEndBar { get; set; } = true;
        public bool AutoAdjustBPM { get; set; } = true;
        public bool UseExtendedMode { get; set; } = false;
        public float AutoAdjustSpeedThreshold { get; set; } = 2.0f;
        public int MinNoteDurationMs { get; set; } = 101;

        public bool UseMetronomeLink { get; set; } = true;
        public bool UsePlayback { get; set; } = true;
        public bool AutoResume { get; set; } = false;
        public int AssistMode { get; set; } = 2;

        public int AssistNote2Markers { get; set; } = 1;
        public int AssistNote2WarnMs { get; set; } = 100;
        public bool AssistNote2Enabled { get; set; } = true;
        public float AssistBindScaleKeyboard { get; set; } = 1.0f;
        public float AssistBindScaleGamepad { get; set; } = 1.5f;
        public int AssistBindRows { get; set; } = 3;
        public int AssistBindExtraHints { get; set; } = 2;
        public bool AssistAllNotes { get; set; } = false;

        public bool UseTrainingMode { get; set; } = true;
        public bool ShowScore { get; set; } = true;
        public bool ShowOverlay { get; set; } = true;

        public List<Tuple<ushort, string>> VKAlias { get; set; } = new();
        public float AssistBgAlpha = 1.0f;

        public void Initialize()
        {
            ApplyVKAliases();
        }

        public void Save()
        {
            Service.pluginInterface.SavePluginConfig(this);
        }

        public bool UseAssistNote() => (AssistMode == 1) || (AssistMode == 3);
        public bool UseAssistBind() => AssistMode == 2;
        public bool UseAssistNoteMarkers() => (AssistMode == 1) && AssistNote2Enabled;
        public bool UseAssistNoteMixed() => (AssistMode == 3);

        public void ApplyVKAliases()
        {
            InputBindingUtils.mapVKAliases.Clear();
            foreach (var alias in VKAlias)
            {
                var vk = (VirtualKey)alias.Item1;
                InputBindingUtils.mapVKAliases.TryAdd(vk, alias.Item2);
            }
        }
    }
}
