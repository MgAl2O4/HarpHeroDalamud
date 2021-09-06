using Dalamud.Logging;
using Melanchall.DryWetMidi.Core;
using System;
using System.Linq;

namespace HarpHero
{
    public class TrackAssistant : IDisposable
    {
        public float NumSecondsFuture = 4.0f;
        public float NumSecondsPast = 0.0f;

        public MidiTrackWrapper musicTrack;
        public MidiTrackViewer musicViewer;
        public MidiTrackPlayer musicPlayer;

        public int midOctaveIdx;
        public float timeScaling = 0.25f;

        private long trackDurationUs;
        public long currentTimeUs;
        public bool isPlaying;

        public bool Start()
        {
            if (musicTrack != null)
            {
                isPlaying = true;
                currentTimeUs = 0;

                musicPlayer = new MidiTrackPlayer(musicTrack);
                musicPlayer.timeScale = timeScaling;
                musicPlayer.Start();
            }

            return isPlaying;
        }

        public void Stop()
        {
            isPlaying = false;

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
                var midiFile = MidiFile.Read(@"D:\temp\test.mid");

                var tracks = MidiTrackWrapper.GenerateTracks(midiFile);
                if (tracks != null && tracks.Count > 0)
                {
                    musicTrack = tracks.Last();
                    if (musicTrack.IsOctaveRangeValid(out midOctaveIdx))
                    {
                        trackDurationUs = musicTrack.GetDurationUs();

                        musicViewer = new MidiTrackViewer(musicTrack);
                        musicViewer.timeWindowSecondsAhead = NumSecondsFuture;
                        musicViewer.timeWindowSecondsBehind = NumSecondsPast;
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
                musicPlayer.autoDispose = true;
                musicPlayer.Stop();
            }
        }
    }
}
