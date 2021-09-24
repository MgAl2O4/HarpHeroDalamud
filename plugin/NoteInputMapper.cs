using Dalamud.Game.ClientState.GamePad;
using Dalamud.Game.ClientState.Keys;
using Melanchall.DryWetMidi.Interaction;
using System.Collections.Generic;

namespace HarpHero
{
    public class NoteInputMapper
    {
        private readonly NoteUIMapper uiMapper;
        private readonly UnsafeReaderPerformanceKeybinds bindingReader;

        private PerformanceBindingInfo? keyBinds;
        private Dictionary<int, InputBindingChord> mapNoteBinding = new();
        private GamepadButtons gamepadButtonsLeft = 0;

        private InputBindingKey missingBindingKey = new();
        private InputBindingChord missingBingingChord = new();

        private bool isWideModeCached = false;
        private bool isKeyboardMode = true;

        public bool IsKeyboardMode => isKeyboardMode;

        public NoteInputMapper(NoteUIMapper noteMapper, UnsafeReaderPerformanceKeybinds bindingReader)
        {
            this.uiMapper = noteMapper;
            this.bindingReader = bindingReader;

            gamepadButtonsLeft = GamepadButtons.DpadUp | GamepadButtons.DpadDown | GamepadButtons.DpadLeft | GamepadButtons.DpadRight | GamepadButtons.L1 | GamepadButtons.L2;
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

        private InputBindingKey[] GetNoteOctaveKeyboardBindings(PerformanceBindingInfo.Mode modeBindings, int useNoteIdx, int useOctaveOffset)
        {
            if (FindNoteKeyOctaveBindings(modeBindings, useNoteIdx, useOctaveOffset, out VirtualKey noteKey, out VirtualKey octaveKey))
            {
                if (octaveKey != VirtualKey.NO_KEY)
                {
                    return new InputBindingKey[] { InputBindingUtils.GetVirtualKeyData(octaveKey), InputBindingUtils.GetVirtualKeyData(noteKey) };
                }

                return new InputBindingKey[] { InputBindingUtils.GetVirtualKeyData(noteKey) };
            }

            return null;
        }

        private InputBindingKey[] GetNoteOctaveGamepadBindings(int useNoteIdx, int useOctaveOffset)
        {
            if (useNoteIdx < 0 || useNoteIdx > 12)
            {
                return null;
            }

            // special case for mid octave high C
            if (useNoteIdx == 0 && useOctaveOffset == 1 && keyBinds.Value.gamepadNotes[12] != GamepadButtons.None)
            {
                useNoteIdx = 12;
                useOctaveOffset = 0;
            }

            if (useOctaveOffset < -1 || useOctaveOffset > 1)
            {
                return null;
            }

            var activeOctaveBind = (useOctaveOffset < 0) ? keyBinds.Value.gamepadOctaveDown :
                (useOctaveOffset > 0) ? keyBinds.Value.gamepadOctaveUp :
                GamepadButtons.None;

            if (useOctaveOffset != 0 && activeOctaveBind == GamepadButtons.None)
            {
                return null;
            }

            var descParts = new List<InputBindingKey>();
            if (activeOctaveBind != GamepadButtons.None)
            {
                descParts.Add(InputBindingUtils.GetGamepadButtonData(activeOctaveBind));
            }

            var noteBind = keyBinds.Value.gamepadNotes[useNoteIdx];
            if (noteBind == GamepadButtons.None)
            {
                var halfStepBind = GamepadButtons.None;

                // require both for "simplicity" below
                if (keyBinds.Value.gamepadHalfDown != GamepadButtons.None && keyBinds.Value.gamepadHalfUp != GamepadButtons.None)
                {
                    // feeling generous for fingers: prioritize step up/down to keep on the same side of pad as active octave modifier
                    bool isOctaveModOnLeft = (gamepadButtonsLeft & activeOctaveBind) == activeOctaveBind;
                    bool isStepUpOnLeft = (gamepadButtonsLeft & keyBinds.Value.gamepadHalfUp) == keyBinds.Value.gamepadHalfUp;
                    bool preferStepUp = (activeOctaveBind == GamepadButtons.None) || (isOctaveModOnLeft == isStepUpOnLeft);

                    if (preferStepUp && useNoteIdx < 12)
                    {
                        noteBind = keyBinds.Value.gamepadNotes[useNoteIdx + 1];
                        halfStepBind = (noteBind == GamepadButtons.None) ? GamepadButtons.None : keyBinds.Value.gamepadHalfUp;
                    }

                    if (noteBind == GamepadButtons.None && useNoteIdx > 0)
                    {
                        noteBind = keyBinds.Value.gamepadNotes[useNoteIdx - 1];
                        halfStepBind = (noteBind == GamepadButtons.None) ? GamepadButtons.None : keyBinds.Value.gamepadHalfDown;
                    }

                    if (noteBind == GamepadButtons.None && !preferStepUp && useNoteIdx < 12)
                    {
                        noteBind = keyBinds.Value.gamepadNotes[useNoteIdx + 1];
                        halfStepBind = (noteBind == GamepadButtons.None) ? GamepadButtons.None : keyBinds.Value.gamepadHalfUp;
                    }
                }

                if (noteBind == GamepadButtons.None || halfStepBind == GamepadButtons.None)
                {
                    return null;
                }

                descParts.Add(InputBindingUtils.GetGamepadButtonData(halfStepBind));
            }

            descParts.Add(InputBindingUtils.GetGamepadButtonData(noteBind));
            return descParts.ToArray();
        }

        public InputBindingChord GetNoteKeyBinding(Note note)
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
                return missingBingingChord;
            }

            InputBindingKey[] descParts = null;
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

            var newInputChord = new InputBindingChord(descParts);
            mapNoteBinding.Add(noteNumber, newInputChord);
            return newInputChord;
        }

        public InputBindingKey GeOctaveKeyBinding(int octaveOffset)
        {
            if (!keyBinds.HasValue || octaveOffset == 0)
            {
                return missingBindingKey;
            }

            if (isKeyboardMode)
            {
                var octaveKey = uiMapper.isWideMode ?
                    ((octaveOffset > 0) ? keyBinds.Value.threeOctaves.octaveUp : keyBinds.Value.threeOctaves.octaveDown) :
                    ((octaveOffset > 0) ? keyBinds.Value.singleOctave.octaveUp : keyBinds.Value.singleOctave.octaveDown);

                if (octaveKey != VirtualKey.NO_KEY)
                {
                    return InputBindingUtils.GetVirtualKeyData(octaveKey);
                }
            }
            else
            {
                var gamepadButton = (octaveOffset > 0) ? keyBinds.Value.gamepadOctaveUp : keyBinds.Value.gamepadOctaveDown;
                if (gamepadButton != GamepadButtons.None)
                {
                    return InputBindingUtils.GetGamepadButtonData(gamepadButton);
                }
            }

            return missingBindingKey;
        }
    }
}
