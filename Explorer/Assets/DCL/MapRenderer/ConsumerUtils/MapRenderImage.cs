﻿using DCL.MapRenderer.MapCameraController;
using DCL.MapRenderer.MapLayers.Pins;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Profiling;
using UnityEngine.UI;
using Utility;

namespace DCL.MapRenderer.ConsumerUtils
{
    /// <summary>
    ///     Extends <see cref="RawImage" /> to provide interactivity functionality
    /// </summary>
    public class MapRenderImage : RawImage, IPointerMoveHandler, IPointerExitHandler, IPointerClickHandler,
        IDragHandler, IBeginDragHandler, IEndDragHandler
    {
        public event Action<ParcelClickData>? ParcelClicked;

        private static readonly string DRAG_SAMPLE_NAME = string.Format("{0}.{1}", nameof(MapRenderImage), nameof(OnDrag));
        private static readonly string POINTER_MOVE_SAMPLE_NAME = string.Format("{0}.{1}", nameof(MapRenderImage), nameof(OnPointerMove));
        private static readonly string POINTER_CLICK_SAMPLE_NAME = string.Format("{0}.{1}", nameof(MapRenderImage), nameof(OnPointerClick));

        private MapCameraDragBehavior? dragBehavior;
        private bool highlightEnabled;
        private Camera? hudCamera;
        private IMapInteractivityController? interactivityController;
        private bool isActive;
        private Vector2Int previousParcel;
        private CancellationTokenSource cts = new ();

        private bool dragging => dragBehavior is { dragging: true };

        protected override void OnDestroy()
        {
            base.OnDestroy();

            dragBehavior?.Dispose();
            cts.SafeCancelAndDispose();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!isActive) return;

            DragStarted?.Invoke();
            dragBehavior?.OnBeginDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!isActive) return;

            Profiler.BeginSample(DRAG_SAMPLE_NAME);

            dragBehavior?.OnDrag(eventData);

            Profiler.EndSample();
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!isActive) return;

            dragBehavior?.OnEndDrag(eventData);

            ProcessHover(eventData);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            Profiler.BeginSample(POINTER_CLICK_SAMPLE_NAME);
            Vector2Int? hitParcel = null;
            bool parcelUnderPoint = TryGetParcelUnderPointer(eventData, out Vector2Int parcel, out _, out _);

            if (isActive && !dragging)
            {
                if (RectTransformUtility.ScreenPointToWorldPointInRectangle(rectTransform, eventData.position, hudCamera, out Vector3 worldPosition))
                {
                    Vector2 rectSize = rectTransform.rect.size;
                    Vector2 localPosition = rectTransform.InverseTransformPoint(worldPosition);
                    Vector2 leftCornerRelativeLocalPosition = localPosition + (rectTransform.pivot * rectSize);
                    hitParcel = interactivityController!.ProcessMouseClick(leftCornerRelativeLocalPosition / rectSize, parcel);
                }

                if (hitParcel != null && parcelUnderPoint)
                    InvokeParcelClicked(hitParcel.Value);
            }

            Profiler.EndSample();
        }

        public void OnSearchResultParcelSelected(Vector2Int parcel)
        {
            InvokeParcelClicked(parcel);
        }

        private void InvokeParcelClicked(Vector2Int parcel)
        {
            ParcelClicked?.Invoke(new ParcelClickData
            {
                Parcel = parcel,
                WorldPosition = GetParcelWorldPosition(parcel)
            });
        }


        public void OnPointerExit(PointerEventData eventData)
        {
            if (!isActive)
                return;

            if (!highlightEnabled)
                return;

            interactivityController!.ExitRenderImage();
        }

        public void OnPointerMove(PointerEventData eventData)
        {
            if (!isActive)
                return;

            if (dragging)
                return;

            GameObject? hitObject = null;

            Profiler.BeginSample(POINTER_MOVE_SAMPLE_NAME);

            if (RectTransformUtility.ScreenPointToWorldPointInRectangle(rectTransform, eventData.position, hudCamera, out Vector3 worldPosition))
            {
                Vector2 rectSize = rectTransform.rect.size;
                Vector2 localPosition = rectTransform.InverseTransformPoint(worldPosition);
                Vector2 leftCornerRelativeLocalPosition = localPosition + (rectTransform.pivot * rectSize);
                hitObject = interactivityController!.ProcessMousePosition(leftCornerRelativeLocalPosition / rectSize, leftCornerRelativeLocalPosition);
            }

            ProcessHover(eventData, hitObject);

            Profiler.EndSample();
        }

        public event Action<Vector2>? HoveredParcel;
        public event Action? DragStarted;

        public void EmbedMapCameraDragBehavior(MapCameraDragBehavior.MapCameraDragBehaviorData data)
        {
            dragBehavior = new MapCameraDragBehavior(rectTransform, data);
        }

        public void Activate(Camera hudCamera, RenderTexture renderTexture, IMapCameraController mapCameraController)
        {
            interactivityController = mapCameraController.GetInteractivityController();
            highlightEnabled = interactivityController.HighlightEnabled;
            this.hudCamera = hudCamera;

            texture = renderTexture;

            dragBehavior?.Activate(mapCameraController);

            isActive = true;
        }

        public void Deactivate()
        {
            dragBehavior?.Deactivate();

            hudCamera = null;
            interactivityController = null;
            texture = null;

            isActive = false;
        }

        private Vector2 GetParcelWorldPosition(Vector2Int parcel)
        {
            Vector2 normalizedDiscretePosition = interactivityController!.GetNormalizedPosition(parcel);
            return rectTransform.TransformPoint(rectTransform.rect.size * (normalizedDiscretePosition - rectTransform.pivot));
        }

        private void ProcessHover(PointerEventData eventData, GameObject? hitObject = null)
        {
            if (TryGetParcelUnderPointer(eventData, out Vector2Int parcel, out _, out Vector3 worldPosition))
            {
                if (highlightEnabled && previousParcel != parcel)
                {
                    previousParcel = parcel;

                    if (hitObject == null)
                        interactivityController!.HighlightParcel(parcel);
                    else
                        interactivityController!.RemoveHighlight();

                    HoveredParcel?.Invoke(parcel);
                }
            }
            else if (highlightEnabled)
                interactivityController!.RemoveHighlight();
        }

        private bool TryGetParcelUnderPointer(PointerEventData eventData, out Vector2Int parcel, out Vector2 localPosition, out Vector3 worldPosition)
        {
            Vector2 screenPoint = eventData.position;

            if (RectTransformUtility.ScreenPointToWorldPointInRectangle(rectTransform, screenPoint, hudCamera, out worldPosition))
            {
                Vector2 rectSize = rectTransform.rect.size;
                localPosition = rectTransform.InverseTransformPoint(worldPosition);
                Vector2 leftCornerRelativeLocalPosition = localPosition + (rectTransform.pivot * rectSize);
                return interactivityController!.TryGetParcel(leftCornerRelativeLocalPosition / rectSize, out parcel);
            }

            parcel = Vector2Int.zero;
            localPosition = Vector2.zero;
            return false;
        }

        public struct ParcelClickData
        {
            public Vector2Int Parcel;
            public Vector2 WorldPosition;
            public IPinMarker? PinMarker;
        }
    }
}
