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
        private const float TrackAssistOctaveShiftX = 100.0f;
        private const float NoMusicUpkeepTime = 3.0f;

        private const uint colorTimeLineBeat = 0x80404040;
        private const uint colorTimeLineBar = 0x80808080;
        private const uint colorNoteLowerOctave = 0xffff0000;
        private const uint colorNoteThisOctave = 0xff00ff00;
        private const uint colorNoteHigherOctave = 0xff0000ff;

        private const uint colorGuideNextRGB = 0x40cbf9;
        private const float alphaGuideInactive = 0.05f;
        private const float alphaGuideFar = 0.25f;
        private const float alphaGuideMed = 0.5f;
        private const float alphaGuideNear = 1.0f;

        private readonly UIReaderBardPerformance uiReader;
        private readonly NoteUIMapper noteMapper;
        private readonly NoteInputWatcher noteInput;
        private readonly TrackAssistant trackAssistant;

        public bool showOctaveShiftHints = true;
        public bool showBars = false;

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
                Size = new Vector2(uiHighC.pos.X + uiHighC.size.X - uiLowC.pos.X + TrackAssistOctaveShiftX, uiLowC.pos.Y - newWindowPosY);
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

                        if (showBars)
                        {
                            DrawBars();
                        }

                        DrawNotes();
                    }
                }
            }
        }

        private void DrawKeyGuides()
        {
            var drawList = ImGui.GetWindowDrawList();

            const float keyHalfWidth = 8.0f;
            const float halfStepFrame = 2.0f;
            const float markerHeight = 15.0f;
            float bgAlpha = BgAlpha.GetValueOrDefault();

            int nextPlayIdx = -1;
            float nextPlayTime = minNoteTime[0];
            for (int idx = 0; idx < minNoteTime.Length; idx++)
            {
                if (nextPlayTime >= minNoteTime[idx] && minNoteTime[idx] <= 1.0f)
                {
                    nextPlayTime = minNoteTime[idx];
                    nextPlayIdx = idx;
                }
            }

            for (int idx = 0; idx < noteMapper.notes.Length; idx++)
            {
                float lineAlpha = alphaGuideInactive;
                if (minNoteTime[idx] < 0.33f)
                {
                    lineAlpha = alphaGuideNear;
                }
                else if (minNoteTime[idx] < 0.66f)
                {
                    lineAlpha = alphaGuideMed;
                }
                else if (minNoteTime[idx] < 1.0f)
                {
                    lineAlpha = alphaGuideFar;
                }

                uint drawAlpha = (uint)(0xff * bgAlpha * lineAlpha);
                uint drawColor = (nextPlayIdx == idx ? colorGuideNextRGB : 0xffffff) | (drawAlpha << 24);

                bool isHalfStep = noteMapper.octaveNotes[noteMapper.notes[idx].octaveIdx].isHalfStep;
                drawList.AddTriangleFilled(
                    new Vector2(cachedNotePosX[idx] - keyHalfWidth, cachedNoteActivationPosY + 1),
                    new Vector2(cachedNotePosX[idx] + keyHalfWidth, cachedNoteActivationPosY + 1),
                    new Vector2(cachedNotePosX[idx], cachedNoteActivationPosY + markerHeight), drawColor);

                if (isHalfStep)
                {
                    uint drawColorHalfStep = 0x000000 | (drawAlpha << 24);
                    drawList.AddTriangleFilled(
                        new Vector2(cachedNotePosX[idx] - keyHalfWidth + halfStepFrame, cachedNoteActivationPosY + 1 + halfStepFrame),
                        new Vector2(cachedNotePosX[idx] + keyHalfWidth - halfStepFrame, cachedNoteActivationPosY + 1 + halfStepFrame),
                        new Vector2(cachedNotePosX[idx], cachedNoteActivationPosY + markerHeight - halfStepFrame), drawColorHalfStep);
                }
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

            int activeOctaveOffset = noteInput.GetActiveOctaveOffset();
            int shownOctaveOffset = 100;
            int numShownOffsets = 0;
            bool canShowOctaveOffsetKey = showOctaveShiftHints;

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

                var useNoteHalfWidth = noteHalfWidth;
                if (activeOctaveOffset != octaveOffset)
                {
                    noteColor &= 0x80ffffff;
                    useNoteHalfWidth -= 1;
                }

                if (shownOctaveOffset != octaveOffset)
                {
                    shownOctaveOffset = octaveOffset;
                    if (numShownOffsets <= 3)
                    {
                        uint octaveShiftColor = (noteColor & 0x00ffffff) | 0xc0000000;
                        uint octaveShiftColorFaded = (noteColor & 0x00ffffff) | 0x10000000;
                        uint octaveShiftColorWide = (noteColor & 0x00ffffff) | 0x0f000000;

                        var lineBEndX = Position.Value.X + Size.Value.X - 10;
                        var lineBStartX = lineBEndX - TrackAssistOctaveShiftX;

                        bool hideActiveShift = (activeOctaveOffset == octaveOffset) && (numShownOffsets == 0) && (t0 < 0.2f);
                        if (!hideActiveShift)
                        {
                            drawList.AddRectFilledMultiColor(new Vector2(lineBStartX, posY0), new Vector2(lineBEndX, posY0 + 1), octaveShiftColorFaded, octaveShiftColor, octaveShiftColor, octaveShiftColorFaded);
                        }

                        var lineAStartX = Position.Value.X + 10;
                        var lineAEndX = hideActiveShift ? lineBEndX : lineBStartX;
                        drawList.AddRectFilled(new Vector2(lineAStartX, posY0), new Vector2(lineAEndX, posY0 + 1), octaveShiftColorWide);

                        if (canShowOctaveOffsetKey && (octaveOffset != 0) && !hideActiveShift)
                        {
                            //canShowOctaveOffsetKey = false;
                            var octaveShiftHint = noteInput.GeOctaveKeyBinding(octaveOffset);
                            var octaveShiftSize = ImGui.CalcTextSize(octaveShiftHint);
                            drawList.AddText(new Vector2(lineBEndX - octaveShiftSize.X, posY0 - octaveShiftSize.Y), octaveShiftColor, octaveShiftHint);
                        }
                    }

                    numShownOffsets++;
                }

                drawList.AddRectFilledMultiColor(new Vector2(posX - useNoteHalfWidth, posY0), new Vector2(posX + useNoteHalfWidth, posY1), noteColor, noteColor, noteColorFar, noteColorFar);

                if (minNoteTime[mappedNoteIdx] > t0)
                {
                    minNoteTime[mappedNoteIdx] = t0;
                }
            }
        }
    }
}
