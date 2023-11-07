using DCLServices.MapRenderer.MapCameraController;
using System;
using UnityEngine;
using Utility;

namespace DCLServices.MapRenderer.ConsumerUtils
{
    /// <summary>
    /// Updates Map Camera Controller's position accordingly to the player position
    /// </summary>
    public struct MapRendererTrackPlayerPosition
    {
        private const float SQR_DISTANCE_TOLERANCE = 0.01f;

        private readonly IMapCameraController cameraController;
        //TODO: find base variable solution

        private Vector3 lastPlayerPosition;

        public MapRendererTrackPlayerPosition(IMapCameraController cameraController)
        {
            this.cameraController = cameraController;

            lastPlayerPosition = Vector3.positiveInfinity;
        }

        public void OnPlayerPositionChanged(Vector3 position)
        {
            if (Vector3.SqrMagnitude(position - lastPlayerPosition) < SQR_DISTANCE_TOLERANCE)
                return;

            lastPlayerPosition = position;
            cameraController.SetPosition(GetPlayerCentricCoords(position));
        }

        public static Vector2 GetPlayerCentricCoords(Vector3 playerPosition)
        {
            var newCoords = ParcelMathHelper.WorldToGridPositionUnclamped(playerPosition);
            return GetPlayerCentricCoords(newCoords);
        }

        public static Vector2 GetPlayerCentricCoords(Vector2 playerCoords)
        {
            // quick hack to align with `CoordsToPositionWithOffset` and the pivot
            return playerCoords - Vector2.one;
        }
    }
}
