using Cysharp.Threading.Tasks;
using MVC;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI
{
    public class ElementWithCloseArea : ViewBaseWithAnimationElement
    {
        [field: SerializeField] internal Button closeAreaButton { get; private set; }

        private void Awake()
        {
            closeAreaButton.onClick.AddListener(CloseElement);
        }

        public void CloseElement()
        {
            if (gameObject.activeSelf) { HideAsync(CancellationToken.None).Forget(); }
        }
    }
}
