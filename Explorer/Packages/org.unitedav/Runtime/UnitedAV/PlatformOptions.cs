// SPDX-License-Identifier: Apache-2.0

using System;
using UnitedAV.Windows;

namespace UnitedAV
{
    public partial class MediaPlayer
    {
        public class PlatformOptions
        {
            public enum AudioMode
            {
                SystemDirect = 0,
                Unity = 1,
                FacebookAudio360 = 2,
            }
        }
    }

    [Serializable]
    public class PlatformOptionsWindows
    {
        public VideoApi videoApi = VideoApi.WinRT;
        public AudioOutput _audioMode = AudioOutput.System;
        public bool startWithHighestBitrate = false;
        public bool useLowLiveLatency = false;
    }

    [Serializable]
    public class PlatformOptions_macOS
    {
        public MediaPlayer.PlatformOptions.AudioMode audioMode = MediaPlayer.PlatformOptions.AudioMode.SystemDirect;
    }
}
