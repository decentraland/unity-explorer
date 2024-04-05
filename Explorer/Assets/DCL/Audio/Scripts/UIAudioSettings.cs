using DCL.Diagnostics;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DCL.Audio
{
    [CreateAssetMenu(fileName = "UIAudioSettings", menuName = "SO/Audio/UIAudioSettings")]
    public class UIAudioSettings : ScriptableObject
    {
        [SerializeField] private AudioClip defaultAudioClip;
        [SerializeField] private float uiAudioVolume = 0.5f;
        [SerializeField] private int uiAudioPriority = 125;

        public float UIAudioVolume => uiAudioVolume;

    }
}
