using Dalamud.Interface.Windowing;
using ImGuiNET;
using System;
using System.Numerics;

namespace HarpHero
{
    public class PluginWindowBindAssistant : Window, IDisposable, ITickable
    {
        // TODO: expose
        private const float TrackAssistSizeMinY = 100.0f;
        private const float TrackAssistOffsetY = 20.0f;
        private const float NoMusicUpkeepTime = 3.0f;

        private readonly uint[] colorBinds = { 0xff5c5b4c, 0xff09edbc, 0xff40cbf9, 0xff5b71ff };
        private readonly uint[] colorBindsDark = { 0xff5c5b4c, 0xff069d7d, 0xff07b4ed, 0xff0a2bff };

        private readonly UIReaderBardPerformance uiReader;
        private readonly NoteUIMapper noteMapper;
        private readonly NoteInputMapper noteInput;
        private readonly TrackAssistant trackAssistant;

        private float noMusicUpkeepRemaining;

        public PluginWindowBindAssistant(UIReaderBardPerformance uiReader, TrackAssistant trackAssistant, NoteUIMapper noteMapper, NoteInputMapper noteInput) : base("Bind Assistant")
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
            else if (trackAssistant?.IsPlaying ?? false)
            {
                OnPlayChanged(true);
            }
        }

        public void OnPlayChanged(bool active)
        {
            if (trackAssistant.CanShowBindAssistant)
            {
                if (active && uiReader.IsVisible)
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
                float upkeepPct = (noMusicUpkeepRemaining / NoMusicUpkeepTime);
                float upkeepAlpha = upkeepPct * upkeepPct;
                float newWindowPosY = Math.Max(50, uiReader.cachedState.keysPos.Y - TrackAssistSizeMinY - TrackAssistOffsetY);

                bool isWide = (uiReader.cachedState.keys.Count > 13);
                float newWindowSizeX = uiReader.cachedState.keysSize.X / (isWide ? 3 : 1);
                float newWindowPosX = uiReader.cachedState.keysPos.X +
                    (!isWide ? 0 : (uiReader.cachedState.keysSize.X - newWindowSizeX) * 0.5f);

                Position = new Vector2(newWindowPosX, newWindowPosY);
                Size = new Vector2(newWindowSizeX, uiReader.cachedState.keysPos.Y - newWindowPosY);
                BgAlpha = upkeepAlpha;
            }
        }

        public void Tick(float deltaSeconds)
        {
            if (IsOpen && (trackAssistant == null || !trackAssistant.IsPlaying))
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
            if (trackAssistant != null && trackAssistant.musicViewer != null && Size.HasValue)
            {
                if (trackAssistant.IsPlaying)
                {
                    noMusicUpkeepRemaining = NoMusicUpkeepTime;

                    DrawBindTimeline();
                }
            }
        }

        private void DrawBindTimeline()
        {
            var drawList = ImGui.GetWindowDrawList();
            var timeRangeStartUs = trackAssistant.musicViewer.TimeRangeStartUs;
            var timeRangeUs = trackAssistant.musicViewer.TimeRangeUs;

            float noteHalfHeight = 5.0f;
            int playingColorIdx = 0;

            foreach (var noteBinding in trackAssistant.musicViewer.GetShownNotesBindings())
            {
                float tX0 = Math.Min(1.0f, Math.Max(0.0f, 1.0f * (noteBinding.noteInfo.startUs - timeRangeStartUs) / timeRangeUs));
                float tX1 = Math.Min(1.0f, Math.Max(0.0f, 1.0f * (noteBinding.noteInfo.endUs - timeRangeStartUs) / timeRangeUs));
                float tY = Math.Min(1.0f, Math.Max(0.0f, 1.0f * noteBinding.bindingIdx / trackAssistant.musicViewer.maxBindingsToShow));

                var posX0 = Position.Value.X + 10 + Size.Value.X * tX0;
                var posX1 = Position.Value.X + 10 + Size.Value.X * tX1;
                var posY = Position.Value.Y + 30 + (Size.Value.Y - 20) * tY;

                int hintColorIdx =
                    (noteBinding.pressIdx >= trackAssistant.musicViewer.maxBindingsToShow) ? 0 :
                    (noteBinding.bindingIdx >= 0 && noteBinding.bindingIdx < colorBinds.Length - 1) ? noteBinding.bindingIdx + 1 :
                    0;

                if (noteBinding.pressIdx == 0)
                {
                    playingColorIdx = hintColorIdx;
                }

                var noteColor = colorBinds[hintColorIdx];
                var noteColorFar = noteColor & 0x40ffffff;

                drawList.AddRectFilledMultiColor(new Vector2(posX0, posY - noteHalfHeight), new Vector2(posX1, posY + noteHalfHeight), noteColor, noteColorFar, noteColorFar, noteColor);
                if (noteBinding.showHint)
                {
                    var noteDesc = noteInput.GetNoteKeyBinding(noteBinding.noteInfo.note);
                    if (!string.IsNullOrEmpty(noteDesc))
                    {
                        drawList.AddText(new Vector2(posX0 + 5, posY - ImGui.GetTextLineHeight() - 5), colorBindsDark[hintColorIdx], noteDesc);
                    }
                }
            }

            float tLX = 1.0f * trackAssistant.musicViewer.TimeRangeNowOffset / timeRangeUs;
            var posLineX = Position.Value.X + 10 + Size.Value.X * tLX;
            drawList.AddLine(new Vector2(posLineX, Position.Value.Y + 10), new Vector2(posLineX, Position.Value.Y + Size.Value.Y - 10), colorBindsDark[playingColorIdx]);
        }
    }
}
