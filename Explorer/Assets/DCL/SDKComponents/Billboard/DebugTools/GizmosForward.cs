using UnityEngine;

namespace DCL.Billboard.DebugTools
{
    public class GizmosForward : MonoBehaviour
    {
        private void OnDrawGizmos()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, transform.position + transform.forward);
        }
    }
}
