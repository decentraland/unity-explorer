using MVC;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using Utility;

namespace DCL.UI
{
    public class ElementWithCloseArea : ViewBaseWithAnimationElement, IView
    {
        [field: SerializeField] internal Button closeAreaButton { get; private set; } = null!;

        public event Action? Closed;

        private CancellationTokenSource showingCts = new ();

        private void Awake()
        {
            closeAreaButton.onClick.AddListener(OnCloseAreaButtonClicked);
        }

        private void OnCloseAreaButtonClicked()
        {
            showingCts = showingCts.SafeRestart();
            Closed?.Invoke();
        }
    }
}
