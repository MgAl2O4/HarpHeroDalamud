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

        public float timeWindowSecondsAhead = 9.0f;
        public float timeWindowSecondsBehind = 1.0f;
        public bool generateBarData = true;

        private NoteInfo[] cachedNotes;
        private TempoMap tempoMap;
        public TempoMap TempoMap => tempoMap;

        private long timeUs;
        public long TimeUs => timeUs;
        public long TimeRangeStartUs => timeUs - (long)(timeWindowSecondsBehind * 1000 * 1000);
        public long TimeRangeEndUs => timeUs + (long)(timeWindowSecondsAhead * 1000 * 1000);
        public long TimeRangeExactUs => (long)(timeWindowSecondsBehind * 1000 * 1000);
        public long TimeRangeUs => (long)((timeWindowSecondsBehind + timeWindowSecondsAhead) * 1000 * 1000);

        public List<NoteInfo> shownNotes = new List<NoteInfo>();
        public List<long> shownBarLines = new List<long>();
        public List<long> shownBeatLines = new List<long>();

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

                for (int idx = 0; idx < cachedNotes.Length; idx++)
                {
                    if (cachedNotes[idx].startUs < endTimeUs && cachedNotes[idx].endUs >= startTimeUs)
                    {
                        shownNotes.Add(cachedNotes[idx]);
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
    }
}
