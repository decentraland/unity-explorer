using DCL.FeatureFlags;
using System;

namespace DCL.VoiceChat.UI
{
    /// <summary>
    ///     Decides when the Nearby Voice Chat intro tip is shown: once every <see cref="ShowEverySessions" /> launches,
    ///     at most <see cref="MaxTimesShown" /> times over the user's lifetime, and never once the user has actually
    ///     used nearby voice chat. With the defaults this lands on launch 5 and launch 10.
    /// </summary>
    public readonly struct NearbyVoiceTipSchedule
    {
        private const int DEFAULT_SHOW_EVERY_SESSIONS = 5;
        private const int DEFAULT_MAX_TIMES_SHOWN = 2;

        public readonly int ShowEverySessions;
        public readonly int MaxTimesShown;

        /// <summary>
        ///     A schedule that never comes due, used when the tip's feature flag is off.
        /// </summary>
        public static NearbyVoiceTipSchedule Disabled => new (1, 0);

        public NearbyVoiceTipSchedule(int showEverySessions, int maxTimesShown)
        {
            // A non-positive period would make the tip due on every single launch.
            ShowEverySessions = Math.Max(1, showEverySessions);
            MaxTimesShown = Math.Max(0, maxTimesShown);
        }

        /// <summary>
        ///     Reads the frequency from the feature flag variant payload, falling back to the defaults when the flag
        ///     carries no usable configuration.
        /// </summary>
        public static NearbyVoiceTipSchedule FromFeatureFlags(FeatureFlagsConfiguration featureFlags)
        {
            if (!featureFlags.TryGetJsonPayload(FeatureFlagsStrings.NEARBY_VOICE_CHAT_TIP, FeatureFlagsStrings.NEARBY_VOICE_CHAT_TIP_CONFIG_VARIANT, out ConfigDto config))
                return new NearbyVoiceTipSchedule(DEFAULT_SHOW_EVERY_SESSIONS, DEFAULT_MAX_TIMES_SHOWN);

            return new NearbyVoiceTipSchedule(
                config.showEverySessions ?? DEFAULT_SHOW_EVERY_SESSIONS,
                config.maxTimesShown ?? DEFAULT_MAX_TIMES_SHOWN);
        }

        /// <param name="launchCount">How many times the application has been launched, including the current run.</param>
        /// <param name="timesShown">How many times the tip has already been displayed to this user.</param>
        /// <param name="lastShownAtLaunch">The launch count at which the tip was last displayed, or 0 if never.</param>
        /// <param name="hasUsedNearbyVoice">Whether the user has ever spoken over nearby voice chat.</param>
        public bool ShouldShow(int launchCount, int timesShown, int lastShownAtLaunch, bool hasUsedNearbyVoice)
        {
            if (hasUsedNearbyVoice) return false;
            if (timesShown >= MaxTimesShown) return false;

            // Measured from the last display rather than from launch 0, otherwise a returning user who is already
            // past every threshold would burn all their displays on consecutive launches.
            return launchCount >= lastShownAtLaunch + ShowEverySessions;
        }

        // ReSharper disable InconsistentNaming
        [Serializable]
        private struct ConfigDto
        {
            public int? showEverySessions;
            public int? maxTimesShown;
        }
        // ReSharper restore InconsistentNaming
    }
}