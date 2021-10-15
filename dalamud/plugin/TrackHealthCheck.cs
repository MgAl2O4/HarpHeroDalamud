namespace HarpHero
{
    // helper class for verifying play conditions between various parts of plugins
    public class TrackHealthCheck
    {
        public enum Status
        {
            NoTrack,
            TooManyOctaves,
            MissingWideMode,
            MissingKeyboardMode,
            MissingBindings,
            CanPlayBasic,
            CanPlayExtended,
        }

        private readonly NoteInputMapper inputMapper;
        private readonly TrackAssistant trackAssistant;
        private readonly UIReaderBardPerformance uiReaderPerformance;
        private readonly Configuration config;

        public Status cachedStatus;

        private bool canRefreshBindings;
        private float refreshBindingsTimeRemaining;

        public TrackHealthCheck(NoteInputMapper inputMapper, TrackAssistant trackAssistant, UIReaderBardPerformance uiReaderPerformance, Configuration config)
        {
            this.inputMapper = inputMapper;
            this.trackAssistant = trackAssistant;
            this.uiReaderPerformance = uiReaderPerformance;
            this.config = config;
        }

        public void UpdatePlayStatus(float deltaTime)
        {
            if (!canRefreshBindings)
            {
                refreshBindingsTimeRemaining -= deltaTime;
                if (refreshBindingsTimeRemaining <= 0.0f)
                {
                    canRefreshBindings = true;
                }
            }

            cachedStatus = FindPlayStatus();
        }

        private Status FindPlayStatus()
        {
            if (!trackAssistant.IsPlaying)
            {
                if (trackAssistant.musicTrack == null)
                {
                    return Status.NoTrack;
                }

                if (!trackAssistant.IsValidBasicMode && !trackAssistant.IsValidExtendedMode)
                {
                    return Status.TooManyOctaves;
                }

                if (trackAssistant.IsValidExtendedMode)
                {
                    if (!config.UseExtendedMode)
                    {
                        return Status.TooManyOctaves;
                    }

                    if (!inputMapper.IsKeyboardMode)
                    {
                        return Status.MissingKeyboardMode;
                    }

                    if (uiReaderPerformance.status != UIReaderBardPerformance.Status.NoErrorsWide)
                    {
                        return Status.MissingWideMode;
                    }

                    if (trackAssistant.CanShowBindAssistant)
                    {
                        // check every few seconds, doesn't need to be same frame
                        if (canRefreshBindings)
                        {
                            canRefreshBindings = false;
                            refreshBindingsTimeRemaining = 2.0f;

                            inputMapper.UpdateBindingState();
                        }

                        if (!inputMapper.HasAllExtendedModeBindings)
                        {
                            return Status.MissingBindings;
                        }
                    }
                }
            }

            return trackAssistant.IsValidExtendedMode ? Status.CanPlayExtended : Status.CanPlayBasic;
        }

        public float GetTrackLengthPct()
        {
            var statBlock = trackAssistant.musicTrack.stats;
            return (statBlock.numBarsTotal > 0) ? (1.0f * statBlock.numBars / statBlock.numBarsTotal) : 0.0f;
        }
    }
}
