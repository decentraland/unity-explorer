using System;
using System.Collections.Generic;
using UnityEngine.Audio;

namespace DCL.Audio
{
    public enum AudioMixerExposedParam
    {
        Avatar_Volume,
        Chat_Volume,
        Music_Volume,
        UI_Volume,
        World_Volume
    }

    public class AudioMixerVolumesController
    {
        private const float MUTE_VALUE = -80;

        private readonly AudioMixer audioMixer;
        private readonly string[] allExposedParams;
        private readonly Dictionary<string, float> originalVolumes = new ();

        public AudioMixerVolumesController(AudioMixer audioMixer)
        {
            this.audioMixer = audioMixer;
            allExposedParams = Enum.GetNames(typeof(AudioMixerExposedParam));
        }

        public void MuteGroup(AudioMixerExposedParam groupParam)
        {
            var groupParamString = groupParam.ToString();

            foreach (string exposedParam in allExposedParams)
            {
                if (exposedParam != groupParamString)
                    continue;

                audioMixer.GetFloat(groupParamString, out float originalVolume);
                originalVolumes[groupParamString] = originalVolume;
                audioMixer.SetFloat(groupParamString, MUTE_VALUE);
                break;
            }
        }

        public void UnmuteGroup(AudioMixerExposedParam groupParam)
        {
            var groupParamString = groupParam.ToString();

            foreach (string exposedParam in allExposedParams)
            {
                if (exposedParam != groupParamString)
                    continue;

                audioMixer.SetFloat(groupParamString, originalVolumes[groupParamString]);
                break;
            }
        }
    }
}
