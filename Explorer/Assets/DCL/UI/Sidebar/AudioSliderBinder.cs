using DCL.Prefs;
using DCL.Settings.Utils;
using System;
using UnityEngine.Audio;
using UnityEngine.UI;

namespace DCL.UI.Sidebar
{
    /// <summary>
    ///     Binds one sidebar audio slider to the same mixer parameters and preference keys the Settings panel writes,
    ///     so both surfaces stay on a single stored value. Sidebar sliders are normalised 0-1 while the stored
    ///     preference is a 0-100 percentage.
    /// </summary>
    public class AudioSliderBinder : IDisposable
    {
        public readonly struct Channel
        {
            public readonly string ExposedParam;
            public readonly string PrefKey;

            public Channel(string exposedParam, string prefKey)
            {
                ExposedParam = exposedParam;
                PrefKey = prefKey;
            }
        }

        private const float PERCENTAGE = 100f;

        private readonly Slider slider;
        private readonly AudioMixer mixer;
        private readonly Channel[] channels;
        private readonly Action<float>? setNormalisedBusVolume;

        public AudioSliderBinder(Slider slider, AudioMixer mixer, Action<float>? setNormalisedBusVolume, params Channel[] channels)
        {
            this.slider = slider;
            this.mixer = mixer;
            this.channels = channels;
            this.setNormalisedBusVolume = setNormalisedBusVolume;

            Refresh();
            slider.onValueChanged.AddListener(Apply);
        }

        public void Dispose()
        {
            slider.onValueChanged.RemoveListener(Apply);
        }

        /// <summary>
        ///     Re-reads the stored value so the slider reflects changes made from the Settings panel.
        /// </summary>
        public void Refresh()
        {
            string prefKey = channels[0].PrefKey;

            if (DCLPlayerPrefs.HasKey(prefKey))
                slider.SetValueWithoutNotify(DCLPlayerPrefs.GetFloat(prefKey) / PERCENTAGE);

            Apply(slider.value);
        }

        private void Apply(float normalisedVolume)
        {
            float volumePercentage = normalisedVolume * PERCENTAGE;
            float decibel = AudioUtils.PercentageVolumeToDecibel(volumePercentage);

            foreach (Channel channel in channels)
            {
                mixer.SetFloat(channel.ExposedParam, decibel);
                DCLPlayerPrefs.SetFloat(channel.PrefKey, volumePercentage, save: true);
            }

            setNormalisedBusVolume?.Invoke(normalisedVolume);
        }
    }
}
