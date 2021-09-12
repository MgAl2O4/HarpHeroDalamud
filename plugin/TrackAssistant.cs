using Melanchall.DryWetMidi.Interaction;
using System;
using System.Collections.Generic;

namespace HarpHero
{
    public class TrackAssistant : IDisposable, ITickable
    {
        // TODO: expose
        public float NumSecondsFuture = 4.0f;
        public float NumSecondsPast = 0.0f;
        public int NumWarmupBars = 1;

        public readonly UnsafeMetronomeLink metronomeLink;
        public MidiTrackWrapper musicTrack;
        public MidiTrackViewer musicViewer;
        public MidiTrackPlayer musicPlayer;

        public bool HasMetronomeLink => metronomeLink != null && !metronomeLink.HasErrors;
        public bool CanPlay => (musicViewer != null) && (musicTrack != null);
        public int TargetBPM => targetBPM;

        // TODO: expose
        public bool CanShowNoteAssistant => isNoteAssistant;
        public bool CanShowBindAssistant => !isNoteAssistant;
        public bool isNoteAssistant = false;

        public int midOctaveIdx;
        public float timeScaling = 1.0f;
        private int targetBPM;

        private long trackDurationUs;
        public long currentTimeUs;
        public bool isPlaying;
        private bool isPlayingSound;

        public Action<bool> OnPlayChanged;
        public Action<bool> OnTrackChanged;

        public TrackAssistant(UnsafeMetronomeLink metronomeLink)
        {
            this.metronomeLink = metronomeLink;
            if (metronomeLink != null)
            {
                metronomeLink.OnVisibilityChanged += (active) => { if (active) { SetMetronomeParams(); } };
                metronomeLink.OnBPMChanged += (newBPM) => SetTargetBPM(newBPM);
                metronomeLink.OnPlayingChanged += (newIsPlaying) => OnMetronomePlaying(newIsPlaying);
                // TODO: waht about messing with measure? this should be driven by track - reset on play?
            }
        }

        public void SetTrack(MidiTrackWrapper track)
        {
            Stop();

            musicTrack = track;
            musicViewer = null;
            OnTrackUpdated();

            // refresh time scaling
            SetTargetBPM(targetBPM);
        }

        public void SetTrackSection(int startBar, int endBar)
        {
            Stop();

            musicTrack.SetSection(new BarBeatTicksTimeSpan(startBar), new BarBeatTicksTimeSpan(endBar));
            musicViewer = null;
            OnTrackUpdated();
        }

        public void SetTargetBPM(int targetBPM)
        {
            this.targetBPM = targetBPM;
            timeScaling = (musicTrack != null && targetBPM > 0) ? musicTrack.GetScalingForBPM(targetBPM) : 1.0f;

            if (musicPlayer != null)
            {
                musicPlayer.SetTimeScaling(timeScaling);
            }

            SetMetronomeParams();
        }

        public void OnTracksImported(List<MidiTrackWrapper> tracks)
        {
            if (tracks != null && tracks.Count > 0)
            {
                SetTrack(null);
                SetTargetBPM(0);

                // auto select first track with notes
                foreach (var track in tracks)
                {
                    if (track.stats.numNotes > 0)
                    {
                        SetTrack(track);
                        break;
                    }
                }
            }
        }

        public bool Start()
        {
            if (musicTrack != null && CanPlay)
            {
                isPlaying = true;

                musicPlayer = new MidiTrackPlayer(musicTrack);
                musicPlayer.SetTimeScaling(timeScaling);
                isPlayingSound = false;

                currentTimeUs = -TimeConverter.ConvertTo<MetricTimeSpan>(new BarBeatTicksTimeSpan(NumWarmupBars, 0), musicTrack.tempoMap).TotalMicroseconds;
                Tick(0);

                OnPlayChanged?.Invoke(true);
            }

            return isPlaying;
        }

        public void Stop()
        {
            bool wasPlaying = isPlaying;
            isPlaying = false;
            isPlayingSound = false;

            if (musicPlayer != null)
            {
                musicPlayer.Stop();
                musicPlayer = null;
            }

            if (wasPlaying)
            {
                OnPlayChanged?.Invoke(false);
            }

            if (HasMetronomeLink && metronomeLink.IsPlaying)
            {
                metronomeLink.Stop();
            }
        }

        public void Tick(float deltaSeconds)
        {
            if (isPlaying)
            {
                // keep int64 for accuracy, floats will gradually degrade
                // prob overkill since most midis won't last longer than 10 minutes
                // ~7 significant digits, 600s.0000 - accurate up to 100 us?

                // once it starts playing sound, sync time from playback
                // unless metronome sync is available, then use it
                if (HasMetronomeLink && metronomeLink.IsPlaying)
                {
                    currentTimeUs = (long)(metronomeLink.GetCurrentTime() * timeScaling);

                    if (!isPlayingSound && currentTimeUs >= 0)
                    {
                        isPlayingSound = true;
                        musicPlayer.StartAt(currentTimeUs);
                    }
                    else if (isPlayingSound)
                    {
                        // TOOD: how to sync? will set time skip over note events?
                        // set to playback time for eyeballing diffs
                        currentTimeUs = musicPlayer.GetCurrentTimeUs();
                    }
                }
                else if (!isPlayingSound)
                {
                    long deltaUs = (long)(deltaSeconds * timeScaling * 1000 * 1000);
                    currentTimeUs += deltaUs;

                    if (currentTimeUs >= 0)
                    {
                        isPlayingSound = true;
                        musicPlayer.StartAt(currentTimeUs);
                    }
                }
                else
                {
                    currentTimeUs = musicPlayer.GetCurrentTimeUs();
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

        private void OnTrackUpdated()
        {
            musicViewer = null;

            if (musicTrack != null)
            {
                trackDurationUs = musicTrack.GetDurationUs();

                if (musicTrack.IsOctaveRangeValid(out midOctaveIdx))
                {
                    musicViewer = new MidiTrackViewer(musicTrack);
                    musicViewer.timeWindowSecondsAhead = NumSecondsFuture;
                    musicViewer.timeWindowSecondsBehind = NumSecondsPast;

                    SetMetronomeParams();
                }
            }
            else
            {
                trackDurationUs = 0;
            }

            OnTrackChanged?.Invoke(musicViewer != null);
        }

        public void OnAssistModeChanged()
        {
            OnPlayChanged.Invoke(isPlaying);
        }

        public void Dispose()
        {
            if (musicPlayer != null)
            {
                musicPlayer.Stop();
                musicPlayer = null;
            }
        }

        public float GetScaledKeysPerSecond()
        {
            return (musicTrack != null && musicTrack.stats != null) ? musicTrack.stats.GetKeysPerSecond(timeScaling) : 0.0f;
        }

        private void SetMetronomeParams()
        {
            if (HasMetronomeLink && musicTrack != null)
            {
                int newBPMValue = (targetBPM != 0) ? targetBPM : musicTrack.stats.beatsPerMinute;
                metronomeLink.BPM = newBPMValue;
                metronomeLink.Measure = musicTrack.stats.timeSignature.Numerator;
            }
        }

        private void OnMetronomePlaying(bool isMetronomePlaying)
        {
            if (isMetronomePlaying)
            {
                // make sure thaht time signature is matching current track
                metronomeLink.Measure = musicTrack.stats.timeSignature.Numerator;

                Start();
            }
            else
            {
                Stop();
            }
        }
    }
}
