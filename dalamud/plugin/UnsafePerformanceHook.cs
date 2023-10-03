using Dalamud.Hooking;
using Dalamud.Memory;
using System;

namespace HarpHero
{
    public class UnsafePerformanceHook : IDisposable
    {
        public delegate void OnNotePlayedDelegate(ulong agentPtr, int noteIdx, byte state);
        private readonly Hook<OnNotePlayedDelegate> hookNote;

        public Action<int> OnPlayingNoteChanged;

        public bool IsValid = false;
        private bool isDisposed = false;

        private int lastPressedNote = 0;
        private int activeNote = 0;

        public UnsafePerformanceHook()
        {
            // break on write in agent's memory, when playing notes
            // parent seems to be UI message procesing handler

            try
            {
                hookNote = Service.interOp.HookFromSignature<OnNotePlayedDelegate>("48 89 5c 24 08 48 89 74 24 10 57 48 83 ec 20 8b fa 41 0f b6 f0 03 79 5c 48 8b d9", OnNoteDetour);
                hookNote.Enable();

                IsValid = true;
            }
            catch (Exception ex)
            {
                Service.logger.Error(ex, "oh noes!");
            }

            Plugin.OnDebugSnapshot += (_) =>
            {
                Service.logger.Info($"UnsafePerformanceHook: valid:{IsValid}, note:{activeNote}, lastPressed:{lastPressedNote}");
            };
        }

        public void Dispose()
        {
            if (!isDisposed)
            {
                hookNote?.Dispose();
                isDisposed = true;
                IsValid = false;
            }
        }

        public void OnNoteDetour(ulong agentPtr, int noteIdx, byte state)
        {
            hookNote.Original(agentPtr, noteIdx, state);

            if (state != 0)
            {
                // agent + 5c is part of function sig, should be safe to read from
                int octaveOffset = MemoryHelper.Read<int>(new IntPtr((long)agentPtr + 0x5c));
                noteIdx += octaveOffset;

                // +9 to match midi library note indices
                noteIdx += 9;

                lastPressedNote = noteIdx;
            }
            else
            {
                noteIdx = 0;
            }

            activeNote = noteIdx;
            OnPlayingNoteChanged?.Invoke(noteIdx);
        }
    }
}
