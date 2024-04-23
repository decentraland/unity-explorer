using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

#nullable disable

namespace DCL.Quality
{
    [CreateAssetMenu(menuName = "Quality/Create Quality Settings", fileName = "Quality Settings", order = 0)]
    public class QualitySettingsAsset : ScriptableObject
    {
        // Draw quality tab from the settings

        // Custom
        [SerializeField] public List<QualityCustomLevel> customSettings;

        /// <summary>
        ///     This list is needed to avoid reflection to get all possible renderer features
        /// </summary>
        [SerializeField] internal List<ScriptableRendererFeature> allRendererFeatures;

        [SerializeField] internal bool updateInEditor;

        [Serializable]
        public class QualityCustomLevel
        {
            /// <summary>
            ///     References to the assets
            /// </summary>
            [SerializeField] internal VolumeProfile volumeProfile;

            [SerializeField] internal FogSettings fogSettings;

            [SerializeField] internal bool lensFlareEnabled;

            [SerializeField] internal LensFlareComponentSRP lensFlareComponent;

            [SerializeField] public EnvironmentSettings environmentSettings;
        }
    }
}
