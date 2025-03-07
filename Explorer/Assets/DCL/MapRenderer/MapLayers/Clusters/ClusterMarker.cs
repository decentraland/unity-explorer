﻿using Cysharp.Threading.Tasks;
using DCL.MapRenderer.CommonBehavior;
using DCL.MapRenderer.CoordsUtils;
using DCL.MapRenderer.Culling;
using DG.Tweening;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.Pool;

namespace DCL.MapRenderer.MapLayers.Cluster
{
    internal class ClusterMarker : IClusterMarker
    {
        internal const int MAX_TITLE_LENGTH = 29;
        private static readonly Vector2 TARGET_SCALE = new (1.2f, 1.2f);
        private static readonly float ANIMATION_DURATION = 0.5f;

        private readonly IMapCullingController cullingController;

        private MapMarkerPoolableBehavior<ClusterMarkerObject> poolableBehavior;
        private readonly ICoordsUtils coordsUtils;
        private float currentBaseScale;
        private float currentNewScale;

        public Vector3 CurrentPosition => poolableBehavior.currentPosition;

        public bool IsVisible => poolableBehavior.isVisible;

        public Vector2 Pivot => new (0.5f, 0.5f);

        internal string title { get; private set; }

        internal Sprite iconSprite { get; private set; }

        public ClusterMarker(IObjectPool<ClusterMarkerObject> objectsPool, IMapCullingController cullingController, ICoordsUtils coordsUtils)
        {
            poolableBehavior = new MapMarkerPoolableBehavior<ClusterMarkerObject>(objectsPool);
            this.cullingController = cullingController;
            this.coordsUtils = coordsUtils;
        }

        public void Dispose()
        {
            OnBecameInvisible();
            cullingController.StopTracking(this);
        }

        public void SetCategorySprite(Sprite sprite)
        {
            iconSprite = sprite;
        }

        public void SetData(string title, Vector3 position)
        {
            poolableBehavior.SetCurrentPosition(coordsUtils.PivotPosition(this, position));
            this.title = title.Length > MAX_TITLE_LENGTH ? title.Substring(0, MAX_TITLE_LENGTH) : title;
        }

        public void OnBecameVisible()
        {
            poolableBehavior.OnBecameVisible().title.text = title;
            poolableBehavior.instance.SetCategorySprite(iconSprite);

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

        public async UniTaskVoid AnimateSelectionAsync(CancellationToken ct)
        {
            if (poolableBehavior.instance != null)
                await MarkerHelper.ScaleToAsync(poolableBehavior.instance.scalingParent, TARGET_SCALE, ANIMATION_DURATION, Ease.OutBack, ct);
        }

        public async UniTaskVoid AnimateDeSelectionAsync(CancellationToken ct)
        {
            if (poolableBehavior.instance != null)
                await MarkerHelper.ScaleToAsync(poolableBehavior.instance.scalingParent, Vector2.one, ANIMATION_DURATION, Ease.OutBack, ct, Vector3.one);
        }

        public GameObject? GetGameObject() =>
            poolableBehavior.instance != null ? poolableBehavior.instance.gameObject : null;
    }
}
