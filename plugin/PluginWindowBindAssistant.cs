using Dalamud.Game.ClientState.GamePad;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using MgAl2O4.Utils;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace HarpHero
{
    public class PluginWindowBindAssistant : Window, IDisposable, ITickable
    {
        // TODO: expose
        private const float TrackAssistSizeMinY = 100.0f;
        private const float TrackAssistOffsetY = 20.0f;
        private const float NoMusicUpkeepTime = 3.0f;

        private readonly UIReaderBardPerformance uiReader;
        private readonly NoteUIMapper noteMapper;
        private readonly NoteInputMapper noteInput;
        private readonly TrackAssistant trackAssistant;

        private float noMusicUpkeepRemaining;

        private struct GamepadColors
        {
            public uint colorLight;
            public uint colorDark;
            public uint colorLightBk;
            public uint colorDarkBk;
            public bool isDualColor;
        }
        private Dictionary<GamepadButtons, GamepadColors> mapGamepadColors = new();

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
            RespectCloseHotkey = false;

            Flags = ImGuiWindowFlags.NoDecoration |
                ImGuiWindowFlags.NoResize |
                ImGuiWindowFlags.NoSavedSettings |
                ImGuiWindowFlags.NoMove |
                ImGuiWindowFlags.NoMouseInputs |
                ImGuiWindowFlags.NoDocking |
                ImGuiWindowFlags.NoFocusOnAppearing |
                ImGuiWindowFlags.NoNav;

            InitGamepadButtonColors();
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
            uint colorFarMask = 0x40ffffff;
            uint colorPlayingDark = 0xffffffff;

            foreach (var noteBinding in trackAssistant.musicViewer.GetShownNotesBindings())
            {
                float tX0 = Math.Min(1.0f, Math.Max(0.0f, 1.0f * (noteBinding.noteInfo.startUs - timeRangeStartUs) / timeRangeUs));
                float tX1 = Math.Min(1.0f, Math.Max(0.0f, 1.0f * (noteBinding.noteInfo.endUs - timeRangeStartUs) / timeRangeUs));
                float tY = Math.Min(1.0f, Math.Max(0.0f, 1.0f * noteBinding.bindingIdx / trackAssistant.musicViewer.maxBindingsToShow));

                var posX0 = Position.Value.X + 10 + Size.Value.X * tX0;
                var posX1 = Position.Value.X + 10 + Size.Value.X * tX1;
                var posY = Position.Value.Y + 30 + (Size.Value.Y - 20) * tY;

                uint colorLight = UIColors.colorGray33;
                uint colorDark = UIColors.colorGray33;
                var noteInputChord = noteInput.GetNoteKeyBinding(noteBinding.noteInfo.note);
                bool canDraw = true;

                if (noteBinding.pressIdx < trackAssistant.musicViewer.maxBindingsToShow)
                {
                    // note colors:
                    // - keyboard: colorKeyboardNotes[noteBinding.bindingIdx]
                    // - gamepad:  gamepadStyle[noteButtonIdx]

                    if (noteInput.IsKeyboardMode)
                    {
                        bool hasValidBinding = (noteBinding.bindingIdx >= 0) && (noteBinding.bindingIdx < UIColors.colorKeyboardNotes.Length);
                        if (hasValidBinding)
                        {
                            colorLight = UIColors.colorKeyboardNotes[noteBinding.bindingIdx];
                            colorDark = UIColors.colorKeyboardNotesDark[noteBinding.bindingIdx];
                        }
                    }
                    else
                    {
                        if (mapGamepadColors.TryGetValue(noteInputChord.mainButton, out var gamepadColors))
                        {
                            colorLight = gamepadColors.colorLight;
                            colorDark = gamepadColors.colorDark;

                            if (gamepadColors.isDualColor)
                            {
                                drawList.AddRectFilledMultiColor(new Vector2(posX0, posY), new Vector2(posX1, posY + noteHalfHeight),
                                    gamepadColors.colorLightBk, gamepadColors.colorLightBk & colorFarMask, gamepadColors.colorLightBk & colorFarMask, gamepadColors.colorLightBk);

                                drawList.AddRectFilledMultiColor(new Vector2(posX0, posY - noteHalfHeight), new Vector2(posX1, posY),
                                    gamepadColors.colorLight, gamepadColors.colorLight & colorFarMask, gamepadColors.colorLight & colorFarMask, gamepadColors.colorLight);

                                canDraw = false;
                            }
                        }
                    }
                }

                if (canDraw)
                {
                    drawList.AddRectFilledMultiColor(new Vector2(posX0, posY - noteHalfHeight), new Vector2(posX1, posY + noteHalfHeight),
                        colorLight, colorLight & colorFarMask, colorLight & colorFarMask, colorLight);
                }

                if (noteBinding.showHint)
                {
                    InputBindingUtils.AddToDrawList(drawList, new Vector2(posX0 + 5, posY - ImGui.GetTextLineHeight() - 5), colorDark, noteInputChord);
                }

                if (noteBinding.pressIdx == 0)
                {
                    colorPlayingDark = colorDark;
                }
            }

            float tLX = 1.0f * trackAssistant.musicViewer.TimeRangeNowOffset / timeRangeUs;
            var posLineX = Position.Value.X + 10 + Size.Value.X * tLX;
            drawList.AddLine(new Vector2(posLineX, Position.Value.Y + 10), new Vector2(posLineX, Position.Value.Y + Size.Value.Y - 10), colorPlayingDark);
        }

        private void InitGamepadButtonColors()
        {
            if (InputBindingUtils.IsUsingXboxGamepadStyle)
            {
                mapGamepadColors.Add(GamepadButtons.DpadUp, new GamepadColors() { colorLight = UIColors.colorXboxActionNorth, colorDark = UIColors.colorXboxActionNorthDark, colorLightBk = UIColors.colorXboxDPadNorth, colorDarkBk = UIColors.colorXboxDPadNorthDark, isDualColor = true });
                mapGamepadColors.Add(GamepadButtons.DpadDown, new GamepadColors() { colorLight = UIColors.colorXboxActionSouth, colorDark = UIColors.colorXboxActionSouthDark, colorLightBk = UIColors.colorXboxDPadSouth, colorDarkBk = UIColors.colorXboxDPadSouthDark, isDualColor = true });
                mapGamepadColors.Add(GamepadButtons.DpadLeft, new GamepadColors() { colorLight = UIColors.colorXboxActionWest, colorDark = UIColors.colorXboxActionWestDark, colorLightBk = UIColors.colorXboxDPadWest, colorDarkBk = UIColors.colorXboxDPadWestDark, isDualColor = true });
                mapGamepadColors.Add(GamepadButtons.DpadRight, new GamepadColors() { colorLight = UIColors.colorXboxActionEast, colorDark = UIColors.colorXboxActionEastDark, colorLightBk = UIColors.colorXboxDPadEast, colorDarkBk = UIColors.colorXboxDPadEastDark, isDualColor = true });

                mapGamepadColors.Add(GamepadButtons.North, new GamepadColors() { colorLight = UIColors.colorXboxActionNorth, colorDark = UIColors.colorXboxActionNorthDark });
                mapGamepadColors.Add(GamepadButtons.South, new GamepadColors() { colorLight = UIColors.colorXboxActionSouth, colorDark = UIColors.colorXboxActionSouthDark });
                mapGamepadColors.Add(GamepadButtons.West, new GamepadColors() { colorLight = UIColors.colorXboxActionWest, colorDark = UIColors.colorXboxActionWestDark });
                mapGamepadColors.Add(GamepadButtons.East, new GamepadColors() { colorLight = UIColors.colorXboxActionEast, colorDark = UIColors.colorXboxActionEastDark });
            }
            else
            {
                mapGamepadColors.Add(GamepadButtons.DpadUp, new GamepadColors() { colorLight = UIColors.colorSonyActionNorth, colorDark = UIColors.colorSonyActionNorthDark, colorLightBk = UIColors.colorSonyDPadNorth, colorDarkBk = UIColors.colorSonyDPadNorthDark, isDualColor = true });
                mapGamepadColors.Add(GamepadButtons.DpadDown, new GamepadColors() { colorLight = UIColors.colorSonyActionSouth, colorDark = UIColors.colorSonyActionSouthDark, colorLightBk = UIColors.colorSonyDPadSouth, colorDarkBk = UIColors.colorSonyDPadSouthDark, isDualColor = true });
                mapGamepadColors.Add(GamepadButtons.DpadLeft, new GamepadColors() { colorLight = UIColors.colorSonyActionWest, colorDark = UIColors.colorSonyActionWestDark, colorLightBk = UIColors.colorSonyDPadWest, colorDarkBk = UIColors.colorSonyDPadWestDark, isDualColor = true });
                mapGamepadColors.Add(GamepadButtons.DpadRight, new GamepadColors() { colorLight = UIColors.colorSonyActionEast, colorDark = UIColors.colorSonyActionEastDark, colorLightBk = UIColors.colorSonyDPadEast, colorDarkBk = UIColors.colorSonyDPadEastDark, isDualColor = true });

                mapGamepadColors.Add(GamepadButtons.North, new GamepadColors() { colorLight = UIColors.colorSonyActionNorth, colorDark = UIColors.colorSonyActionNorthDark });
                mapGamepadColors.Add(GamepadButtons.South, new GamepadColors() { colorLight = UIColors.colorSonyActionSouth, colorDark = UIColors.colorSonyActionSouthDark });
                mapGamepadColors.Add(GamepadButtons.West, new GamepadColors() { colorLight = UIColors.colorSonyActionWest, colorDark = UIColors.colorSonyActionWestDark });
                mapGamepadColors.Add(GamepadButtons.East, new GamepadColors() { colorLight = UIColors.colorSonyActionEast, colorDark = UIColors.colorSonyActionEastDark });
            }

            mapGamepadColors.Add(GamepadButtons.None, new GamepadColors() { colorLight = UIColors.colorGray33, colorDark = UIColors.colorGray33 });
        }
    }
}
