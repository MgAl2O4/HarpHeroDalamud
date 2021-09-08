using Dalamud.Logging;
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

        public long startTick;
        public long endTick;

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

        public bool IsOctaveRangeValid(out int midOctaveId)
        {
            bool isValid = false;
            midOctaveId = 0;

            int minOctave = NoteUtilities.GetNoteOctave(minNote);
            int maxOctave = NoteUtilities.GetNoteOctave(maxNote);
            int octaveDiff = maxOctave - minOctave;

            midOctaveId = minOctave + (octaveDiff / 2);
            isValid = (octaveDiff < 3) || (octaveDiff == 3 && NoteUtilities.GetNoteName(maxNote) == NoteName.C);
#if DEBUG
            PluginLog.Log($"{NoteUtilities.GetNoteName(minNote)} [oct {minOctave}] .. {NoteUtilities.GetNoteName(maxNote)} [oct {maxOctave}] ==> valid:{isValid}, midOct:{midOctaveId}");
#endif // DEBUG

            return isValid;
        }

        public float GetKeysPerSecond(float timeScaling)
        {
            float numBeatsPerSecond = beatsPerMinute * timeScaling / 60.0f;
            return numBeatsPerSecond * notesPerBeat;
        }

        private void CalcDuration(TrackChunk track, TempoMap tempoMap, ITimeSpan sectionStart, ITimeSpan sectionEnd)
        {
            if (sectionStart != null && sectionEnd != null)
            {
                startTick = TimeConverter.ConvertFrom(sectionStart, tempoMap);
                endTick = TimeConverter.ConvertFrom(sectionEnd, tempoMap);
            }
            else
            {
                var lastNote = track.GetNotes().Last();

                startTick = 0;
                endTick = lastNote.Time + lastNote.Length;
            }

            var barStart = TimeConverter.ConvertTo<BarBeatTicksTimeSpan>(startTick, tempoMap);
            var barEnd = TimeConverter.ConvertTo<BarBeatTicksTimeSpan>(endTick, tempoMap);
            numBars = (int)(barEnd.Bars - barStart.Bars + 1);

            duration = TimeConverter.ConvertTo<MetricTimeSpan>(DurationTicks, tempoMap);
        }

        private void CalcNoteRange(TrackChunk track)
        {
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
                    if (note.Time >= endTick)
                    {
                        break;
                    }

                    if (note.Time >= currentBeatEnd && currentBeatIdx < beatTimes.Count)
                    {
                        if (maxNotesPerBeat < currentBeatNotes)
                        {
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
