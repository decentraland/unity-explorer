using Cysharp.Threading.Tasks;
using DCL.Audio;
using DCL.MapRenderer.MapCameraController;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using Utility;

namespace DCL.Navmap
{
    public class NavmapZoomController : IDisposable
    {
        private const float MOUSE_WHEEL_THRESHOLD = 0.04f;

        private readonly NavmapZoomView view;
        private readonly DCLInput dclInput;
        private readonly INavmapBus navmapBus;

        private AnimationCurve normalizedCurve;
        private int zoomSteps;

        private bool active;

        private CancellationTokenSource cts;
        private bool isScaling;

        private float targetNormalizedZoom;
        private int currentZoomLevel;

        private IMapCameraController cameraController;
        private bool blockZoom;

        public NavmapZoomController(
            NavmapZoomView view,
            DCLInput dclInput,
            INavmapBus navmapBus)
        {
            this.view = view;
            this.dclInput = dclInput;
            this.navmapBus = navmapBus;

            normalizedCurve = view.normalizedZoomCurve;
            zoomSteps = normalizedCurve.length;

            this.navmapBus.OnZoomCamera += Zoom;

            CurveClamp01();
        }

        private void MouseWheel(InputAction.CallbackContext obj)
        {
            if (obj.ReadValue<Vector2>().y == 0 || Mathf.Abs(obj.ReadValue<Vector2>().y) < MOUSE_WHEEL_THRESHOLD)
                return;

            bool zoomAction = obj.ReadValue<Vector2>().y > 0;
            Zoom(zoomAction);
        }

        public void Dispose()
        {
            cts.SafeCancelAndDispose();
        }

        public void SetBlockZoom(bool value) =>
            blockZoom = value;

        private void CurveClamp01()
        {
            // Keys should be int for zoomSteps to work properly
            for (var i = 0; i < normalizedCurve.keys.Length; i++)
            {
                Keyframe keyFrame = normalizedCurve.keys[i];

                if (i == 0)
                {
                    keyFrame.time = 0;
                    keyFrame.value = 0;
                }
                else if (i == normalizedCurve.length - 1)
                {
                    keyFrame.time = Mathf.RoundToInt(keyFrame.time);
                    keyFrame.value = 1;
                }
                else
                {
                    keyFrame.time = Mathf.RoundToInt(keyFrame.time);
                    keyFrame.value = keyFrame.value;
                }

                normalizedCurve.MoveKey(i, keyFrame);
            }
        }

        public float ResetZoomToMidValue()
        {
            SetZoomLevel(Mathf.FloorToInt((zoomSteps - 1) / 2f));
            return targetNormalizedZoom;
        }

        public void Activate(IMapCameraController mapCameraController)
        {
            if (active)
            {
                if (cameraController == mapCameraController)
                    return;

                Deactivate();
            }
            dclInput.UI.ScrollWheel.performed += MouseWheel;

            cts = new CancellationTokenSource();

            cameraController = mapCameraController;

            //view.MouseWheelAction.OnValueChanged += OnMouseWheelValueChanged;
            view.ZoomIn.Button.onClick.AddListener(() => Zoom(true));
            view.ZoomOut.Button.onClick.AddListener(() => Zoom(false));

            active = true;
        }

        public void Deactivate()
        {
            if (!active) return;

            dclInput.UI.ScrollWheel.performed -= MouseWheel;
            cts.Cancel();
            cts.Dispose();
            cts = null;

            //view.MouseWheelAction.OnValueChanged -= OnMouseWheelValueChanged;
            view.ZoomIn.Button.onClick.RemoveAllListeners();
            view.ZoomOut.Button.onClick.RemoveAllListeners();

            active = false;
        }

        private void OnMouseWheelValueChanged(bool action, float value)
        {
            if (value == 0 || Mathf.Abs(value) < MOUSE_WHEEL_THRESHOLD)
                return;

            bool zoomAction = value > 0;
            Zoom(zoomAction);
        }

        private void SetZoomLevel(int zoomLevel)
        {
            currentZoomLevel = Mathf.Clamp(zoomLevel, 0, zoomSteps - 1);
            targetNormalizedZoom = normalizedCurve.Evaluate(currentZoomLevel);

            SetUiButtonsInteractivity();
        }

        private void Zoom(bool zoomIn)
        {
            if (!active || isScaling || blockZoom)
                return;

            EventSystem.current.SetSelectedGameObject(null);

            if ((zoomIn && Mathf.Approximately(targetNormalizedZoom, 1f)) || (!zoomIn && Mathf.Approximately(targetNormalizedZoom, 0f)))
                return;

            UIAudioEventsBus.Instance.SendPlayAudioEvent(zoomIn? view.ZoomInAudio : view.ZoomOutAudio);
            int zoomLevel = currentZoomLevel + (zoomIn ? 1 : -1);
            SetZoomLevel(zoomLevel);
            ScaleOverTimeAsync(cameraController.Zoom, targetNormalizedZoom, zoomLevel, cts.Token).Forget();

            SetUiButtonsInteractivity();
        }

        private void SetUiButtonsInteractivity()
        {
            view.ZoomIn.SetUiInteractable(isInteractable: currentZoomLevel < zoomSteps - 1);
            view.ZoomOut.SetUiInteractable(isInteractable: currentZoomLevel > 0);
        }

        private async UniTaskVoid ScaleOverTimeAsync(float from, float to, int zoomLevel, CancellationToken ct)
        {
            isScaling = true;
            float scaleDuration = view.scaleDuration;

            for (float timer = 0; timer < scaleDuration; timer += Time.deltaTime)
            {
                if (ct.IsCancellationRequested)
                    break;

                cameraController.SetZoom(Mathf.Lerp(from, to, timer / scaleDuration), zoomLevel);

                // omit CT, handle cancellation gracefully
                await UniTask.NextFrame();
            }

            isScaling = false;
        }
    }
}
