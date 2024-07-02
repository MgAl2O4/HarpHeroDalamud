using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using System;
using System.Collections.Generic;

namespace HarpHero
{
    public class MidiTrackViewer
    {
        public struct NoteInfo
        {
            public Note note;
            public long startUs;
            public long endUs;
            public int cacheIdx;
        }

        public struct NoteBindingInfo
        {
            public NoteInfo noteInfo;
            public int pressIdx;
            public int bindingIdx;
            public bool showHint;
            public bool hasBindingConflict;
        }

        private class LineInfo
        {
            public SevenBitNumber noteNumber = SevenBitNumber.MinValue;
            public int startNoteIdx = -1;
            public int lastNoteIdx = -1;
        }

        public float timeWindowSecondsAhead = 9.0f;
        public float timeWindowSecondsBehind = 1.0f;
        public int maxBindingsToShow = 3;
        public int maxNotesToHint = 5;

        public bool generateBarData = true;
        public bool generateBindingData = true;
        public bool showAllBindings = false;

        private NoteInfo[]? cachedNotes;
        private int[]? cachedBindings;
        private TempoMap? tempoMap;
        public TempoMap? TempoMap => tempoMap;

        private long timeUs;
        public long TimeUs => timeUs;
        public long TimeRangeStartUs => timeUs - (long)(timeWindowSecondsBehind * 1000 * 1000);
        public long TimeRangeEndUs => timeUs + (long)(timeWindowSecondsAhead * 1000 * 1000);
        public long TimeRangeNowOffset => (long)(timeWindowSecondsBehind * 1000 * 1000);
        public long TimeRangeUs => (long)((timeWindowSecondsBehind + timeWindowSecondsAhead) * 1000 * 1000);

        public long startTimeUs = -1;
        public long endTimeUs = -1;

        public List<NoteInfo> shownNotes = [];
        public List<long> shownBarLines = [];
        public List<long> shownBeatLines = [];
        public List<NoteBindingInfo> shownBindings = [];

        public Action<NoteInfo>? OnNoteNotify;
        private long lastNotifyStartUs;

        public MidiTrackViewer(MidiTrackWrapper? track)
        {
            if (track != null)
            {
                GenerateCachedData(track.midiTrack, track.tempoMap);
            }
        }

        public MidiTrackViewer(TrackChunk? trackChunk, TempoMap? tempoMap)
        {
            GenerateCachedData(trackChunk, tempoMap);
        }

        public void OnPlayStart()
        {
            lastNotifyStartUs = -1;
            cachedBindings = null;
        }

        public void SetTime(long time)
        {
            long newTimeUs = (tempoMap != null) ? TimeConverter.ConvertTo<MetricTimeSpan>(time, tempoMap).TotalMicroseconds : 0;
            SetTimeUs(newTimeUs);
        }

        public void SetTimeUs(long timeUs)
        {
            this.timeUs = timeUs;

            UpdateShownNotes();
            UpdateShownBarData();
            UpdateShownBindings();
        }

        public void SetShownBindings(int numBindsToShow, int numNotesAhead)
        {
            maxBindingsToShow = numBindsToShow;
            maxNotesToHint = numBindsToShow + numNotesAhead;
            cachedBindings = null;
        }

        private bool GenerateCachedData(TrackChunk? trackChunk, TempoMap? tempoMap)
        {
            if (trackChunk == null || tempoMap == null)
            {
                return false;
            }

            // cache once, this is likely to be queried every tick with moving time window
            // depending on how complex tempoMap is, conversions can get expensive
            this.tempoMap = tempoMap;
            GenerateCachedNotes(trackChunk);

            // don't generate cached bindings immediately, this is likely called from ctor
            // and desired values of setting flags are not set. will be created on first use

            return true;
        }

        private void GenerateCachedNotes(TrackChunk trackChunk)
        {
            var cacheBuilder = new List<NoteInfo>();
            foreach (var note in trackChunk.GetNotes())
            {
                cacheBuilder.Add(new NoteInfo()
                {
                    note = note,
                    startUs = note.TimeAs<MetricTimeSpan>(tempoMap).TotalMicroseconds,
                    endUs = note.EndTimeAs<MetricTimeSpan>(tempoMap).TotalMicroseconds,
                    cacheIdx = cacheBuilder.Count
                });
            }

            cachedNotes = cacheBuilder.ToArray();
        }

        private void GenerateCachedBindings()
        {
            cachedBindings = null;
            if (!generateBindingData || cachedNotes == null || cachedNotes.Length <= 0)
            {
                return;
            }

            var lineInfo = new List<LineInfo>();
            for (int idx = 0; idx < maxBindingsToShow; idx++)
            {
                lineInfo.Add(new LineInfo() { lastNoteIdx = (-maxBindingsToShow + idx) });
            }

            var cacheBuilder = new List<int>();
            for (int noteIdx = 0; noteIdx < cachedNotes.Length; noteIdx++)
            {
                var noteNumber = cachedNotes[noteIdx].note.NoteNumber;
                int bestLineIdx = -1;

                // can it match any of existing lines?
                bestLineIdx = lineInfo.FindIndex(x => x.noteNumber == noteNumber);

                // find next unique note
                for (int nextNoteIdx = noteIdx + 1; nextNoteIdx < cachedNotes.Length; nextNoteIdx++)
                {
                    if (cachedNotes[nextNoteIdx].note.NoteNumber != noteNumber)
                    {
                        // ...and try to match it with existing lines
                        int nextNoteLineIdx = lineInfo.FindIndex(x => x.noteNumber == cachedNotes[nextNoteIdx].note.NoteNumber);
                        if (nextNoteLineIdx >= 0)
                        {
                            // do oldest line, except upcoming one

                            int bestActivity = 0;
                            for (int lineIdx = 0; lineIdx < lineInfo.Count; lineIdx++)
                            {
                                if (lineIdx == nextNoteLineIdx) { continue; }

                                int lineLastActivity = lineInfo[lineIdx].lastNoteIdx;
                                if (bestLineIdx < 0 || bestActivity > lineLastActivity)
                                {
                                    bestLineIdx = lineIdx;
                                    bestActivity = lineLastActivity;
                                }
                            }
                        }

                        break;
                    }
                }

                // fallback assignment: oldest line
                if (bestLineIdx < 0)
                {
                    int bestActivity = 0;
                    for (int lineIdx = 0; lineIdx < lineInfo.Count; lineIdx++)
                    {
                        int lineLastActivity = lineInfo[lineIdx].lastNoteIdx;
                        if (bestLineIdx < 0 || bestActivity > lineLastActivity)
                        {
                            bestLineIdx = lineIdx;
                            bestActivity = lineLastActivity;
                        }
                    }
                }

                // save current line info
                var saveLineInfo = lineInfo[bestLineIdx];
                if (saveLineInfo.startNoteIdx < 0 || (saveLineInfo.noteNumber != noteNumber))
                {
                    saveLineInfo.startNoteIdx = noteIdx;
                    saveLineInfo.noteNumber = noteNumber;
                }
                saveLineInfo.lastNoteIdx = noteIdx;

                cacheBuilder.Add(bestLineIdx);
            }

            cachedBindings = cacheBuilder.ToArray();
        }

        private void UpdateShownNotes()
        {
            shownNotes.Clear();

            if (cachedNotes != null)
            {
                var startTimeUs = TimeRangeStartUs;
                var endTimeUs = TimeRangeEndUs;

                bool foundPlayingNote = false;
                for (int idx = 0; idx < cachedNotes.Length; idx++)
                {
                    if (cachedNotes[idx].startUs < endTimeUs && cachedNotes[idx].endUs >= startTimeUs)
                    {
                        shownNotes.Add(cachedNotes[idx]);

                        if (!foundPlayingNote && cachedNotes[idx].startUs <= timeUs && cachedNotes[idx].endUs > timeUs)
                        {
                            foundPlayingNote = true;
                            if (lastNotifyStartUs != cachedNotes[idx].startUs)
                            {
                                lastNotifyStartUs = cachedNotes[idx].startUs;
                                OnNoteNotify?.Invoke(cachedNotes[idx]);
                            }
                        }
                    }
                }
            }
        }

        private void UpdateShownBarData()
        {
            shownBarLines.Clear();
            shownBeatLines.Clear();

            if (cachedNotes != null && generateBarData && tempoMap != null)
            {
                var startTimeBar = TimeConverter.ConvertTo<BarBeatTicksTimeSpan>(new MetricTimeSpan(Math.Max(0, TimeRangeStartUs)), tempoMap);
                var endTimeBar = TimeConverter.ConvertTo<BarBeatTicksTimeSpan>(new MetricTimeSpan(Math.Max(0, TimeRangeEndUs)), tempoMap);

                if (TimeRangeStartUs < 0)
                {
                    // midi refuses to acknoledge negative numbers, snapshot initial beat time + num per bar and extrapolate
                    var numBeatsPerBar = tempoMap.GetTimeSignatureAtTime(startTimeBar).Numerator;
                    var oneBeatTimeUs = TimeConverter.ConvertTo<MetricTimeSpan>(new BarBeatTicksTimeSpan(0, 1), tempoMap).TotalMicroseconds;

                    int numWarmupBeats = (int)(Math.Min(0, TimeRangeEndUs) % oneBeatTimeUs);
                    long warmupEndTimeUs = -Math.Max(numWarmupBeats, 1) * oneBeatTimeUs;

                    long warmupStartTimeUs = TimeRangeStartUs;
                    for (long itTimeUs = warmupEndTimeUs; itTimeUs > warmupStartTimeUs; itTimeUs -= oneBeatTimeUs)
                    {
                        int totalBeatIdx = (int)(itTimeUs / oneBeatTimeUs);
                        int beatInBar = -totalBeatIdx % numBeatsPerBar;
                        if (beatInBar == 0)
                        {
                            shownBarLines.Add(itTimeUs);
                        }
                        else
                        {
                            shownBeatLines.Add(itTimeUs);
                        }
                    }
                }

                var itTimeInc = new BarBeatTicksTimeSpan(0, 1);
                var numBeatsRemaining = MidiTrackStats.MaxBeatsToProcess;

                for (var itTimeBar = new BarBeatTicksTimeSpan(startTimeBar.Bars, startTimeBar.Beats); itTimeBar < endTimeBar; itTimeBar += itTimeInc)
                {
                    var prevItBar = itTimeBar;
                    var itTimeMetric = TimeConverter.ConvertTo<MetricTimeSpan>(itTimeBar, tempoMap);
                    itTimeBar = TimeConverter.ConvertTo<BarBeatTicksTimeSpan>(itTimeMetric, tempoMap);
                    if (prevItBar.CompareTo(itTimeBar) > 0)
                    {
                        // how???
                        itTimeBar = prevItBar;
                        itTimeBar += itTimeInc;
                    }

                    if (itTimeBar.Beats == 0)
                    {
                        shownBarLines.Add(itTimeMetric.TotalMicroseconds);
                    }
                    else
                    {
                        shownBeatLines.Add(itTimeMetric.TotalMicroseconds);
                    }

                    numBeatsRemaining--;
                    if (numBeatsRemaining <= 0)
                    {
                        break;
                    }
                }
            }
        }

        private void UpdateShownBindings()
        {
            if (generateBindingData && cachedBindings == null)
            {
                GenerateCachedBindings();
            }

            shownBindings.Clear();
            if (cachedBindings != null)
            {
                int maxPressWithConflicts = maxNotesToHint;
                int pressIdx = 0;
                bool hasAnyConflict = false;

                for (int idx = 0; idx < shownNotes.Count; idx++)
                {
                    var noteInfo = shownNotes[idx];
                    if (noteInfo.startUs < timeUs && noteInfo.endUs <= timeUs)
                    {
                        continue;
                    }

                    if ((startTimeUs >= 0 && noteInfo.startUs < startTimeUs) || (endTimeUs >= 0 && noteInfo.endUs >= endTimeUs))
                    {
                        continue;
                    }

                    if (noteInfo.cacheIdx < 0 || noteInfo.cacheIdx >= cachedBindings.Length)
                    {
                        // this should never happen...
                        break;
                    }

                    var newBinding = new NoteBindingInfo() { noteInfo = noteInfo, bindingIdx = cachedBindings[noteInfo.cacheIdx], pressIdx = pressIdx };
                    newBinding.hasBindingConflict = shownBindings.FindIndex(x =>
                        (x.bindingIdx == newBinding.bindingIdx) && (x.noteInfo.note.NoteNumber != newBinding.noteInfo.note.NoteNumber)) >= 0;
                    newBinding.showHint = pressIdx < maxNotesToHint;

                    hasAnyConflict = hasAnyConflict || newBinding.hasBindingConflict;
                    if (!showAllBindings && hasAnyConflict && newBinding.pressIdx > maxPressWithConflicts)
                    {
                        break;
                    }

                    shownBindings.Add(newBinding);
                    pressIdx++;
                }
            }
        }
    }
}
