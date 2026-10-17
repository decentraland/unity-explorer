using DCL.DebugUtilities;
using System;
using System.Collections.Generic;
using UnityEngine.Rendering;

namespace DCL.Quality.Runtime
{
    /// <summary>
    ///     Currently not used, it might be a further improvement to control every individual volume component
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class VolumeComponentQualitySettingRuntime<T> : IQualitySettingRuntime where T: VolumeComponent
    {
        /// <summary>
        ///     The target volume profile that exists in a single instance in the scene
        /// </summary>
        private readonly VolumeProfile volumeProfile;

        /// <summary>
        ///     Volume component asset
        /// </summary>
        private readonly T volumeComponent;

        public bool IsActive => volumeComponent.active;

        public VolumeComponentQualitySettingRuntime(VolumeProfile volumeProfile, T volumeComponent)
        {
            this.volumeProfile = volumeProfile;
            this.volumeComponent = volumeComponent;
        }

        public void SetActive(bool active)
        {
            if (active)
            {
                // ensure the volume component is added to the profile
                if (!volumeProfile.Has<T>())
                {
                    volumeProfile.components.Add(volumeComponent);
                    volumeProfile.isDirty = true;
                }
            }

            // Don't remove the profile if it was already added
            volumeComponent.active = active;
        }

        public void ApplyPreset(QualitySettingsAsset.QualityCustomLevel preset) { }

        public void RestoreState(QualitySettingsAsset.QualityCustomLevel currentPreset) { }

        public void AddDebugView(DebugWidgetBuilder debugWidgetBuilder, List<Action> onUpdate) { }
    }
}
