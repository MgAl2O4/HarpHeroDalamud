using Melanchall.DryWetMidi.Devices;
using Melanchall.DryWetMidi.Interaction;
using System;

namespace HarpHero
{
    public class MidiTrackPlayer : IDisposable
    {
        private OutputDevice midiDevice;
        private Playback midiPlayback;

        public event Action OnFinished;
        public bool IsPlaying => midiPlayback?.IsRunning ?? false;

        public bool autoDispose = true;
        private bool isDisposed = false;

        public MidiTrackPlayer(MidiTrackWrapper track, bool useOrgTrack = false)
        {
            if (track != null && track.tempoMap != null)
            {
                var useTrack = useOrgTrack ? track.midiTrackOrg : track.midiTrack;

                midiDevice = OutputDevice.GetById(0);
                if (midiDevice != null && useTrack != null)
                {
                    midiPlayback = useTrack.GetPlayback(track.tempoMap, midiDevice);

                    midiPlayback.InterruptNotesOnStop = true;
                    midiPlayback.PlaybackStart = track.sectionStart;
                    midiPlayback.PlaybackEnd = track.sectionEnd;
                    midiPlayback.Finished += MidiPlayback_Finished;
                    midiPlayback.Stopped += MidiPlayback_Finished;
                }
            }
        }

        public bool Start()
        {
            if (midiPlayback != null)
            {
                midiPlayback.Start();
                return true;
            }

            return false;
        }

        public bool StartAt(long timeUs)
        {
            bool started = Start();
            if (started)
            {
                midiPlayback.MoveToTime(new MetricTimeSpan(timeUs));
            }

            return started;
        }

        public void Stop()
        {
            if (isDisposed) { return; }
            midiPlayback?.Stop();

            if (midiDevice != null)
            {
                midiDevice.Dispose();
                midiDevice = null;
            }
        }

        public void WarmupDevice()
        {
            midiDevice?.PrepareForEventsSending();
        }

        public void SetTimeScaling(float timeScaling)
        {
            if (midiPlayback != null && timeScaling != midiPlayback.Speed)
            {
                midiPlayback.Speed = timeScaling;

                // restart internal counters
                midiPlayback.MoveToTime(midiPlayback.GetCurrentTime<MetricTimeSpan>());
            }
        }

        public long GetCurrentTimeUs()
        {
            return (midiPlayback != null) ? midiPlayback.GetCurrentTime<MetricTimeSpan>().TotalMicroseconds : 0;
        }

        public long GetCurrentTimeMidi()
        {
            return (midiPlayback != null) ? midiPlayback.GetCurrentTime<MidiTimeSpan>().TimeSpan : 0;
        }

        public void Dispose()
        {
            if (isDisposed) { return; }
            isDisposed = true;

            if (midiDevice != null)
            {
                midiDevice.Dispose();
                midiDevice = null;
            }

            if (midiPlayback != null)
            {
                midiPlayback.Dispose();
                midiPlayback = null;
            }
        }

        private void MidiPlayback_Finished(object sender, EventArgs e)
        {
            if (isDisposed) { return; }
            OnFinished?.Invoke();

            if (autoDispose)
            {
                Dispose();
            }
        }
    }
}
