using System;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using UnityEngine;
using UnityEngine.Animations.Rigging;

namespace DCL.Character.CharacterMotion.AdditiveBreath
{
    [Serializable]
    public struct AdditiveBreathData : IAnimationJobData
    {
        [SerializeField] private Transform m_UpperArm;
        [SerializeField] private Transform m_Forearm;
        [SerializeField] private Transform m_Hand;
        [SerializeField] private AdditiveBreathDataBridge m_Bridge;

        public Transform UpperArm => m_UpperArm;
        public Transform Forearm => m_Forearm;
        public Transform Hand => m_Hand;
        public AdditiveBreathDataBridge Bridge => m_Bridge;

        bool IAnimationJobData.IsValid()
        {
            return m_UpperArm != null && m_Forearm != null && m_Hand != null
                   && m_Bridge != null && m_Bridge.IsInitialized;
        }

        void IAnimationJobData.SetDefaultValues()
        {
            m_UpperArm = null;
            m_Forearm = null;
            m_Hand = null;
            m_Bridge = null;
        }
    }
}
