// SPDX-License-Identifier: Apache-2.0

namespace UnitedAV.Windows
{
    public enum VideoApi
    {
        WinRT = 0,
        MediaFoundation = 1,
        DirectShow = 2,
    }

    public enum AudioOutput
    {
        System = 0,
        Unity = 1,
        FacebookAudio360 = 2,
    }
}
