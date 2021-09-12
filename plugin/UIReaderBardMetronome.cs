using Dalamud.Game.Gui;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System;
using System.Runtime.InteropServices;

namespace HarpHero
{
    public class UIReaderBardMetronome
    {
        [StructLayout(LayoutKind.Explicit, Size = 0x80)]
        public unsafe struct AgentData
        {
            //[FieldOffset(0x60)] public int LastWriteBeatLengthUs;
            [FieldOffset(0x68)] public int CurrentBeatUs;
            [FieldOffset(0x71)] public byte CurrentBeat;
            //[FieldOffset(0x72)] public byte LastWriteNumBeatsPerMeasure;
            [FieldOffset(0x73)] public byte IsPlaying;
            [FieldOffset(0x78)] public int CurrentBar;

            // LastWrite* are not usable (not initialized, write defaults on window close)
        }

        private GameGui gameGui;
        private IntPtr cachedAddonPtr;
        private IntPtr cachedAgentPtr;

        public IntPtr AgentPtr => cachedAgentPtr;

        public UIReaderBardMetronome(GameGui gameGui)
        {
            this.gameGui = gameGui;
        }

        public unsafe void Update()
        {
            IntPtr addonPtr = gameGui.GetAddonByName("PerformanceMetronome", 1);

            var baseNode = (AtkUnitBase*)addonPtr;
            if (baseNode != null && baseNode->RootNode != null && baseNode->RootNode->IsVisible)
            {
                if (cachedAddonPtr != addonPtr)
                {
                    cachedAddonPtr = addonPtr;
                    cachedAgentPtr = gameGui.FindAgentInterface(addonPtr);
                }
            }
            else
            {
                cachedAddonPtr = IntPtr.Zero;
                cachedAgentPtr = IntPtr.Zero;
            }
        }
    }
}
