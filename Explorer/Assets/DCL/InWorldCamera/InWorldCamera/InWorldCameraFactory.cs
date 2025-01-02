using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Utility;
using Object = UnityEngine.Object;

namespace DCL.InWorldCamera
{
    public class InWorldCameraFactory : IDisposable
    {
        private const string AUTO_VOLUME_NAME = "InWorldCamera.GlobalVolume";

        private GameObject hud;
        private CharacterController followTarget;

        private Volume globalVolume;

        public void Dispose()
        {
            followTarget.SelfDestroy();
            hud.SelfDestroy();

            globalVolume.profile.SelfDestroy();
            globalVolume.gameObject.SelfDestroy();
        }

        public CharacterController CreateFollowTarget(GameObject prefab)
        {
            followTarget = Object.Instantiate(prefab, Vector3.zero, Quaternion.identity)
                                 .GetComponent<CharacterController>();

            followTarget.enabled = false;

            return followTarget;
        }

        public GameObject CreateScreencaptureHud(GameObject prefab)
        {
            hud = Object.Instantiate(prefab, Vector3.zero, Quaternion.identity);
            hud.SetActive(false);

            return hud;
        }

        public (Volume, ColorAdjustments, DepthOfField) CreatePostProcessing()
        {
            var volumeObject = new GameObject(AUTO_VOLUME_NAME);

            globalVolume = volumeObject.AddComponent<Volume>();
            globalVolume.isGlobal = true;
            globalVolume.priority = 100f;

            VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
            globalVolume.profile = profile;

            ColorAdjustments colorAdjustments = profile.Add<ColorAdjustments>();
            DepthOfField depthOfField = profile.Add<DepthOfField>();

            return (globalVolume, colorAdjustments, depthOfField);
        }
    }
}
