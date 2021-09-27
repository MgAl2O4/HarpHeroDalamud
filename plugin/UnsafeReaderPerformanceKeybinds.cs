using Dalamud;
using Dalamud.Game;
using Dalamud.Game.ClientState.GamePad;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Game.Gui;
using Dalamud.Logging;
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
        [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
        private delegate IntPtr GetInputManagerDelegate(IntPtr uiObject);

        [StructLayout(LayoutKind.Explicit, Size = 0xB)]
        private unsafe struct KeybindMemory
        {
            [FieldOffset(0x0)] public byte Key;
            [FieldOffset(0x2)] public byte Gamepad;
        }

        private readonly GameGui gameGui;
        public bool HasErrors { get; private set; }

        private int baseShortNotes = 0;
        private int baseShortOctave = 0;
        private int baseWideNotes = 0;
        private int baseWideOctave = 0;
        private int baseGamepadNotes = 0;
        private int baseGamepadModifiers = 0;

        private Dictionary<byte, GamepadButtons> mapGamepad = new();

        public UnsafeReaderPerformanceKeybinds(GameGui gameGui, SigScanner sigScanner)
        {
            this.gameGui = gameGui;

            var ptrShortNotes = IntPtr.Zero;
            var ptrShortOctaves = IntPtr.Zero;
            var ptrWideNotes = IntPtr.Zero;
            var ptrWideOctaves = IntPtr.Zero;
            var ptrGamepadNotes = IntPtr.Zero;
            var ptrGamepadModifiers = IntPtr.Zero;

            if (sigScanner != null)
            {
                // use LEA opcode from setting loops in WritePerformanceBindingsWide/WritePerformanceBindingsSingleOctave
                try
                {
                    ptrShortNotes = sigScanner.GetStaticAddressFromSig("48 8d 35 ?? ?? ?? ?? 4c 8b e1 4c 2b e6 4c 8d 71 10 48 8b e9 41 bf 16");
                    ptrWideNotes = sigScanner.GetStaticAddressFromSig("48 8d 35 ?? ?? ?? ?? 4c 8b e1 4c 2b e6 4c 8d 71 10 48 8b e9 41 bf 2e");
                    ptrShortOctaves = sigScanner.GetStaticAddressFromSig("48 8d 35 ?? ?? ?? ?? 48 2b ee 4c 8d bd 84 00 00 00 bd 02 00");
                    ptrWideOctaves = sigScanner.GetStaticAddressFromSig("48 8d 35 ?? ?? ?? ?? 48 2b ee 4c 8d bd 44 01 00 00 bd 02 00");
                    ptrGamepadNotes = sigScanner.GetStaticAddressFromSig("48 8d 3d ?? ?? ?? ?? 33 db 4c 8d 3d ?? ?? ?? ?? 66 0f");
                    ptrGamepadModifiers = sigScanner.GetStaticAddressFromSig("48 8d 3d ?? ?? ?? ?? 66 66 0f 1f 84 00 00 00 00 00 48 8b 4e 10");
                }
                catch (Exception ex)
                {
                    PluginLog.Error(ex, "oh noes!");
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
                PluginLog.Error("Failed to find key bind indices, turning reader off");
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
            }

            Plugin.OnDebugSnapshot += (_) =>
            {
                Dalamud.Logging.PluginLog.Log($"UnsafeReaderPerformanceKeybinds: error:{HasErrors} (S:{baseShortNotes}:{baseShortOctave}, W:{baseWideNotes}:{baseWideOctave}, G:{baseGamepadNotes}:{baseGamepadModifiers})");
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
                // inputManager = uiModule.vf53()
                // bindingData = inputManager + 0x848
                //
                // binding indicies in 5.58:
                // - 0x243 (0x2e elems = notes)
                // - 0x23f (0x2 elems = octave modifiers)
                // - 0x22b (0x16 elems = notes)
                // - 0x227 (0x2 elems = octave modifiers)
                // - 0x22b (0x16 elems = gamepad notes)
                // - 0x227 (0x4 elems = gamepad modifiers)

                var uiModulePtr = (gameGui != null) ? gameGui.GetUIModule() : IntPtr.Zero;
                if (uiModulePtr != IntPtr.Zero)
                {
                    var getInputManagerPtr = new IntPtr(((UIModule*)uiModulePtr)->vfunc[53]);
                    var getInputManager = Marshal.GetDelegateForFunctionPointer<GetInputManagerDelegate>(getInputManagerPtr);

                    var inputManager = getInputManager(uiModulePtr);
                    if (inputManager != IntPtr.Zero)
                    {
                        //PluginLog.Log($"bindings ptr: {(inputManager.ToInt64() + 0x848):X}");
                        var bindingArr = *((KeybindMemory**)(inputManager.ToInt64() + 0x848));

                        VirtualKey ReadKeyBinding(int baseIdx, int offset)
                        {
                            return (VirtualKey)bindingArr[baseIdx + offset].Key;
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
                            //PluginLog.Log($"Gamepad.note[{idx}]: {gamepadNotes[idx]:X} [{(baseGamepadNotes + idx):X}, {(long)&bindingArr[baseGamepadNotes + idx]:X}]");
                        }

                        int[] gamepadModifiers = new int[4];
                        for (int idx = 0; idx < gamepadModifiers.Length; idx++)
                        {
                            gamepadModifiers[idx] = bindingArr[baseGamepadModifiers + idx].Gamepad;
                            //PluginLog.Log($"Gamepad.mod[{idx}]:  {gamepadModifiers[idx]:X} [{(baseGamepadModifiers + idx):X}, {(long)&bindingArr[baseGamepadModifiers + idx]:X}]");
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
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, "Failed to read keybinds data, turning reader off");
                HasErrors = true;
                resultBindings = null;
            }

            return resultBindings;
        }
    }
}
