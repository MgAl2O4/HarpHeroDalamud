using Dalamud;
using Dalamud.Game.ClientState.GamePad;
using Dalamud.Game.ClientState.Keys;
using FFXIVClientStructs.FFXIV.Client.UI;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace HarpHero
{
    public struct PerformanceBindingInfo
    {
        public struct Mode
        {
            public VirtualKey[] notes;
            public VirtualKey octaveUp;
            public VirtualKey octaveDown;
        }

        public Mode singleOctave;
        public Mode threeOctaves;

        public GamepadButtons[] gamepadNotes;
        public GamepadButtons gamepadOctaveUp;
        public GamepadButtons gamepadOctaveDown;
        public GamepadButtons gamepadHalfUp;
        public GamepadButtons gamepadHalfDown;
    }

    public unsafe class UnsafeReaderPerformanceKeybinds
    {
        [StructLayout(LayoutKind.Explicit, Size = 0xB)]
        private unsafe struct KeybindMemory
        {
            [FieldOffset(0x0)] public byte Key;
            [FieldOffset(0x2)] public byte Gamepad;
        }

        public bool HasErrors { get; private set; }

        private int baseShortNotes = 0;
        private int baseShortOctave = 0;
        private int baseWideNotes = 0;
        private int baseWideOctave = 0;
        private int baseGamepadNotes = 0;
        private int baseGamepadModifiers = 0;

        private Dictionary<byte, GamepadButtons> mapGamepad = new();
        private Dictionary<byte, VirtualKey> mapKeys = new();

        public UnsafeReaderPerformanceKeybinds()
        {
            var ptrShortNotes = IntPtr.Zero;
            var ptrShortOctaves = IntPtr.Zero;
            var ptrWideNotes = IntPtr.Zero;
            var ptrWideOctaves = IntPtr.Zero;
            var ptrGamepadNotes = IntPtr.Zero;
            var ptrGamepadModifiers = IntPtr.Zero;

            if (Service.sigScanner != null)
            {
                // use LEA opcode from setting loops in WritePerformanceBindingsWide/WritePerformanceBindingsSingleOctave
                try
                {
                    ptrShortNotes = Service.sigScanner.GetStaticAddressFromSig("48 8d 35 ?? ?? ?? ?? 4c 8b e1 4c 2b e6 4c 8d 71 10 48 8b e9 41 bf 16");
                    ptrWideNotes = Service.sigScanner.GetStaticAddressFromSig("48 8d 35 ?? ?? ?? ?? 4c 8b e1 4c 2b e6 4c 8d 71 10 48 8b e9 41 bf 2e");
                    ptrShortOctaves = Service.sigScanner.GetStaticAddressFromSig("48 8d 35 ?? ?? ?? ?? 48 2b ee 4c 8d bd 84 00 00 00 bd 02 00");
                    ptrWideOctaves = Service.sigScanner.GetStaticAddressFromSig("48 8d 35 ?? ?? ?? ?? 48 2b ee 4c 8d bd 44 01 00 00 bd 02 00");
                    ptrGamepadNotes = Service.sigScanner.GetStaticAddressFromSig("48 8d 3d ?? ?? ?? ?? 33 db 4c 8d 3d ?? ?? ?? ?? 66 0f");
                    ptrGamepadModifiers = Service.sigScanner.GetStaticAddressFromSig("48 8D 3D ?? ?? ?? ?? 66 66 0F 1F 84 ?? 00 00 00 00 48 8B 4E");
                }
                catch (Exception ex)
                {
                    Service.logger.Error(ex, "oh noes!");
                }
            }

            HasErrors = (ptrShortNotes == IntPtr.Zero) || (ptrShortOctaves == IntPtr.Zero) ||
                (ptrWideNotes == IntPtr.Zero) || (ptrWideOctaves == IntPtr.Zero) ||
                (ptrGamepadNotes == IntPtr.Zero) || (ptrGamepadModifiers == IntPtr.Zero);

            HasErrors = HasErrors || !SafeMemory.Read(ptrShortNotes, out baseShortNotes);
            HasErrors = HasErrors || !SafeMemory.Read(ptrShortOctaves, out baseShortOctave);
            HasErrors = HasErrors || !SafeMemory.Read(ptrWideNotes, out baseWideNotes);
            HasErrors = HasErrors || !SafeMemory.Read(ptrWideOctaves, out baseWideOctave);
            HasErrors = HasErrors || !SafeMemory.Read(ptrGamepadNotes, out baseGamepadNotes);
            HasErrors = HasErrors || !SafeMemory.Read(ptrGamepadModifiers, out baseGamepadModifiers);

            if (HasErrors)
            {
                Service.logger.Error("Failed to find key bind indices, turning reader off");
            }
            else
            {
                // seems to be hardcoded mapping, idk if there's ay meaning to those numbers :<
                mapGamepad.Add(0xA7, GamepadButtons.DpadUp);
                mapGamepad.Add(0xA8, GamepadButtons.DpadDown);
                mapGamepad.Add(0xA9, GamepadButtons.DpadLeft);
                mapGamepad.Add(0xAA, GamepadButtons.DpadRight);
                mapGamepad.Add(0xAB, GamepadButtons.North);
                mapGamepad.Add(0xAC, GamepadButtons.South);
                mapGamepad.Add(0xAD, GamepadButtons.West);
                mapGamepad.Add(0xAE, GamepadButtons.East);
                mapGamepad.Add(0xAF, GamepadButtons.L1);
                mapGamepad.Add(0xB0, GamepadButtons.L2);
                mapGamepad.Add(0xB2, GamepadButtons.R1);
                mapGamepad.Add(0xB3, GamepadButtons.R2);

                // non alphanumeric chars are not real virtual keys? SE being SE...
                mapKeys.Add(0x82, VirtualKey.OEM_PLUS);
                mapKeys.Add(0x83, VirtualKey.OEM_COMMA);
                mapKeys.Add(0x84, VirtualKey.OEM_MINUS);
                mapKeys.Add(0x85, VirtualKey.OEM_PERIOD);
                mapKeys.Add(0x86, VirtualKey.OEM_2);
                mapKeys.Add(0x87, VirtualKey.OEM_1);
                mapKeys.Add(0x88, VirtualKey.OEM_3);
                mapKeys.Add(0x89, VirtualKey.OEM_4);
                mapKeys.Add(0x8A, VirtualKey.OEM_5);
                mapKeys.Add(0x8B, VirtualKey.OEM_6);
                mapKeys.Add(0x8C, VirtualKey.OEM_7);
                mapKeys.Add(0x8D, VirtualKey.OEM_8);
            }

            Plugin.OnDebugSnapshot += (_) =>
            {
                Service.logger.Info($"UnsafeReaderPerformanceKeybinds: error:{HasErrors} (S:{baseShortNotes}:{baseShortOctave}, W:{baseWideNotes}:{baseWideOctave}, G:{baseGamepadNotes}:{baseGamepadModifiers})");
            };
        }

        public PerformanceBindingInfo? ReadBindings()
        {
            if (HasErrors)
            {
                // hard nope, reverse code again.
                return null;
            }

            PerformanceBindingInfo? resultBindings = null;
            try
            {
                // .text: 4c 8d 71 10 48 8b e9 41 bf 2e 00 00 00  => WritePerformanceBindingsWide(void* PerformanceSettingsAgent)
                // .text: 4c 8d 71 10 48 8b e9 41 bf 16 00 00 00  => WritePerformanceBindingsSingleOctave(void* PerformanceSettingsAgent)
                // .text: 88 44 2a 2a 33 c0 c6 44 24 22 00 39 19  => WritePerformanceBindingsGamepad(void* PerformanceSettingsAgent)
                // 
                // WritePerformanceBindings: open settings agent in memory view, break on binding byte access, snigle function reading it on save ^
                //
                // inputManager = uiModule.vf60()
                // bindingData = inputManager + 0x9b0
                //
                // binding indicies in 5.58:
                // - 0x243 (0x2e elems = notes)
                // - 0x23f (0x2 elems = octave modifiers)
                // - 0x22b (0x16 elems = notes)
                // - 0x227 (0x2 elems = octave modifiers)
                // - 0x22b (0x16 elems = gamepad notes)
                // - 0x227 (0x4 elems = gamepad modifiers)

                var uiModule = (Service.gameGui != null) ? (UIModule*)Service.gameGui.GetUIModule() : null;
                var uiInputData = (uiModule != null) ? uiModule->GetUIInputData() : null;

                if (uiInputData != null)
                {
                    //Service.logger.Info($"bindings ptr: {(inputManager.ToInt64() + 0x9b8):X}");
                    var bindingArr = *((KeybindMemory**)((nint)uiInputData + 0x9b8));

                    VirtualKey ReadKeyBinding(int baseIdx, int offset)
                    {
                        var value = bindingArr[baseIdx + offset].Key;
                        if (mapKeys.TryGetValue(value, out var mappedValue))
                        {
                            return mappedValue;
                        }

                        return (VirtualKey)value;
                    }

                    VirtualKey[] ReadKeyBindings(int baseIdx, int count)
                    {
                        var result = new VirtualKey[count];
                        for (int idx = 0; idx < count; idx++)
                        {
                            result[idx] = ReadKeyBinding(baseIdx, idx);
                        }

                        return result;
                    }

                    GamepadButtons ReadGamepadBinding(int baseIdx, int offset)
                    {
                        if (mapGamepad.TryGetValue(bindingArr[baseIdx + offset].Gamepad, out var buttonEnum))
                        {
                            return buttonEnum;
                        }

                        return GamepadButtons.None;
                    }

                    GamepadButtons[] ReadGamepadBindings(int baseIdx, int count)
                    {
                        var result = new GamepadButtons[count];
                        for (int idx = 0; idx < count; idx++)
                        {
                            result[idx] = ReadGamepadBinding(baseIdx, idx);
                        }

                        return result;
                    }

                    int[] gamepadNotes = new int[16];
                    for (int idx = 0; idx < gamepadNotes.Length; idx++)
                    {
                        gamepadNotes[idx] = bindingArr[baseGamepadNotes + idx].Gamepad;
                        //Service.logger.Info($"Gamepad.note[{idx}]: {gamepadNotes[idx]:X} [{(baseGamepadNotes + idx):X}, {(long)&bindingArr[baseGamepadNotes + idx]:X}]");
                    }

                    int[] gamepadModifiers = new int[4];
                    for (int idx = 0; idx < gamepadModifiers.Length; idx++)
                    {
                        gamepadModifiers[idx] = bindingArr[baseGamepadModifiers + idx].Gamepad;
                        //Service.logger.Info($"Gamepad.mod[{idx}]:  {gamepadModifiers[idx]:X} [{(baseGamepadModifiers + idx):X}, {(long)&bindingArr[baseGamepadModifiers + idx]:X}]");
                    }

                    resultBindings = new PerformanceBindingInfo()
                    {
                        singleOctave = new PerformanceBindingInfo.Mode()
                        {
                            notes = ReadKeyBindings(baseShortNotes, 12 + 1),
                            octaveUp = ReadKeyBinding(baseShortOctave, 0),
                            octaveDown = ReadKeyBinding(baseShortOctave, 1),
                        },
                        threeOctaves = new PerformanceBindingInfo.Mode()
                        {
                            notes = ReadKeyBindings(baseWideNotes, 12 + 12 + 12 + 1),
                            octaveUp = ReadKeyBinding(baseWideOctave, 0),
                            octaveDown = ReadKeyBinding(baseWideOctave, 1),
                        },
                        gamepadNotes = ReadGamepadBindings(baseGamepadNotes, 12 + 1),
                        gamepadOctaveUp = ReadGamepadBinding(baseGamepadModifiers, 0),
                        gamepadOctaveDown = ReadGamepadBinding(baseGamepadModifiers, 1),
                        gamepadHalfUp = ReadGamepadBinding(baseGamepadModifiers, 2),
                        gamepadHalfDown = ReadGamepadBinding(baseGamepadModifiers, 3)
                    };
                }
            }
            catch (Exception ex)
            {
                Service.logger.Error(ex, "Failed to read keybinds data, turning reader off");
                HasErrors = true;
                resultBindings = null;
            }

            return resultBindings;
        }
    }
}
