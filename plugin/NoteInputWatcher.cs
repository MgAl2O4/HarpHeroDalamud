using Dalamud.Game.ClientState.Keys;
using Melanchall.DryWetMidi.Interaction;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace HarpHero
{
    public class NoteInputWatcher
    {
        private readonly NoteUIMapper noteMapper;
        private readonly KeyState keyState;

        private PerformanceBindingInfo? keyBinds;
        private Dictionary<int, string> mapNoteBindingDesc = new();
        private Dictionary<VirtualKey, string> mapBindingDesc = new();
        private bool isWideModeCached = false;

        public NoteInputWatcher(NoteUIMapper noteMapper, KeyState keyState)
        {
            this.noteMapper = noteMapper;
            this.keyState = keyState;
        }

        public void OnKeyBindsSet(PerformanceBindingInfo? keyBinds)
        {
            this.keyBinds = keyBinds;

            mapNoteBindingDesc.Clear();
            isWideModeCached = noteMapper.isWideMode;
        }

        public int GetActiveOctaveOffset()
        {
            int offset = 0;

            if (keyBinds.HasValue)
            {
                bool isOctaveUpPressed = keyState[noteMapper.isWideMode ? keyBinds.Value.threeOctaves.octaveUp : keyBinds.Value.singleOctave.octaveUp];
                bool isOctaveDownPressed = keyState[noteMapper.isWideMode ? keyBinds.Value.threeOctaves.octaveDown : keyBinds.Value.singleOctave.octaveDown];

                if (isOctaveUpPressed && !isOctaveDownPressed)
                {
                    offset = 1;
                }
                else if (isOctaveDownPressed && !isOctaveUpPressed)
                {
                    offset = -1;
                }
            }

            return offset;
        }

        public string GetNoteKeyBinding(Note note)
        {
            if (isWideModeCached != noteMapper.isWideMode)
            {
                mapNoteBindingDesc.Clear();
                isWideModeCached = noteMapper.isWideMode;
            }

            int noteNumber = note.NoteNumber;
            if (mapNoteBindingDesc.TryGetValue(noteNumber, out string noteBindingDesc))
            {
                return noteBindingDesc;
            }

            if (!keyBinds.HasValue || !noteMapper.GetMappedNoteIdx(note, out int mappedNoteIdx, out int octaveOffset))
            {
                return null;
            }

            string GetNoteOctaveBindingDesc(PerformanceBindingInfo.Mode modeBindings, int useNoteIdx, int useOctaveOffset)
            {
                var noteKey = modeBindings.notes[useNoteIdx];
                bool hasBindings = noteKey != VirtualKey.NO_KEY;

                var octaveKey = VirtualKey.NO_KEY;
                if (useOctaveOffset > 0)
                {
                    octaveKey = modeBindings.octaveUp;
                    hasBindings = hasBindings && (octaveKey != VirtualKey.NO_KEY);
                }
                else if (useOctaveOffset < 0)
                {
                    octaveKey = modeBindings.octaveDown;
                    hasBindings = hasBindings && (octaveKey != VirtualKey.NO_KEY);
                }

                if (hasBindings)
                {
                    string desc = "";
                    if (octaveKey != VirtualKey.NO_KEY)
                    {
                        desc += GetVirtualKeyDesc(octaveKey) + " ";
                    }

                    desc += GetVirtualKeyDesc(noteKey);
                    return desc;
                }

                return null;
            }

            string desc = null;
            if (isWideModeCached)
            {
                desc = GetNoteOctaveBindingDesc(keyBinds.Value.threeOctaves, mappedNoteIdx, octaveOffset);
                if (desc == null && octaveOffset == 0)
                {
                    // no binds: lower/higher octave? try using center + offset
                    if (mappedNoteIdx < 12)
                    {
                        desc = GetNoteOctaveBindingDesc(keyBinds.Value.threeOctaves, mappedNoteIdx + 12, -1);
                    }
                    else if (mappedNoteIdx >= 12 + 12)
                    {
                        desc = GetNoteOctaveBindingDesc(keyBinds.Value.threeOctaves, mappedNoteIdx - 12, 1);
                    }
                }
            }
            else
            {
                desc = GetNoteOctaveBindingDesc(keyBinds.Value.singleOctave, mappedNoteIdx, octaveOffset);
            }

            mapNoteBindingDesc.Add(noteNumber, desc);
            return desc;
        }

        public string GeOctaveKeyBinding(int octaveOffset)
        {
            if (!keyBinds.HasValue || octaveOffset == 0)
            {
                return null;
            }

            var octaveKey = noteMapper.isWideMode ?
                ((octaveOffset > 0) ? keyBinds.Value.threeOctaves.octaveUp : keyBinds.Value.threeOctaves.octaveDown) :
                ((octaveOffset > 0) ? keyBinds.Value.singleOctave.octaveUp : keyBinds.Value.singleOctave.octaveDown);

            return (octaveKey == VirtualKey.NO_KEY) ? null : GetVirtualKeyDesc(octaveKey);
        }

        private string GetVirtualKeyDesc(VirtualKey key)
        {
            if (mapBindingDesc.TryGetValue(key, out string desc))
            {
                return desc;
            }

            var resultDesc = new StringBuilder();
            uint scanCode = MapVirtualKey((uint)key, 0);
            int lParam = (int)(scanCode << 16);

            GetKeyNameText(lParam, resultDesc, 260);
            desc = resultDesc.ToString();

            mapBindingDesc.Add(key, desc);
            return desc;
        }

        [DllImport("user32.dll")]
        static extern uint MapVirtualKey(uint uCode, uint uMapType);

        [DllImport("user32.dll")]
        static extern int GetKeyNameText(int lParam, [Out] StringBuilder lpString, int nSize);
    }
}
