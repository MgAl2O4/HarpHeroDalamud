using Dalamud.Interface.Windowing;
using Dalamud.Logging;
using ImGuiNET;
using System;
using System.Numerics;

namespace HarpHero
{
    public class PluginWindowAssistant : Window, IDisposable
    {
        // TODO: scale from viewport? expose?
        private const float TrackAssistSizeY = 500.0f;
        private const float TrackAssistOffsetY = 20.0f;

        private const uint colorTimeLineBeat = 0x80404040;
        private const uint colorTimeLineBar = 0x80808080;
        private const uint colorNoteLowerOctave = 0xffff0000;
        private const uint colorNoteThisOctave = 0xff00ff00;
        private const uint colorNoteHigherOctave = 0xff0000ff;

        private const uint colorGuideLineTone = 0xffffffff;
        private const uint colorGuideLineHalfStep = 0xff808080;

        private readonly UIReaderBardPerformance uiReader;
        private readonly TrackAssistant trackAssistant;

        // UI keys are ordered: C+1, B, A, G, F, E, D, C, A#, G#, F#, D#, C#
        // maps are indexed with notes from music track: low C -> high C
        struct NoteMap
        {
            public int uiIndex;
            public bool isHalfStep;
            public string name;
        }
        private readonly NoteMap[] mapNotes = {
            new NoteMap() { name = "C", isHalfStep = false, uiIndex = 7 },
            new NoteMap() { name = "C#", isHalfStep = true, uiIndex = 12 },
            new NoteMap() { name = "D", isHalfStep = false, uiIndex = 6 },
            new NoteMap() { name = "D#", isHalfStep = true, uiIndex = 11 },
            new NoteMap() { name = "E", isHalfStep = false, uiIndex = 5 },
            new NoteMap() { name = "F", isHalfStep = false, uiIndex = 4 },
            new NoteMap() { name = "F#", isHalfStep = true, uiIndex = 10 },
            new NoteMap() { name = "G", isHalfStep = false, uiIndex = 3 },
            new NoteMap() { name = "G#", isHalfStep = true, uiIndex = 9 },
            new NoteMap() { name = "A", isHalfStep = false, uiIndex = 2 },
            new NoteMap() { name = "A#", isHalfStep = true, uiIndex = 8 },
            new NoteMap() { name = "B", isHalfStep = false, uiIndex = 1 },
            new NoteMap() { name = "C", isHalfStep = false, uiIndex = 0 },
        };

        private float cachedNoteActivationPosY;
        private float cachedNoteAppearPosY;
        private float[] cachedNotePosX = null;

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
            IsOpen = active;
        }

        public override void PreDraw()
        {
            if (uiReader != null && uiReader.cachedState != null && uiReader.cachedState.keys.Count == mapNotes.Length)
            {
                var uiLowC = uiReader.cachedState.keys[mapNotes[0].uiIndex];
                var uiHighC = uiReader.cachedState.keys[mapNotes[12].uiIndex];

                Position = new Vector2(uiLowC.pos.X, uiLowC.pos.Y - TrackAssistSizeY - TrackAssistOffsetY);
                Size = new Vector2(uiHighC.pos.X + uiHighC.size.X - uiLowC.pos.X, TrackAssistSizeY + TrackAssistOffsetY);

                cachedNoteAppearPosY = Position.Value.Y + 10;
                cachedNoteActivationPosY = Position.Value.Y + Size.Value.Y - 20;

                if (cachedNotePosX == null)
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

        public override void Draw()
        {
            if (cachedNotePosX != null)
            {
                DrawKeyGuides();

                if (trackAssistant != null &&
                    trackAssistant.musicViewer != null &&
                    trackAssistant.isPlaying)
                {
                    trackAssistant.Tick(ImGui.GetIO().DeltaTime);
                    
                    DrawBars();
                    DrawNotes();
                }
            }
        }

        private void DrawKeyGuides()
        {
            var drawList = ImGui.GetWindowDrawList();

            for (int idx = 0; idx < mapNotes.Length; idx++)
            {
                uint drawColor = mapNotes[idx].isHalfStep ? colorGuideLineHalfStep : colorGuideLineTone;

                drawList.AddLine(new Vector2(cachedNotePosX[idx], cachedNoteAppearPosY), new Vector2(cachedNotePosX[idx], cachedNoteActivationPosY), drawColor);
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

            foreach (var noteInfo in trackAssistant.musicViewer.shownNotes)
            {
                float t0 = Math.Min(1.0f, Math.Max(0.0f, 1.0f * (noteInfo.startUs - timeRangeStartUs) / timeRangeUs));
                float t1 = Math.Min(1.0f, Math.Max(0.0f, 1.0f * (noteInfo.endUs - timeRangeStartUs) / timeRangeUs));

                var posY0 = GetTimeCoordY(t0);
                var posY1 = GetTimeCoordY(t1);

                int noteIdx = (int)noteInfo.note.NoteName;
                int relativeOctave = noteInfo.note.Octave - trackAssistant.midOctaveIdx;
                if (relativeOctave == 2)
                {
                    noteIdx = mapNotes.Length - 1;
                    relativeOctave = 1;
                }

                var posX = cachedNotePosX[mapNotes[noteIdx].uiIndex];
                var noteColor = (relativeOctave == 0) ? colorNoteThisOctave : (relativeOctave < 0) ? colorNoteLowerOctave : colorNoteHigherOctave;

                drawList.AddRectFilled(new Vector2(posX - noteHelfWidth, posY0), new Vector2(posX + noteHelfWidth, posY1), noteColor);
            }
        }
    }
}
