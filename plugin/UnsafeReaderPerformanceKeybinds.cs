using Dalamud;
using Dalamud.Game;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Game.Gui;
using Dalamud.Logging;
using FFXIVClientStructs.FFXIV.Client.UI;
using System;
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
    }

    public unsafe class UnsafeReaderPerformanceKeybinds
    {
        [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
        private delegate IntPtr GetInputManagerDelegate(IntPtr uiObject);

        [StructLayout(LayoutKind.Explicit, Size = 0xB)]
        private unsafe struct KeybindMemory
        {
            [FieldOffset(0x0)] public byte Key;
        }

        private readonly GameGui gameGui;
        public bool HasErrors { get; private set; }

        private int baseShortNotes = 0;
        private int baseShortOctave = 0;
        private int baseWideNotes = 0;
        private int baseWideOctave = 0;

        public UnsafeReaderPerformanceKeybinds(GameGui gameGui, SigScanner sigScanner)
        {
            this.gameGui = gameGui;

            var ptrShortNotes = IntPtr.Zero;
            var ptrShortOctaves = IntPtr.Zero;
            var ptrWideNotes = IntPtr.Zero;
            var ptrWideOctaves = IntPtr.Zero;

            if (sigScanner != null)
            {
                // use LEA opcode from setting loops in WritePerformanceBindingsWide/WritePerformanceBindingsSingleOctave

                ptrShortNotes = sigScanner.GetStaticAddressFromSig("48 8d 35 ?? ?? ?? ?? 4c 8b e1 4c 2b e6 4c 8d 71 10 48 8b e9 41 bf 16");
                ptrWideNotes = sigScanner.GetStaticAddressFromSig("48 8d 35 ?? ?? ?? ?? 4c 8b e1 4c 2b e6 4c 8d 71 10 48 8b e9 41 bf 2e");
                ptrShortOctaves = sigScanner.GetStaticAddressFromSig("48 8d 35 ?? ?? ?? ?? 48 2b ee 4c 8d bd 84 00 00 00 bd 02 00");
                ptrWideOctaves = sigScanner.GetStaticAddressFromSig("48 8d 35 ?? ?? ?? ?? 48 2b ee 4c 8d bd 44 01 00 00 bd 02 00");
            }

            HasErrors = (ptrShortNotes == IntPtr.Zero) || (ptrShortOctaves == IntPtr.Zero) || (ptrWideNotes == IntPtr.Zero) || (ptrWideOctaves == IntPtr.Zero);
            HasErrors = HasErrors || !SafeMemory.Read(ptrShortNotes, out baseShortNotes);
            HasErrors = HasErrors || !SafeMemory.Read(ptrShortOctaves, out baseShortOctave);
            HasErrors = HasErrors || !SafeMemory.Read(ptrWideNotes, out baseWideNotes);
            HasErrors = HasErrors || !SafeMemory.Read(ptrWideOctaves, out baseWideOctave);

            if (HasErrors)
            {
                PluginLog.Error("Failed to find key bind indices, turning reader off");
            }
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

                        VirtualKey ReadBinding(int baseIdx, int offset)
                        {
                            return (VirtualKey)bindingArr[baseIdx + offset].Key;
                        }

                        VirtualKey[] ReadBindings(int baseIdx, int count)
                        {
                            var result = new VirtualKey[count];
                            for (int idx = 0; idx < count; idx++)
                            {
                                result[idx] = ReadBinding(baseIdx, idx);
                            }

                            return result;
                        }

                        resultBindings = new PerformanceBindingInfo()
                        {
                            singleOctave = new PerformanceBindingInfo.Mode()
                            {
                                notes = ReadBindings(baseShortNotes, 12 + 1),
                                octaveUp = ReadBinding(baseShortOctave, 0),
                                octaveDown = ReadBinding(baseShortOctave, 1),
                            },
                            threeOctaves = new PerformanceBindingInfo.Mode()
                            {
                                notes = ReadBindings(baseWideNotes, 12 + 12 + 12 + 1),
                                octaveUp = ReadBinding(baseWideOctave, 0),
                                octaveDown = ReadBinding(baseWideOctave, 1),
                            }
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
