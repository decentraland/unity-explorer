using DCL.InWorldCamera.CameraReelStorageService.Schemas;
using System;
using UnityEngine;

namespace DCL.InWorldCamera.CameraReelGallery.Components
{
    public class ContextMenuController : IDisposable
    {
        private enum ContextMenuOpenDirection
        {
            BOTTOM_RIGHT,
            BOTTOM_LEFT,
            TOP_LEFT,
            TOP_RIGHT
        }

        private readonly ContextMenuView view;
        private readonly RectTransform controlsRectTransform;
        private readonly Rect backgroundButtonRect;

        public event Action<CameraReelResponseCompact, bool> SetPublicRequested;
        public event Action<CameraReelResponseCompact> ShareToXRequested;
        public event Action<CameraReelResponseCompact> CopyPictureLinkRequested;
        public event Action<CameraReelResponseCompact> DownloadRequested;
        public event Action<CameraReelResponseCompact> DeletePictureRequested;

        public event Action AnyControlClicked;

        private CameraReelResponseCompact imageData;

        public ContextMenuController(ContextMenuView view)
        {
            this.view = view;

            this.view.backgroundCloseButton.onClick.AddListener(Hide);
            this.view.backgroundCloseButton.onClick.AddListener(() => AnyControlClicked?.Invoke());

            this.controlsRectTransform = this.view.controlsParent.GetComponent<RectTransform>();
            this.backgroundButtonRect = this.view.backgroundCloseButton.GetComponent<RectTransform>().GetWorldRect();

            view.shareOnX.onClick.AddListener(() =>
            {
                ShareToXRequested?.Invoke(imageData);
                AnyControlClicked?.Invoke();
            });

            view.copyLink.onClick.AddListener(() =>
            {
                CopyPictureLinkRequested?.Invoke(imageData);
                AnyControlClicked?.Invoke();
            });

            view.download.onClick.AddListener(() =>
            {
                DownloadRequested?.Invoke(imageData);
                AnyControlClicked?.Invoke();
            });

            view.delete.onClick.AddListener(() =>
            {
                DeletePictureRequested?.Invoke(imageData);
                AnyControlClicked?.Invoke();
            });
        }

        private void SetAsPublicInvoke(bool toggle) =>
            SetPublicRequested?.Invoke(imageData, toggle);

        public void Show(Vector3 anchorPosition)
        {
            view.gameObject.SetActive(true);

            //Align the "public" toggle status according to the imageData without triggering an "invoke"
            view.setAsPublic.onValueChanged.RemoveListener(SetAsPublicInvoke);
            view.setAsPublic.isOn = imageData.isPublic;
            view.setAsPublic.onValueChanged.AddListener(SetAsPublicInvoke);

            view.controlsParent.transform.position = GetControlsPosition(anchorPosition);
        }

        private Vector3 GetOffsetByDirection(ContextMenuOpenDirection direction)
        {
            return direction switch
            {
                ContextMenuOpenDirection.BOTTOM_RIGHT => view.offsetFromTarget,
                ContextMenuOpenDirection.BOTTOM_LEFT => new Vector3(-view.offsetFromTarget.x - controlsRectTransform.rect.width, view.offsetFromTarget.y, view.offsetFromTarget.z),
                ContextMenuOpenDirection.TOP_RIGHT => new Vector3(view.offsetFromTarget.x, -view.offsetFromTarget.y + controlsRectTransform.rect.height, view.offsetFromTarget.z),
                ContextMenuOpenDirection.TOP_LEFT => new Vector3(-view.offsetFromTarget.x - controlsRectTransform.rect.width, -view.offsetFromTarget.y + controlsRectTransform.rect.height, view.offsetFromTarget.z),
                _ => Vector3.zero
            };
        }

        private Vector3 GetControlsPosition(Vector3 anchorPosition)
        {
            Vector3 position = anchorPosition;
            position.x += controlsRectTransform.rect.width / 2;
            position.y -= controlsRectTransform.rect.height / 2;

            Vector3 newPosition = Vector3.zero;
            float minNonOverlappingArea = float.MaxValue;
            foreach (ContextMenuOpenDirection enumVal in Enum.GetValues(typeof(ContextMenuOpenDirection)))
            {
                Vector3 currentPosition = position + GetOffsetByDirection(enumVal);
                float nonOverlappingArea = CalculateNonOverlappingArea(backgroundButtonRect, GetProjectedRect(currentPosition));
                if (nonOverlappingArea < minNonOverlappingArea)
                {
                    newPosition = currentPosition;
                    minNonOverlappingArea = nonOverlappingArea;
                }
            }

            return newPosition;
        }

        private float CalculateNonOverlappingArea(Rect rect1, Rect rect2)
        {
            float area1 = rect1.width * rect1.height;
            float area2 = rect2.width * rect2.height;

            Rect intersection = Rect.MinMaxRect(
                Mathf.Max(rect1.xMin, rect2.xMin),
                Mathf.Max(rect1.yMin, rect2.yMin),
                Mathf.Min(rect1.xMax, rect2.xMax),
                Mathf.Min(rect1.yMax, rect2.yMax)
            );

            float intersectionArea = 0;

            if (intersection is { width: > 0, height: > 0 })
                intersectionArea = intersection.width * intersection.height;

            return area1 + area2 - intersectionArea;
        }

        private Rect GetProjectedRect(Vector3 newPosition)
        {
            Vector3 originalPosition = view.controlsParent.transform.position;
            view.controlsParent.transform.position = newPosition;
            Rect rect = controlsRectTransform.GetWorldRect();
            view.controlsParent.transform.position = originalPosition;

            return rect;
        }

        public void Hide() =>
            view.gameObject.SetActive(false);

        public bool IsOpen() =>
            view.gameObject.activeSelf;

        public void SetImageData(CameraReelResponseCompact cameraReelResponse) =>
            imageData = cameraReelResponse;

        public void Dispose()
        {
            view.backgroundCloseButton.onClick.RemoveAllListeners();
            view.setAsPublic.onValueChanged.RemoveAllListeners();
            view.shareOnX.onClick.RemoveAllListeners();
            view.copyLink.onClick.RemoveAllListeners();
            view.download.onClick.RemoveAllListeners();
            view.delete.onClick.RemoveAllListeners();
            SetPublicRequested = null;
            ShareToXRequested = null;
            CopyPictureLinkRequested = null;
            DownloadRequested = null;
            DeletePictureRequested = null;
        }
    }
}
