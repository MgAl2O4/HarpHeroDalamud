using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using Melanchall.DryWetMidi.MusicTheory;
using System.Collections.Generic;
using System.Linq;

namespace HarpHero
{
    public class MidiTrackStats
    {
        public SevenBitNumber minNote;
        public SevenBitNumber maxNote;
        public int notesPerBeat;
        public int beatsPerMinute;

        public int numNotes;
        public int numBars;
        public int numBarsTotal;

        public long startTick;
        public long endTick;
        public int startBar;
        public int endBar;

        public TimeSignature timeSignature;
        public int numTimeSignatures;

        public MetricTimeSpan duration;
        public long DurationTicks => endTick - startTick;

        public void Update(TrackChunk track, TempoMap tempoMap, ITimeSpan sectionStart, ITimeSpan sectionEnd)
        {
            numNotes = track.GetNotes().Count;
            if (numNotes > 0)
            {
                CalcDuration(track, tempoMap, sectionStart, sectionEnd);
                CalcNoteRange(track);
                CalcNotePerBeat(track, tempoMap);
                CalcTempoAndTimeSignature(tempoMap);
            }
        }

        public void Update(TrackChunk track, TempoMap tempoMap) => Update(track, tempoMap, null, null);

        public int GetOctaveRange()
        {
            int minOctave = NoteUtilities.GetNoteOctave(minNote);
            int maxOctave = NoteUtilities.GetNoteOctave(maxNote);
            return maxOctave - minOctave + 1;
        }

        public bool IsOctaveRangeValid(SevenBitNumber minNoteNumber, SevenBitNumber maxNoteNumber, out int midOctaveId, int octaveRange = 3)
        {
            int minOctave = NoteUtilities.GetNoteOctave(minNoteNumber);
            int maxOctave = NoteUtilities.GetNoteOctave(maxNoteNumber);
            int octaveDiff = maxOctave - minOctave;
            midOctaveId = minOctave + (octaveDiff / 2);

            return (octaveDiff < octaveRange) || (octaveDiff == octaveRange && NoteUtilities.GetNoteName(maxNoteNumber) == NoteName.C);
        }
        public bool IsOctaveRangeValid(out int midOctaveId, int octaveRange = 3) => IsOctaveRangeValid(minNote, maxNote, out midOctaveId, octaveRange);

        public float GetKeysPerSecond(float timeScaling)
        {
            float numBeatsPerSecond = beatsPerMinute * timeScaling / 60.0f;
            return numBeatsPerSecond * notesPerBeat;
        }

        private void CalcDuration(TrackChunk track, TempoMap tempoMap, ITimeSpan sectionStart, ITimeSpan sectionEnd)
        {
            var lastNote = track.GetNotes().Last();
            var lastNoteEndTick = lastNote.Time + lastNote.Length;
            numBarsTotal = (int)TimeConverter.ConvertTo<BarBeatTicksTimeSpan>(lastNoteEndTick, tempoMap).Bars + 1;

            if (sectionStart != null && sectionEnd != null)
            {
                startTick = TimeConverter.ConvertFrom(sectionStart, tempoMap);
                endTick = TimeConverter.ConvertFrom(sectionEnd, tempoMap);
            }
            else
            {
                startTick = 0;
                endTick = lastNoteEndTick;
            }

            startBar = (int)TimeConverter.ConvertTo<BarBeatTicksTimeSpan>(startTick, tempoMap).Bars;
            endBar = (int)TimeConverter.ConvertTo<BarBeatTicksTimeSpan>(endTick, tempoMap).Bars;

            if (startTick < endTick)
            {
                numBars = endBar - startBar + 1;
                duration = TimeConverter.ConvertTo<MetricTimeSpan>(DurationTicks, tempoMap);
            }
            else
            {
                numBars = 0;
                numBarsTotal = 0;
            }
        }

        private void CalcNoteRange(TrackChunk track)
        {
            if (startTick >= endTick)
            {
                minNote = SevenBitNumber.MinValue;
                maxNote = SevenBitNumber.MinValue;
                return;
            }

            minNote = SevenBitNumber.MaxValue;
            maxNote = SevenBitNumber.MinValue;

            foreach (var note in track.GetNotes())
            {
                if ((note.Time + note.Length) >= startTick && note.Time < endTick)
                {
                    if (minNote > note.NoteNumber)
                    {
                        minNote = note.NoteNumber;
                    }
                    if (maxNote < note.NoteNumber)
                    {
                        maxNote = note.NoteNumber;
                    }
                }
            }
        }

        private void CalcNotePerBeat(TrackChunk track, TempoMap tempoMap)
        {
            if (startTick >= endTick)
            {
                notesPerBeat = 0;
                return;
            }

            var beatTimes = new List<long>();

            var startTimeBar = TimeConverter.ConvertTo<BarBeatTicksTimeSpan>(startTick, tempoMap);
            var endTimeBar = TimeConverter.ConvertTo<BarBeatTicksTimeSpan>(endTick, tempoMap);

            var itTimeInc = new BarBeatTicksTimeSpan(0, 1);
            for (var itTimeBeat = new BarBeatTicksTimeSpan(startTimeBar.Bars, startTimeBar.Beats); itTimeBeat < endTimeBar; itTimeBeat += itTimeInc)
            {
                long itTicks = TimeConverter.ConvertFrom(itTimeBeat, tempoMap);
                itTimeBeat = TimeConverter.ConvertTo<BarBeatTicksTimeSpan>(itTicks, tempoMap);   // convert back from raw ticks to resolve time signature dependency

                beatTimes.Add(itTicks);
            }

            if (beatTimes.Count >= 2)
            {
                int maxNotesPerBeat = 0;

                int currentBeatIdx = 0;
                int currentBeatNotes = 0;
                long currentBeatEnd = beatTimes[currentBeatIdx + 1];

                foreach (var note in track.GetNotes())
                {
                    if (note.Time < beatTimes[currentBeatIdx])
                    {
                        continue;
                    }

                    if (note.Time >= endTick)
                    {
                        break;
                    }

                    if (note.Time >= currentBeatEnd && currentBeatIdx < beatTimes.Count)
                    {
                        if (maxNotesPerBeat < currentBeatNotes)
                        {
                            var noteTimeBar = TimeConverter.ConvertTo<BarBeatTicksTimeSpan>(note.Time, tempoMap);
                            maxNotesPerBeat = currentBeatNotes;
                        }

                        currentBeatNotes = 0;

                        for (currentBeatIdx++; currentBeatIdx < beatTimes.Count; currentBeatIdx++)
                        {
                            currentBeatEnd = beatTimes[currentBeatIdx];
                            if (note.Time < currentBeatEnd)
                            {
                                break;
                            }
                        }
                    }

                    currentBeatNotes++;
                }

                notesPerBeat = (maxNotesPerBeat > currentBeatNotes) ? maxNotesPerBeat : currentBeatNotes;
            }
            else
            {
                notesPerBeat = track.GetNotes().Count();
            }
        }

        private void CalcTempoAndTimeSignature(TempoMap tempoMap)
        {
            var startTimeTS = new MidiTimeSpan(0);
            var tempo = tempoMap.GetTempoAtTime(startTimeTS);
            beatsPerMinute = (int)tempo.BeatsPerMinute;

            timeSignature = tempoMap.GetTimeSignatureAtTime(startTimeTS);

            // look only at compatibility with game metronome ticks, e.g. 6/8 and 3/4 can kind of coexist
            // (ik it's not the same number of strong beats, but meh, it's salvagable)
            float orgSigProportion = 1.0f * timeSignature.Numerator / timeSignature.Denominator;
            var listUniqueSigs = new List<float>();
            listUniqueSigs.Add(orgSigProportion);

            foreach (var timeSig in tempoMap.GetTimeSignatureChanges())
            {
                float timeSigProportion = 1.0f * timeSig.Value.Numerator / timeSig.Value.Denominator;
                if (!listUniqueSigs.Contains(timeSigProportion))
                {
                    listUniqueSigs.Add(timeSigProportion);
                }
            }

            numTimeSignatures = listUniqueSigs.Count;
        }
    }
}
