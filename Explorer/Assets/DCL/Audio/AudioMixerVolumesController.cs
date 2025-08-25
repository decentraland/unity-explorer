using DCL.Diagnostics;
using System;
using System.Collections.Generic;
using UnityEngine.Audio;

namespace DCL.Audio
{
    public enum AudioMixerExposedParam
    {
        Master_Volume,
        Avatar_Volume,
        Chat_Volume,
        Music_Volume,
        UI_Volume,
        World_Volume,
        VoiceChat_Volume,
        Microphone_Volume
    }

    public class AudioMixerVolumesController
    {
        private const float MUTE_VALUE = -80;

        private readonly AudioMixer audioMixer;
        private readonly VolumeBus volumeBus;
        private readonly string[] allExposedParams;
        private readonly Dictionary<string, float> originalVolumes = new ();
        private readonly HashSet<string> mutedGroups = new ();

        public AudioMixerVolumesController(AudioMixer audioMixer, VolumeBus volumeBus)
        {
            this.audioMixer = audioMixer;
            this.volumeBus = volumeBus;
            allExposedParams = Enum.GetNames(typeof(AudioMixerExposedParam));
            //We mute microphone by default as we don't want to hear ourselves
            MuteGroup(AudioMixerExposedParam.Microphone_Volume);

            volumeBus.OnGlobalMuteChanged += GlobalMuteChanged;
        }

        private void GlobalMuteChanged(bool value)
        {
            if(value)
                MuteGroup(AudioMixerExposedParam.Master_Volume);
            else
                UnmuteGroup(AudioMixerExposedParam.Master_Volume);
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
                        ReportHub.LogWarning(ReportCategory.AUDIO, "Cannot unmute audio mixer group: missing original volume. Probably the group was not previously muted.");

                    mutedGroups.Remove(groupParamString);
                }
                break;
            }
        }
    }
}
