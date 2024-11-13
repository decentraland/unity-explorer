using DCL.MapRenderer.CommonBehavior;
using DCL.MapRenderer.CoordsUtils;
using DCL.MapRenderer.Culling;
using System;
using UnityEngine;
using UnityEngine.Pool;

namespace DCL.MapRenderer.MapLayers.Favorites
{
    internal class FavoritesMarker : IFavoritesMarker
    {
        internal const int MAX_TITLE_LENGTH = 29;
        private float currentBaseScale;
        private float currentNewScale;

        public Vector3 CurrentPosition => poolableBehavior.currentPosition;

        public bool IsVisible => poolableBehavior.isVisible;

        public Vector2 Pivot => new (0.5f, 0.5f);

        internal string title { get; private set; }

        private MapMarkerPoolableBehavior<FavoriteMarkerObject> poolableBehavior;

        private readonly IMapCullingController cullingController;
        private readonly ICoordsUtils coordsUtils;

        public FavoritesMarker(IObjectPool<FavoriteMarkerObject> objectsPool, IMapCullingController cullingController, ICoordsUtils coordsUtils)
        {
            poolableBehavior = new MapMarkerPoolableBehavior<FavoriteMarkerObject>(objectsPool);
            this.cullingController = cullingController;
            this.coordsUtils = coordsUtils;
        }

        public void SetData(string title, Vector3 position)
        {
            poolableBehavior.SetCurrentPosition(coordsUtils.PivotPosition(this, position));
            this.title = title.Length > MAX_TITLE_LENGTH ? title.Substring(0, MAX_TITLE_LENGTH) : title;
        }

        public void OnBecameVisible()
        {
            poolableBehavior.OnBecameVisible().title.text = title;

            if(currentBaseScale != 0)
                poolableBehavior.instance.SetScale(currentBaseScale, currentNewScale);
        }

        public void OnBecameInvisible()
        {
            poolableBehavior.OnBecameInvisible();
        }

        public void SetZoom(float baseScale, float baseZoom, float zoom)
        {
            currentBaseScale = baseScale;
            currentNewScale = Math.Max(zoom / baseZoom * baseScale, baseScale);

            if (poolableBehavior.instance != null)
                poolableBehavior.instance.SetScale(currentBaseScale, currentNewScale);
        }

        public void ResetScale(float scale)
        {
            currentNewScale = scale;

            if (poolableBehavior.instance != null)
                poolableBehavior.instance.SetScale(scale, scale);
        }

        public void Dispose()
        {
            OnBecameInvisible();
            cullingController.StopTracking(this);
        }
    }
}
