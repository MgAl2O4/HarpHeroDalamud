using System;

namespace HarpHero
{
    public class TrackScore
    {
        private int lastPressedNoteNumber = -1;
        private int lastPlayingNoteNumber = -1;
        private long lastPressedTimeUs;
        private long lastPlayingTimeUs;
        private bool isKeyPressUsed = false;
        private bool isNotePlayUsed = false;
        private bool isTraining = false;

        private bool isPlaying = false;
        public bool IsActive => isPlaying;

        private long accumulatedTimeDiff = 0;
        private long maxPenaltyFreeTimeUs = 50 * 1000;
        private long maxScoreWindowUs = 100 * 1000;
        private long maxTrainingWindowUs = 150 * 1000;
        private long useScoreWindowUs;
        public long AccumulatedTimeDiff => accumulatedTimeDiff;

        private int scoreComboChain;
        private int scoreRankMinor;
        private int scoreRankMinorMax;
        private int scoreRankMajor;
        private string[] rankNames = { "...", "E", "D", "C", "A", "S", ":D" };

        public string RankName => rankNames[Math.Clamp(scoreRankMajor, 0, rankNames.Length - 1)];
        public int RankMajor => scoreRankMajor;
        public int RankMinor => scoreRankMinor;
        public int RankMinorMax => scoreRankMinorMax;
        public int ComboChain => scoreComboChain;

        public void SetTrainingMode(bool isTraining)
        {
            this.isTraining = isTraining;
            useScoreWindowUs = isTraining ? maxTrainingWindowUs : maxScoreWindowUs;
        }

        public void OnNotePressed(int noteNumber, long currentTimeUs, int nextPlayingNoteNumber, long nextPlayingTimeUs)
        {
            if (isPlaying)
            {
                lastPressedNoteNumber = noteNumber;
                lastPressedTimeUs = currentTimeUs;

                if (noteNumber <= 0)
                {
                    isKeyPressUsed = false;
                }
                else if (noteNumber == lastPlayingNoteNumber)
                {
                    AddNoteScoreTime(currentTimeUs, lastPlayingTimeUs);
                    isKeyPressUsed = true;
                    isNotePlayUsed = true;
                }
                else if (noteNumber != nextPlayingNoteNumber || Math.Abs(nextPlayingTimeUs - currentTimeUs) > useScoreWindowUs)
                {
                    // this wasn't supposed to be pressed. ignore for accuracy tracking, but send bad note notify if needed
                    OnNoteBad();
                }
            }
        }

        public void OnNotePlaying(int noteNumber, long noteStartTimeUs)
        {
            if (isPlaying)
            {
                if (!isNotePlayUsed && lastPlayingNoteNumber > 0)
                {
                    OnNoteMissed();
                    AddNoteScoreTime(noteStartTimeUs, lastPlayingTimeUs, true);
                }

                lastPlayingNoteNumber = noteNumber;
                lastPlayingTimeUs = noteStartTimeUs;
                isNotePlayUsed = false;

                if (lastPressedNoteNumber == noteNumber && !isKeyPressUsed)
                {
                    AddNoteScoreTime(lastPlayingTimeUs, lastPressedTimeUs);
                    isKeyPressUsed = true;
                    isNotePlayUsed = true;
                }
            }
        }

        private void AddNoteScoreTime(long timeA, long timeB, bool isMissed = false)
        {
            long timeDiff = Math.Abs(lastPlayingTimeUs - lastPressedTimeUs);
            if (timeDiff > maxPenaltyFreeTimeUs || isMissed)
            {
                accumulatedTimeDiff += timeDiff;
            }

            if (!isMissed && timeDiff < useScoreWindowUs)
            {
                OnNoteGood();
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
            scoreComboChain = 0;
            SetRank(0);
        }

        public void OnPlayStop()
        {
            isPlaying = false;
        }

        private bool SetRank(int rankMajor)
        {
            scoreRankMajor = Math.Clamp(rankMajor, 0, rankNames.Length - 1);
            scoreRankMinor = 0;

            // 10, 10, 12, 14, 16, ...
            scoreRankMinorMax = Math.Max(10, 8 + (scoreRankMajor * 2));
            return scoreRankMajor == rankMajor;
        }

        private void OnNoteGood()
        {
            scoreComboChain++;
            scoreRankMinor++;

            if (scoreRankMinor > scoreRankMinorMax)
            {
                if (!SetRank(scoreRankMajor + 1))
                {
                    scoreRankMinor = scoreRankMinorMax;
                }
            }
        }

        private void OnNoteBad()
        {
            scoreComboChain = 0;

            if (scoreRankMajor > 0 && scoreRankMinor == 0)
            {
                SetRank(scoreRankMajor - 1);
                scoreRankMinor = scoreRankMinorMax / 2;
            }
            else
            {
                scoreRankMinor = 0;
            }
        }

        private void OnNoteMissed()
        {
            OnNoteBad();
        }
    }
}
