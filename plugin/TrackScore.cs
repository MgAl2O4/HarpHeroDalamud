using System;

namespace HarpHero
{
    public class TrackScore
    {
        private readonly NoteInputWatcher noteInput;

        private int lastPressedNoteNumber = -1;
        private int lastPlayingNoteNumber = -1;
        private long lastPressedTimeUs;
        private long lastPlayingTimeUs;
        private bool isKeyPressUsed = false;
        private bool isNotePlayUsed = false;

        private bool isPlaying = false;
        public bool IsActive => isPlaying;

        private long accumulatedTimeDiff = 0;
        private long maxPenaltyFreeTimeUs = 50 * 1000;
        public long AccumulatedTimeDiff => accumulatedTimeDiff;

        public TrackScore(NoteInputWatcher noteInput)
        {
            this.noteInput = noteInput;
        }

        public void Update(long currentTimeUs)
        {
            if (isPlaying)
            {
                // this kind of sucks, ideally it should be event driven from hooking whatever plays note in game
                // on the other hand, plugin is already getting heavy on custom sigs :/

                // for now just ignore additional key presses and note duration, focus only on accuracy around intended note
                // - pressed before note played (checked in note event)
                // - pressed after note played (checked here)

                int currentNoteNumber = noteInput.GetActiveNoteNumber();
                if (currentNoteNumber != lastPressedNoteNumber)
                {
                    lastPressedNoteNumber = currentNoteNumber;
                    lastPressedTimeUs = currentTimeUs;

                    if (currentNoteNumber < 0)
                    {
                        isKeyPressUsed = false;
                    }
                    else if (currentNoteNumber == lastPlayingNoteNumber)
                    {
                        AddPenaltyTime(currentTimeUs, lastPlayingTimeUs);
                        isKeyPressUsed = true;
                        isNotePlayUsed = true;
                    }
                }
            }
        }

        public void OnNotePlaying(int noteNumber, long noteStartTimeUs)
        {
            if (isPlaying)
            {
                if (!isNotePlayUsed && lastPlayingNoteNumber > 0)
                {
                    AddPenaltyTime(noteStartTimeUs, lastPlayingTimeUs, true);
                }

                lastPlayingNoteNumber = noteNumber;
                lastPlayingTimeUs = noteStartTimeUs;
                isNotePlayUsed = false;

                if (lastPressedNoteNumber == noteNumber && !isKeyPressUsed)
                {
                    AddPenaltyTime(lastPlayingTimeUs, lastPressedTimeUs);
                    isKeyPressUsed = true;
                    isNotePlayUsed = true;
                }
            }
        }

        private void AddPenaltyTime(long timeA, long timeB, bool isMissed = false)
        {
            long timeDiff = Math.Abs(lastPlayingTimeUs - lastPressedTimeUs);
            if (timeDiff > maxPenaltyFreeTimeUs || isMissed)
            {
                accumulatedTimeDiff += timeDiff;
            }
        }

        public void OnPlayStart()
        {
            isPlaying = true;

            isKeyPressUsed = false;
            isNotePlayUsed = false;

            lastPressedNoteNumber = -1;
            lastPressedTimeUs = 0;

            lastPlayingNoteNumber = -1;
            lastPlayingTimeUs = 0;

            accumulatedTimeDiff = 0;
        }

        public void OnPlayStop()
        {
            isPlaying = false;
        }
    }
}
