using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Chat.ChatReactions
{
    /// <summary>
    /// A small popup panel that appears above the reaction button and lets the player
    /// select which emoji to send. Appears on button hold.
    /// Items are pre-wired in the prefab; call <see cref="SetEmojiIndices"/> at runtime to
    /// bind each button to the correct atlas tile index.
    /// </summary>
    public class SituationalReactionPickerView : MonoBehaviour
    {
        public event Action<int>? EmojiSelected;

        [field: SerializeField] public List<Button> EmojiButtons { get; private set; } = new();

        private readonly List<Action> cleanupActions = new();

        private void Awake()
        {
            gameObject.SetActive(false);
        }

        /// <summary>
        /// Binds each button in <see cref="EmojiButtons"/> to fire <see cref="EmojiSelected"/>
        /// with the corresponding atlas tile index. Call once after the view is constructed.
        /// </summary>
        public void SetEmojiIndices(int[] atlasIndices)
        {
            ClearListeners();

            int count = Mathf.Min(EmojiButtons.Count, atlasIndices.Length);

            for (int i = 0; i < count; i++)
            {
                int index = atlasIndices[i];
                Action listener = () => OnEmojiButtonClicked(index);
                EmojiButtons[i].onClick.AddListener(listener.Invoke);
                cleanupActions.Add(() => EmojiButtons[i].onClick.RemoveListener(listener.Invoke));
            }
        }

        public void Show() => gameObject.SetActive(true);

        public void Hide() => gameObject.SetActive(false);

        private void OnEmojiButtonClicked(int atlasIndex)
        {
            Hide();
            EmojiSelected?.Invoke(atlasIndex);
        }

        private void OnDestroy() => ClearListeners();

        private void ClearListeners()
        {
            foreach (var cleanup in cleanupActions)
                cleanup();

            cleanupActions.Clear();
        }
    }
}
