﻿using DCL.MapRenderer.CommonBehavior;
using DCL.MapRenderer.CoordsUtils;
using DCL.MapRenderer.MapLayers.UsersMarker;
using System;
using UnityEngine;
using UnityEngine.Pool;
using Utility;

namespace DCL.MapRenderer.MapLayers.Users
{
    internal class HotUserMarker : IHotUserMarker
    {
        private readonly ICoordsUtils coordsUtils;

        public string CurrentPlayerId { get; private set; }
        public Vector3 CurrentPosition => poolableBehavior.currentPosition;

        public Vector2 Pivot { get; }
        public Vector2Int ParcelCoords => Vector2Int.zero;

        private MapMarkerPoolableBehavior<HotUserMarkerObject> poolableBehavior;

        internal HotUserMarker(IObjectPool<HotUserMarkerObject> pool, ICoordsUtils coordsUtils)
        {
            this.coordsUtils = coordsUtils;

            poolableBehavior = new MapMarkerPoolableBehavior<HotUserMarkerObject>(pool);
        }

        public void ToggleSelection(bool isSelected) { }

        public void UpdateMarkerPosition(string playerId, Vector3 position)
        {
            CurrentPlayerId = playerId;
            var gridPosition = ParcelMathHelper.WorldToGridPositionUnclamped(position);
            poolableBehavior.SetCurrentPosition(coordsUtils.PivotPosition(this, coordsUtils.CoordsToPositionUnclamped(gridPosition)));
        }

        private void ResetPlayer()
        {
            CurrentPlayerId = null;
        }

        public void Dispose()
        {
            OnMapObjectCulled(this);
            ResetPlayer();
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
