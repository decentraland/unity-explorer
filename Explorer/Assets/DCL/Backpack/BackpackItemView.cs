using Cysharp.Threading.Tasks;
using DCL.UI;
using DG.Tweening;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Utility;

namespace DCL.Backpack
{
    public class BackpackItemView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        public event Action<string> OnSelectItem;

        [field: SerializeField]
        public string ItemId { get; set; }

        [field: SerializeField]
        public RectTransform ContainerTransform { get; private set; }

        [field: SerializeField]
        public RectTransform HoverBackgroundTransform { get; private set; }

        [field: SerializeField]
        public Button EquipButton { get; private set; }

        [field: SerializeField]
        public Button UnEquipButton { get; private set; }

        [field: SerializeField]
        public GameObject EquippedIcon { get; private set; }

        [field: SerializeField]
        public Image CategoryImage { get; private set; }

        [field: SerializeField]
        public Image WearableThumbnail { get; private set; }

        [field: SerializeField]
        public Image RarityBackground { get; private set; }

        [field: SerializeField]
        public Image FlapBackground { get; private set; }

        [field: SerializeField]
        public LoadingBrightView LoadingView { get; private set; }

        [field: SerializeField]
        public GameObject FullBackpackItem { get; private set; }

        [field: SerializeField]
        public bool IsEquipped { get; set; }

        private CancellationTokenSource cts;
        private readonly Vector3 hoveredScale = new (1.1f,1.1f,1.1f);

        public void SetEquipButtonsState()
        {
            EquipButton.gameObject.SetActive(!IsEquipped);
            UnEquipButton.gameObject.SetActive(IsEquipped);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            AnimateHover();
            SetEquipButtonsState();

            if (IsEquipped)
                EquippedIcon.gameObject.SetActive(false);
        }

        private void AnimateHover()
        {
            cts?.SafeCancelAndDispose();
            cts = new CancellationTokenSource();
            HoverBackgroundTransform.localScale = Vector3.zero;
            HoverBackgroundTransform.gameObject.SetActive(true);
            ContainerTransform.DOScale(hoveredScale, 0.1f).SetEase(Ease.Flash).ToUniTask(cancellationToken: cts.Token);
            HoverBackgroundTransform.DOScale(Vector3.one, 0.1f).SetEase(Ease.Flash).ToUniTask(cancellationToken: cts.Token);
        }

        private void AnimateExit()
        {
            cts?.SafeCancelAndDispose();
            cts = new CancellationTokenSource();
            ContainerTransform.DOScale(Vector3.one, 0.1f).SetEase(Ease.Flash).ToUniTask(cancellationToken: cts.Token);
            HoverBackgroundTransform.DOScale(Vector3.zero, 0.1f).SetEase(Ease.Flash)
                                    .OnComplete(()=>HoverBackgroundTransform.gameObject.SetActive(false)).ToUniTask(cancellationToken: cts.Token);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            AnimateExit();
            EquippedIcon.gameObject.SetActive(IsEquipped);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (string.IsNullOrEmpty(ItemId))
                return;

            OnSelectItem?.Invoke(ItemId);
        }
    }
}
