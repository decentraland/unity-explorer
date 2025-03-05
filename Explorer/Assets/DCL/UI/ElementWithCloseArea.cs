using Cysharp.Threading.Tasks;
using DCL.UI.SharedSpaceManager;
using MVC;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI
{
    public class ElementWithCloseArea : ViewBaseWithAnimationElement, IView, IPanelInSharedSpace
    {
        [field: SerializeField] internal Button closeAreaButton { get; private set; }

        public event Action Closed;
        public event IPanelInSharedSpace.ViewShowingCompleteDelegate? ViewShowingComplete;

        public bool IsVisibleInSharedSpace => gameObject.activeSelf;

        public async UniTask OnShownInSharedSpaceAsync(CancellationToken ct, object parameters = null)
        {
            gameObject.SetActive(true);
            await PlayShowAnimationAsync(ct);
            ViewShowingComplete?.Invoke(this);
        }

        public async UniTask OnHiddenInSharedSpaceAsync(CancellationToken ct)
        {
            await HideAsync(ct);
        }

        private void Awake()
        {
            closeAreaButton.onClick.AddListener(OnCloseAreaButtonClicked);
        }

        private void OnCloseAreaButtonClicked()
        {
            Closed?.Invoke();
        }
    }
}
