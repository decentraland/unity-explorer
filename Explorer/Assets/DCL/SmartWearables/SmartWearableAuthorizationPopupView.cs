using Cysharp.Threading.Tasks;
using MVC;
using SceneRuntime.ScenePermissions;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Runtime.Wearables
{
    public class SmartWearableAuthorizationPopupView : ViewBase, IView
    {
        [field: Header("Smart Wearable Authorization Popup")]
        [field: SerializeField]
        public Button AuthorizeButton { get; private set; }

        [field: SerializeField]
        public Button DenyButton { get; private set; }

        [field: SerializeField]
        public Image WearableRarity { get; private set; }

        [field: SerializeField]
        public Image WearableThumbnail { get; private set; }

        [field: SerializeField]
        public Image WearableThumbnailFlap { get; private set; }

        [field: SerializeField]
        public Image WearableCategoryIcon { get; private set; }

        [field: SerializeField]
        public GameObject Web3PermissionContent { get; private set; }

        [field: SerializeField]
        public GameObject OpenExternalUrlPermissionContent { get; private set; }

        [field: SerializeField]
        public GameObject WebSocketPermissionContent { get; private set; }

        [field: SerializeField]
        public GameObject FetchAPIPermissionContent { get; private set; }

        public async UniTask WaitChoiceAsync()
        {
            await UniTask.WhenAny(AuthorizeButton.OnClickAsync(), DenyButton.OnClickAsync());
        }

        public void SetPermissions(List<string> permissions)
        {
            Web3PermissionContent.SetActive(permissions.Contains(ScenePermissionNames.USE_WEB3_API));
            OpenExternalUrlPermissionContent.SetActive(permissions.Contains(ScenePermissionNames.OPEN_EXTERNAL_LINK));
            WebSocketPermissionContent.SetActive(permissions.Contains(ScenePermissionNames.USE_WEBSOCKET));
            FetchAPIPermissionContent.SetActive(permissions.Contains(ScenePermissionNames.USE_FETCH));
        }
    }
}
