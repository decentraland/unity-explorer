using Cysharp.Threading.Tasks;
using MVC;
using SceneRuntime.ScenePermissions;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI.PortableExperiences
{
    public class PortableExperienceAuthorizationPopupView : ViewBase, IView
    {
        [field: Header("Portable Experience Authorization Popup")]
        [field: SerializeField]
        [field: TextArea]
        public string PromptFormat { get; private set; }

        [field: SerializeField]
        public TMP_Text PromptText { get; private set; }

        [field: SerializeField]
        public Button AuthorizeButton { get; private set; }

        [field: SerializeField]
        public Button DenyButton { get; private set; }

        [field: SerializeField]
        public GameObject Web3PermissionContent { get; private set; }

        [field: SerializeField]
        public GameObject OpenExternalUrlPermissionContent { get; private set; }

        [field: SerializeField]
        public GameObject WebSocketPermissionContent { get; private set; }

        [field: SerializeField]
        public GameObject FetchAPIPermissionContent { get; private set; }

        [field: SerializeField]
        public GameObject SpawnPortableExperiencePermissionContent { get; private set; }

        public async UniTask WaitChoiceAsync()
        {
            await UniTask.WhenAny(AuthorizeButton.OnClickAsync(), DenyButton.OnClickAsync());
        }

        public void Setup(string portableExperienceName, IReadOnlyList<string> permissions)
        {
            PromptText.text = string.Format(PromptFormat, portableExperienceName);

            Web3PermissionContent.SetActive(Contains(permissions, ScenePermissionNames.USE_WEB3_API));
            OpenExternalUrlPermissionContent.SetActive(Contains(permissions, ScenePermissionNames.OPEN_EXTERNAL_LINK));
            WebSocketPermissionContent.SetActive(Contains(permissions, ScenePermissionNames.USE_WEBSOCKET));
            FetchAPIPermissionContent.SetActive(Contains(permissions, ScenePermissionNames.USE_FETCH));
            SpawnPortableExperiencePermissionContent.SetActive(Contains(permissions, ScenePermissionNames.PORTABLE_EXPERIENCE));
        }

        private static bool Contains(IReadOnlyList<string> permissions, string permission)
        {
            for (var i = 0; i < permissions.Count; i++)
            {
                if (permissions[i] == permission)
                    return true;
            }

            return false;
        }
    }
}
