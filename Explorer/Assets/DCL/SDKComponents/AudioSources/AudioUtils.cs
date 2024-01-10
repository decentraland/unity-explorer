using System;
using UnityEngine;

namespace DCL.SDKComponents.AudioSources
{
    public static class AudioUtils
    {
        public static AudioType ToAudioType(this string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                Debug.LogError($"Cannot detect AudioType. UrlName doesn't contain file extension!. Setting to {AudioType.UNKNOWN.ToString()}");
                return AudioType.UNKNOWN;
            }

            var ext = url.AsSpan()[^3..];

            if (ext.Equals("mp3", StringComparison.OrdinalIgnoreCase))
                return AudioType.MPEG;

            if (ext.Equals("wav", StringComparison.OrdinalIgnoreCase))
                return AudioType.WAV;

            if (ext.Equals("ogg", StringComparison.OrdinalIgnoreCase))
                return AudioType.OGGVORBIS;

            return AudioType.UNKNOWN;
        }
    }
}
