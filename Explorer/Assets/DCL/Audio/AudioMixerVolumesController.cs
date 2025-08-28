using DCL.Diagnostics;
using System;
using System.Collections.Generic;
using DCL.Prefs;
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
            volumeBus.OnMusicAndSFXMuteChanged += MusicAndSFXMuteChanged;
        }

        private void GlobalMuteChanged(bool value)
        {
            if(value)
                MuteGroup(AudioMixerExposedParam.Master_Volume);
            else
                UnmuteGroup(AudioMixerExposedParam.Master_Volume);
        }

        private void MusicAndSFXMuteChanged(bool value)
        {
            if(value)
                MuteGroup(AudioMixerExposedParam.Music_Volume);
            else
                UnmuteGroup(AudioMixerExposedParam.Music_Volume);
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
                    
                    // If that group is globally muted, override original volume with serialized volume.
                    // This makes sure that bellow flow returns correct value: 
                    // Muted globally -> temporarily muted (original value gets saved here as 0, which is incorrect) 
                    // -> Globally unmuted -> locally unmuted (group volume gets wrongly set to 0)
                    switch (groupParam)
                    {
                        case AudioMixerExposedParam.Music_Volume:
                            if (volumeBus.GetMusicAndSFXMuteValue())
                                originalVolume = volumeBus.GetSerializedMusicVolume();
                            break;
                        case AudioMixerExposedParam.World_Volume:
                            if (volumeBus.GetMusicAndSFXMuteValue())
                                originalVolume = volumeBus.GetSerializedWorldVolume();
                            break;
                        case AudioMixerExposedParam.Master_Volume:
                            if (volumeBus.GetGlobalMuteValue())
                                originalVolume = volumeBus.GetSerializedMasterVolume();
                            break;
                    }
                    
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
                    bool isGloballyMuted = false;
                    
                    switch (groupParam)
                    {
                        case AudioMixerExposedParam.Music_Volume:
                        case AudioMixerExposedParam.World_Volume:
                            isGloballyMuted = volumeBus.GetMusicAndSFXMuteValue();
                            break;
                        
                        case AudioMixerExposedParam.Master_Volume:
                            isGloballyMuted = volumeBus.GetGlobalMuteValue();
                            break;
                    }
                    
                    // Don't unmute the group if its already muted.
                    if (!isGloballyMuted)
                    {
                        if (originalVolumes.TryGetValue(groupParamString, out float originalVolume))
                            audioMixer.SetFloat(groupParamString, originalVolume);
                        else
                            ReportHub.LogWarning(ReportCategory.AUDIO, "Cannot unmute audio mixer group: missing original volume. Probably the group was not previously muted.");
                    }
                    mutedGroups.Remove(groupParamString);
                }
                break;
            }
        }
    }
}
