using System;
using DCL.Chat.ChatReactions.Configs;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DCL.Chat.ChatReactions
{
    /// <summary>
    /// Individual emoji item in the shortcuts bar. Displays an emoji from the atlas
    /// and fires <see cref="OnClicked"/> when tapped.
    /// </summary>
    public sealed class ChatReactionItemView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private static readonly Vector3 HOVERED_SCALE = new (1.2f, 1.2f, 1.2f);
        private const float ANIM_DURATION = 0.1f;

        [SerializeField] private Button selectButton;
        [SerializeField] private RawImage emojiImage;

        public int AtlasIndex { get; private set; }

        public event Action<int>? OnClicked;

        private bool listenersAttached;

        public void Initialize(int atlasIndex, ChatReactionsAtlasConfig atlasConfig)
        {
            AtlasIndex = atlasIndex;

            emojiImage.texture = atlasConfig.Atlas;
            emojiImage.uvRect = atlasConfig.GetUVRect(atlasIndex);

            if (!listenersAttached)
            {
                selectButton.onClick.AddListener(HandleClicked);
                listenersAttached = true;
            }
        }

        public void Show() => gameObject.SetActive(true);

        public void Hide() => gameObject.SetActive(false);

        public void ResetForPool()
        {
            OnClicked = null;
            AtlasIndex = -1;
        }

        private void OnDestroy()
        {
            if (!listenersAttached) return;

            selectButton.onClick.RemoveListener(HandleClicked);
            listenersAttached = false;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            emojiImage.transform.DOScale(HOVERED_SCALE, ANIM_DURATION).SetEase(Ease.OutQuad);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            emojiImage.transform.DOScale(Vector3.one, ANIM_DURATION).SetEase(Ease.OutQuad);
        }

        private void HandleClicked() => OnClicked?.Invoke(AtlasIndex);
    }
}
