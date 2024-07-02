using Dalamud.Game.ClientState.GamePad;
using Dalamud.Interface.Utility;
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

        public PluginWindowBindAssistant(UIReaderBardPerformance uiReader, NoteUIMapper noteMapper, NoteInputMapper noteInput) : base("Bind Assistant")
        {
            this.uiReader = uiReader;
            this.noteMapper = noteMapper;
            this.noteInput = noteInput;

            uiReader.OnVisibilityChanged += OnPerformanceActive;
            Service.trackAssistant.OnPlayChanged += OnPlayChanged;

            IsOpen = false;

            // doesn't matter will be updated on next draw
            PositionCondition = ImGuiCond.None;
            SizeCondition = ImGuiCond.None;
            RespectCloseHotkey = false;
            ForceMainWindow = true;

            Flags = ImGuiWindowFlags.NoDecoration |
                ImGuiWindowFlags.NoResize |
                ImGuiWindowFlags.NoSavedSettings |
                ImGuiWindowFlags.NoMove |
                ImGuiWindowFlags.NoMouseInputs |
                ImGuiWindowFlags.NoDocking |
                ImGuiWindowFlags.NoFocusOnAppearing |
                ImGuiWindowFlags.NoNav;

            InitGamepadButtonColors();

            Plugin.OnDebugSnapshot += (_) =>
            {
                int drawErrState =
                    (Service.trackAssistant == null) ? 1 :
                    (Service.trackAssistant.musicViewer == null) ? 2 :
                    !Size.HasValue ? 3 :
                    !Service.trackAssistant.IsPlaying ? 4 :
                    0;

                Service.logger.Info($"PluginWindowBindAssistant: open:{IsOpen}, numNotes:{noteMapper.notes?.Length ?? 0}, canShow:{Service.trackAssistant?.CanShowBindAssistant}, fade:{BgAlpha} ({noMusicUpkeepRemaining}), drawErr:{drawErrState}");
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
            else if (Service.trackAssistant?.IsPlaying ?? false)
            {
                OnPlayChanged(true);
            }
        }

        public void OnPlayChanged(bool active)
        {
            if (Service.trackAssistant.CanShowBindAssistant)
            {
                if (active && uiReader.IsVisible)
                {
                    IsOpen = true;
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
                float upkeepPct = (noMusicUpkeepRemaining / NoMusicUpkeepTime);
                float upkeepAlpha = upkeepPct * upkeepPct;
                float trackSizePerHints = TrackAssistSizeMinY * (Math.Max(3, Service.config.AssistBindRows) / 3.0f);
                float newWindowPosY = Math.Max(50, uiReader.cachedState.keysPos.Y - (trackSizePerHints + TrackAssistOffsetY) * ImGuiHelpers.GlobalScale);

                bool isWide = (uiReader.cachedState.keys.Count > 13);
                float useScale = noteInput.IsKeyboardMode ? Service.config.AssistBindScaleKeyboard : Service.config.AssistBindScaleGamepad;
                float newWindowSizeX = useScale * uiReader.cachedState.keysSize.X / (isWide ? 3 : 1);
                float newWindowPosX = uiReader.cachedState.keysPos.X + (uiReader.cachedState.keysSize.X - newWindowSizeX) * 0.5f;

                Position = new Vector2(newWindowPosX, newWindowPosY);
                Size = new Vector2(newWindowSizeX, uiReader.cachedState.keysPos.Y - newWindowPosY) / ImGuiHelpers.GlobalScale;
                BgAlpha = upkeepAlpha * Service.config.AssistBgAlpha;
            }
        }

        public bool Tick(float deltaSeconds)
        {
            bool canFadeOut = IsOpen && (Service.trackAssistant == null || !Service.trackAssistant.IsPlaying);
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
            Service.trackAssistant?.OnAssistantWindowClosed();
        }

        public override void OnOpen()
        {
            Service.trackAssistant?.OnAssistantWindowOpened();
        }

        public override void Draw()
        {
            if (Service.trackAssistant != null && Service.trackAssistant.musicViewer != null && Size.HasValue)
            {
                if (Service.trackAssistant.IsPlaying)
                {
                    noMusicUpkeepRemaining = NoMusicUpkeepTime;

                    DrawBindTimeline();
                }
            }
        }

        private struct BindHintDrawInfo
        {
            public Vector2 pos;
            public Vector2 size;
            public uint color;
            public float notePosY;
            public int noteNum;
            public InputBindingChord chord;
        }

        private void DrawBindTimeline()
        {
            if (Service.trackAssistant.musicViewer == null || Position == null || Size == null)
            {
                return;
            }

            var drawList = ImGui.GetWindowDrawList();
            var viewportOffset = ImGui.GetMainViewport().Pos;
            var timeRangeStartUs = Service.trackAssistant.musicViewer.TimeRangeStartUs;
            var timeRangeUs = Service.trackAssistant.musicViewer.TimeRangeUs;
            var bindHints = new List<BindHintDrawInfo>();

            float noteHalfHeight = 5.0f;
            uint colorFarMask = 0x40ffffff;
            uint colorPlayingDark = 0xffffffff;

            foreach (var noteBinding in Service.trackAssistant.musicViewer.shownBindings)
            {
                float tX0Raw = 1.0f * (noteBinding.noteInfo.startUs - timeRangeStartUs) / timeRangeUs;
                float tX0 = Math.Min(1.0f, Math.Max(0.0f, tX0Raw));
                float tX1 = Math.Min(1.0f, Math.Max(0.0f, 1.0f * (noteBinding.noteInfo.endUs - timeRangeStartUs) / timeRangeUs));
                float tY = Math.Min(1.0f, Math.Max(0.0f, 1.0f * noteBinding.bindingIdx / Service.trackAssistant.musicViewer.maxBindingsToShow));

                var posX0Raw = Position.Value.X + viewportOffset.X + (10 + (Size.Value.X * tX0Raw)) * ImGuiHelpers.GlobalScale;
                var posX0 = Position.Value.X + viewportOffset.X + (10 + (Size.Value.X * tX0)) * ImGuiHelpers.GlobalScale;
                var posX1 = Position.Value.X + viewportOffset.X + (10 + (Size.Value.X * tX1)) * ImGuiHelpers.GlobalScale;
                var posY = Position.Value.Y + viewportOffset.Y + (30 + ((Size.Value.Y - 20) * tY)) * ImGuiHelpers.GlobalScale;

                uint colorLight = UIColors.colorGray33;
                uint colorDark = UIColors.colorGray33;
                var noteInputChord = noteInput.GetNoteKeyBinding(noteBinding.noteInfo.note);
                bool canDraw = true;

                if (noteBinding.hasBindingConflict)
                {
                    var colorLightBk = UIColors.colorGray50;

                    drawList.AddRectFilledMultiColor(new Vector2(posX0, posY), new Vector2(posX1, posY + noteHalfHeight),
                        colorLightBk, colorLightBk & colorFarMask, colorLightBk & colorFarMask, colorLightBk);

                    drawList.AddRectFilledMultiColor(new Vector2(posX0, posY - noteHalfHeight), new Vector2(posX1, posY),
                        colorLight, colorLight & colorFarMask, colorLight & colorFarMask, colorLight);

                    canDraw = false;
                }
                else if (noteBinding.pressIdx < Service.trackAssistant.musicViewer.maxBindingsToShow)
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
                    var hintSize = InputBindingUtils.CalcInputChordSize(noteInputChord);
                    var hintPos = new Vector2(posX0 + 5, posY - ImGui.GetTextLineHeight() - 5);

                    bindHints.Add(new BindHintDrawInfo() { pos = hintPos, size = hintSize, chord = noteInputChord, color = colorDark, notePosY = posY, noteNum = noteBinding.noteInfo.note.NoteNumber });
                }

                if (noteBinding.pressIdx == 0)
                {
                    colorPlayingDark = colorDark;
                }
            }

            // draw from furthest to most current for proper occlusions
            var padOfset = 5 * ImGuiHelpers.GlobalScale;
            uint bindBackground = UIColors.GetAlphaModulated(0xcc000000, Service.config.AssistBgAlpha);

            for (int hintIdx = bindHints.Count - 1; hintIdx >= 0; hintIdx--)
            {
                var hintInfo = bindHints[hintIdx];
                if (hintIdx == 0)
                {
                    drawList.AddRectFilled(new Vector2(hintInfo.pos.X - padOfset, hintInfo.pos.Y), new Vector2(hintInfo.pos.X + hintInfo.size.X + padOfset, hintInfo.notePosY - noteHalfHeight - 1), bindBackground);
                }

                // if overlapping with next AND being exactly the same note, make more transparent
                uint useHintColor = hintInfo.color;
                if ((hintIdx - 1 >= 0) &&
                    bindHints[hintIdx - 1].noteNum == hintInfo.noteNum &&
                    bindHints[hintIdx - 1].notePosY == hintInfo.notePosY)
                {
                    bool isOverlapping = (bindHints[hintIdx - 1].pos.X + bindHints[hintIdx - 1].size.X) > hintInfo.pos.X;
                    if (isOverlapping)
                    {
                        useHintColor &= 0x40ffffff;
                    }
                }

                InputBindingUtils.AddToDrawList(drawList, hintInfo.pos, useHintColor, hintInfo.chord);
            }

            float tLX = 1.0f * Service.trackAssistant.musicViewer.TimeRangeNowOffset / timeRangeUs;
            var posLineX = Position.Value.X + viewportOffset.X + (10 + (Size.Value.X * tLX)) * ImGuiHelpers.GlobalScale;

            drawList.AddLine(
                new Vector2(posLineX, Position.Value.Y + 10 + viewportOffset.Y),
                new Vector2(posLineX, Position.Value.Y + viewportOffset.Y + (Size.Value.Y - 10) * ImGuiHelpers.GlobalScale),
                colorPlayingDark);
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
