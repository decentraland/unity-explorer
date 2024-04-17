using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine.UI;

namespace MVC.PopupsController.PopupCloser
{
    public interface IPopupCloserView : IView
    {
        public Button CloseButton { get; }

        public class Fake : IPopupCloserView
        {
            public void SetDrawOrder(CanvasOrdering order)
            {
                throw new Exception("I'm fake!");
            }

            public UniTask ShowAsync(CancellationToken ct) =>
                throw new Exception("I'm fake!");

            public UniTask HideAsync(CancellationToken ct, bool isInstant = false) =>
                throw new Exception("I'm fake!");

            public Button CloseButton => throw new Exception("I'm fake!");
        }
    }
}
