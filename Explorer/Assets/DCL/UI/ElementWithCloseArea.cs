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
        public event Action Closed;

        [field: SerializeField] internal Button closeAreaButton { get; private set; }

        private void Awake()
        {
            closeAreaButton.onClick.AddListener(OnCloseAreaButtonClicked);
        }

        private void OnCloseAreaButtonClicked()
        {
            Closed?.Invoke();
        }

        public event IPanelInSharedSpace.ViewShowingCompleteDelegate? ViewShowingComplete;
        public bool IsVisibleInSharedSpace => gameObject.activeSelf;

        public async UniTask ShowInSharedSpaceAsync(CancellationToken ct, object parameters = null)
        {
            gameObject.SetActive(true);
            await PlayShowAnimationAsync(ct);
            ViewShowingComplete?.Invoke(this);
            await UniTask.CompletedTask;
        }

        public async UniTask HideInSharedSpaceAsync(CancellationToken ct)
        {
            await HideAsync(ct);
        }
    }
}
