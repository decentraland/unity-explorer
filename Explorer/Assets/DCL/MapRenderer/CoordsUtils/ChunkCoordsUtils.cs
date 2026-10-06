using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Utility;

namespace DCL.MapRenderer.CoordsUtils
{
    internal class ChunkCoordsUtils : ICoordsUtils
    {
        private const int PADDING = 25;

        // Parcels of open map around a world's parcels.
        private const int WORLD_PADDING = 10;

        private static readonly Vector2Int WORLD_MIN_COORDS = GenesisCityData.MIN_PARCEL;
        private static readonly Vector2Int WORLD_MAX_COORDS = GenesisCityData.MAX_SQUARE_CITY_PARCEL + (PADDING * Vector2Int.one); // DCL map is not squared, there are some extra parcels in the top right

        private static readonly Vector2Int VISIBLE_WORLD_MIN_COORDS = WORLD_MIN_COORDS - (PADDING * Vector2Int.one);
        private static readonly Vector2Int VISIBLE_WORLD_MAX_COORDS = WORLD_MAX_COORDS; // DCL map is not squared, there are some extra parcels in the top right

        private readonly Rect genesisVisibleWorldBounds;
        private readonly List<Rect> genesisInteractableBounds;
        private readonly List<Rect> worldInteractableBounds = new (1);

        private List<Rect> interactableWorldBoundsInLocalCoordinates;

        public event Action? VisibleWorldBoundsChanged;

        public Vector2Int WorldMinCoords => WORLD_MIN_COORDS;
        public Vector2Int WorldMaxCoords => WORLD_MAX_COORDS;

        public int ParcelSize { get; }
        public Rect VisibleWorldBounds { get; private set; }

        // Genesis City is centred on the origin, not on its padded bounds.
        public Vector2 VisibleWorldCenter { get; private set; }

        public ChunkCoordsUtils(int parcelSize)
        {
            ParcelSize = parcelSize;

            var min = (VISIBLE_WORLD_MIN_COORDS - Vector2Int.one) * parcelSize;
            var max = VISIBLE_WORLD_MAX_COORDS * parcelSize;
            VisibleWorldBounds = genesisVisibleWorldBounds = RectUtils.MinMaxRect(min, max);

            interactableWorldBoundsInLocalCoordinates = genesisInteractableBounds = GenesisCityData.INTERACTABLE_WORLD_BOUNDS
                                                       .Select(chunk => Rect.MinMaxRect((chunk.xMin - 1) * parcelSize, (chunk.yMin - 1) * parcelSize, chunk.xMax * parcelSize, chunk.yMax * parcelSize))
                                                       .ToList();
        }

        public void SetWorldBounds(RectInt? worldParcels)
        {
            if (worldParcels is { } parcels)
            {
                // A parcel's area ends at its own coordinates and starts one parcel before them.
                Rect parcelsRect = Rect.MinMaxRect((parcels.xMin - 1) * ParcelSize, (parcels.yMin - 1) * ParcelSize, (parcels.xMax - 1) * ParcelSize, (parcels.yMax - 1) * ParcelSize);
                float padding = WORLD_PADDING * ParcelSize;

                VisibleWorldBounds = Rect.MinMaxRect(parcelsRect.xMin - padding, parcelsRect.yMin - padding, parcelsRect.xMax + padding, parcelsRect.yMax + padding);
                VisibleWorldCenter = parcelsRect.center;

                worldInteractableBounds.Clear();
                worldInteractableBounds.Add(parcelsRect);
                interactableWorldBoundsInLocalCoordinates = worldInteractableBounds;
            }
            else
            {
                VisibleWorldBounds = genesisVisibleWorldBounds;
                VisibleWorldCenter = Vector2.zero;
                interactableWorldBoundsInLocalCoordinates = genesisInteractableBounds;
            }

            VisibleWorldBoundsChanged?.Invoke();
        }

        public bool TryGetCoordsWithinInteractableBounds(Vector3 pos, out Vector2Int coords)
        {
            coords = default;

            foreach (Rect rect in interactableWorldBoundsInLocalCoordinates)
            {
                if (rect.Contains(pos))
                {
                    coords = PositionToCoords(pos);
                    return true;
                }
            }

            return false;
        }

        public Vector2Int PositionToCoords(Vector3 pos) =>
            new (Mathf.CeilToInt(pos.x / ParcelSize), Mathf.CeilToInt(pos.y / ParcelSize));

        public Vector2 PositionToCoordsUnclamped(Vector3 pos) =>
            pos / ParcelSize;

        public Vector3 CoordsToPositionUnclamped(Vector2 coords) =>
            coords * ParcelSize;

        public Vector3 CoordsToPosition(Vector2Int coords) =>
            (Vector2)(coords * ParcelSize);

        public Vector3 CoordsToPositionWithOffset(Vector2 coords) =>
            (coords * ParcelSize) - new Vector2(ParcelSize / 2f, ParcelSize / 2f);
    }
}
