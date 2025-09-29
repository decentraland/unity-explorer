using Cysharp.Threading.Tasks;
using DCL.Chat.ControllerShowParams;
using DCL.UI.SharedSpaceManager;
using MVC;
using System.Threading;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Pool;

namespace DCL.ChatArea
{
    public class ChatMainController : ControllerBase<ChatMainView, ChatControllerShowParams>,
                                  IControllerInSharedSpace<ChatMainView, ChatControllerShowParams>
    {
        private readonly IMVCManager mvcManager;
        private readonly ChatAreaEventBus chatAreaEventBus;

        private readonly HashSet<IBlocksChat> chatBlockers = new ();

        public event IPanelInSharedSpace.ViewShowingCompleteDelegate? ViewShowingComplete;

        public bool IsVisibleInSharedSpace => false;

        public ChatMainController(ViewFactoryMethod viewFactory,
            IMVCManager mvcManager,
            ChatAreaEventBus chatAreaEventBus) : base(viewFactory)
        {
            this.mvcManager = mvcManager;
            this.chatAreaEventBus = chatAreaEventBus;
        }

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Persistent;

        protected override void OnViewInstantiated()
        {
            base.OnViewInstantiated();

            mvcManager.OnViewShowed += OnMvcViewShowed;
            mvcManager.OnViewClosed += OnMvcViewClosed;
            viewInstance!.OnPointerEnterEvent += HandlePointerEnter;
            viewInstance.OnPointerExitEvent += HandlePointerExit;

            DCLInput.Instance.UI.Click.performed += HandleGlobalClick;
        }

        protected override void OnViewShow()
        {
            chatAreaEventBus.RaiseViewShowEvent();
        }

        public void SetVisibility(bool isVisible)
        {
            chatAreaEventBus.RaiseVisibilityEvent(isVisible);
        }

        public void SetFocusState()
        {
            chatAreaEventBus.RaiseFocusEvent();
        }

        public void ToggleState()
        {
            chatAreaEventBus.RaiseToggleEvent();
        }

        public async UniTask OnShownInSharedSpaceAsync(CancellationToken ct, ChatControllerShowParams showParams)
        {
            // This method is called when we want to "show" the chat.
            // This can happen when:
            // 1. Toggling from a Minimized state.
            // 2. Another panel (like Friends) that was obscuring the chat is closed.
            if (State == ControllerState.ViewHidden)
            {
                // If the entire controller view is not even active, we can't proceed.
                await UniTask.CompletedTask;
                return;
            }

            // If the chat was fully hidden (e.g., by the Friends panel), transition to Default.
            // If it was minimized, transition to Default or Focused based on the input.
            // The `showParams.Focus` will be true when toggling with Enter/shortcut, and false when returning from another panel.
            chatAreaEventBus.RaiseShownInSharedSpaceEvent(showParams.Focus);


            ViewShowingComplete?.Invoke(this);
            await UniTask.CompletedTask;
        }

        public async UniTask OnHiddenInSharedSpaceAsync(CancellationToken ct)
        {
            chatAreaEventBus.RaiseHiddenInSharedSpaceEvent();
            await UniTask.CompletedTask;
        }

        protected override async UniTask WaitForCloseIntentAsync(CancellationToken ct)
        {
            ViewShowingComplete?.Invoke(this);
            await UniTask.WaitUntil(() => State == ControllerState.ViewHidden, PlayerLoopTiming.Update, ct);
        }

        private void HandlePointerEnter()
        {
            chatAreaEventBus.RaisePointerEnter();
        }

        private void HandlePointerExit()
        {
            chatAreaEventBus.RaisePointerExit();
        }

        private void HandleGlobalClick(InputAction.CallbackContext context)
        {
            if (EventSystem.current == null) return;

            var eventData = new PointerEventData(EventSystem.current)
            {
                position = GetPointerPosition(context)
            };

            using PooledObject<List<RaycastResult>> _ = ListPool<RaycastResult>.Get(out List<RaycastResult>? results);

            EventSystem.current.RaycastAll(eventData, results);

            // Check if click is inside any chat panel
            var clickedInsideChat = false;
            foreach (RaycastResult result in results)
            {
                if (result.gameObject.transform.IsChildOf(viewInstance!.transform))
                {
                    clickedInsideChat = true;
                    break;
                }
            }

            if (clickedInsideChat)
                chatAreaEventBus.RaiseClickInsideEvent(results);
            else
                chatAreaEventBus.RaiseClickOutsideEvent(results);
        }

        private static Vector2 GetPointerPosition(InputAction.CallbackContext ctx)
        {
            if (ctx.control is Pointer pointer) return pointer.position.ReadValue();
            if (Pointer.current != null) return Pointer.current.position.ReadValue();
            if (Mouse.current != null) return Mouse.current.position.ReadValue();
            if (Touchscreen.current?.primaryTouch != null)
                return Touchscreen.current.primaryTouch.position.ReadValue();
            return Vector2.zero;
        }

        public override void Dispose()
        {
            if (viewInstance != null)
            {
                mvcManager.OnViewShowed -= OnMvcViewShowed;
                mvcManager.OnViewClosed -= OnMvcViewClosed;
                viewInstance.OnPointerEnterEvent -= HandlePointerEnter;
                viewInstance.OnPointerExitEvent -= HandlePointerExit;
            }

            DCLInput.Instance.UI.Click.performed -= HandleGlobalClick;

            base.Dispose();

            chatBlockers.Clear();
        }

        private void OnMvcViewShowed(IController controller)
        {
            if (controller is not IBlocksChat blocker) return;

            chatBlockers.Add(blocker);
            chatAreaEventBus.RaiseMvcViewShowedEvent();
        }

        private void OnMvcViewClosed(IController controller)
        {
            if (controller is not IBlocksChat blocker) return;

            chatBlockers.Remove(blocker);
             if (chatBlockers.Count == 0)
                chatAreaEventBus.RaiseMvcViewClosedEvent();
        }
    }
}
