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
        }

        public class BindingInfo
        {
            public SevenBitNumber noteNumber;
            public int pressIdx;
            public int showIdx;
        }

        public struct NoteBindingInfo
        {
            public NoteInfo noteInfo;
            public int pressIdx;
            public int bindingIdx;
            public bool showHint;
            public bool hasBindingConflict;
        }

        public float timeWindowSecondsAhead = 9.0f;
        public float timeWindowSecondsBehind = 1.0f;
        public int maxBindingsToShow = 3;
        public int maxNotesToHint = 5;

        public bool generateBarData = true;
        public bool generateBindingData = true;

        private NoteInfo[] cachedNotes;
        private TempoMap tempoMap;
        public TempoMap TempoMap => tempoMap;

        private long timeUs;
        public long TimeUs => timeUs;
        public long TimeRangeStartUs => timeUs - (long)(timeWindowSecondsBehind * 1000 * 1000);
        public long TimeRangeEndUs => timeUs + (long)(timeWindowSecondsAhead * 1000 * 1000);
        public long TimeRangeNowOffset => (long)(timeWindowSecondsBehind * 1000 * 1000);
        public long TimeRangeUs => (long)((timeWindowSecondsBehind + timeWindowSecondsAhead) * 1000 * 1000);

        public long startTimeUs = -1;
        public long endTimeUs = -1;

        public List<NoteInfo> shownNotes = new List<NoteInfo>();
        public List<long> shownBarLines = new List<long>();
        public List<long> shownBeatLines = new List<long>();
        public List<BindingInfo> shownBindings = new List<BindingInfo>();

        public Action<NoteInfo> OnNoteNotify;
        private long lastNotifyStartUs;

        public MidiTrackViewer(MidiTrackWrapper track)
        {
            if (track != null)
            {
                GenerateCachedData(track.midiTrack, track.tempoMap);
            }
        }

        public MidiTrackViewer(TrackChunk trackChunk, TempoMap tempoMap)
        {
            GenerateCachedData(trackChunk, tempoMap);
        }

        public void OnPlayStart()
        {
            lastNotifyStartUs = -1;
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

        private bool GenerateCachedData(TrackChunk trackChunk, TempoMap tempoMap)
        {
            if (trackChunk == null || tempoMap == null)
            {
                return false;
            }

            // cache once, this is likely to be queried every tick with moving time window
            // depending on how complex tempoMap is, conversions can get expensive
            this.tempoMap = tempoMap;

            var cacheBuilder = new List<NoteInfo>();
            foreach (var note in trackChunk.GetNotes())
            {
                cacheBuilder.Add(new NoteInfo()
                {
                    note = note,
                    startUs = note.TimeAs<MetricTimeSpan>(tempoMap).TotalMicroseconds,
                    endUs = note.EndTimeAs<MetricTimeSpan>(tempoMap).TotalMicroseconds
                });
            }

            cachedNotes = cacheBuilder.ToArray();
            return true;
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

            if (cachedNotes != null && generateBarData)
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
                for (var itTimeBar = new BarBeatTicksTimeSpan(startTimeBar.Bars, startTimeBar.Beats); itTimeBar < endTimeBar; itTimeBar += itTimeInc)
                {
                    var itTimeMetric = TimeConverter.ConvertTo<MetricTimeSpan>(itTimeBar, tempoMap);
                    itTimeBar = TimeConverter.ConvertTo<BarBeatTicksTimeSpan>(itTimeMetric, tempoMap);

                    if (itTimeBar.Beats == 0)
                    {
                        shownBarLines.Add(itTimeMetric.TotalMicroseconds);
                    }
                    else
                    {
                        shownBeatLines.Add(itTimeMetric.TotalMicroseconds);
                    }
                }
            }
        }

        private void UpdateShownBindings()
        {
            if (generateBindingData)
            {
                // find next set of bindings
                var nearestBindings = new List<BindingInfo>();
                int pressIdx = 0;
                for (int idx = 0; idx < shownNotes.Count; idx++)
                {
                    var noteInfo = shownNotes[idx];

                    if ((startTimeUs >= 0 && noteInfo.startUs < startTimeUs) || (endTimeUs >= 0 && noteInfo.endUs >= endTimeUs))
                    {
                        continue;
                    }

                    if (noteInfo.startUs >= timeUs || noteInfo.endUs > timeUs)
                    {
                        if (nearestBindings.FindIndex(x => x.noteNumber == noteInfo.note.NoteNumber) < 0)
                        {
                            var newBinding = new BindingInfo() { noteNumber = noteInfo.note.NoteNumber, pressIdx = pressIdx };
                            newBinding.showIdx = shownBindings.FindIndex(x => x.noteNumber == noteInfo.note.NoteNumber);

                            nearestBindings.Add(newBinding);
                            if (nearestBindings.Count == maxBindingsToShow)
                            {
                                break;
                            }
                        }

                        pressIdx++;
                    }
                }

                int newNumToShow = Math.Max(nearestBindings.Count, shownBindings.Count);
                if (newNumToShow > 0)
                {
                    var freeShowSlots = new List<int>();
                    for (int idx = 0; idx < newNumToShow; idx++)
                    {
                        bool isUsed = nearestBindings.FindIndex(x => x.showIdx == idx) >= 0;
                        if (!isUsed)
                        {
                            freeShowSlots.Add(idx);

                            if (idx < shownBindings.Count)
                            {
                                shownBindings[idx].pressIdx = -1;
                            }
                        }

                        if (shownBindings.Count <= idx)
                        {
                            shownBindings.Add(new BindingInfo());
                        }
                    }

                    for (int idx = 0; idx < nearestBindings.Count; idx++)
                    {
                        var bindingInfo = nearestBindings[idx];

                        if (bindingInfo.showIdx < 0 && freeShowSlots.Count > 0)
                        {
                            bindingInfo.showIdx = freeShowSlots[0];
                            freeShowSlots.RemoveAt(0);
                        }

                        shownBindings[bindingInfo.showIdx] = bindingInfo;
                    }
                }
            }
            else
            {
                shownBindings.Clear();
            }
        }

        public IEnumerable<NoteBindingInfo> GetShownNotesBindings()
        {
            int pressIdx = 0;
            int maxPressIdxWithConflict = maxNotesToHint;
            var listHistory = new List<int>();
            var mapBindingConflicts = new Dictionary<int, int>();

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

                int bindingIdx = shownBindings.FindIndex(x => x.noteNumber == noteInfo.note.NoteNumber);
                if ((bindingIdx < 0) && (pressIdx >= maxPressIdxWithConflict))
                {
                    break;
                }

                bool hasConflict = (bindingIdx < 0);
                if (hasConflict)
                {
                    if (!mapBindingConflicts.TryGetValue(noteInfo.note.NoteNumber, out bindingIdx))
                    {
                        bindingIdx = (listHistory.Count > 0) ? listHistory[0] : 0;
                        mapBindingConflicts.Add(noteInfo.note.NoteNumber, bindingIdx);
                    }
                }

                listHistory.Remove(bindingIdx);
                listHistory.Add(bindingIdx);

                yield return new NoteBindingInfo()
                {
                    noteInfo = noteInfo,
                    pressIdx = pressIdx,
                    bindingIdx = bindingIdx,
                    showHint = (pressIdx >= 0) && (pressIdx < maxNotesToHint),
                    hasBindingConflict = hasConflict
                };

                pressIdx++;
            }
        }
    }
}
