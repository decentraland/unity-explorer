using DCL.Lobby;
using MVC;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace DCL.PluginSystem.Global
{
    /// <summary>
    ///     Dev tool: F8 (or the debug panel button) swaps the lobby on screen for the other implementation, shown with the same
    ///     parameter, so both can be compared in place. Works at startup too: the flow keeps waiting for the Jump in of whichever lobby ends up visible.
    ///     The uGUI lobby gets a red tint over its background so the two are told apart at a glance.
    /// </summary>
    public class LobbyDevSwitcher : MonoBehaviour
    {
        private IMVCManager mvcManager = null!;
        private LobbyController ugui = null!;
        private LobbyDocumentController uitk = null!;

        public void Initialize(IMVCManager mvcManager, LobbyController ugui, LobbyDocumentController uitk, LobbyView uguiView)
        {
            this.mvcManager = mvcManager;
            this.ugui = ugui;
            this.uitk = uitk;

            TintBackground(uguiView);
        }

        // Neither view animates, so leaving one and showing the other in the same call swaps them within the frame
        public void Switch()
        {
            if (IsOnScreen(ugui))
                mvcManager.ShowAndForget(LobbyDocumentController.IssueCommand(ugui.DevLeaveForSwitch()));
            else if (IsOnScreen(uitk))
                mvcManager.ShowAndForget(LobbyController.IssueCommand(uitk.DevLeaveForSwitch()));
        }

        private static bool IsOnScreen(IController controller) =>
            controller.State is ControllerState.ViewFocused or ControllerState.ViewBlurred;

        // A translucent red image right above the fullscreen avatar preview: it covers the stage and background, not the cards and top bar over them
        private static void TintBackground(LobbyView view)
        {
            Transform root = view.transform;
            Transform previewBranch = view.CharacterPreviewView.transform;

            while (previewBranch.parent != null && previewBranch.parent != root)
                previewBranch = previewBranch.parent;

            var overlay = new GameObject("DevRedTint", typeof(RectTransform), typeof(Image));
            var rect = overlay.GetComponent<RectTransform>();
            rect.SetParent(root, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.SetSiblingIndex(previewBranch == root ? 0 : previewBranch.GetSiblingIndex() + 1);

            var image = overlay.GetComponent<Image>();
            image.color = new Color(1f, 0f, 0f, 0.3f);
            image.raycastTarget = false;
        }

        private void Update()
        {
            if (Keyboard.current?.f8Key.wasPressedThisFrame == true)
                Switch();
        }
    }
}
