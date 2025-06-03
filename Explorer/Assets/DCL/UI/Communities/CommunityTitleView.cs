using Cysharp.Threading.Tasks;
using DCL.Profiles;
using System;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI.Communities
{
    public class CommunityTitleView : MonoBehaviour
    {
        private static readonly Vector2 CONTEXT_MENU_OFFSET = new (0, -20);

        public Action ContextMenuOpened;
        public Action ContextMenuClosed;

        [SerializeField] private CommunityThumbnailView thumbnailView;
        [SerializeField] private Button openProfileButton;
        [SerializeField] private TMP_Text userNameElement;

        private CancellationTokenSource cts;
        private UniTaskCompletionSource contextMenuTask = new ();

        public async UniTaskVoid SetupAsync(IThumbnailCache thumbnailCache, string communityId, string communityName, string thumbnailUrl, CancellationToken ct)
        {
            userNameElement.text = communityName;
            await thumbnailView.LoadThumbnailAsync(thumbnailCache, thumbnailUrl, communityId, ct);
        }

        private void Awake()
        {
            openProfileButton.onClick.AddListener(OnOpenProfileClicked);
        }

        private void OnOpenProfileClicked()
        {
            ContextMenuOpened?.Invoke();
        }

        private void OnProfileContextMenuClosed()
        {
            ContextMenuClosed?.Invoke();
            openProfileButton.OnDeselect(null);
        }

        private void OnDisable()
        {
            contextMenuTask?.TrySetResult();
        }
    }
}
