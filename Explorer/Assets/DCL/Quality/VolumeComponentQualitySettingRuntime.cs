using UnityEngine.Rendering;

namespace DCL.Quality
{
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

        public VolumeComponentQualitySettingRuntime(VolumeProfile volumeProfile, T volumeComponent)
        {
            this.volumeProfile = volumeProfile;
            this.volumeComponent = volumeComponent;
        }

        public bool IsActive => volumeComponent.active;

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
    }
}
