using DCL.UI;
using System;
using UnityEngine;

namespace DCL.InWorldCamera.CameraReel
{
    public class CameraReelController : ISection, IDisposable
    {
        private readonly CameraReelView view;

        public CameraReelController(CameraReelView view)
        {
            this.view = view;
        }

        public void Activate() =>
            view.gameObject.SetActive(true);

        public void Deactivate()=>
            view.gameObject.SetActive(false);

        public void Animate(int triggerId)
        {
            throw new NotImplementedException();
        }

        public void ResetAnimator()
        {
            throw new NotImplementedException();
        }

        public RectTransform GetRectTransform() =>
            throw new NotImplementedException();

        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}
