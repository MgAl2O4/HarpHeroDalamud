using Dalamud.Logging;
using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using Melanchall.DryWetMidi.MusicTheory;
using System.Collections.Generic;
using System.Linq;

namespace HarpHero
{
    public class MidiTrackWrapper
    {
        public const float MinNoteDurationSeconds = 0.100f;

        public readonly TempoMap tempoMap;
        public string name;

        public readonly TrackChunk midiTrackOrg;
        public TrackChunk midiTrack;

        public ITimeSpan sectionStart;
        public ITimeSpan sectionEnd;

        private long duration;
        private long trackStartTick;
        private long trackEndTick;

        public SevenBitNumber noteNumberMin;
        public SevenBitNumber noteNumberMax;
        public SevenBitNumber noteNumberMinOrg;
        public SevenBitNumber noteNumberMaxOrg;

        public MidiTrackWrapper(TrackChunk midiTrack, TempoMap midiTempo)
        {
            tempoMap = midiTempo;
            midiTrackOrg = midiTrack;

            TransformTrack();
            CacheTrackInfo();

            var nameEvent = midiTrack.Events.Where(trackEvent => trackEvent.EventType == MidiEventType.SequenceTrackName).FirstOrDefault() as SequenceTrackNameEvent;
            if (nameEvent != null)
            {
                name = nameEvent.Text;
            }
        }

        public static List<MidiTrackWrapper> GenerateTracks(MidiFile midiFile)
        {
            var list = new List<MidiTrackWrapper>();
            var midiTempo = midiFile.GetTempoMap();

            foreach (var track in midiFile.Chunks.OfType<TrackChunk>())
            {
                var trackOb = new MidiTrackWrapper(track, midiTempo);
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

            CacheTrackInfo();
        }

        public bool IsOctaveRangeValid(out int midOctaveId)
        {
            int minOctave = NoteUtilities.GetNoteOctave(noteNumberMin);
            int maxOctave = NoteUtilities.GetNoteOctave(noteNumberMax);
            int octaveDiff = maxOctave - minOctave;

            midOctaveId = minOctave + (octaveDiff / 2);
            bool isValid = (octaveDiff < 3) || (octaveDiff == 3 && NoteUtilities.GetNoteName(noteNumberMax) == NoteName.C);

#if DEBUG
            PluginLog.Log($"{NoteUtilities.GetNoteName(noteNumberMin)} [oct {minOctave}] .. {NoteUtilities.GetNoteName(noteNumberMax)} [oct {maxOctave}] ==> valid:{isValid}, midOct:{midOctaveId}");
#endif // DEBUG

            return isValid;
        }

        public void ResetTrackChanges()
        {
            midiTrack = midiTrackOrg.Clone() as TrackChunk;
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

        private void TransformTrack()
        {
            ResetTrackChanges();
            SimplifyChords();
            SimplifyOverlaps();
            FilterTooShort();
        }

        private void CacheDuration()
        {
            var lastNote = midiTrack.GetNotes().Last();
            duration = lastNote.EndTimeAs<MidiTimeSpan>(tempoMap).TimeSpan;

            if (sectionStart != null && sectionEnd != null)
            {
                trackStartTick = TimeConverter.ConvertFrom(sectionStart, tempoMap);
                trackEndTick = TimeConverter.ConvertFrom(sectionEnd, tempoMap);
            }
            else
            {
                trackStartTick = 0;
                trackEndTick = duration;
            }
        }

        private void FindNoteRange(TrackChunk trackChunk, out SevenBitNumber minNoteNum, out SevenBitNumber maxNoteNum)
        {
            minNoteNum = SevenBitNumber.MaxValue;
            maxNoteNum = SevenBitNumber.MinValue;

            if (sectionStart != null && sectionEnd != null)
            {
                foreach (var note in trackChunk.GetNotes())
                {
                    if ((note.Time + note.Length) >= trackStartTick && note.Time < trackEndTick)
                    {
                        if (minNoteNum > note.NoteNumber)
                        {
                            minNoteNum = note.NoteNumber;
                        }
                        if (maxNoteNum < note.NoteNumber)
                        {
                            maxNoteNum = note.NoteNumber;
                        }
                    }
                }
            }
            else
            {
                foreach (var note in trackChunk.GetNotes())
                {
                    if (minNoteNum > note.NoteNumber)
                    {
                        minNoteNum = note.NoteNumber;
                    }
                    if (maxNoteNum < note.NoteNumber)
                    {
                        maxNoteNum = note.NoteNumber;
                    }
                }
            }
        }

        private void CacheNoteRange()
        {
            FindNoteRange(midiTrack, out noteNumberMin, out noteNumberMax);
            FindNoteRange(midiTrackOrg, out noteNumberMinOrg, out noteNumberMaxOrg);
        }

        private void CacheTrackInfo()
        {
            CacheDuration();
            CacheNoteRange();
        }

        public TTimeSpan GetDurationIgnoringSection<TTimeSpan>() where TTimeSpan : ITimeSpan
        {
            return TimeConverter.ConvertTo<TTimeSpan>(duration, tempoMap);
        }

        public TTimeSpan GetDuration<TTimeSpan>() where TTimeSpan : ITimeSpan
        {
            return TimeConverter.ConvertTo<TTimeSpan>(trackEndTick - trackStartTick, tempoMap);
        }

        public long GetDurationMidi() => GetDuration<MidiTimeSpan>().TimeSpan;
        public long GetDurationUs() => GetDuration<MetricTimeSpan>().TotalMicroseconds;
    }
}
