using System;
using UnityEngine;
using Utility;
using Object = UnityEngine.Object;

namespace DCL.InWorldCamera.ScreencaptureCamera
{
    public class InWorldCameraFactory : IDisposable
    {
        private GameObject hud;
        private CharacterController followTarget;

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

        public void Dispose()
        {
            followTarget.SelfDestroy();
            hud.SelfDestroy();
        }
    }
}
