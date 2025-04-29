using DCL.MapRenderer.CommonBehavior;
using DCL.MapRenderer.CoordsUtils;
using DCL.MapRenderer.MapLayers.UsersMarker;
using UnityEngine;
using UnityEngine.Pool;
using Utility;

namespace DCL.MapRenderer.MapLayers.Users
{
    internal class HotUserMarker : IHotUserMarker
    {
        private readonly ICoordsUtils coordsUtils;

        public Vector3 CurrentPosition => poolableBehavior.currentPosition;

        public Vector2 Pivot { get; }

        private MapMarkerPoolableBehavior<HotUserMarkerObject> poolableBehavior;

        internal HotUserMarker(IObjectPool<HotUserMarkerObject> pool, ICoordsUtils coordsUtils)
        {
            this.coordsUtils = coordsUtils;

            poolableBehavior = new MapMarkerPoolableBehavior<HotUserMarkerObject>(pool);
        }

        public void UpdateMarkerPosition(string playerId, Vector3 position)
        {
            var gridPosition = ParcelMathHelper.WorldToGridPositionUnclamped(position);
            poolableBehavior.SetCurrentPosition(coordsUtils.PivotPosition(this, coordsUtils.CoordsToPositionUnclamped(gridPosition)));
        }

        public void Dispose()
        {
            OnMapObjectCulled(this);
        }

        public void OnMapObjectBecameVisible(IHotUserMarker obj)
        {
            poolableBehavior.OnBecameVisible();
        }

        public void OnMapObjectCulled(IHotUserMarker obj)
        {
            poolableBehavior.OnBecameInvisible();
            // Keep tracking position
        }
    }
}
