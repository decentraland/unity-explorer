using Cysharp.Threading.Tasks;
using DCL.EventsApi;
using DCL.MapRenderer.CommonBehavior;
using DCL.MapRenderer.CoordsUtils;
using DCL.MapRenderer.Culling;
using DCL.PlacesAPIService;
using DG.Tweening;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.Pool;

namespace DCL.MapRenderer.MapLayers.Categories
{
    internal class CategoryMarker : ICategoryMarker
    {
        internal const int MAX_TITLE_LENGTH = 29;

        private readonly IMapCullingController cullingController;

        private MapMarkerPoolableBehavior<CategoryMarkerObject> poolableBehavior;
        private readonly ICoordsUtils coordsUtils;
        private float currentBaseScale;
        private float currentNewScale;

        public Vector3 CurrentPosition => poolableBehavior.currentPosition;

        public bool IsVisible => poolableBehavior.isVisible;

        public PlacesData.PlaceInfo? PlaceInfo => placeInfo;

        public EventDTO EventDTO => eventDTO;

        public Vector2 Pivot => new (0.5f, 0.5f);

        internal string title { get; private set; }

        internal Sprite iconSprite { get; private set; }

        internal PlacesData.PlaceInfo? placeInfo { get; private set; }

        internal EventDTO eventDTO { get; private set; }

        public CategoryMarker(IObjectPool<CategoryMarkerObject> objectsPool, IMapCullingController cullingController, ICoordsUtils coordsUtils)
        {
            poolableBehavior = new MapMarkerPoolableBehavior<CategoryMarkerObject>(objectsPool);
            this.cullingController = cullingController;
            this.coordsUtils = coordsUtils;
        }

        public void Dispose()
        {
            OnBecameInvisible();
            cullingController.StopTracking(this);
        }

        public void SetData(string title, Vector3 position, PlacesData.PlaceInfo? place, EventDTO eventDto)
        {
            poolableBehavior.SetCurrentPosition(coordsUtils.PivotPosition(this, position));
            this.title = title.Length > MAX_TITLE_LENGTH ? title.Substring(0, MAX_TITLE_LENGTH) : title;
            this.placeInfo = place;
            this.eventDTO = eventDto;
        }

        public void SetCategorySprite(Sprite sprite)
        {
            iconSprite = sprite;
        }

        public void OnBecameVisible()
        {
            poolableBehavior.OnBecameVisible().title.text = title;
            poolableBehavior.instance.SetCategorySprite(iconSprite);

            MarkerHelper.SetAlpha(poolableBehavior.OnBecameVisible().renderers, poolableBehavior.OnBecameVisible().textRenderers, 0);
            MarkerHelper.FadeToAsync(poolableBehavior.OnBecameVisible().renderers, poolableBehavior.OnBecameVisible().textRenderers, 1, 0.5f, Ease.OutBack, CancellationToken.None).Forget();

            if(currentBaseScale != 0)
                poolableBehavior.instance.SetScale(currentBaseScale, currentNewScale);
        }

        public void OnBecameInvisible()
        {
            ToggleSelection(false);
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

        public async UniTaskVoid AnimateSelectionAsync(CancellationToken ct)
        {
            if (poolableBehavior.instance != null)
                await MarkerHelper.ScaleToAsync(poolableBehavior.instance.scalingParent, new Vector2 (1.2f, 1.2f), 0.5f, Ease.OutBack, ct);
        }

        public async UniTaskVoid AnimateDeSelectionAsync(CancellationToken ct)
        {
            if (poolableBehavior.instance != null)
                await MarkerHelper.ScaleToAsync(poolableBehavior.instance.scalingParent, Vector2.one, 0.5f, Ease.OutBack, ct, Vector3.one);
        }

        public GameObject? GetGameObject() =>
            poolableBehavior.instance != null ? poolableBehavior.instance.gameObject : null;

        public void ToggleSelection(bool isSelected)
        {
            if (poolableBehavior.instance != null)
                poolableBehavior.instance.ToggleSelection(isSelected);
        }
    }
}
