using Dalamud.Logging;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using System;
using System.Linq;

namespace HarpHero
{
    public class TrackAssistant : IDisposable
    {
        public float NumSecondsFuture = 4.0f;
        public float NumSecondsPast = 0.0f;
        public int NumWarmupBars = 2;

        public MidiTrackWrapper musicTrack;
        public MidiTrackViewer musicViewer;
        public MidiTrackPlayer musicPlayer;

        public int midOctaveIdx;
        public float timeScaling = 1.0f;

        private long trackDurationUs;
        public long currentTimeUs;
        public bool isPlaying;
        private bool isPlayingSound;

        public bool Start()
        {
#if DEBUG
            if (musicTrack == null)
            {
                DebugLoadTrack();
            }
#endif // DEBUG

            if (musicTrack != null)
            {
                isPlaying = true;

                musicPlayer = new MidiTrackPlayer(musicTrack) { timeScale = timeScaling };
                isPlayingSound = false;

                currentTimeUs = -TimeConverter.ConvertTo<MetricTimeSpan>(new BarBeatTicksTimeSpan(NumWarmupBars, 0), musicTrack.tempoMap).TotalMicroseconds;
                Tick(0);
            }

            return isPlaying;
        }

        public void Stop()
        {
            isPlaying = false;
            isPlayingSound = false;

            if (musicPlayer != null)
            {
                musicPlayer.Stop();
                musicPlayer = null;
            }
        }

        public void Tick(float deltaSeconds)
        {
            if (isPlaying)
            {
                // keep int64 for accuracy, floats will gradually degrade
                // prob overkill since most midis won't last longer than 10 minutes
                // ~7 significant digits, 600s.0000 - accurate up to 100 us?

                long deltaUs = (long)(deltaSeconds * timeScaling * 1000 * 1000);
                currentTimeUs += deltaUs;

                if (!isPlayingSound && currentTimeUs >= 0)
                {
                    isPlayingSound = true;
                    musicPlayer.StartAt(currentTimeUs);
                }

                if (currentTimeUs < trackDurationUs)
                {
                    musicViewer.SetTimeUs(currentTimeUs);
                }
                else
                {
                    Stop();
                }
            }
        }

        public void DebugLoadTrack()
        {
            // temporary until i'll make some proper ui
            musicViewer = null;
            musicTrack = null;

            try
            {
                var midiFile = MidiFile.Read(@"D:\temp\test3.mid");

                var tracks = MidiTrackWrapper.GenerateTracks(midiFile);
                if (tracks != null && tracks.Count > 0)
                {
                    musicTrack = tracks.Last();
                    musicTrack.SetSection(new BarBeatTicksTimeSpan(0, 0, 0), new BarBeatTicksTimeSpan(10, 0, 0));

                    if (musicTrack.IsOctaveRangeValid(out midOctaveIdx))
                    {
                        trackDurationUs = musicTrack.GetDurationUs();

                        musicViewer = new MidiTrackViewer(musicTrack);
                        musicViewer.timeWindowSecondsAhead = NumSecondsFuture;
                        musicViewer.timeWindowSecondsBehind = NumSecondsPast;

                        timeScaling = musicTrack.GetScalingForBPM(60);
                    }
                    else
                    {
                        musicTrack = null;
                    }
                }
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, "failed to load midi");
            }
        }

        public void Dispose()
        {
            if (musicPlayer != null)
            {
                musicPlayer.Stop();
                musicPlayer = null;
            }
        }

        public float GetScaledKeyPerSecond()
        {
            return (musicTrack != null && musicTrack.stats != null) ? musicTrack.stats.GetKeysPerSecond(timeScaling) : 0.0f;
        }
    }
}
