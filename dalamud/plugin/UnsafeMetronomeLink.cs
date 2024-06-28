using FFXIVClientStructs.FFXIV.Client.UI;
using System;
using System.Runtime.InteropServices;

namespace HarpHero
{
    public class UnsafeMetronomeLink
    {
        [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
        private delegate void SetMetronomeValueDelegate(IntPtr agentPtr, int BPM, byte idkSomething);
        private SetMetronomeValueDelegate SetMetronomeBPMFn;
        private SetMetronomeValueDelegate SetMetronomeMeasureFn;

        [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
        private delegate ushort GetMetronomeValueDelegate(nint uiDataPtr);
        private GetMetronomeValueDelegate GetMetronomeBPMFn;
        private GetMetronomeValueDelegate GetMetronomeMeasureFn;

        [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
        private delegate IntPtr StopMetronomeDelegate(IntPtr agentPtr);
        private StopMetronomeDelegate StopMetronomeFn;

        public readonly UIReaderBardMetronome uiReader;

        public Action<int> OnBPMChanged;
        public Action<int> OnMeasureChanged;
        public Action<bool> OnPlayingChanged;
        public Action<bool> OnVisibilityChanged;

        public bool HasErrors { get; private set; }

        private int cachedBPM = 60;
        public int BPM
        {
            get => cachedBPM;
            set { if (value != cachedBPM) { SetBPM(value); } }
        }

        private int cachedMeasure = 4;
        public int Measure
        {
            get => cachedMeasure;
            set { if (value != cachedMeasure) { SetMeasure(value); } }
        }

        private bool cachedIsPlaying = false;
        public bool IsPlaying => cachedIsPlaying;

        public bool IsActive => uiReader.AgentPtr != IntPtr.Zero;

        public UnsafeMetronomeLink()
        {
            uiReader = new UIReaderBardMetronome();
            uiReader.updateNotify = this;

            var ptrSetMeasureFunc = IntPtr.Zero;
            var ptrSetBPMFunc = IntPtr.Zero;
            var ptrGetMeasureFunc = IntPtr.Zero;
            var ptrGetBPMFunc = IntPtr.Zero;
            var ptrMetronomeStopFunc = IntPtr.Zero;
            bool hasException = false;

            if (Service.sigScanner != null)
            {
                // find both on agent's value write break
                // single function for each
                // 
                // measure header = .text: 48 89 5C 24 18 48 89 74 24 20 89 54 24 10 57 48 83 EC 20 48 8B D9 C7 44 24 30 07
                // bpm header: .text: 48 89 5C 24 18 48 89 74 24 20 89 54 24 10 57 48 83 EC 20 48 8B F1 C7 44 24 30 C8
                //
                // (both accessed in some init func, use sigs for their calls instead because why not)
                // boh contain setters in some manager, source of value for reads
                //
                // stop metronome: break on play write to 0 

                try
                {
                    ptrSetMeasureFunc = Service.sigScanner.ScanText("E8 ?? ?? ?? ?? 48 63 06");
                    ptrSetBPMFunc = Service.sigScanner.ScanText("E8 ?? ?? ?? ?? 48 8B 43 ?? 48 8B 48 ?? 48 8B 01 FF 90 ?? ?? ?? ?? 48 8B C8 E8 ?? ?? ?? ?? 0F BE 56");

                    ptrGetBPMFunc = Service.sigScanner.ScanText("E8 ?? ?? ?? ?? F3 0F 10 4E ?? 0F B7 C0");
                    ptrGetMeasureFunc = Service.sigScanner.ScanText("E8 ?? ?? ?? ?? 0F BE 56 ?? 0F B7 C0");

                    ptrMetronomeStopFunc = Service.sigScanner.ScanText("E8 ?? ?? ?? ?? 48 8B CB E8 ?? ?? ?? ?? 48 8B 4F ?? 48 85 C9");
                    //Service.logger.Info($"stopFunc: +0x{((long)ptrMetronomeStopFunc - (long)Service.sigScanner.Module.BaseAddress):X}");
                }
                catch (Exception)
                {
                    hasException = true;
                }
            }

            HasErrors = hasException || (ptrSetMeasureFunc == IntPtr.Zero) || (ptrSetBPMFunc == IntPtr.Zero) || (ptrGetBPMFunc == IntPtr.Zero) || (ptrGetMeasureFunc == IntPtr.Zero) || (ptrMetronomeStopFunc == IntPtr.Zero);
            if (!HasErrors)
            {
                SetMetronomeMeasureFn = Marshal.GetDelegateForFunctionPointer<SetMetronomeValueDelegate>(ptrSetMeasureFunc);
                SetMetronomeBPMFn = Marshal.GetDelegateForFunctionPointer<SetMetronomeValueDelegate>(ptrSetBPMFunc);

                GetMetronomeBPMFn = Marshal.GetDelegateForFunctionPointer<GetMetronomeValueDelegate>(ptrGetBPMFunc);
                GetMetronomeMeasureFn = Marshal.GetDelegateForFunctionPointer<GetMetronomeValueDelegate>(ptrGetMeasureFunc);

                StopMetronomeFn = Marshal.GetDelegateForFunctionPointer<StopMetronomeDelegate>(ptrMetronomeStopFunc);
            }
            else
            {
                Service.logger?.Error("Failed to find metronome functions, turning link off");
            }

            Plugin.OnDebugSnapshot += (_) =>
            {
                Service.logger.Info($"UnsafeMetronomeLink: error:{HasErrors} (F1:{ptrSetMeasureFunc:X}, F2:{ptrSetBPMFunc:X}, F3:{ptrGetBPMFunc:X}, F4:{ptrGetMeasureFunc:X}, F5:{ptrMetronomeStopFunc:X}, ex:{hasException})");
            };
        }

        public unsafe void Update()
        {
            bool wasActive = IsActive;

            bool newIsPlaying = false;
            var statePtr = (UIReaderBardMetronome.AgentData*)uiReader.AgentPtr;
            if (statePtr != null)
            {
                newIsPlaying = statePtr->IsPlaying != 0;

                var uiModule = (Service.gameGui != null) ? (UIModule*)Service.gameGui.GetUIModule() : null;
                var raptureUIData = (uiModule != null) ? uiModule->GetRaptureUiDataModule() : null;

                if (raptureUIData != null)
                {
                    int currentBPM = GetMetronomeBPMFn((nint)raptureUIData);
                    if (cachedBPM != currentBPM)
                    {
                        cachedBPM = currentBPM;
                        OnBPMChanged?.Invoke(currentBPM);
                    }

                    int currentMeasure = GetMetronomeMeasureFn((nint)raptureUIData);
                    if (cachedMeasure != currentMeasure)
                    {
                        cachedMeasure = currentMeasure;
                        OnMeasureChanged?.Invoke(currentMeasure);
                    }
                }
                else
                {
                    HasErrors = true;
                }
            }

            if (cachedIsPlaying != newIsPlaying)
            {
                cachedIsPlaying = newIsPlaying;
                OnPlayingChanged?.Invoke(newIsPlaying);
            }

            if (IsActive != wasActive)
            {
                OnVisibilityChanged?.Invoke(IsActive);
            }
        }

        public unsafe void GetCurrentTime(out int bar, out int beat, out long timeUs)
        {
            var statePtr = (UIReaderBardMetronome.AgentData*)uiReader.AgentPtr;
            if (statePtr != null)
            {
                bar = (statePtr->CurrentBar < 0) ? statePtr->CurrentBar : (statePtr->CurrentBar + 1);
                beat = (statePtr->CurrentBeat == 0) ? cachedMeasure : statePtr->CurrentBeat;

                long numFullBeats = (statePtr->CurrentBar * cachedMeasure) + (beat - 1);
                timeUs = (numFullBeats * 60000000 / cachedBPM) + statePtr->CurrentBeatUs;
            }
            else
            {
                bar = 0;
                beat = 0;
                timeUs = 0;
            }
        }

        public void Stop()
        {
            if (uiReader.AgentPtr != IntPtr.Zero)
            {
                StopMetronomeFn(uiReader.AgentPtr);
            }
        }

        public long GetCurrentTime()
        {
            GetCurrentTime(out int dummyBar, out int dummyBeat, out long timeUs);
            return timeUs;
        }

        private bool SetBPM(int value)
        {
            if (value >= 10 && value <= 200)
            {
                if (!HasErrors && uiReader != null && uiReader.AgentPtr != IntPtr.Zero)
                {
                    SetMetronomeBPMFn(uiReader.AgentPtr, value, 0);
                    cachedBPM = value;
                    return true;
                }
            }

            return false;
        }

        private bool SetMeasure(int value)
        {
            if (value >= 2 && value <= 7)
            {
                if (!HasErrors && uiReader != null && uiReader.AgentPtr != IntPtr.Zero)
                {
                    SetMetronomeMeasureFn(uiReader.AgentPtr, value, 0);
                    cachedMeasure = value;
                    return true;
                }
            }

            return false;
        }
    }
}
