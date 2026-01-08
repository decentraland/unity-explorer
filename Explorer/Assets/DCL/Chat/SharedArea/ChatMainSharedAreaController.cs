using Cysharp.Threading.Tasks;
using DCL.Chat;
using DCL.Chat.ChatCommands;
using DCL.Chat.ChatServices;
using MVC;
using System.Threading;
using Utility;
using InputAction = UnityEngine.InputSystem.InputAction;

namespace DCL.ChatArea
{
    public class ChatMainSharedAreaController : ControllerBase<ChatMainSharedAreaView>
    {
        private readonly IMVCManager mvcManager;
        private readonly ChatSharedAreaEventBus chatSharedAreaEventBus;
        private readonly IEventBus chatEventBus;
        private readonly EventSubscriptionScope eventScope = new ();
        private readonly DCLInput dclInput;
        public readonly ChatCommandRegistry ChatCommandRegistry;

        public bool IsVisible { get; private set; }

        public ChatMainSharedAreaController(ViewFactoryMethod viewFactory,
            IMVCManager mvcManager,
            ChatSharedAreaEventBus chatSharedAreaEventBus,
            ChatCommandRegistry chatCommandRegistry,
            IEventBus chatEventBus,
            DCLInput dclInput) : base(viewFactory)
        {
            this.mvcManager = mvcManager;
            this.chatSharedAreaEventBus = chatSharedAreaEventBus;
            this.ChatCommandRegistry = chatCommandRegistry;
            this.chatEventBus = chatEventBus;
            this.dclInput = dclInput;
        }

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.PERSISTENT;

        protected override void OnViewInstantiated()
        {
            base.OnViewInstantiated();

            mvcManager.OnViewShowed += OnMvcViewShowed;
            mvcManager.OnViewClosed += OnMvcViewClosed;
            viewInstance!.OnPointerEnterEvent += HandlePointerEnter;
            viewInstance.OnPointerExitEvent += HandlePointerExit;
            dclInput.UI.Submit.performed += OnUISubmitPerformed;

            eventScope.Add(chatEventBus.Subscribe<ChatEvents.ToggleChatEvent>(ToggleChatState));
            eventScope.Add(chatEventBus.Subscribe<ChatEvents.FocusRequestedEvent>(SetFocusState));
            eventScope.Add(chatSharedAreaEventBus.Subscribe<ChatSharedAreaEvents.ChatPanelVisibilityStateChangedEvent>(HandleVisibilityStateChanged));

            CentralizedChatClickDetectionService.Instance.Resume();
        }

        protected override void OnBeforeViewShow()
        {
            base.OnBeforeViewShow();
            mvcManager.CloseAllNonPersistentControllers();
        }

        protected override void OnViewShow()
        {
            CentralizedChatClickDetectionService.Instance.Resume();
            chatSharedAreaEventBus.RaiseViewShowEvent();
        }

        private void SetFocusState(ChatEvents.FocusRequestedEvent _)
        {
            chatSharedAreaEventBus.RaiseFocusEvent();
        }

        private void ToggleChatState(ChatEvents.ToggleChatEvent _)
        {
            chatSharedAreaEventBus.RaiseToggleEvent();
        }

        private void OnUISubmitPerformed(InputAction.CallbackContext _)
        {
            chatSharedAreaEventBus.RaiseUISubmitEvent();
        }

        protected override async UniTask WaitForCloseIntentAsync(CancellationToken ct)
        {
            await UniTask.WaitUntil(() => State == ControllerState.ViewHidden, PlayerLoopTiming.Update, ct);
            CentralizedChatClickDetectionService.Instance.Pause();
        }

        private void HandlePointerEnter()
        {
            chatSharedAreaEventBus.RaisePointerEnter();
        }

        private void HandlePointerExit()
        {
            chatSharedAreaEventBus.RaisePointerExit();
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

            eventScope.Dispose();

            base.Dispose();
        }

        private void OnMvcViewShowed(IController controller)
        {
            //We only need to hide the chat if a fullscreen view is shown
            if (controller.Layer is not CanvasOrdering.SortingLayer.FULLSCREEN) return;

            chatSharedAreaEventBus.RaiseFullscreenOpenEvent();
        }

        private void OnMvcViewClosed(IController controller)
        {
            if (controller.Layer is not CanvasOrdering.SortingLayer.FULLSCREEN) return;

            //We restore the chat to its previous appearance if the fullscreen view is closed
            chatSharedAreaEventBus.RaiseFullscreenClosedEvent();
        }

        private void HandleVisibilityStateChanged(ChatSharedAreaEvents.ChatPanelVisibilityStateChangedEvent evt)
        {
            IsVisible = evt.IsVisible;
            if (IsVisible)
                CentralizedChatClickDetectionService.Instance.Resume();
            else
                CentralizedChatClickDetectionService.Instance.Pause();
        }
    }
}
