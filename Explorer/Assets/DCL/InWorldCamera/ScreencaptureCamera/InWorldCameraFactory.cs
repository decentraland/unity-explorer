using DCL.AssetsProvision;
using System;
using UnityEngine;
using Utility;
using Object = UnityEngine.Object;

namespace DCL.InWorldCamera.ScreencaptureCamera
{
    public class InWorldCameraFactory : IDisposable
    {
        private CharacterController followTarget;
        private ProvidedAsset<GameObject> hudPrefab;
        private GameObject hud;

        public CharacterController CreateFollowTarget()
        {
            followTarget = new GameObject("InWorldCameraFollowTarget").AddComponent<CharacterController>();
            followTarget.gameObject.layer = LayerMask.NameToLayer("CharacterController");

            followTarget.slopeLimit = 0;
            followTarget.stepOffset = 0;
            followTarget.skinWidth = 0.01f;

            followTarget.minMoveDistance = 0;
            followTarget.center = Vector3.zero;
            followTarget.radius = 0.1f;
            followTarget.height = 0.2f;

            followTarget.enabled = false;

            return followTarget;
        }

        public GameObject CreateScreencaptureHud(ProvidedAsset<GameObject> prefab)
        {
            hudPrefab = prefab;

            hud = Object.Instantiate(hudPrefab.Value, Vector3.zero, Quaternion.identity);
            hud.SetActive(false);

            return hud;
        }

        public void Dispose()
        {
            hudPrefab.Dispose();

            followTarget.SelfDestroy();
            hud.SelfDestroy();
        }
    }
}
