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
        public static bool CollectNoteProcessing = false;

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

        public enum NoteProcessingType
        {
            SimplifyChord,
            ShortenOverlap,
            RemoveOverlap,
            RemoveTooShort,
        }

        public struct NoteProcessingInfo
        {
            public Melanchall.DryWetMidi.Interaction.Note note;
            public NoteProcessingType type;
            public string desc;
            public long timeUs;

            public override string ToString()
            {
                return $"{timeUs * 0.000001f:0.00}s {note} => {type}: {desc}";
            }
        }

        public List<NoteProcessingInfo> noteProcessing = new List<NoteProcessingInfo>();

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
            noteProcessing.Clear();
        }

        private struct NoteChangeInfo
        {
            public int noteNumber;
            public long time;
            public long newDuration;
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
                    var removeChordNotes = chord.Notes.Where(x => x != keepNote);

                    if (CollectNoteProcessing)
                    {
                        var keepNoteTime = TimeConverter.ConvertTo<BarBeatFractionTimeSpan>(keepNote.Time, tempoMap);
                        foreach (var note in removeChordNotes)
                        {
                            noteProcessing.Add(new NoteProcessingInfo()
                            {
                                note = note,
                                timeUs = TimeConverter.ConvertTo<MetricTimeSpan>(note.Time, tempoMap).TotalMicroseconds,
                                type = NoteProcessingType.SimplifyChord,
                                desc = $"{keepNote} at {keepNoteTime}"
                            });
                        }
                    }

                    removeNotes.AddRange(removeChordNotes);
                }
            }

            midiTrack.RemoveNotes(x => removeNotes.Find(rx => (rx.Time == x.Time) && (rx.NoteNumber == x.NoteNumber)) != null);
        }

        private void SimplifyOverlaps()
        {
            var changes = new List<NoteChangeInfo>();
            bool needsRemove = false;
            bool needsDurationChange = false;

            Melanchall.DryWetMidi.Interaction.Note prevNote = null;
            long prevTime = -1;
            long prevDuration = -1;

            foreach (var note in midiTrack.GetNotes())
            {
                if (prevDuration > 0)
                {
                    if (note.Time < (prevTime + prevDuration))
                    {
                        long newDuration = note.Time - prevTime;
                        bool shouldRemove = (newDuration <= 0);

                        changes.Add(new NoteChangeInfo() { time = prevTime, noteNumber = prevNote.NoteNumber, newDuration = newDuration });
                        needsRemove = needsRemove || shouldRemove;
                        needsDurationChange = needsDurationChange || !shouldRemove;

                        if (CollectNoteProcessing)
                        {
                            noteProcessing.Add(new NoteProcessingInfo()
                            {
                                note = prevNote,
                                timeUs = TimeConverter.ConvertTo<MetricTimeSpan>(note.Time, tempoMap).TotalMicroseconds,
                                type = shouldRemove ? NoteProcessingType.RemoveOverlap : NoteProcessingType.ShortenOverlap,
                                desc = $"next:{note}"
                            });
                        }
                    }
                }

                prevNote = note;
                prevTime = note.Time;
                prevDuration = note.Length;
            }

            if (needsRemove)
            {
                midiTrack.RemoveNotes(x =>
                {
                    int matchIdx = changes.FindIndex(n => (n.noteNumber == x.NoteNumber) && (n.time == x.Time));
                    return (matchIdx >= 0) && changes[matchIdx].newDuration <= 0;
                });
            }
            if (needsDurationChange)
            {
                midiTrack.ProcessNotes(x =>
                {
                    int matchIdx = changes.FindIndex(n => (n.noteNumber == x.NoteNumber) && (n.time == x.Time));
                    if (matchIdx >= 0)
                    {
                        x.Length = changes[matchIdx].newDuration;
                    }
                });
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
                bool shouldRemove = noteLengthUs <= minLengthUs;

                if (CollectNoteProcessing)
                {
                    if (shouldRemove)
                    {
                        noteProcessing.Add(new NoteProcessingInfo()
                        {
                            note = x,
                            timeUs = startTimeMetric.TotalMicroseconds,
                            type = NoteProcessingType.RemoveTooShort,
                            desc = $"duration:{endTimeMetric - startTimeMetric}"
                        });
                    }
                }

                return shouldRemove;
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

#if DEBUG
                PluginLog.Log($"Unified tempo: {roundedBPM} BPM");
#endif // DEBUG
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

            if (CollectNoteProcessing)
            {
                noteProcessing.Sort((a, b) => a.timeUs.CompareTo(b.timeUs));
            }
        }

        public int FindValidEndBar(int octaveRange)
        {
            var minNote = SevenBitNumber.MaxValue;
            var maxNote = SevenBitNumber.MinValue;
            long lastValidTick = 0;
            long lastValidTickEnd = 0;
            bool hasRemovedNotes = false;

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

                bool isValid = stats.IsOctaveRangeValid(minNote, maxNote, out int dummyId, octaveRange);
                if (isValid)
                {
                    lastValidTick = note.Time;
                    lastValidTickEnd = note.Time + note.Length;
                }
                else
                {
                    hasRemovedNotes = true;
                    break;
                }
            }

            var useTick = hasRemovedNotes ? lastValidTick : lastValidTickEnd;
            return (int)TimeConverter.ConvertTo<BarBeatTicksTimeSpan>(useTick, tempoMap).Bars;
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
        public long GetStartTimeUs() => TimeConverter.ConvertTo<MetricTimeSpan>(stats.startTick, tempoMap).TotalMicroseconds;
        public long GetEndTimeUs() => TimeConverter.ConvertTo<MetricTimeSpan>(stats.endTick, tempoMap).TotalMicroseconds;

        public bool IsOctaveRangeValid(out int midOctaveId) => stats.IsOctaveRangeValid(out midOctaveId, octaveRange: 3);
        public bool IsOctaveRange5Valid(out int midOctaveId) => stats.IsOctaveRangeValid(out midOctaveId, octaveRange: 5);
    }
}
