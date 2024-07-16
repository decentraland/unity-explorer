using DCL.MapRenderer.MapCameraController;
using DCL.MapRenderer.MapLayers.Pins;
using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Profiling;
using UnityEngine.UI;

namespace DCL.MapRenderer.ConsumerUtils
{
    /// <summary>
    /// Extends <see cref="RawImage"/> to provide interactivity functionality
    /// </summary>
    public class MapRenderImage : RawImage, IPointerMoveHandler, IPointerExitHandler, IPointerClickHandler,
        IDragHandler, IBeginDragHandler, IEndDragHandler
    {
        public struct ParcelClickData
        {
            public Vector2Int Parcel;
            public Vector2 WorldPosition;
            public IPinMarker PinMarker;
        }

        private static readonly string DRAG_SAMPLE_NAME = string.Format("{0}.{1}", nameof(MapRenderImage), nameof(OnDrag));
        private static readonly string POINTER_MOVE_SAMPLE_NAME = string.Format("{0}.{1}", nameof(MapRenderImage), nameof(OnPointerMove));
        private static readonly string POINTER_CLICK_SAMPLE_NAME = string.Format("{0}.{1}", nameof(MapRenderImage), nameof(OnPointerClick));

        public event Action<ParcelClickData> ParcelClicked;
        public event Action<ParcelClickData> MapPinHovered;

        /// <summary>
        /// Notifies with the world position
        /// </summary>
        public event Action<Vector2> Hovered;
        public event Action<Vector2> HoveredParcel;
        public event Action<Vector2Int, IPinMarker> HoveredMapPin;

        public event Action DragStarted;

        private MapCameraDragBehavior dragBehavior;

        private bool highlightEnabled;
        private IMapInteractivityController interactivityController;
        private Camera hudCamera;

        private bool isActive;

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

        public void OnPointerMove(PointerEventData eventData)
        {
            if (!isActive)
                return;

            if (dragging)
                return;

            Profiler.BeginSample(POINTER_MOVE_SAMPLE_NAME);

            ProcessHover(eventData);

            Profiler.EndSample();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!isActive)
                return;

            if (!highlightEnabled)
                return;

            interactivityController.RemoveHighlight();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            Profiler.BeginSample(POINTER_CLICK_SAMPLE_NAME);

            //Process different click types if normal parcel or a map pin
            if (isActive && !dragging && TryGetParcelUnderPointer(eventData, out Vector2Int parcel, out _, out _, out IPinMarker pinMarker))
                ParcelClicked?.Invoke(new ParcelClickData
                {
                    Parcel = parcel,
                    WorldPosition = GetParcelWorldPosition(parcel),
                    PinMarker = pinMarker
                });

            Profiler.EndSample();
        }

        private bool dragging => dragBehavior is { dragging: true };
        private Vector2Int previousParcel;

        private Vector2 GetParcelWorldPosition(Vector2Int parcel)
        {
            Vector2 normalizedDiscretePosition = interactivityController.GetNormalizedPosition(parcel);
            return rectTransform.TransformPoint(rectTransform.rect.size * (normalizedDiscretePosition - rectTransform.pivot));
        }

        private IPinMarker previousMarker;

        private void ProcessHover(PointerEventData eventData)
        {
            if (TryGetParcelUnderPointer(eventData, out Vector2Int parcel, out _, out Vector3 worldPosition, out IPinMarker pinMarker))
            {
                if (highlightEnabled && previousParcel != parcel)
                {
                    if (previousMarker != null)
                    {
                        previousMarker.AnimateOut();
                        previousMarker = null;
                    }

                    previousParcel = parcel;
                    if (pinMarker == null)
                    {
                        interactivityController.HighlightParcel(parcel);
                    }
                    else
                    {
                        previousMarker = pinMarker;
                        pinMarker.AnimateIn();
                        interactivityController.RemoveHighlight();
                    }

                    Hovered?.Invoke(worldPosition);
                    HoveredParcel?.Invoke(parcel);

                    if(pinMarker != null)
                        HoveredMapPin?.Invoke(parcel, pinMarker);
                }
            }
            else if (highlightEnabled)
                interactivityController.RemoveHighlight();
        }

        private bool TryGetParcelUnderPointer(PointerEventData eventData, out Vector2Int parcel, out Vector2 localPosition, out Vector3 worldPosition, out IPinMarker pinMarker)
        {
            pinMarker = null;
            Vector2 screenPoint = eventData.position;

            if (RectTransformUtility.ScreenPointToWorldPointInRectangle(rectTransform, screenPoint, hudCamera, out worldPosition))
            {
                Vector2 rectSize = rectTransform.rect.size;
                localPosition = (Vector2)rectTransform.InverseTransformPoint(worldPosition);
                Vector2 leftCornerRelativeLocalPosition = localPosition + (rectTransform.pivot * rectSize);
                return interactivityController.TryGetParcel(leftCornerRelativeLocalPosition / rectSize, out parcel, out pinMarker);
            }

            parcel = Vector2Int.zero;
            localPosition = Vector2.zero;
            return false;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!isActive) return;

            Profiler.BeginSample(DRAG_SAMPLE_NAME);

            dragBehavior?.OnDrag(eventData);

            Profiler.EndSample();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!isActive) return;

            DragStarted?.Invoke();
            dragBehavior?.OnBeginDrag(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!isActive) return;

            dragBehavior?.OnEndDrag(eventData);

            ProcessHover(eventData);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            dragBehavior?.Dispose();
        }
    }
}
