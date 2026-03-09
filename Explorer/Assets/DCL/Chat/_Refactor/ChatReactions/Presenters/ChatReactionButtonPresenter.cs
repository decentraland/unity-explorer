using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DCL.Chat.ChatReactions
{
    /// <summary>
    /// Wires <see cref="ChatReactionButtonView"/> to <see cref="ISituationalReactionService"/>.
    /// <list type="bullet">
    ///   <item>Tap (release before <see cref="HOLD_THRESHOLD_SEC"/>) → single burst from button position.</item>
    ///   <item>Hold → opens <see cref="SituationalReactionPickerView"/> above the button for emoji selection.</item>
    ///   <item>Emoji selected → burst from button position with the chosen emoji, picker closes.</item>
    /// </list>
    /// </summary>
    public sealed class ChatReactionButtonPresenter : IDisposable
    {
        private const float HOLD_THRESHOLD_SEC = 0.3f;

        private readonly ChatReactionButtonView view;
        private readonly RectTransform buttonRect;
        private readonly ISituationalReactionService reactionService;
        private readonly EventTrigger eventTrigger;
        private readonly EventTrigger.Entry pointerDownEntry;
        private readonly EventTrigger.Entry pointerUpEntry;
        private readonly EventTrigger.Entry pointerExitEntry;

        private CancellationTokenSource? holdCts;
        private bool pickerOpen;
        private int selectedEmojiIndex = -1;

        public event Action? HoldTriggered;
        public event Action<int>? PickerEmojiSelected;

        public ChatReactionButtonPresenter(ChatReactionButtonView view, ISituationalReactionService reactionService)
        {
            if (view == null) throw new ArgumentNullException(nameof(view));

            this.view = view;
            this.buttonRect = view.ReactionButton.GetComponent<RectTransform>();
            this.reactionService = reactionService ?? throw new ArgumentNullException(nameof(reactionService));

            if (view.PickerView != null)
            {
                view.PickerView.SetEmojiIndices(Array.Empty<int>());
                view.PickerView.EmojiSelected += OnEmojiSelected;
            }

            eventTrigger = view.ButtonEventTrigger;

            pointerDownEntry = AddTriggerEntry(EventTriggerType.PointerDown, _ => OnPointerDown());
            pointerUpEntry = AddTriggerEntry(EventTriggerType.PointerUp, _ => OnPointerUp());
            pointerExitEntry = AddTriggerEntry(EventTriggerType.PointerExit, _ => OnPointerExit());
        }

        public void Show() => view.Show();

        public void Hide()
        {
            view.PickerView?.Hide();
            pickerOpen = false;
            CancelHoldTimer();
            view.Hide();
        }

        public void Dispose()
        {
            CancelHoldTimer();

            if (view.PickerView != null)
            {
                view.PickerView.EmojiSelected -= OnEmojiSelected;
                view.PickerView.Hide();
            }

            eventTrigger.triggers.Remove(pointerDownEntry);
            eventTrigger.triggers.Remove(pointerUpEntry);
            eventTrigger.triggers.Remove(pointerExitEntry);
        }

        private void OnPointerDown()
        {
            pickerOpen = false;
            CancelHoldTimer();

            holdCts = new CancellationTokenSource();
            WaitForHoldAsync(holdCts.Token).Forget();
        }

        private void OnPointerUp()
        {
            CancelHoldTimer();

            if (!pickerOpen)
            {
                if (selectedEmojiIndex >= 0)
                    reactionService.TriggerUIReactionFromRect(buttonRect, selectedEmojiIndex, count: 1);
                else
                    reactionService.TriggerDefaultUIReactionFromRect(buttonRect);
            }

            pickerOpen = false;
        }

        private void OnPointerExit()
        {
            // Cancel pending hold timer but do not fire tap or close the picker —
            // the player may have dragged onto a picker item.
            CancelHoldTimer();
        }

        public void SetSelectedEmoji(int atlasIndex)
        {
            selectedEmojiIndex = atlasIndex;
        }

        private void OnEmojiSelected(int atlasIndex)
        {
            pickerOpen = false;
            selectedEmojiIndex = atlasIndex;
            reactionService.TriggerUIReactionFromRect(buttonRect, atlasIndex, count: 1);
            PickerEmojiSelected?.Invoke(atlasIndex);
        }

        private async UniTaskVoid WaitForHoldAsync(CancellationToken ct)
        {
            try
            {
                await UniTask.Delay(
                    TimeSpan.FromSeconds(HOLD_THRESHOLD_SEC),
                    ignoreTimeScale: true,
                    cancellationToken: ct);

                HoldTriggered?.Invoke();

                if (view.PickerView != null)
                {
                    pickerOpen = true;
                    view.PickerView.Show();
                }
            }
            catch (OperationCanceledException) { }
        }

        private void CancelHoldTimer()
        {
            holdCts?.Cancel();
            holdCts?.Dispose();
            holdCts = null;
        }

        private EventTrigger.Entry AddTriggerEntry(EventTriggerType type, UnityEngine.Events.UnityAction<BaseEventData> action)
        {
            var entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(action);
            eventTrigger.triggers.Add(entry);
            return entry;
        }
    }
}
