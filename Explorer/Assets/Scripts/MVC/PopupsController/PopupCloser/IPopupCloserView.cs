using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine.UI;

namespace MVC.PopupsController.PopupCloser
{
    public interface IPopupCloserView : IView
    {
        public Button CloseButton { get; }
    }
}
