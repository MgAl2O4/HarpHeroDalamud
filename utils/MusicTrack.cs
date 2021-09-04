using MidiParser;
using System;
using System.Collections.Generic;

namespace HarpHero
{
    public class MusicTrack
    {
        // limits enforced by game mechanics
        // - only 1 note can play at the same time
        // - notes span over max 3 octaves (well, 4, but 4th can accept only note[0] = C)
        //
        // additional limits:
        // - constant tempo for entire track, allows easy use of built in metronome
        // - consider notes with overlap / super low delay as a chord and trim it to just the root note

        public enum Status
        {
            NoErrors,
            FailedMidiFormat,
            FailedOctaveRange,
            FailedTextImport,
        }

        public class Note
        {
            public int time;
            public int duration;
            public byte noteIdx;
            public sbyte octaveIdx;

            public override string ToString()
            {
                return string.Format("t:{0}, d:{1}, o:{2}, n:{3}", time, duration, octaveIdx, noteIdx);
            }
        }
        public List<Note> notes = new();
        public int beatsPerMinute = 120;
        public Status status;
        public string Name;

        public int numTicksPerQuarterNote = 960;
        private float minNoteDelay = 0.05f;

        public bool Load(MidiFile midi, int trackIdx = 0)
        {
            if (midi == null || midi.Tracks == null || trackIdx < 0 || trackIdx >= midi.TracksCount)
            {
                status = Status.FailedMidiFormat;
                return false;
            }

            var midiTrack = midi.Tracks[trackIdx];

            var nameEvent = midiTrack.TextEvents.Find(x => x.TextEventType == TextEventType.TrackName);
            Name = !string.IsNullOrEmpty(nameEvent.Value) ? nameEvent.Value : $"Track #{trackIdx + 1}";

            Dictionary<int, Note> activeNotes = new();
            int minOctave = 100;
            int maxOctave = -100;
            bool hasLockedTempo = false;

            foreach (var trackEvent in midiTrack.MidiEvents)
            {
                if (trackEvent.MidiEventType == MidiEventType.MetaEvent)
                {
                    if (trackEvent.MetaEventType == MetaEventType.Tempo)
                    {
                        if (!hasLockedTempo)
                        {
                            beatsPerMinute = trackEvent.Arg2;
                            hasLockedTempo = true;
                        }
                    }
                }
                else if (trackEvent.MidiEventType == MidiEventType.NoteOn)
                {
                    if (!activeNotes.ContainsKey(trackEvent.Note))
                    {
                        var noteIdx = trackEvent.Note % 12;
                        var octaveIdx = (trackEvent.Note / 12) - 1;
                        minOctave = Math.Min(minOctave, octaveIdx);
                        maxOctave = Math.Max(maxOctave, octaveIdx);

                        int octaveSpan = maxOctave - minOctave + 1;
                        if (octaveSpan > 4)
                        {
                            status = Status.FailedOctaveRange;
                            return false;
                        }
                        if (octaveSpan == 4 && octaveIdx == maxOctave && noteIdx > 0)
                        {
                            status = Status.FailedOctaveRange;
                            return false;
                        }

                        activeNotes.Add(trackEvent.Note, new Note() { noteIdx = (byte)noteIdx, octaveIdx = (sbyte)octaveIdx, time = trackEvent.Time, duration = 0 });
                    }
                }
                else if (trackEvent.MidiEventType == MidiEventType.NoteOff)
                {
                    if (activeNotes.ContainsKey(trackEvent.Note))
                    {
                        var noteInfo = activeNotes[trackEvent.Note];
                        noteInfo.duration = trackEvent.Time - noteInfo.time;
                        notes.Add(noteInfo);

                        activeNotes.Remove(trackEvent.Note);
                    }
                }
            }

            notes.Sort((x, y) => x.time.CompareTo(y.time));

            ConvertChords();
            SeparateOverlaps();

            if (!ConvertToRelativeOctaves(minOctave, maxOctave))
            {
                status = Status.FailedOctaveRange;
            }

            status = Status.NoErrors;
            return true;
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

        private void ConvertChords()
        {
            // works only with default BPM
            int maxChordDelayTicks = ConvertSecondstoTicks(minNoteDelay);

            // chord = mutliple notes starting together or with super low delay
            for (int idx = 0; idx < notes.Count; idx++)
            {
                int startTime = notes[idx].time;
                int chainLen = 1;

                for (int endIdx = idx + 1; endIdx < notes.Count; endIdx++)
                {
                    int delay = notes[endIdx].time - startTime;
                    if (delay > maxChordDelayTicks)
                    {
                        break;
                    }

                    chainLen++;
                }

                if (chainLen > 1)
                {
                    //Console.WriteLine($"Chord! {idx} num:{chainLen}");
                    int rootNoteAndOct = 1000;
                    int rootIdx = 0;

                    for (int chordIdx = 0; chordIdx < chainLen; chordIdx++)
                    {
                        var testNodeOb = notes[idx + chordIdx];
                        var noteAndOct = testNodeOb.noteIdx + (testNodeOb.octaveIdx * 12);
                        if (rootNoteAndOct > noteAndOct)
                        {
                            rootNoteAndOct = noteAndOct;
                            rootIdx = chordIdx;
                        }
                    }

                    for (int chordIdx = chainLen - 1; chordIdx >= 0; chordIdx--)
                    {
                        //var testNodeOb = notes[idx + chordIdx];
                        //Console.WriteLine($">> n:{testNodeOb.noteIdx}, o:{testNodeOb.octaveIdx}, duration:{testNodeOb.duration}, root:{chordIdx == rootIdx}");

                        if (chordIdx != rootIdx)
                        {
                            notes.RemoveAt(idx + chordIdx);
                        }
                    }
                }
            }
        }

        private void SeparateOverlaps()
        {
            for (int idx = 0; idx < notes.Count - 1; idx++)
            {
                int ticksToNext = notes[idx + 1].time - notes[idx].time - 1;
                notes[idx].duration = Math.Min(notes[idx].duration, ticksToNext);
            }
        }

        private bool ConvertToRelativeOctaves(int minOctave, int maxOctave)
        {
            // -1, 0, +1
            // if spans over 3 or has just 1 - easy mode mapping
            // if 2, find which one has more notes and make it idx:0

            int octaveSpan = maxOctave - minOctave + 1;
            if (octaveSpan >= 3)
            {
                for (int idx = 0; idx < notes.Count; idx++)
                {
                    notes[idx].octaveIdx -= (sbyte)(minOctave + 1);

                    if (notes[idx].octaveIdx == 2)
                    {
                        // special case for single note in 4th octave
                        if (notes[idx].noteIdx > 0)
                        {
                            return false;
                        }

                        notes[idx].octaveIdx = 1;
                        notes[idx].noteIdx = 12;
                    }
                }
            }
            else if (octaveSpan == 2)
            {
                int numNotesMin = 0;
                for (int idx = 0; idx < notes.Count; idx++)
                {
                    numNotesMin += (notes[idx].octaveIdx == minOctave) ? 1 : 0;
                }

                bool minOctaveIsBase = numNotesMin > (notes.Count / 2);
                for (int idx = 0; idx < notes.Count; idx++)
                {
                    bool isMinOctave = notes[idx].octaveIdx == minOctave;
                    notes[idx].octaveIdx = (sbyte)(minOctaveIsBase ? (isMinOctave ? 0 : 1) : (isMinOctave ? -1 : 0));
                }
            }
            else
            {
                for (int idx = 0; idx < notes.Count; idx++)
                {
                    notes[idx].octaveIdx = 0;
                }
            }

            return true;
        }

        public float ConvertTicksToSeconds(int ticks)
        {
            // tbh, i have no idea what i'm doing here.
            // use 1 beat = 1 quarter note and pretend it's fine? 

            float numSecondsPerBeat = 60.0f / beatsPerMinute;
            float numSecondsPerTick = numSecondsPerBeat / numTicksPerQuarterNote;
            return ticks * numSecondsPerTick;
        }

        private int ConvertSecondstoTicks(float seconds)
        {
            float numBeatsPerSecond = beatsPerMinute / 60.0f;
            float numTicksPerSecond = numBeatsPerSecond * numTicksPerQuarterNote;
            return (int)(seconds * numTicksPerSecond);
        }
    }
}
