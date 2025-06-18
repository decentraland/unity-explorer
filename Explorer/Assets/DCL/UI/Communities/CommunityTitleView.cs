using Cysharp.Threading.Tasks;
using DCL.Profiles;
using DCL.UI.GenericContextMenu;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Utility;
using DCL.UI.GenericContextMenu.Controls.Configs;
using System;

namespace DCL.UI.Communities
{
    /// <summary>
    /// A small UI panel that shows basic info about a community, like its thumbnail and its name.
    /// </summary>
    public class CommunityTitleView : MonoBehaviour
    {
        public delegate void OpenContextMenuDelegate(GenericContextMenuParameter parameter, Action onClosed, CancellationToken ct);
        public delegate void ContextMenuOpenedDelegate();
        public delegate void ContextMenuClosedDelegate();
        public delegate void ViewCommunityRequestedDelegate();

        public ContextMenuOpenedDelegate ContextMenuOpened;
        public ContextMenuClosedDelegate ContextMenuClosed;
        public ViewCommunityRequestedDelegate ViewCommunityRequested;

        [SerializeField]
        private CommunityThumbnailView thumbnailView;

        [SerializeField]
        private Button openTitleButton;

        [SerializeField]
        private TMP_Text userNameElement;

        [Header("Context menu")]
        [SerializeField]
        private CommunityChatConversationContextMenuSettings contextMenuSettings;

        private OpenContextMenuDelegate openContextMenu;
        private CancellationTokenSource cts;
        private UniTaskCompletionSource contextMenuTask = new ();
        private GenericContextMenu.Controls.Configs.GenericContextMenu contextMenuConfig;

        public async UniTaskVoid SetupAsync(IThumbnailCache thumbnailCache, string communityId, string communityName, string thumbnailUrl, OpenContextMenuDelegate openContextMenuAction, CancellationToken ct)
        {
            contextMenuConfig = new GenericContextMenu.Controls.Configs.GenericContextMenu(contextMenuSettings.Width, contextMenuSettings.Offset, contextMenuSettings.VerticalLayoutPadding, contextMenuSettings.ElementsSpacing, ContextMenuOpenDirection.TOP_LEFT)
                                        .AddControl(new ButtonContextMenuControlSettings(contextMenuSettings.ViewCommunityText, contextMenuSettings.ViewCommunitySprite, () => ViewCommunityRequested?.Invoke()));

            openContextMenu = openContextMenuAction;
            userNameElement.text = communityName;
            thumbnailView.SetDefaultThumbnail();

            if (thumbnailUrl != null)
                await thumbnailView.LoadThumbnailAsync(thumbnailCache, thumbnailUrl, communityId, ct);
        }

        private void Awake()
        {
            openTitleButton.onClick.AddListener(OnTitleClicked);
        }

        private void OnTitleClicked()
        {
            contextMenuTask.TrySetResult();
            contextMenuTask = new UniTaskCompletionSource();
            cts = cts.SafeRestart();
            ContextMenuOpened?.Invoke();
            openTitleButton.OnSelect(null);
            openContextMenu(new GenericContextMenuParameter(contextMenuConfig, openTitleButton.transform.position), OnContextMenuClosed, cts.Token);
        }

        private void OnContextMenuClosed()
        {
            ContextMenuClosed?.Invoke();
            openTitleButton.OnDeselect(null);
        }

        private void OnDisable()
        {
            contextMenuTask?.TrySetResult();
        }
    }
}
