using Melanchall.DryWetMidi.Interaction;
using System;
using System.Collections.Generic;

namespace HarpHero
{
    public class TrackAssistant : IDisposable, ITickable
    {
        public readonly UnsafeMetronomeLink metronomeLink;
        private readonly NoteInputWatcher noteInput;

        public MidiTrackWrapper musicTrack;
        public MidiTrackViewer musicViewer;
        public MidiTrackPlayer musicPlayer;

        public bool HasMetronomeLink => metronomeLink != null && !metronomeLink.HasErrors;
        public bool CanPlay => (musicViewer != null) && (musicTrack != null);
        public bool CanUsePlayback => usePlayback && !useWaitingForInput;
        public bool CanShowNoteAssistant => useNoteAssistant;
        public bool CanShowBindAssistant => !useNoteAssistant;
        public int TargetBPM => targetBPM;
        public bool IsPlaying => isPlaying;
        public bool IsPlayingPreview => !isPlaying && (musicPlayer?.IsPlaying ?? false);
        public bool IsPausedForInput => notePausedForInput != null;
        public float CurrentTime => currentTimeUs * timeScaling / 1000000.0f;

        // TODO: expose
        public float NumSecondsFuture = 4.0f;
        public float NumSecondsPast = 0.0f;
        public int NumWarmupBars = 1;
        public bool useNoteAssistant = false;
        public bool useWaitingForInput = true;
        public bool usePlayback = true;

        public int midOctaveIdx;
        public float timeScaling = 1.0f;
        private int targetBPM;

        private long trackDurationUs;
        private long currentTimeUs;
        private bool isPlaying;
        private bool isPlayingSound;
        private Note notePausedForInput;

        public Action<bool> OnPlayChanged;
        public Action<bool> OnTrackChanged;

        public TrackAssistant(UnsafeMetronomeLink metronomeLink, NoteInputWatcher noteInput)
        {
            this.metronomeLink = metronomeLink;
            this.noteInput = noteInput;

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

            SetTargetBPM(0);
            OnTrackUpdated();
        }

        public void SetTrackSection(int startBar, int endBar)
        {
            Stop();

            if (startBar < 0 || endBar < 0)
            {
                musicTrack.SetSection(null, null);
            }
            else
            {
                musicTrack.SetSection(new BarBeatTicksTimeSpan(startBar), new BarBeatTicksTimeSpan(endBar));
            }

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
                isPlayingSound = false;
                notePausedForInput = null;

                try
                {
                    musicPlayer = new MidiTrackPlayer(musicTrack);
                    musicPlayer.SetTimeScaling(timeScaling);

                    // this can throw exception when device is in use
                    musicPlayer.WarmupDevice();
                }
                catch (Exception)
                {
                    return false;
                }

                isPlaying = true;

                musicViewer.generateBindingData = CanShowBindAssistant;
                musicViewer.generateBarData = false; // TODO: expose?
                musicViewer.OnPlayStart();

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
            notePausedForInput = null;

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

        public void PlayPreview()
        {
            Stop();

            if (musicTrack != null)
            {
                try
                {
                    musicPlayer = new MidiTrackPlayer(musicTrack);
                    musicPlayer.SetTimeScaling(2.0f);
                    musicPlayer.Start();

                    isPlayingSound = true;
                }
                catch (Exception)
                {
                    // it's ok, midi device was busy or sth, ignore
                }
            }
        }

        public void Tick(float deltaSeconds)
        {
            if (isPlaying)
            {
                // time source priorities:
                // - active playback
                // - metronome link
                // - training pause
                // - tick's accumulator

                if (isPlayingSound)
                {
                    // time scaling already applied through musiPlayer's speed
                    currentTimeUs = musicPlayer.GetCurrentTimeUs();
                }
                else if (HasMetronomeLink && metronomeLink.IsPlaying)
                {
                    currentTimeUs = (long)(metronomeLink.GetCurrentTime() * timeScaling);
                }
                else if (IsPausedForInput)
                {
                    bool isPressed = noteInput.IsNoteKeyPressed(notePausedForInput);
                    if (isPressed)
                    {
                        notePausedForInput = null;
                    }
                }
                else
                {
                    // keep int64 for accuracy, floats will gradually degrade
                    // prob overkill since most midis won't last longer than 10 minutes
                    // ~7 significant digits, 600s.0000 - accurate up to 100 us?

                    long deltaUs = (long)(deltaSeconds * timeScaling * 1000 * 1000);
                    currentTimeUs += deltaUs;
                }

                // try starting playback 
                if (CanUsePlayback && !isPlayingSound && currentTimeUs >= 0)
                {
                    isPlayingSound = musicPlayer.StartAt(currentTimeUs);
                }

                // update viewer & look for end of track
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
                    musicViewer.OnNoteNotify += OnMusicViewerNote;

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
            OnPlayChanged.Invoke(IsPlaying);
        }

        public void OnTrainingModeChanged()
        {
            notePausedForInput = null;
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
                metronomeLink.Measure = musicTrack.stats.timeSignature?.Numerator ?? 4;
            }
        }

        private void OnMetronomePlaying(bool isMetronomePlaying)
        {
            if (isMetronomePlaying)
            {
                // make sure thaht time signature is matching current track
                metronomeLink.Measure = musicTrack.stats.timeSignature?.Numerator ?? 4;

                Start();
            }
            else
            {
                Stop();
            }
        }

        private void OnMusicViewerNote(MidiTrackViewer.NoteInfo noteInfo)
        {
            if (useWaitingForInput && !noteInput.IsNoteKeyPressed(noteInfo.note))
            {
                notePausedForInput = noteInfo.note;
                currentTimeUs = noteInfo.startUs;
            }
        }
    }
}
