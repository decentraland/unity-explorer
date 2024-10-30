using DCL.UI;
using System;
using UnityEngine;

namespace DCL.InWorldCamera.CameraReel
{
    public class CameraReelController : ISection, IDisposable
    {
        private readonly CameraReelView view;
        private readonly RectTransform rectTransform;

        public CameraReelController(CameraReelView view)
        {
            this.view = view;
            rectTransform = view.transform.parent.GetComponent<RectTransform>();
        }

        public void Activate() =>
            view.gameObject.SetActive(true);

        public void Deactivate()=>
            view.gameObject.SetActive(false);

        public void Animate(int triggerId)
        {

        }

        public void ResetAnimator()
        {

        }

        public RectTransform GetRectTransform() =>
            rectTransform;

        public void Dispose()
        {

        }
    }
}
