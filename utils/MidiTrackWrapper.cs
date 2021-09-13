using Dalamud.Logging;
using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using Melanchall.DryWetMidi.MusicTheory;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HarpHero
{
    public class MidiTrackWrapper
    {
        public const float MinNoteDurationSeconds = 0.100f;
        public const int MaxBarsToCalculateTempo = 10;

        public string name;

        public readonly TrackChunk midiTrackOrg;
        public readonly TempoMap tempoMapOrg;

        public TrackChunk midiTrack;
        public TempoMap tempoMap;

        public ITimeSpan sectionStart;
        public ITimeSpan sectionEnd;

        public MidiTrackStats stats = new MidiTrackStats();
        public MidiTrackStats statsOrg = new MidiTrackStats();

        public MidiTrackWrapper(TrackChunk midiTrack, TempoMap tempoMap)
        {
            tempoMapOrg = tempoMap;
            midiTrackOrg = midiTrack;
            statsOrg.Update(midiTrackOrg, tempoMapOrg);

            TransformTrack();
            stats.Update(this.midiTrack, this.tempoMap);

            var nameEvent = midiTrack.Events.Where(trackEvent => trackEvent.EventType == MidiEventType.SequenceTrackName).FirstOrDefault() as SequenceTrackNameEvent;
            if (nameEvent != null)
            {
                name = nameEvent.Text;
            }
        }

        public static List<MidiTrackWrapper> GenerateTracks(MidiFile midiFile)
        {
            var list = new List<MidiTrackWrapper>();

            var tempoMap = midiFile.GetTempoMap();
            foreach (var track in midiFile.Chunks.OfType<TrackChunk>())
            {
                var trackOb = new MidiTrackWrapper(track, tempoMap);
                if (string.IsNullOrEmpty(trackOb.name))
                {
                    trackOb.name = $"#{list.Count + 1}";
                }

                list.Add(trackOb);
            }

            return list;
        }

        /*
         * Suddenly, a mermaid.
         *                             .-""-.
         *                           (___/\ \
         *         ,                 (|^ ^ ) )
         *        /(                _)_\=_/  (
         *  ,..__/ `\          ____(_/_ ` \   )
         *   `\    _/        _/---._/(_)_  `\ (
         * jgs '--\ `-.__..-'    /.    (_), |  )
         *         `._        ___\_____.'_| |__/
         *            `~----"`   `-.........'
         */

        public void SetSection(ITimeSpan sectionStart, ITimeSpan sectionEnd)
        {
            this.sectionStart = sectionStart;
            this.sectionEnd = sectionEnd;

            stats.Update(midiTrack, tempoMap, sectionStart, sectionEnd);
        }

        public void ResetTrackChanges()
        {
            midiTrack = midiTrackOrg.Clone() as TrackChunk;
            tempoMap = tempoMapOrg;
        }

        private void SimplifyChords()
        {
            var removeNotes = new List<Melanchall.DryWetMidi.Interaction.Note>();
            foreach (var chord in midiTrack.GetChords())
            {
                Melanchall.DryWetMidi.Interaction.Note keepNote = null;
                int numNotes = 0;

                foreach (var note in chord.Notes)
                {
                    numNotes++;

                    if (keepNote == null || keepNote.NoteNumber < note.NoteNumber)
                    {
                        keepNote = note;
                    }
                }

                if (numNotes > 1)
                {
                    removeNotes.AddRange(chord.Notes.Where(x => x != keepNote));
                }
            }

            int numNotesBefore = midiTrack.GetNotes().Count;
            midiTrack.RemoveNotes(x => removeNotes.Find(rx => (rx.Time == x.Time) && (rx.NoteNumber == x.NoteNumber)) != null);

#if DEBUG
            PluginLog.Log($"Simplifying chords, notes: {numNotesBefore} => {midiTrack.GetNotes().Count}");
#endif // DEBUG
        }

        private void SimplifyOverlaps()
        {
            var notes = midiTrack.GetNotes();
            var mapTimeDurations = new Dictionary<long, long>();
            var removeTimes = new List<long>();

            long prevTime = -1;
            long prevDuration = -1;

            foreach (var note in notes)
            {
                if (prevDuration > 0)
                {
                    if (note.Time < (prevTime + prevDuration))
                    {
                        long newDuration = note.Time - prevTime;
                        bool shouldRemove = (newDuration <= 0);

                        if (shouldRemove)
                        {
                            removeTimes.Add(prevTime);
                        }
                        else
                        {
                            mapTimeDurations.Add(prevTime, newDuration);
                        }
                    }
                }

                prevTime = note.Time;
                prevDuration = note.Length;
            }

#if DEBUG
            PluginLog.Log($"Simplifying overlaps, remove:{removeTimes.Count}, adjust:{mapTimeDurations.Count}");
#endif // DEBUG

            if (removeTimes.Count > 0)
            {
                midiTrack.RemoveNotes(x => removeTimes.Contains(x.Time));
            }
            if (mapTimeDurations.Count > 0)
            {
                midiTrack.ProcessNotes(x => x.Length = mapTimeDurations[x.Time], x => mapTimeDurations.ContainsKey(x.Time));
            }
        }

        private void FilterTooShort()
        {
            long minLengthUs = (long)(MinNoteDurationSeconds * 1000 * 1000);

            midiTrack.RemoveNotes(x =>
            {
                var startTimeMetric = x.TimeAs<MetricTimeSpan>(tempoMap);
                var endTimeMetric = x.EndTimeAs<MetricTimeSpan>(tempoMap);

                var noteLengthUs = (endTimeMetric - startTimeMetric).TotalMicroseconds;
                return noteLengthUs <= minLengthUs;
            });
        }

        private void UnifyTempo()
        {
            int numTempoChanges = tempoMap.GetTempoChanges().Count();
            if (numTempoChanges > 1)
            {
                // look at first few bars sharing same time signature
                // calc average quarter note length and apply it on entire track

                int testEndBar = 1;
                var startTimeSig = tempoMap.GetTimeSignatureAtTime(new BarBeatTicksTimeSpan(0));

                int numTimeSignatureChanges = tempoMap.GetTimeSignatureChanges().Count();
                if (numTimeSignatureChanges > 0)
                {
                    while (testEndBar < MaxBarsToCalculateTempo)
                    {
                        var testTimeSig = tempoMap.GetTimeSignatureAtTime(new BarBeatTicksTimeSpan(testEndBar));
                        if (testTimeSig != startTimeSig)
                        {
                            testEndBar--;
                            break;
                        }

                        testEndBar++;
                    }
                }
                else
                {
                    testEndBar = MaxBarsToCalculateTempo;
                }

                testEndBar = (testEndBar <= 0) ? 1 : testEndBar;
                var endTimeUs = TimeConverter.ConvertTo<MetricTimeSpan>(new BarBeatTicksTimeSpan(testEndBar), tempoMap);
                int numQuarterNotes = testEndBar * 4 * startTimeSig.Numerator / startTimeSig.Denominator;

                long newQuarterNoteTimeUs = endTimeUs.TotalMicroseconds / numQuarterNotes;
                int roundedBPM = (int)Math.Round(60000000.0 / newQuarterNoteTimeUs);
                long roundedQuarterNoteTimeUs = 60000000 / roundedBPM;

                var lastNote = midiTrack.GetNotes().Last();
                var trackDuration = lastNote.EndTimeAs<MidiTimeSpan>(tempoMap).TimeSpan;

                var tracksToUpdate = new TrackChunk[] { midiTrack };
                using (var tempoManager = TempoMapManagingUtilities.ManageTempoMap(tracksToUpdate, tempoMap.TimeDivision))
                {
                    tempoManager.ClearTempo(0, trackDuration);
                    tempoManager.SetTempo(0, new Tempo(roundedQuarterNoteTimeUs));
                    tempoMap = tempoManager.TempoMap;
                }

                PluginLog.Log($"Unified tempo: {roundedBPM} BPM");
            }
        }

        private void TransformTrack()
        {
            ResetTrackChanges();

            int numNotes = midiTrack.GetNotes().Count;
            if (numNotes > 0)
            {
                SimplifyChords();
                SimplifyOverlaps();
                FilterTooShort();

                UnifyTempo();
            }
        }

        public int FindValidEndBar()
        {
            var minNote = SevenBitNumber.MaxValue;
            var maxNote = SevenBitNumber.MinValue;
            long lastValidTick = 0;

            foreach (var note in midiTrack.GetNotes())
            {
                if (minNote > note.NoteNumber)
                {
                    minNote = note.NoteNumber;
                }
                if (maxNote < note.NoteNumber)
                {
                    maxNote = note.NoteNumber;
                }

                bool isValid = stats.IsOctaveRangeValid(minNote, maxNote, out int dummyId);
                if (isValid)
                {
                    lastValidTick = note.Time;
                }
                else
                {
                    break;
                }
            }

            return (int)TimeConverter.ConvertTo<BarBeatTicksTimeSpan>(lastValidTick, tempoMap).Bars;
        }

        public float GetScalingForBPM(int targetBPM)
        {
            return 1.0f * targetBPM / stats.beatsPerMinute;
        }

        public TTimeSpan GetDurationIgnoringSection<TTimeSpan>() where TTimeSpan : ITimeSpan
        {
            return TimeConverter.ConvertTo<TTimeSpan>(statsOrg.DurationTicks, tempoMap);
        }

        public TTimeSpan GetDuration<TTimeSpan>() where TTimeSpan : ITimeSpan
        {
            return TimeConverter.ConvertTo<TTimeSpan>(stats.endTick - stats.startTick, tempoMap);
        }

        public long GetDurationMidi() => GetDuration<MidiTimeSpan>().TimeSpan;
        public long GetDurationUs() => GetDuration<MetricTimeSpan>().TotalMicroseconds;

        public bool IsOctaveRangeValid(out int midOctaveId) => stats.IsOctaveRangeValid(out midOctaveId);
    }
}
