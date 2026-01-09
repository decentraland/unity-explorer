using CrdtEcsBridge.Physics;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace DCL.JumpIndicator
{
    [RequireComponent(typeof(DecalProjector))]
    public class JumpIndicatorController : MonoBehaviour
    {
        private static readonly int PLAYER_GROUND_DISTANCE_ID = Shader.PropertyToID("_PlayerGroundDistance");

        [SerializeField] private float groundCheckRadius = 0.1f;

        private DecalProjector decalProjector;

        private float groundDistance;

        private void Awake()
        {
            decalProjector = GetComponent<DecalProjector>();
            enabled = decalProjector;
        }

        private void Update()
        {
            Vector3 origin = GetGroundCheckOrigin();
            float maxDistance = decalProjector.size.z;
            LayerMask layerMask = PhysicsLayers.CHARACTER_ONLY_MASK;

            bool didHit = Physics.SphereCast(origin, groundCheckRadius, Vector3.down, out RaycastHit hit, maxDistance, layerMask);
            groundDistance = didHit ? hit.distance : maxDistance;

            Shader.SetGlobalFloat(PLAYER_GROUND_DISTANCE_ID, groundDistance);
        }

        private Vector3 GetGroundCheckOrigin() =>
            transform.position + ((groundCheckRadius + 0.001f) * Vector3.up);

        private void OnDrawGizmosSelected()
        {
            Vector3 origin = GetGroundCheckOrigin();
            Vector3 hitPoint = origin + (Vector3.down * groundDistance);

            UnityEngine.Gizmos.color = Color.yellow;
            UnityEngine.Gizmos.DrawLine(origin, hitPoint);
            UnityEngine.Gizmos.DrawWireSphere(hitPoint, groundCheckRadius);
        }
    }
}
