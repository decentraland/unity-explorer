using Cysharp.Threading.Tasks;
using DCL.Audio;
using DCL.Emoji;
using DCL.UI;
using MVC;
using DG.Tweening;
using SuperScrollView;
using System;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Utility;

namespace DCL.Chat
{
    public class ChatView : ViewBase, IView, IPointerEnterHandler, IPointerExitHandler
    {
        public event Action OnChatViewPointerEnter;
        public event Action OnChatViewPointerExit;

        private const float BACKGROUND_FADE_TIME = 0.2f;
        private const float CHAT_ENTRIES_FADE_TIME = 3f;
        private const int CHAT_ENTRIES_WAIT_BEFORE_FADE_MS = 10000;

        [field: SerializeField]
        public ToggleView ChatBubblesToggle { get; private set; }

        [field: SerializeField]
        public EmojiPanelView EmojiPanel { get; private set; }

        [field: SerializeField]
        public EmojiSuggestionPanelView EmojiSuggestionPanel { get; private set; }

        [field: SerializeField]
        public TMP_InputField InputField { get; private set; }

        [field: SerializeField]
        public CharacterCounterView CharacterCounter { get; private set; }

        [field: SerializeField]
        public CanvasGroup PanelBackgroundCanvasGroup { get; private set; }

        [field: SerializeField]
        public CanvasGroup ScrollbarCanvasGroup { get; private set; }

        [field: SerializeField]
        public CanvasGroup ChatEntriesCanvasGroup { get; private set; }

        [field: SerializeField]
        public LoopListView2 LoopList { get; private set; }

        [field: SerializeField]
        public EmojiButtonView EmojiPanelButton { get; private set; }

        [field: SerializeField]
        public Button CloseChatButton { get; private set; }


        [field: Header("Audio")]
        [field: SerializeField]
        public AudioClipConfig AddEmojiAudio { get; private set; }
        [field: SerializeField]
        public AudioClipConfig OpenEmojiPanelAudio { get; private set; }
        [field: SerializeField]
        public AudioClipConfig ChatSendMessageAudio { get; private set; }
        [field: SerializeField]
        public AudioClipConfig ChatReceiveMessageAudio { get; private set; }
        [field: SerializeField]
        public AudioClipConfig ChatInputTextAudio { get; private set; }
        [field: SerializeField]
        public AudioClipConfig EnterInputAudio { get; private set; }


        private CancellationTokenSource cts;

        private void Start()
        {
            PanelBackgroundCanvasGroup.alpha = 0;
            ScrollbarCanvasGroup.alpha = 0;
        }

        public void ToggleChat(bool isOn)
        {
            PanelBackgroundCanvasGroup.gameObject.SetActive(isOn);
            LoopList.gameObject.SetActive(isOn);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            OnChatViewPointerEnter?.Invoke();
            PanelBackgroundCanvasGroup.DOFade(1, BACKGROUND_FADE_TIME);
            ScrollbarCanvasGroup.DOFade(1, BACKGROUND_FADE_TIME);
            StopChatEntriesFadeout();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            OnChatViewPointerExit?.Invoke();
            PanelBackgroundCanvasGroup.DOFade(0, BACKGROUND_FADE_TIME);
            ScrollbarCanvasGroup.DOFade(0, BACKGROUND_FADE_TIME);
            StartChatEntriesFadeout();
        }

        public void StopChatEntriesFadeout()
        {
            cts.SafeCancelAndDispose();
            ChatEntriesCanvasGroup.alpha = 1;
        }

        public void StartChatEntriesFadeout()
        {
            cts.SafeCancelAndDispose();
            cts = new CancellationTokenSource();

            AwaitAndFadeChatEntriesAsync(cts.Token).Forget();
        }

        public void ResetChatEntriesFadeout()
        {
            StopChatEntriesFadeout();
            StartChatEntriesFadeout();
        }

        private async UniTaskVoid AwaitAndFadeChatEntriesAsync(CancellationToken ct)
        {
            cts.Token.ThrowIfCancellationRequested();
            ChatEntriesCanvasGroup.alpha = 1;
            await UniTask.Delay(CHAT_ENTRIES_WAIT_BEFORE_FADE_MS, cancellationToken: ct);
            await ChatEntriesCanvasGroup.DOFade(0.4f, CHAT_ENTRIES_FADE_TIME).ToUniTask(cancellationToken: ct);
        }
    }
}
