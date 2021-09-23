using Dalamud.Game.ClientState.Keys;
using Dalamud.Game.Text;
using Dalamud.Interface;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using Melanchall.DryWetMidi.Interaction;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace HarpHero
{
    public class NoteInputMapper
    {
        private readonly NoteUIMapper uiMapper;
        private readonly UnsafeReaderPerformanceKeybinds bindingReader;

        public struct CompButtonDesc
        {
            public string simpleText;
            public ButtonDesc[] buttonList;

            public CompButtonDesc(ButtonDesc[] list)
            {
                simpleText = null;
                buttonList = list;

                if (list != null)
                {
                    int numIcons = 0;
                    int numCustomScales = 0;
                    for (int idx = 0; idx < list.Length; idx++)
                    {
                        numIcons += (list[idx].icon != FontAwesomeIcon.None) ? 1 : 0;
                        numCustomScales += (list[idx].customScale != 1.0f) ? 1 : 0;
                    }

                    if (numIcons == 0 && numCustomScales == 0)
                    {
                        buttonList = null;
                        simpleText = string.Join(" + ", list);
                    }
                }
            }

            public bool IsValid()
            {
                return !string.IsNullOrEmpty(simpleText) || buttonList != null;
            }
        }

        public struct ButtonDesc
        {
            public FontAwesomeIcon icon;
            public string text;
            public float customScale;

            public bool IsValid()
            {
                return !string.IsNullOrEmpty(text) || icon != FontAwesomeIcon.None;
            }

            public override string ToString()
            {
                return !string.IsNullOrEmpty(text) ? text : icon.ToString();
            }
        }

        public struct GamepadModeDesc
        {
            public ButtonDesc xbox;
            public ButtonDesc sony;

            public GamepadModeDesc(FontAwesomeIcon iconXbox, FontAwesomeIcon iconSony)
            {
                xbox = new ButtonDesc() { icon = iconXbox, customScale = 0.9f };
                sony = new ButtonDesc() { icon = iconSony, customScale = 0.9f };
            }

            public GamepadModeDesc(string textXbox, FontAwesomeIcon iconSony)
            {
                xbox = new ButtonDesc() { text = textXbox, customScale = 1.0f };
                sony = new ButtonDesc() { icon = iconSony, customScale = 0.9f };
            }

            public GamepadModeDesc(string textXbox, string textSony, float scaleSony = 1.0f)
            {
                xbox = new ButtonDesc() { text = textXbox, customScale = 1.0f };
                sony = new ButtonDesc() { text = textSony, customScale = scaleSony };
            }
        }

        private PerformanceBindingInfo? keyBinds;
        private Dictionary<int, CompButtonDesc> mapNoteBinding = new();
        private Dictionary<VirtualKey, ButtonDesc> mapVirtualKeyDesc = new();
        private Dictionary<GamepadButton, GamepadModeDesc> mapGamepadButtonDesc = new();
        private Dictionary<GamepadButton, int> mapGamepadSide = new();

        private ButtonDesc missingBinding = new();
        private CompButtonDesc missingCompBinging = new();

        private bool isWideModeCached = false;
        private bool isKeyboardMode = true;
        private bool isUsingXboxGamepadStyle = true;

        public NoteInputMapper(NoteUIMapper noteMapper, UnsafeReaderPerformanceKeybinds bindingReader)
        {
            this.uiMapper = noteMapper;
            this.bindingReader = bindingReader;

            // can't change without client restart
            isUsingXboxGamepadStyle = IsUsingXboxGamepadStyle();

            // font is kind of small, but still better than using mountains for triangle :<
            bool useUnicodeActions = true;

            mapGamepadButtonDesc.Add(GamepadButton.DPadN, new GamepadModeDesc(FontAwesomeIcon.ChevronCircleUp, FontAwesomeIcon.ChevronCircleUp));
            mapGamepadButtonDesc.Add(GamepadButton.DPadS, new GamepadModeDesc(FontAwesomeIcon.ChevronCircleDown, FontAwesomeIcon.ChevronCircleDown));
            mapGamepadButtonDesc.Add(GamepadButton.DPadE, new GamepadModeDesc(FontAwesomeIcon.ChevronCircleRight, FontAwesomeIcon.ChevronCircleRight));
            mapGamepadButtonDesc.Add(GamepadButton.DPadW, new GamepadModeDesc(FontAwesomeIcon.ChevronCircleLeft, FontAwesomeIcon.ChevronCircleLeft));

            if (useUnicodeActions)
            {
                mapGamepadButtonDesc.Add(GamepadButton.ActionN, new GamepadModeDesc("Y", "" + Convert.ToChar(SeIconChar.Triangle), 1.25f));
                mapGamepadButtonDesc.Add(GamepadButton.ActionS, new GamepadModeDesc("A", "" + Convert.ToChar(SeIconChar.Cross), 1.25f));
                mapGamepadButtonDesc.Add(GamepadButton.ActionE, new GamepadModeDesc("B", "" + Convert.ToChar(SeIconChar.Circle), 1.25f));
                mapGamepadButtonDesc.Add(GamepadButton.ActionW, new GamepadModeDesc("X", "" + Convert.ToChar(SeIconChar.Square), 1.25f));
            }
            else
            {
                mapGamepadButtonDesc.Add(GamepadButton.ActionN, new GamepadModeDesc("Y", FontAwesomeIcon.Mountain));
                mapGamepadButtonDesc.Add(GamepadButton.ActionS, new GamepadModeDesc("A", FontAwesomeIcon.Times));
                mapGamepadButtonDesc.Add(GamepadButton.ActionE, new GamepadModeDesc("B", FontAwesomeIcon.CircleNotch));
                mapGamepadButtonDesc.Add(GamepadButton.ActionW, new GamepadModeDesc("X", FontAwesomeIcon.Expand));
            }

            mapGamepadButtonDesc.Add(GamepadButton.LB, new GamepadModeDesc("LB", "L1"));
            mapGamepadButtonDesc.Add(GamepadButton.LT, new GamepadModeDesc("LT", "L2"));
            mapGamepadButtonDesc.Add(GamepadButton.RB, new GamepadModeDesc("RB", "R1"));
            mapGamepadButtonDesc.Add(GamepadButton.RT, new GamepadModeDesc("RT", "R2"));

            mapGamepadSide.Add(GamepadButton.DPadN, -1);
            mapGamepadSide.Add(GamepadButton.DPadS, -1);
            mapGamepadSide.Add(GamepadButton.DPadE, -1);
            mapGamepadSide.Add(GamepadButton.DPadW, -1);
            mapGamepadSide.Add(GamepadButton.LB, -1);
            mapGamepadSide.Add(GamepadButton.LT, -1);

            mapGamepadSide.Add(GamepadButton.ActionN, 1);
            mapGamepadSide.Add(GamepadButton.ActionS, 1);
            mapGamepadSide.Add(GamepadButton.ActionE, 1);
            mapGamepadSide.Add(GamepadButton.ActionW, 1);
            mapGamepadSide.Add(GamepadButton.RB, 1);
            mapGamepadSide.Add(GamepadButton.RT, 1);
        }

        public void OnPlayChanged(bool active)
        {
            if (active)
            {
                keyBinds = bindingReader.ReadBindings();

                mapNoteBinding.Clear();
                isWideModeCached = uiMapper.isWideMode;
            }
        }

        public void OnKeyboardModeChanged(bool isKeyboard)
        {
            isKeyboardMode = isKeyboard;
            mapNoteBinding.Clear();
        }

        private bool FindNoteKeyOctaveBindings(PerformanceBindingInfo.Mode modeBindings, int useNoteIdx, int useOctaveOffset, out VirtualKey noteKey, out VirtualKey octaveKey)
        {
            noteKey = modeBindings.notes[useNoteIdx];
            bool hasBindings = noteKey != VirtualKey.NO_KEY;

            octaveKey = VirtualKey.NO_KEY;
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

            return hasBindings;
        }

        private ButtonDesc[] GetNoteOctaveKeyboardBindings(PerformanceBindingInfo.Mode modeBindings, int useNoteIdx, int useOctaveOffset)
        {
            if (FindNoteKeyOctaveBindings(modeBindings, useNoteIdx, useOctaveOffset, out VirtualKey noteKey, out VirtualKey octaveKey))
            {
                if (octaveKey != VirtualKey.NO_KEY)
                {
                    return new ButtonDesc[] { GetVirtualKeyDesc(octaveKey), GetVirtualKeyDesc(noteKey) };
                }

                return new ButtonDesc[] { GetVirtualKeyDesc(noteKey) };
            }

            return null;
        }

        private ButtonDesc[] GetNoteOctaveGamepadBindings(int useNoteIdx, int useOctaveOffset)
        {
            if (useNoteIdx < 0 || useNoteIdx > 12)
            {
                return null;
            }

            // special case for mid octave high C
            if (useNoteIdx == 0 && useOctaveOffset == 1 && keyBinds.Value.gamepadNotes[12] != GamepadButton.Unknown)
            {
                useNoteIdx = 12;
                useOctaveOffset = 0;
            }

            if (useOctaveOffset < -1 || useOctaveOffset > 1)
            {
                return null;
            }

            var activeOctaveMod = (useOctaveOffset < 0) ? keyBinds.Value.gamepadOctaveDown :
                (useOctaveOffset > 0) ? keyBinds.Value.gamepadOctaveUp :
                GamepadButton.Unknown;

            if (useOctaveOffset != 0 && activeOctaveMod == GamepadButton.Unknown)
            {
                return null;
            }

            var descParts = new List<ButtonDesc>();
            if (activeOctaveMod != GamepadButton.Unknown)
            {
                descParts.Add(GetGamepadButtonDesc(activeOctaveMod));
            }

            if (keyBinds.Value.gamepadNotes[useNoteIdx] != GamepadButton.Unknown)
            {
                descParts.Add(GetGamepadButtonDesc(keyBinds.Value.gamepadNotes[useNoteIdx]));
            }
            else
            {
                GamepadButton foundBind = GamepadButton.Unknown;

                // require both for "simplicity" below
                if (keyBinds.Value.gamepadHalfDown != GamepadButton.Unknown && keyBinds.Value.gamepadHalfUp != GamepadButton.Unknown)
                {
                    // feeling generous for fingers: prioritize step up/down to keep on the same side of pad as active octave modifier
                    bool preferStepUp = (activeOctaveMod == GamepadButton.Unknown) || mapGamepadSide[activeOctaveMod] == mapGamepadSide[keyBinds.Value.gamepadHalfUp];
                    if (preferStepUp)
                    {
                        if (useNoteIdx < 12)
                        {
                            foundBind = keyBinds.Value.gamepadNotes[useNoteIdx + 1];
                            if (foundBind != GamepadButton.Unknown)
                            {
                                descParts.Add(GetGamepadButtonDesc(keyBinds.Value.gamepadHalfUp));
                                descParts.Add(GetGamepadButtonDesc(foundBind));
                            }
                        }
                    }

                    if (foundBind == GamepadButton.Unknown)
                    {
                        if (useNoteIdx > 0)
                        {
                            foundBind = keyBinds.Value.gamepadNotes[useNoteIdx - 1];
                            if (foundBind != GamepadButton.Unknown)
                            {
                                descParts.Add(GetGamepadButtonDesc(keyBinds.Value.gamepadHalfDown));
                                descParts.Add(GetGamepadButtonDesc(foundBind));
                            }
                        }
                    }

                    if (!preferStepUp && foundBind == GamepadButton.Unknown)
                    {
                        if (useNoteIdx < 12)
                        {
                            foundBind = keyBinds.Value.gamepadNotes[useNoteIdx + 1];
                            if (foundBind != GamepadButton.Unknown)
                            {
                                descParts.Add(GetGamepadButtonDesc(keyBinds.Value.gamepadHalfUp));
                                descParts.Add(GetGamepadButtonDesc(foundBind));
                            }
                        }
                    }
                }

                if (foundBind == GamepadButton.Unknown)
                {
                    return null;
                }
            }

            return descParts.ToArray();
        }

        public CompButtonDesc GetNoteKeyBinding(Note note)
        {
            if (isWideModeCached != uiMapper.isWideMode)
            {
                mapNoteBinding.Clear();
                isWideModeCached = uiMapper.isWideMode;
            }

            int noteNumber = note.NoteNumber;
            if (mapNoteBinding.TryGetValue(noteNumber, out var noteBindings))
            {
                return noteBindings;
            }

            if (!keyBinds.HasValue || !uiMapper.GetMappedNoteIdx(note, out int mappedNoteIdx, out int octaveOffset))
            {
                return missingCompBinging;
            }

            ButtonDesc[] descParts = null;
            if (isKeyboardMode)
            {
                if (isWideModeCached)
                {
                    descParts = GetNoteOctaveKeyboardBindings(keyBinds.Value.threeOctaves, mappedNoteIdx, octaveOffset);
                    if (descParts == null && octaveOffset == 0)
                    {
                        // no binds: lower/higher octave? try using center + offset
                        if (mappedNoteIdx < 12)
                        {
                            descParts = GetNoteOctaveKeyboardBindings(keyBinds.Value.threeOctaves, mappedNoteIdx + 12, -1);
                        }
                        else if (mappedNoteIdx >= 12 + 12)
                        {
                            descParts = GetNoteOctaveKeyboardBindings(keyBinds.Value.threeOctaves, mappedNoteIdx - 12, 1);
                        }
                    }
                }
                else
                {
                    descParts = GetNoteOctaveKeyboardBindings(keyBinds.Value.singleOctave, mappedNoteIdx, octaveOffset);
                }
            }
            else
            {
                descParts = GetNoteOctaveGamepadBindings(mappedNoteIdx, octaveOffset);
            }

            var compDesc = new CompButtonDesc(descParts);
            mapNoteBinding.Add(noteNumber, compDesc);
            return compDesc;
        }

        public ButtonDesc GeOctaveKeyBinding(int octaveOffset)
        {
            if (!keyBinds.HasValue || octaveOffset == 0)
            {
                return missingBinding;
            }

            if (isKeyboardMode)
            {
                var octaveKey = uiMapper.isWideMode ?
                    ((octaveOffset > 0) ? keyBinds.Value.threeOctaves.octaveUp : keyBinds.Value.threeOctaves.octaveDown) :
                    ((octaveOffset > 0) ? keyBinds.Value.singleOctave.octaveUp : keyBinds.Value.singleOctave.octaveDown);

                if (octaveKey != VirtualKey.NO_KEY)
                {
                    return GetVirtualKeyDesc(octaveKey);
                }
            }
            else
            {
                var gamepadButton = (octaveOffset > 0) ? keyBinds.Value.gamepadOctaveUp : keyBinds.Value.gamepadOctaveDown;
                if (gamepadButton != GamepadButton.Unknown)
                {
                    return GetGamepadButtonDesc(gamepadButton);
                }
            }

            return missingBinding;
        }

        private unsafe bool IsUsingXboxGamepadStyle()
        {
            // magic number being magical.
            const short gamepadStyleOption = 91;

            ConfigModule* modulePtr = ConfigModule.Instance();
            if (modulePtr != null)
            {
                var valuePtr = modulePtr->GetValueById(gamepadStyleOption);
                if (valuePtr != null && valuePtr->Value > 0)
                {
                    return false;
                }
            }

            return true;
        }

        private ButtonDesc GetGamepadButtonDesc(GamepadButton button)
        {
            if (mapGamepadButtonDesc.TryGetValue(button, out var combinedDesc))
            {
                return isUsingXboxGamepadStyle ? combinedDesc.xbox : combinedDesc.sony;
            }

            return missingBinding;
        }

        private ButtonDesc GetVirtualKeyDesc(VirtualKey key)
        {
            if (mapVirtualKeyDesc.TryGetValue(key, out var desc))
            {
                return desc;
            }

            var resultDesc = new StringBuilder();
            uint scanCode = MapVirtualKey((uint)key, 0);
            int lParam = (int)(scanCode << 16);

            GetKeyNameText(lParam, resultDesc, 260);
            desc = new ButtonDesc() { text = resultDesc.ToString(), customScale = 1.0f };

            mapVirtualKeyDesc.Add(key, desc);
            return desc;
        }

        [DllImport("user32.dll")]
        static extern uint MapVirtualKey(uint uCode, uint uMapType);

        [DllImport("user32.dll")]
        static extern int GetKeyNameText(int lParam, [Out] StringBuilder lpString, int nSize);
    }
}
