using Dalamud.Interface.Windowing;
using ImGuiNET;
using System;
using System.Numerics;

namespace HarpHero
{
    public class PluginWindowNoteAssistant : Window, IDisposable, ITickable
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
        private readonly NoteUIMapper noteMapper;
        private readonly NoteInputWatcher noteInput;
        private readonly TrackAssistant trackAssistant;

        private float[] minNoteTime = null;

        private float cachedNoteActivationPosY;
        private float cachedNoteAppearPosY;
        private float[] cachedNotePosX = null;

        private float noMusicUpkeepRemaining = 0.0f;

        public PluginWindowNoteAssistant(UIReaderBardPerformance uiReader, TrackAssistant trackAssistant, NoteUIMapper noteMapper, NoteInputWatcher noteInput) : base("Note Assistant")
        {
            this.uiReader = uiReader;
            this.noteMapper = noteMapper;
            this.noteInput = noteInput;
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
            // this can blink out and back in when changing wide/short during performance
            if (!active)
            {
                IsOpen = false;
                noMusicUpkeepRemaining = 0.0f;
            }
            else if (trackAssistant?.isPlaying ?? false)
            {
                OnPlayChanged(true);
            }
        }

        public void OnPlayChanged(bool active)
        {
            if (trackAssistant.CanShowNoteAssistant)
            {
                if (active)
                {
                    IsOpen = true;
                    noMusicUpkeepRemaining = NoMusicUpkeepTime;
                }
            }
            else
            {
                IsOpen = false;
            }
        }

        public override void PreDraw()
        {
            noteMapper.Update(uiReader != null ? uiReader.cachedState : null);

            int numMappedNotes = noteMapper.notes?.Length ?? 0;
            if (numMappedNotes > 0)
            {
                var uiLowC = uiReader.cachedState.keys[noteMapper.notes[0].uiIndex];
                var uiHighC = uiReader.cachedState.keys[noteMapper.notes[numMappedNotes - 1].uiIndex];

                float upkeepPct = (noMusicUpkeepRemaining / NoMusicUpkeepTime);
                float upkeepAlpha = upkeepPct * upkeepPct;
                float trackAssistSizeY = Math.Min(TrackAssistSizeViewportPctY * ImGui.GetWindowViewport().Size.Y, TrackAssistSizeMinY) * upkeepAlpha;
                float newWindowPosY = Math.Max(50, uiLowC.pos.Y - trackAssistSizeY - TrackAssistOffsetY);

                Position = new Vector2(uiLowC.pos.X, newWindowPosY);
                Size = new Vector2(uiHighC.pos.X + uiHighC.size.X - uiLowC.pos.X, uiLowC.pos.Y - newWindowPosY);
                BgAlpha = upkeepAlpha;

                cachedNoteAppearPosY = Position.Value.Y + 10;
                cachedNoteActivationPosY = Position.Value.Y + Size.Value.Y - 20;

                if (cachedNotePosX == null || cachedNotePosX.Length != numMappedNotes)
                {
                    cachedNotePosX = new float[numMappedNotes];
                }

                if (minNoteTime == null || minNoteTime.Length != numMappedNotes)
                {
                    minNoteTime = new float[numMappedNotes];
                    for (int idx = 0; idx < minNoteTime.Length; idx++)
                    {
                        minNoteTime[idx] = 100.0f;
                    }
                }

                for (int idx = 0; idx < numMappedNotes; idx++)
                {
                    var uiKey = uiReader.cachedState.keys[noteMapper.notes[idx].uiIndex];
                    cachedNotePosX[idx] = uiKey.pos.X + (uiKey.size.X * 0.5f);
                }
            }
            else
            {
                cachedNotePosX = null;
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

        public override void OnClose()
        {
            trackAssistant?.Stop();
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

            for (int idx = 0; idx < noteMapper.notes.Length; idx++)
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

                if (noteMapper.octaveNotes[noteMapper.notes[idx].octaveIdx].isHalfStep)
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

            float noteHalfWidth = 5.0f;

            for (int idx = 0; idx < minNoteTime.Length; idx++)
            {
                minNoteTime[idx] = 100.0f;
            }

            foreach (var noteInfo in trackAssistant.musicViewer.shownNotes)
            {
                if (!noteMapper.GetMappedNoteIdx(noteInfo.note, out int mappedNoteIdx, out int octaveOffset))
                {
                    // this shouldn't be happening...
                    continue;
                }

                float t0 = Math.Min(1.0f, Math.Max(0.0f, 1.0f * (noteInfo.startUs - timeRangeStartUs) / timeRangeUs));
                float t1 = Math.Min(1.0f, Math.Max(0.0f, 1.0f * (noteInfo.endUs - timeRangeStartUs) / timeRangeUs));

                var posY0 = GetTimeCoordY(t0);
                var posY1 = GetTimeCoordY(t1);

                var posX = cachedNotePosX[mappedNoteIdx];
                var noteColor = (octaveOffset == 0) ? colorNoteThisOctave : (octaveOffset < 0) ? colorNoteLowerOctave : colorNoteHigherOctave;
                var noteColorFar = noteColor & 0x40ffffff;

                drawList.AddRectFilledMultiColor(new Vector2(posX - noteHalfWidth, posY0), new Vector2(posX + noteHalfWidth, posY1), noteColor, noteColor, noteColorFar, noteColorFar);

                if (minNoteTime[mappedNoteIdx] > t0)
                {
                    minNoteTime[mappedNoteIdx] = t0;
                }
            }
        }
    }
}
