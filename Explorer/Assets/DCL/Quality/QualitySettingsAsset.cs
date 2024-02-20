using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using UnityEngine;
using UnityEngine.Rendering;

namespace DCL.Quality
{
    [CreateAssetMenu(menuName = "Quality/Create Quality Settings", fileName = "Quality Settings", order = 0)]
    public class QualitySettingsAsset : ScriptableObject
    {
        // Draw quality tab from the settings

        // Custom
        [SerializeField] private List<QualityCustomLevel> customSettings;

        [Serializable]
        public class QualityCustomLevel
        {
            /// <summary>
            ///     References to the assets
            /// </summary>
            [SerializeField] internal VolumeProfile volumeProfile;

            [SerializeField] internal FogSettings fogSettings;
        }

        // Draw fog
    }

    /// <summary>
    ///     it's a copy from the Unity's Fog (FogEditor)
    /// </summary>
    [Serializable]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public class FogSettings
    {
        public bool m_Fog;
        public Color m_FogColor;
        public FogMode m_FogMode = FogMode.Linear;
        public float m_FogDensity;
        public float m_LinearFogStart;
        public float m_LinearFogEnd;
    }
}
