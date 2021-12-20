using MgAl2O4.Utils;
using System;
using System.Runtime.InteropServices;

namespace HarpHero
{
    public class UIReaderBardMetronome : IUIReader
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

        private IntPtr cachedAgentPtr;

        public UnsafeMetronomeLink updateNotify;
        public IntPtr AgentPtr => cachedAgentPtr;

        public UIReaderBardMetronome()
        {
            Plugin.OnDebugSnapshot += (_) =>
            {
                Dalamud.Logging.PluginLog.Log($"UIReaderBardMetronome: active:{AgentPtr != IntPtr.Zero}");
            };
        }

        public string GetAddonName()
        {
            return "PerformanceMetronome";
        }

        public void OnAddonLost()
        {
            cachedAgentPtr = IntPtr.Zero;
            updateNotify.Update();
        }

        public void OnAddonShown(IntPtr addonPtr)
        {
            cachedAgentPtr = Service.gameGui.FindAgentInterface(addonPtr);
            // don't update now, will get OnAddonUpdate() in the same tick
        }

        public void OnAddonUpdate(IntPtr addonPtr)
        {
            updateNotify.Update();
        }
    }
}
