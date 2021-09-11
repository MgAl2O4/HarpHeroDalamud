using Dalamud.Game.ClientState.Keys;
using Melanchall.DryWetMidi.Interaction;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace HarpHero
{
    public class UINoteMapper
    {
        // UI keys are ordered: C+1, B, A, G, F, E, D, C, A#, G#, F#, D#, C#
        // maps are indexed with notes from music track: low C -> high C
        public struct NoteInfo
        {
            public int uiIndex;
            public bool isHalfStep;
            public string name;
        }
        public struct NoteMap
        {
            public int uiIndex;
            public int octaveIdx;
        }

        public readonly NoteInfo[] octaveNotes = {
            new NoteInfo() { name = "C", isHalfStep = false, uiIndex = 6 },
            new NoteInfo() { name = "C#", isHalfStep = true, uiIndex = 11 },
            new NoteInfo() { name = "D", isHalfStep = false, uiIndex = 5 },
            new NoteInfo() { name = "D#", isHalfStep = true, uiIndex = 10 },
            new NoteInfo() { name = "E", isHalfStep = false, uiIndex = 4 },
            new NoteInfo() { name = "F", isHalfStep = false, uiIndex = 3 },
            new NoteInfo() { name = "F#", isHalfStep = true, uiIndex = 9 },
            new NoteInfo() { name = "G", isHalfStep = false, uiIndex = 2 },
            new NoteInfo() { name = "G#", isHalfStep = true, uiIndex = 8 },
            new NoteInfo() { name = "A", isHalfStep = false, uiIndex = 1 },
            new NoteInfo() { name = "A#", isHalfStep = true, uiIndex = 7 },
            new NoteInfo() { name = "B", isHalfStep = false, uiIndex = 0 },
        };
        public const int numNotesWide = 12 + 12 + 12 + 1;
        public const int numNotesShort = 12 + 1;

        private int midOctaveIdx;
        private int midOctaveLowC = 0;
        public NoteMap[] notes = null;
        public bool isWideMode = false;

        private PerformanceBindingInfo? keyBinds;
        private Dictionary<int, string> mapNoteBindingDesc = new();
        private Dictionary<VirtualKey, string> mapBindingDesc = new();

        public void Update(UIStateBardPerformance uiState)
        {
            if (uiState != null && (uiState.keys.Count == numNotesShort || uiState.keys.Count == numNotesWide))
            {
                isWideMode = uiState.keys.Count == numNotesWide;
                midOctaveLowC = isWideMode ? 12 : 0;

                int expectedNumNotes = isWideMode ? numNotesWide : numNotesShort;
                if (notes == null || notes.Length != expectedNumNotes)
                {
                    // iter from high to low, mapNotes is indexes from low to high
                    notes = new NoteMap[expectedNumNotes];

                    // higest C
                    int writeIdx = expectedNumNotes - 1;
                    notes[writeIdx] = new NoteMap() { octaveIdx = 0, uiIndex = 0 };
                    writeIdx--;
                    //PluginLog.Log($"[{expectedNumNotes - 1}] = ui:0, note:0");

                    // octave(s)
                    int noteIdx = 1;
                    while (noteIdx < expectedNumNotes)
                    {
                        for (int idx = octaveNotes.Length - 1; idx >= 0; idx--)
                        {
                            //PluginLog.Log($"[{writeIdx}] = ui:{noteIdx + mapOctaveNotes[idx].uiIndex}, note:{idx}");
                            notes[writeIdx] = new NoteMap() { octaveIdx = idx, uiIndex = noteIdx + octaveNotes[idx].uiIndex };
                            writeIdx--;
                        }

                        noteIdx += octaveNotes.Length;
                    }
                }
            }
            else
            {
                Clear();
            }
        }

        public void Clear()
        {
            midOctaveLowC = 0;
            notes = null;
            isWideMode = false;
        }

        public void OnTrackChanged(TrackAssistant trackAssistant)
        {
            midOctaveIdx = trackAssistant.midOctaveIdx;
        }

        public void OnKeyBindsSet(PerformanceBindingInfo? keyBinds)
        {
            this.keyBinds = keyBinds;
            mapNoteBindingDesc.Clear();
        }

        public bool GetMappedNoteIdx(Note note, out int mappedNoteIdx, out int octaveOffset)
        {
            if (notes == null || note == null)
            {
                mappedNoteIdx = 0;
                octaveOffset = 0;
                return false;
            }

            int noteOctaveIdx = note.Octave;
            int noteName = (int)note.NoteName;

            octaveOffset = 0;
            mappedNoteIdx = noteName + midOctaveLowC + (12 * (noteOctaveIdx - midOctaveIdx));
            if (mappedNoteIdx < 0)
            {
                octaveOffset = -1;
                mappedNoteIdx += 12;
            }
            else if (mappedNoteIdx >= notes.Length)
            {
                octaveOffset = 1;
                mappedNoteIdx -= 12;
            }

            return mappedNoteIdx >= 0 && mappedNoteIdx < notes.Length;
        }

        public string GetNoteKeyBinding(Note note)
        {
            int noteNumber = note.NoteNumber;
            if (mapNoteBindingDesc.TryGetValue(noteNumber, out string noteBindingDesc))
            {
                return noteBindingDesc;
            }

            if (!keyBinds.HasValue || !GetMappedNoteIdx(note, out int mappedNoteIdx, out int octaveOffset))
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
            if (isWideMode)
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
