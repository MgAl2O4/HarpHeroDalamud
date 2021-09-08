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

        public float timeScale = 1.0f;
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
            }
        }

        public bool Start()
        {
            if (midiPlayback != null)
            {
                midiPlayback.Speed = timeScale;
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

        public long GetMidiTime()
        {
            return midiPlayback.GetCurrentTime<MidiTimeSpan>().TimeSpan;
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
