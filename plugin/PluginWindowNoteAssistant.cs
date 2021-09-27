using Dalamud.Interface.Windowing;
using ImGuiNET;
using MgAl2O4.Utils;
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

        private static readonly uint colorTimeLineBeat = UIColors.GetAlphaModulated(UIColors.colorGray25, 0.5f);
        private static readonly uint colorTimeLineBar = UIColors.GetAlphaModulated(UIColors.colorGray50, 0.5f);
        private const uint colorNoteLowerOctave = UIColors.colorBlue;
        private const uint colorNoteThisOctave = UIColors.colorGreen;
        private const uint colorNoteHigherOctave = UIColors.colorRed;

        private const uint colorGuideNextRGB = UIColors.colorYellow;
        private const float alphaGuideInactive = 0.05f;
        private const float alphaGuideFar = 0.25f;
        private const float alphaGuideMed = 0.5f;
        private const float alphaGuideNear = 1.0f;

        private readonly UIReaderBardPerformance uiReader;
        private readonly NoteUIMapper noteMapper;
        private readonly NoteInputMapper noteInput;
        private readonly TrackAssistant trackAssistant;

        public bool showOctaveShiftHints = true;
        public bool showBars = false;

        private float[] minNoteTime = null;

        private float cachedNoteActivationPosY;
        private float cachedNoteAppearPosY;
        private float[] cachedNotePosX = null;

        private float noMusicUpkeepRemaining = 0.0f;

        public PluginWindowNoteAssistant(UIReaderBardPerformance uiReader, TrackAssistant trackAssistant, NoteUIMapper noteMapper, NoteInputMapper noteInput) : base("Note Assistant")
        {
            this.uiReader = uiReader;
            this.noteMapper = noteMapper;
            this.noteInput = noteInput;
            this.trackAssistant = trackAssistant;

            uiReader.OnVisibilityChanged += OnPerformanceActive;
            trackAssistant.OnPlayChanged += OnPlayChanged;

            IsOpen = false;

            // doesn't matter will be updated on next draw
            PositionCondition = ImGuiCond.None;
            SizeCondition = ImGuiCond.None;
            RespectCloseHotkey = false;

            Flags = ImGuiWindowFlags.NoDecoration |
                ImGuiWindowFlags.NoResize |
                ImGuiWindowFlags.NoSavedSettings |
                ImGuiWindowFlags.NoMove |
                ImGuiWindowFlags.NoMouseInputs |
                ImGuiWindowFlags.NoDocking |
                ImGuiWindowFlags.NoFocusOnAppearing |
                ImGuiWindowFlags.NoNav;

            Plugin.OnDebugSnapshot += (_) =>
            {
                int drawErrState =
                    (trackAssistant == null) ? 1 :
                    (trackAssistant.musicViewer == null) ? 2 :
                    !Size.HasValue ? 3 :
                    !trackAssistant.IsPlaying ? 4 :
                    (cachedNotePosX == null) ? 5 :
                    0;

                Dalamud.Logging.PluginLog.Log($"PluginWindowNoteAssistant: open:{IsOpen}, numNotes:{noteMapper.notes?.Length ?? 0}, canShow:{trackAssistant.CanShowNoteAssistant}, fade:{BgAlpha} ({noMusicUpkeepRemaining}), drawErr:{drawErrState}");
            };
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
            else if (trackAssistant?.IsPlaying ?? false)
            {
                OnPlayChanged(true);
            }
        }

        public void OnPlayChanged(bool active)
        {
            if (trackAssistant.CanShowNoteAssistant)
            {
                if (active && uiReader.IsVisible)
                {
                    IsOpen = true;
                    noMusicUpkeepRemaining = NoMusicUpkeepTime;
                }
                else if (!active)
                {
                    // start ticking fadeout
                    noMusicUpkeepRemaining = NoMusicUpkeepTime;
                    Plugin.TickScheduler.Register(this);
                }
            }
            else
            {
                IsOpen = false;
            }
        }

        public override void PreDraw()
        {
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

        public bool Tick(float deltaSeconds)
        {
            bool canFadeOut = IsOpen && (trackAssistant == null || !trackAssistant.IsPlaying);
            if (canFadeOut)
            {
                noMusicUpkeepRemaining -= deltaSeconds;
                if (noMusicUpkeepRemaining <= 0.0f)
                {
                    IsOpen = false;
                }
            }

            // can tick? only when still open and not playing
            return canFadeOut && IsOpen;
        }

        public override void OnClose()
        {
            trackAssistant?.Stop();
        }

        public override void Draw()
        {
            if (cachedNotePosX != null && Size.HasValue)
            {
                DrawKeyGuides();

                if (trackAssistant != null && trackAssistant.musicViewer != null)
                {
                    if (trackAssistant.IsPlaying)
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

                uint drawColor = UIColors.GetAlphaModulated(nextPlayIdx == idx ? colorGuideNextRGB : 0xffffff, bgAlpha * lineAlpha);

                drawList.AddTriangleFilled(
                    new Vector2(cachedNotePosX[idx] - keyHalfWidth, cachedNoteActivationPosY + 1),
                    new Vector2(cachedNotePosX[idx] + keyHalfWidth, cachedNoteActivationPosY + 1),
                    new Vector2(cachedNotePosX[idx], cachedNoteActivationPosY + markerHeight),
                    drawColor);

                bool isHalfStep = noteMapper.octaveNotes[noteMapper.notes[idx].octaveIdx].isHalfStep;
                if (isHalfStep)
                {
                    drawList.AddTriangleFilled(
                        new Vector2(cachedNotePosX[idx] - keyHalfWidth + halfStepFrame, cachedNoteActivationPosY + 1 + halfStepFrame),
                        new Vector2(cachedNotePosX[idx] + keyHalfWidth - halfStepFrame, cachedNoteActivationPosY + 1 + halfStepFrame),
                        new Vector2(cachedNotePosX[idx], cachedNoteActivationPosY + markerHeight - halfStepFrame),
                        drawColor & 0xff000000);
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

            int activeOctaveOffset = uiReader.cachedState.ActiveOctaveOffset;
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
                            // this one is always Lx/Rx with simple text description, ignore icon logic
                            var octaveInputKey = noteInput.GeOctaveKeyBinding(octaveOffset);
                            if (octaveInputKey.IsValid())
                            {
                                var (octaveKeyTextSize, octaveKeyTextScale) = InputBindingUtils.CalcInputKeySizeAndScale(octaveInputKey);
                                drawList.AddText(new Vector2(lineBEndX - octaveKeyTextSize.X, posY0 - octaveKeyTextSize.Y), octaveShiftColor, octaveInputKey.text);
                            }
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
