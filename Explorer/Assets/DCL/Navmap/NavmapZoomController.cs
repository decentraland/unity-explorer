using Cysharp.Threading.Tasks;
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

        private AnimationCurve normalizedCurve;
        private int zoomSteps;

        private bool active;

        private CancellationTokenSource cts;
        private bool isScaling;

        private float targetNormalizedZoom;
        private int currentZoomLevel;

        private IMapCameraController cameraController;

        public NavmapZoomController(NavmapZoomView view, DCLInput dclInput)
        {
            this.view = view;
            this.dclInput = dclInput;
            Initialize();

            normalizedCurve = view.normalizedZoomCurve;
            zoomSteps = normalizedCurve.length;

            CurveClamp01();
        }

        private void MouseWheel(InputAction.CallbackContext obj)
        {
            if (obj.ReadValue<Vector2>().y == 0 || Mathf.Abs(obj.ReadValue<Vector2>().y) < MOUSE_WHEEL_THRESHOLD)
                return;

            bool zoomAction = obj.ReadValue<Vector2>().y > 0;
            Zoom(zoomAction);
        }

        private void Initialize()
        {
            view.zoomVerticalRange = new Vector2Int(view.zoomVerticalRange.x, 40);

            normalizedCurve = new AnimationCurve();
            normalizedCurve.AddKey(0, 0);
            normalizedCurve.AddKey(1, 0.25f);
            normalizedCurve.AddKey(2, 0.5f);
            normalizedCurve.AddKey(3, 0.75f);
            normalizedCurve.AddKey(4, 1);
            zoomSteps = normalizedCurve.length;
        }

        public void Dispose()
        {
            cts.SafeCancelAndDispose();
        }

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
            if (!active || isScaling)
                return;

            EventSystem.current.SetSelectedGameObject(null);

            if ((zoomIn && Mathf.Approximately(targetNormalizedZoom, 1f)) || (!zoomIn && Mathf.Approximately(targetNormalizedZoom, 0f)))
                return;

            SetZoomLevel(currentZoomLevel + (zoomIn ? 1 : -1));
            ScaleOverTimeAsync(cameraController.Zoom, targetNormalizedZoom, cts.Token).Forget();

            SetUiButtonsInteractivity();
        }

        private void SetUiButtonsInteractivity()
        {
            view.ZoomIn.SetUiInteractable(isInteractable: currentZoomLevel < zoomSteps - 1);
            view.ZoomOut.SetUiInteractable(isInteractable: currentZoomLevel > 0);
        }

        private async UniTaskVoid ScaleOverTimeAsync(float from, float to, CancellationToken ct)
        {
            isScaling = true;
            float scaleDuration = view.scaleDuration;

            for (float timer = 0; timer < scaleDuration; timer += Time.deltaTime)
            {
                if (ct.IsCancellationRequested)
                    break;

                cameraController.SetZoom(Mathf.Lerp(from, to, timer / scaleDuration));

                // omit CT, handle cancellation gracefully
                await UniTask.NextFrame();
            }

            isScaling = false;
        }
    }
}
