using System;
using System.Diagnostics.CodeAnalysis;
using UnityEngine;

namespace DCL.Quality
{
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
