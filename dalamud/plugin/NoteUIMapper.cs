using Melanchall.DryWetMidi.Interaction;

namespace HarpHero
{
    public class NoteUIMapper
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
        public NoteMap[]? notes = null;
        public bool isWideMode = false;

        public void OnNumKeysChanged(UIStateBardPerformance uiState)
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
                    //Service.logger.Info($"[{expectedNumNotes - 1}] = ui:0, note:0");

                    // octave(s)
                    int noteIdx = 1;
                    while (noteIdx < expectedNumNotes)
                    {
                        for (int idx = octaveNotes.Length - 1; idx >= 0; idx--)
                        {
                            //Service.logger.Info($"[{writeIdx}] = ui:{noteIdx + mapOctaveNotes[idx].uiIndex}, note:{idx}");
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

        public int GetNoteNumber(int mappedNoteIdx)
        {
            // note numbers starts on octave -1
            return mappedNoteIdx + (12 * (midOctaveIdx + 1));
        }
    }
}
