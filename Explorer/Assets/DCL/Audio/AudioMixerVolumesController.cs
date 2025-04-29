using DCL.Diagnostics;
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
        private readonly HashSet<string> mutedGroups = new ();

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

                // Only store the original volume if this group hasn't been muted before
                if (!mutedGroups.Contains(groupParamString))
                {
                    audioMixer.GetFloat(groupParamString, out float originalVolume);
                    originalVolumes[groupParamString] = originalVolume;
                    mutedGroups.Add(groupParamString);
                }

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

                if (mutedGroups.Contains(groupParamString))
                {
                    if (originalVolumes.TryGetValue(groupParamString, out float originalVolume))
                        audioMixer.SetFloat(groupParamString, originalVolume);
                    else
                        ReportHub.LogError(ReportCategory.AUDIO, "Cannot unmute audio mixer group: missing original volume. Probably the group was not previously muted..");

                    mutedGroups.Remove(groupParamString);
                }
                break;
            }
        }
    }
}
