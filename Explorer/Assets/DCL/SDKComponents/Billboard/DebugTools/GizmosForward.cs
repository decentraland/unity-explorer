using UnityEngine;

namespace DCL.Billboard.DebugTools
{
    public class GizmosForward : MonoBehaviour
    {
        private void OnDrawGizmos()
        {
            UnityEngine.Gizmos.color = Color.green;
            UnityEngine.Gizmos.DrawLine(transform.position, transform.position + transform.forward);
        }
    }
}
