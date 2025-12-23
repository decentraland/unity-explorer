using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace DCL.Utilities
{
    /// <summary>
    /// </summary>
    public class GizmoDrawer : MonoBehaviour
    {
        private struct DrawWireCubeParams
        {
            public Vector3 Center;
            public Vector3 Size;
            public Color Color;
        }

        private struct DrawWireSphereParams
        {
            public Color Color;
            public Vector3 Center;
            public float Radius;
        }

        public static GizmoDrawer Instance
        {
            get
            {
#if UNITY_EDITOR
                if (instance == null)
                {
                    instance = new GameObject("GizmoDrawerSingleton", typeof(GizmoDrawer)).GetComponent<GizmoDrawer>();
                    DontDestroyOnLoad(instance.gameObject);
                }
#endif

                return instance;
            }
        }

        private static GizmoDrawer instance = null!;
        private readonly Dictionary<int, DrawWireCubeParams> drawWireCubeParams = new (128);
        private readonly Dictionary<int, DrawWireSphereParams> drawWireSphereParams = new (128);

        [Conditional("UNITY_EDITOR")]
        public void DrawWireCube(int id, Vector3 center, Vector3 size, Color color)
        {
            drawWireCubeParams[id] = new DrawWireCubeParams
            {
                Center = center, Size = size, Color = color
            };
        }

        [Conditional("UNITY_EDITOR")]
        public void DrawWireSphere(int id, Vector3 center, float radius, Color color)
        {
            drawWireSphereParams[id] = new DrawWireSphereParams
            {
                Center = center, Radius = radius, Color = color
            };
        }

        [Conditional("UNITY_EDITOR")]
        public void EraseWireCube(int id)
        {
            drawWireCubeParams.Remove(id);
        }

        [Conditional("UNITY_EDITOR")]
        public void EraseWireSphere(int id)
        {
            drawWireSphereParams.Remove(id);
        }

        [Conditional("UNITY_EDITOR")]
        public void ClearGizmos()
        {
            drawWireCubeParams.Clear();
            drawWireSphereParams.Clear();
        }

#if UNITY_EDITOR

        private void OnDrawGizmos()
        {
            foreach (var pair in drawWireCubeParams)
            {
                Gizmos.color = pair.Value.Color;
                Gizmos.DrawWireCube(pair.Value.Center, pair.Value.Size);
            }

            foreach (var pair in drawWireSphereParams)
            {
                Gizmos.color = pair.Value.Color;
                Gizmos.DrawWireSphere(pair.Value.Center, pair.Value.Radius);
            }
        }

#endif
    }
}