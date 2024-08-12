using System;
using UnityEngine;

namespace DCL.MapRenderer
{
    [RequireComponent(typeof(LineRenderer))]
    public class MapPathRenderer : MonoBehaviour
    {
        private const float MIN_DOT_SIZE_MINIMAP = 25;
        private const float MIN_DOT_SIZE_NAVMAP = 20;
        private const int NUM_CAP_VERTICES = 0;

        private float currentDotSize;
        private bool destinationSet;
        private LineRenderer lineRenderer;
        private Vector2 originPoint;

        public Vector2 DestinationPoint { get; private set; }

        private void Awake()
        {
            lineRenderer = GetComponent<LineRenderer>();
            SetupLineRenderer();
        }

        private void SetupLineRenderer()
        {
            lineRenderer.useWorldSpace = true;
            Material lineMaterial = lineRenderer.material;
            lineMaterial.mainTextureOffset = new Vector2(0f, 0f);
            lineRenderer.numCapVertices = NUM_CAP_VERTICES;
            currentDotSize = MIN_DOT_SIZE_NAVMAP;
            RecalculateLineSize(MIN_DOT_SIZE_NAVMAP);
        }

        private void RecalculateLineSize(float scale)
        {
            currentDotSize = scale;
            Material lineMaterial = lineRenderer.material;
            lineRenderer.startWidth = scale;
            lineRenderer.endWidth = scale;
            float textureRepeat = 1f / (scale * 2);
            lineMaterial.mainTextureScale = new Vector2(textureRepeat, 1);
        }

        public void SetZoom(float baseZoom, float newZoom)
        {
            RecalculateLineSize(Math.Max(newZoom / baseZoom * MIN_DOT_SIZE_NAVMAP, MIN_DOT_SIZE_NAVMAP));
            UpdateLine();
        }

        public void ResetScale()
        {
            RecalculateLineSize(MIN_DOT_SIZE_MINIMAP);
            UpdateLine();
        }

        public void SetDestination(Vector2 destination)
        {
            destinationSet = true;
            DestinationPoint = destination;
            UpdateLine();
        }

        public void UpdateOrigin(Vector2 origin, bool updateLine = false)
        {
            originPoint = origin;

            if (updateLine && destinationSet) { UpdateLine(); }
        }

        private void UpdateLine()
        {
            Vector3 direction = originPoint - DestinationPoint;
            float distance = direction.magnitude;

            float totalUnitLength = currentDotSize + currentDotSize;
            int numberOfDots = Mathf.FloorToInt(distance / totalUnitLength) + 1;

            lineRenderer.positionCount = numberOfDots * 2;

            for (var i = 0; i < numberOfDots; i++)
            {
                float startT = i * totalUnitLength / distance;
                float endT = startT + (currentDotSize / distance);

                var dotStart = Vector3.Lerp(DestinationPoint, originPoint, startT);
                var dotEnd = Vector3.Lerp(DestinationPoint, originPoint, endT);

                lineRenderer.SetPosition(i * 2, dotStart);
                lineRenderer.SetPosition((i * 2) + 1, dotEnd);
            }
        }
    }
}
