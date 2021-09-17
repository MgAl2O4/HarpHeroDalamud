using Dalamud.Logging;
using Melanchall.DryWetMidi.Interaction;
using System;
using System.Collections.Generic;

namespace HarpHero
{
    public class TrackAssistant : IDisposable, ITickable
    {
        public readonly UnsafeMetronomeLink metronomeLink;
        public readonly TrackScore scoreTracker;
        private readonly UIReaderBardPerformance uiReader;
        private readonly Configuration config;

        public MidiTrackWrapper musicTrack;
        public MidiTrackViewer musicViewer;
        public MidiTrackPlayer musicPlayer;

        public bool HasMetronomeLink => metronomeLink != null && !metronomeLink.HasErrors && config.UseMetronomeLink;
        public bool CanPlay => (musicViewer != null) && (musicTrack != null);
        public bool CanUsePlayback => config.UsePlayback && !useWaitingForInput;
        public bool CanShowNoteAssistant => config.UseAssistNoteA();
        public bool CanShowNoteAssistant2 => config.UseAssistNoteB();
        public bool CanShowBindAssistant => config.UseAssistBind();
        public int TargetBPM => targetBPM;
        public bool IsPlaying => isPlaying;
        public bool IsPlayingPreview => !isPlaying && (musicPlayer?.IsPlaying ?? false);
        public bool IsPausedForInput => notePausedForInput != null;
        public float CurrentTime => currentTimeUs / 1000000.0f;
        public long CurrentTimeUs => currentTimeUs;

        // TODO: expose
        public float NumSecondsFuture = 4.0f;
        public float NumSecondsPast = 0.0f;
        public int NumWarmupBars = 1;
        private long maxEarlyPressTimeUs = 50 * 1000;

        public int midOctaveIdx;
        public float timeScaling = 1.0f;
        public bool useWaitingForInput = true;
        private int targetBPM;

        private long trackDurationUs;
        private long currentTimeUs;
        private bool isPlaying;
        private bool isPlayingSound;
        private Note notePausedForInput;
        private long pausedTimeUs;
        private long lastPressTimeUs;
        private int lastPressNoteNumber;

        public Action<bool> OnPlayChanged;
        public Action<bool> OnTrackChanged;
        public Action<float> OnPerformanceScore;

        public TrackAssistant(UIReaderBardPerformance uiReader, UnsafeMetronomeLink metronomeLink, Configuration config)
        {
            this.uiReader = uiReader;
            this.metronomeLink = metronomeLink;
            this.config = config;
            this.scoreTracker = new TrackScore();

            useWaitingForInput = config?.UseTrainingMode ?? true;

            if (uiReader != null)
            {
                uiReader.OnPlayingNoteChanged += OnPlayingNoteChanged;
            }

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
            if (track != null)
            {
                // modify section length first, it will affect notes per beat
                int initStartBar = -1;
                int initEndBar = -1;

                if (config.AutoAdjustEndBar)
                {
                    int endBarIdx = track.FindValidEndBar();
                    if (endBarIdx > 0)
                    {
                        initStartBar = 0;
                        initEndBar = endBarIdx;
                    }
                }
                SetTrackSection(initStartBar, initEndBar, false);

                if (config.AutoAdjustBPM)
                {
                    float targetBeatsPerSecond = config.AutoAdjustSpeedThreshold / track.stats.notesPerBeat;
                    int newTargetBPM = (int)(targetBeatsPerSecond * 60);
                    if (newTargetBPM < track.stats.beatsPerMinute)
                    {
                        SetTargetBPM(newTargetBPM);
                    }
                }
            }

            OnTrackUpdated();
        }

        public void SetTrackSection(int startBar, int endBar, bool sendNotify = true)
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
                pausedTimeUs = 0;

                try
                {
                    musicPlayer = new MidiTrackPlayer(musicTrack);
                    musicPlayer.SetTimeScaling(timeScaling);

                    // this can throw exception when device is in use
                    musicPlayer.WarmupDevice();
                }
                catch (Exception ex)
                {
                    PluginLog.Error(ex, "Failed to start midi player");
                    return false;
                }

                isPlaying = true;

                musicViewer.generateBindingData = CanShowBindAssistant;
                musicViewer.generateBarData = false; // TODO: expose?
                musicViewer.OnPlayStart();

                currentTimeUs = -TimeConverter.ConvertTo<MetricTimeSpan>(new BarBeatTicksTimeSpan(NumWarmupBars, 0), musicTrack.tempoMap).TotalMicroseconds;
                Tick(0);

                scoreTracker.SetTrainingMode(useWaitingForInput);
                scoreTracker.OnPlayStart();
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
                scoreTracker.OnPlayStop();
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
                    // no updates here, just wait
                    long deltaUs = (long)(deltaSeconds * timeScaling * 1000 * 1000);
                    pausedTimeUs = Math.Min(pausedTimeUs + deltaUs, 1000 * 1000 * 60);
                }
                else
                {
                    // keep int64 for accuracy, floats will gradually degrade
                    // prob overkill since most midis won't last longer than 10 minutes
                    // ~7 significant digits, 600s.0000 - accurate up to 100 us?

                    long deltaUs = (long)(deltaSeconds * timeScaling * 1000 * 1000);
                    currentTimeUs += deltaUs;
                }

                // enforce metronome sync
                if (isPlayingSound && HasMetronomeLink && metronomeLink.IsPlaying)
                {
                    long syncTimeUs = (long)(metronomeLink.GetCurrentTime() * timeScaling);
                    long syncTimeDiff = currentTimeUs - syncTimeUs;
                    long syncTimeDiffAbs = Math.Abs(syncTimeDiff);

                    if (syncTimeDiffAbs > 1000)
                    {
                        float speedOffset = -syncTimeDiff / 100000.0f;

                        if (speedOffset < -0.25f) { speedOffset = -0.25f; }
                        else if (speedOffset > 0.25f) { speedOffset = 0.25f; }

                        if (timeScaling + speedOffset > 0.0f)
                        {
                            musicPlayer.SetTimeScaling(timeScaling + speedOffset);
                        }
                    }
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
                    CalcScore();
                    Stop();
                }
            }
        }

        public bool GetNextPlayingNote(out int noteNumber, out long startTimeUs, int offset = 0)
        {
            if (musicViewer != null)
            {
                for (int idx = 0; idx < musicViewer.shownNotes.Count; idx++)
                {
                    if (musicViewer.shownNotes[idx].startUs < currentTimeUs)
                    {
                        continue;
                    }

                    if (offset <= 0)
                    {
                        noteNumber = musicViewer.shownNotes[idx].note.NoteNumber;
                        startTimeUs = musicViewer.shownNotes[idx].startUs;
                        return true;
                    }

                    offset--;
                }
            }

            noteNumber = 0;
            startTimeUs = 0;
            return false;
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
            config.UseTrainingMode = useWaitingForInput;
            config.Save();

            Stop();
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

        private void OnPlayingNoteChanged(int noteNumber)
        {
            if (noteNumber != 0)
            {
                lastPressTimeUs = currentTimeUs;
                lastPressNoteNumber = noteNumber;
            }

            long useTimeUs = currentTimeUs;
            if (notePausedForInput != null)
            {
                useTimeUs += pausedTimeUs;

                if (noteNumber == notePausedForInput.NoteNumber)
                {
                    notePausedForInput = null;
                    lastPressNoteNumber = 0;
                }
            }

            GetNextPlayingNote(out int nextPlayingNoteNumber, out long nextPlayingTimeUs);
            scoreTracker.OnNotePressed(noteNumber, useTimeUs, nextPlayingNoteNumber, nextPlayingTimeUs);
        }

        private void OnMusicViewerNote(MidiTrackViewer.NoteInfo noteInfo)
        {
            if (useWaitingForInput)
            {
                bool isPressedNow = uiReader.cachedState.ActiveNoteNumber == noteInfo.note.NoteNumber;
                bool wasPressedRecently = (lastPressNoteNumber > 0) && (lastPressNoteNumber == noteInfo.note.NoteNumber) && ((currentTimeUs - lastPressTimeUs) < maxEarlyPressTimeUs);

                if (!isPressedNow && !wasPressedRecently)
                {
                    notePausedForInput = noteInfo.note;
                    currentTimeUs = noteInfo.startUs;
                    lastPressNoteNumber = 0;
                    pausedTimeUs = 0;
                }
            }

            scoreTracker.OnNotePlaying(noteInfo.note.NoteNumber, noteInfo.startUs);
        }

        private void CalcScore()
        {
            if (scoreTracker.IsActive)
            {
                float errorPct = 1.0f * scoreTracker.AccumulatedTimeDiff / trackDurationUs;
                float accuracyPct = Math.Min(1.0f, Math.Max(0.0f, 1.0f - errorPct));

                OnPerformanceScore?.Invoke(accuracyPct);
            }
        }

    }
}
