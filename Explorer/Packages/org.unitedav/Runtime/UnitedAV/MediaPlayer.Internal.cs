// SPDX-License-Identifier: Apache-2.0
// The native ABI is set-only for looping/rate, so the last value set is cached here.

namespace UnitedAV
{
    public partial class MediaPlayer
    {
        internal bool LoopingFlag { get; set; }
        internal float PlaybackRateValue { get; set; } = 1f;

        internal bool IsSeekingInternal => _wasSeekRequested && _wasSeeking;
    }
}
