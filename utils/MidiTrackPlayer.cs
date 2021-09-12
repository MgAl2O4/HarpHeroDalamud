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

        public MidiTrackPlayer(MidiTrackWrapper track)
        {
            if (track != null && track.midiTrack != null && track.tempoMap != null)
            {
                midiDevice = OutputDevice.GetById(0);
                midiPlayback = track.midiTrack.GetPlayback(track.tempoMap, midiDevice);

                midiPlayback.PlaybackStart = track.sectionStart;
                midiPlayback.PlaybackEnd = track.sectionEnd;
                midiPlayback.Finished += MidiPlayback_Finished;
                midiPlayback.Stopped += MidiPlayback_Finished;

                // warmup device immediately
                midiDevice.PrepareForEventsSending();
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
            midiPlayback?.Stop();
        }

        public void SetTimeScaling(float timeScaling)
        {
            if (midiPlayback != null)
            {
                midiPlayback.Speed = timeScaling;
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
            midiDevice?.Dispose();
            midiPlayback?.Dispose();

            midiDevice = null;
            midiPlayback = null;
        }

        private void MidiPlayback_Finished(object sender, EventArgs e)
        {
            OnFinished?.Invoke();

            if (autoDispose)
            {
                Dispose();
            }
        }
    }
}
