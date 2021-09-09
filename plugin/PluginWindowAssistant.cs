using Dalamud.Interface.Windowing;
using ImGuiNET;
using System;
using System.Numerics;

namespace HarpHero
{
    public class PluginWindowAssistant : Window, IDisposable, ITickable
    {
        // TODO: expose
        private const float TrackAssistSizeViewportPctY = 0.5f;
        private const float TrackAssistSizeMinY = 500.0f;
        private const float TrackAssistOffsetY = 20.0f;
        private const float NoMusicUpkeepTime = 5.0f;

        private const uint colorTimeLineBeat = 0x80404040;
        private const uint colorTimeLineBar = 0x80808080;
        private const uint colorNoteLowerOctave = 0xffff0000;
        private const uint colorNoteThisOctave = 0xff00ff00;
        private const uint colorNoteHigherOctave = 0xff0000ff;

        private const uint colorGuideInactive = 0x04ffffff;
        private const uint colorGuideFar = 0x10ffffff;
        private const uint colorGuideMed = 0x40ffffff;
        private const uint colorGuideNear = 0xffffffff;

        private readonly UIReaderBardPerformance uiReader;
        private readonly TrackAssistant trackAssistant;

        // UI keys are ordered: C+1, B, A, G, F, E, D, C, A#, G#, F#, D#, C#
        // maps are indexed with notes from music track: low C -> high C
        struct NoteInfo
        {
            public int uiIndex;
            public bool isHalfStep;
            public string name;
        }
        struct NoteMap
        {
            public int uiIndex;
            public int octaveIdx;
        }

        private readonly NoteInfo[] mapOctaveNotes = {
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
        private const int numNotesWide = 12 + 12 + 12 + 1;
        private const int numNotesShort = 12 + 1;
        private int midOctaveLowC = 0;
        private NoteMap[] mapNotes = null;
        private float[] minNoteTime = null;

        private float cachedNoteActivationPosY;
        private float cachedNoteAppearPosY;
        private float[] cachedNotePosX = null;
        private bool isWideMode = false;

        private float noMusicUpkeepRemaining = 0.0f;

        public PluginWindowAssistant(UIReaderBardPerformance uiReader, TrackAssistant trackAssistant) : base("Harp Hero Assistant")
        {
            this.uiReader = uiReader;
            this.trackAssistant = trackAssistant;

            IsOpen = false;

            // doesn't matter will be updated on next draw
            PositionCondition = ImGuiCond.None;
            SizeCondition = ImGuiCond.None;

            Flags = ImGuiWindowFlags.NoDecoration |
                ImGuiWindowFlags.NoResize |
                ImGuiWindowFlags.NoSavedSettings |
                ImGuiWindowFlags.NoMove |
                ImGuiWindowFlags.NoMouseInputs |
                ImGuiWindowFlags.NoDocking |
                ImGuiWindowFlags.NoFocusOnAppearing |
                ImGuiWindowFlags.NoNav;
        }

        public void Dispose()
        {
            // meh
        }

        public void OnPerformanceActive(bool active)
        {
            if (!active)
            {
                IsOpen = false;
                noMusicUpkeepRemaining = 0.0f;
            }
        }

        public void OnPlayChanged(bool active)
        {
            if (active)
            {
                IsOpen = true;
                noMusicUpkeepRemaining = NoMusicUpkeepTime;
            }
        }

        public override void PreDraw()
        {
            if (uiReader != null && uiReader.cachedState != null && (uiReader.cachedState.keys.Count == numNotesShort || uiReader.cachedState.keys.Count == numNotesWide))
            {
                isWideMode = uiReader.cachedState.keys.Count == numNotesWide;
                GenerateNoteMappings();

                var uiLowC = uiReader.cachedState.keys[mapNotes[0].uiIndex];
                var uiHighC = uiReader.cachedState.keys[mapNotes[mapNotes.Length - 1].uiIndex];

                float upkeepPct = (noMusicUpkeepRemaining / NoMusicUpkeepTime);
                float upkeepAlpha = upkeepPct * upkeepPct;
                float TrackAssistSizeY = Math.Min(TrackAssistSizeViewportPctY * ImGui.GetWindowViewport().Size.Y, TrackAssistSizeMinY) * upkeepAlpha;
                float NewWindowPosY = Math.Max(50, uiLowC.pos.Y - TrackAssistSizeY - TrackAssistOffsetY);

                Position = new Vector2(uiLowC.pos.X, NewWindowPosY);
                Size = new Vector2(uiHighC.pos.X + uiHighC.size.X - uiLowC.pos.X, uiLowC.pos.Y - NewWindowPosY);
                BgAlpha = upkeepAlpha;

                cachedNoteAppearPosY = Position.Value.Y + 10;
                cachedNoteActivationPosY = Position.Value.Y + Size.Value.Y - 20;

                if (cachedNotePosX == null || cachedNotePosX.Length != mapNotes.Length)
                {
                    cachedNotePosX = new float[mapNotes.Length];
                }

                for (int idx = 0; idx < mapNotes.Length; idx++)
                {
                    var uiKey = uiReader.cachedState.keys[mapNotes[idx].uiIndex];
                    cachedNotePosX[idx] = uiKey.pos.X + (uiKey.size.X * 0.5f);
                }
            }
            else
            {
                cachedNotePosX = null;
            }
        }

        private void GenerateNoteMappings()
        {
            int expectedNumNotes = isWideMode ? numNotesWide : numNotesShort;
            if (mapNotes != null && mapNotes.Length == expectedNumNotes)
            {
                return;
            }

            // iter from high to low, mapNotes is indexes from low to high
            mapNotes = new NoteMap[expectedNumNotes];

            // higest C
            int writeIdx = expectedNumNotes - 1;
            mapNotes[writeIdx] = new NoteMap() { octaveIdx = 0, uiIndex = 0 };
            writeIdx--;
            //PluginLog.Log($"[{expectedNumNotes - 1}] = ui:0, note:0");

            // octave(s)
            int noteIdx = 1;
            while (noteIdx < expectedNumNotes)
            {
                for (int idx = mapOctaveNotes.Length - 1; idx >= 0; idx--)
                {
                    //PluginLog.Log($"[{writeIdx}] = ui:{noteIdx + mapOctaveNotes[idx].uiIndex}, note:{idx}");
                    mapNotes[writeIdx] = new NoteMap() { octaveIdx = idx, uiIndex = noteIdx + mapOctaveNotes[idx].uiIndex };
                    writeIdx--;
                }

                noteIdx += mapOctaveNotes.Length;
            }

            midOctaveLowC = isWideMode ? 12 : 0;

            minNoteTime = new float[expectedNumNotes];
            for (int idx = 0; idx < minNoteTime.Length; idx++)
            {
                minNoteTime[idx] = 100.0f;
            }
        }

        public void Tick(float deltaSeconds)
        {
            if (IsOpen && (trackAssistant == null || !trackAssistant.isPlaying))
            {
                noMusicUpkeepRemaining -= deltaSeconds;
                if (noMusicUpkeepRemaining <= 0.0f)
                {
                    IsOpen = false;
                }
            }
        }

        public override void Draw()
        {
            if (cachedNotePosX != null)
            {
                DrawKeyGuides();

                if (trackAssistant != null && trackAssistant.musicViewer != null)
                {
                    if (trackAssistant.isPlaying)
                    {
                        noMusicUpkeepRemaining = NoMusicUpkeepTime;

                        DrawBars();
                        DrawNotes();
                    }
                }
            }
        }

        private void DrawKeyGuides()
        {
            var drawList = ImGui.GetWindowDrawList();

            for (int idx = 0; idx < mapNotes.Length; idx++)
            {
                uint drawColor = colorGuideInactive;
                if (minNoteTime[idx] < 0.33f)
                {
                    drawColor = colorGuideNear;
                }
                else if (minNoteTime[idx] < 0.66f)
                {
                    drawColor = colorGuideMed;
                }
                else if (minNoteTime[idx] < 1.0f)
                {
                    drawColor = colorGuideFar;
                }

                drawList.AddLine(new Vector2(cachedNotePosX[idx], cachedNoteAppearPosY), new Vector2(cachedNotePosX[idx], cachedNoteActivationPosY), drawColor);

                if (mapOctaveNotes[mapNotes[idx].octaveIdx].isHalfStep)
                {
                    drawColor &= 0xff808080;
                }
                drawList.AddCircle(new Vector2(cachedNotePosX[idx], cachedNoteActivationPosY), 10, drawColor);
            }
        }

        private float GetTimeCoordY(float relativeTime)
        {
            return cachedNoteActivationPosY - (relativeTime * (cachedNoteActivationPosY - cachedNoteAppearPosY));
        }

        private void DrawBars()
        {
            var drawList = ImGui.GetWindowDrawList();
            var posStartX = Position.Value.X + 10;
            var posEndX = Position.Value.X + Size.Value.X - 10;

            var timeRangeStartUs = trackAssistant.musicViewer.TimeRangeStartUs;
            var timeRangeUs = trackAssistant.musicViewer.TimeRangeUs;

            foreach (var lineTimeUs in trackAssistant.musicViewer.shownBarLines)
            {
                // no clamping, ignore when moved outside view window
                var timeAlpha = 1.0f * (lineTimeUs - timeRangeStartUs) / timeRangeUs;
                if (timeAlpha >= 0 && timeAlpha <= 1)
                {
                    var posY = GetTimeCoordY(timeAlpha);
                    drawList.AddLine(new Vector2(posStartX, posY), new Vector2(posEndX, posY), colorTimeLineBar);
                }
            }

            foreach (var lineTimeUs in trackAssistant.musicViewer.shownBeatLines)
            {
                // no clamping, ignore when moved outside view window
                var timeAlpha = 1.0f * (lineTimeUs - timeRangeStartUs) / timeRangeUs;
                if (timeAlpha >= 0 && timeAlpha <= 1)
                {
                    var posY = GetTimeCoordY(timeAlpha);
                    drawList.AddLine(new Vector2(posStartX, posY), new Vector2(posEndX, posY), colorTimeLineBeat);
                }
            }
        }

        private void DrawNotes()
        {
            var drawList = ImGui.GetWindowDrawList();
            var timeRangeStartUs = trackAssistant.musicViewer.TimeRangeStartUs;
            var timeRangeUs = trackAssistant.musicViewer.TimeRangeUs;

            float noteHelfWidth = 5.0f;

            for (int idx = 0; idx < minNoteTime.Length; idx++)
            {
                minNoteTime[idx] = 100.0f;
            }

            foreach (var noteInfo in trackAssistant.musicViewer.shownNotes)
            {
                int noteOctaveIdx = noteInfo.note.Octave;
                int noteName = (int)noteInfo.note.NoteName;

                int octaveOffset = 0;
                int mappedNoteIdx = noteName + midOctaveLowC + (12 * (noteOctaveIdx - trackAssistant.midOctaveIdx));
                if (mappedNoteIdx < 0)
                {
                    octaveOffset = -1;
                    mappedNoteIdx += 12;
                }
                else if (mappedNoteIdx >= mapNotes.Length)
                {
                    octaveOffset = 1;
                    mappedNoteIdx -= 12;
                }

                if (mappedNoteIdx < 0 || mappedNoteIdx >= mapNotes.Length)
                {
                    // this should be happening...
                    continue;
                }

                float t0 = Math.Min(1.0f, Math.Max(0.0f, 1.0f * (noteInfo.startUs - timeRangeStartUs) / timeRangeUs));
                float t1 = Math.Min(1.0f, Math.Max(0.0f, 1.0f * (noteInfo.endUs - timeRangeStartUs) / timeRangeUs));

                var posY0 = GetTimeCoordY(t0);
                var posY1 = GetTimeCoordY(t1);

                var posX = cachedNotePosX[mappedNoteIdx];
                var noteColor = (octaveOffset == 0) ? colorNoteThisOctave : (octaveOffset < 0) ? colorNoteLowerOctave : colorNoteHigherOctave;
                var noteColorFar = noteColor & 0x40ffffff;

                drawList.AddRectFilledMultiColor(new Vector2(posX - noteHelfWidth, posY0), new Vector2(posX + noteHelfWidth, posY1), noteColor, noteColor, noteColorFar, noteColorFar);

                if (minNoteTime[mappedNoteIdx] > t0)
                {
                    minNoteTime[mappedNoteIdx] = t0;
                }
            }
        }
    }
}
