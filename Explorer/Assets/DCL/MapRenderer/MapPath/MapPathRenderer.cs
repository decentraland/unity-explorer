using UnityEngine;

namespace DCL.MapRenderer
{
    [RequireComponent(typeof(LineRenderer))]
    public class MapPathRenderer : MonoBehaviour
    {
        public float dotSize = 0.2f;
        public float spaceBetweenDots = 0.2f;

        public Vector2 DestinationPoint => destinationPoint;

        private bool destinationSet;
        private Vector2 destinationPoint;
        private LineRenderer lineRenderer;
        private Vector2 originPoint;

        private void Awake()
        {
            lineRenderer = GetComponent<LineRenderer>();
            SetupLineRenderer();
        }

        private void SetupLineRenderer()
        {
            lineRenderer.useWorldSpace = true;
            lineRenderer.startWidth = dotSize * 2;
            lineRenderer.endWidth = dotSize * 2;
            lineRenderer.numCapVertices = 5;

            Material lineMaterial = lineRenderer.material;
            float textureRepeat = 1f / (dotSize + spaceBetweenDots);
            lineMaterial.mainTextureScale = new Vector2(textureRepeat, 1);
            lineMaterial.mainTextureOffset = new Vector2(0f, 0f);
        }

        public void SetDestination(Vector2 destination)
        {
            destinationSet = true;
            destinationPoint = destination;
            UpdateLine();
        }

        public void UpdateOrigin(Vector2 origin, bool updateLine = false)
        {
            originPoint = origin;

            if (updateLine && destinationSet) { UpdateLine(); }
        }

        private void UpdateLine()
        {
            if (!this.gameObject.activeSelf) return;

            Vector3 direction = destinationPoint - originPoint;
            float distance = direction.magnitude;

            float totalUnitLength = dotSize + spaceBetweenDots;
            int numberOfDots = Mathf.FloorToInt(distance / totalUnitLength) + 1;

            lineRenderer.positionCount = numberOfDots * 2;

            for (var i = 0; i < numberOfDots; i++)
            {
                float startT = i * totalUnitLength / distance;
                float endT = startT + (dotSize / distance);

                var dotStart = Vector3.Lerp(originPoint, destinationPoint, startT);
                var dotEnd = Vector3.Lerp(originPoint, destinationPoint, endT);

                lineRenderer.SetPosition(i * 2, dotStart);
                lineRenderer.SetPosition((i * 2) + 1, dotEnd);
            }
        }
    }
}
