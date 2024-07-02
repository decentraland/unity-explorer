using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Audio;
using DCL.UI;
using DG.Tweening;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Utility;

namespace DCL.Passport.Fields
{
    public class EquippedItem_PassportFieldView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private readonly Vector3 hoveredScale = new (1.1f,1.1f,1.1f);
        private const float ANIMATION_TIME = 0.1f;

        [field: SerializeField]
        public RectTransform ContainerTransform { get; private set; }

        [field: SerializeField]
        public RectTransform SubContainerTransform { get; private set; }

        [field: SerializeField]
        public RectTransform HoverBackgroundTransform { get; private set; }

        [field: SerializeField]
        public Button BuyButton { get; private set; }

        [field: SerializeField]
        public Image CategoryImage { get; private set; }

        [field: SerializeField]
        public Image EquippedItemThumbnail { get; private set; }

        [field: SerializeField]
        public Image RarityBackground { get; private set; }

        [field: SerializeField]
        public Image RarityBackground2 { get; private set; }

        [field: SerializeField]
        public Image FlapBackground { get; private set; }

        [field: SerializeField]
        public RectTransform RarityLabelContainer { get; private set; }

        [field: SerializeField]
        public TMP_Text RarityLabelText { get; private set; }

        [field: SerializeField]
        public TMP_Text AssetNameText { get; private set; }

        [field: SerializeField]
        public LoadingBrightView LoadingView { get; private set; }

        [field: SerializeField]
        public GameObject FullEquippedItemItem { get; private set; }

        [field: SerializeField]
        public AudioClipConfig BuyAudio { get; private set; }

        [field: SerializeField]
        public AudioClipConfig HoverAudio { get; private set; }

        public URN ItemId { get; set; }

        private CancellationTokenSource cts;

        private void Awake()
        {
            BuyButton.onClick.AddListener(() =>
            {
                // TODO (Santi): Implement buy logic...
                UIAudioEventsBus.Instance.SendPlayAudioEvent(BuyAudio);
            });
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            AnimateHover();
            UIAudioEventsBus.Instance.SendPlayAudioEvent(HoverAudio);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            AnimateExit();
        }

        public void SetAsLoading(bool isLoading)
        {
            AssetNameText.gameObject.SetActive(!isLoading);
            RarityLabelText.gameObject.SetActive(!isLoading);

            if (isLoading)
                LoadingView.StartLoadingAnimation(FullEquippedItemItem);
            else
                LoadingView.FinishLoadingAnimation(FullEquippedItemItem);
        }

        public void SetInvisible(bool isInvisible) =>
            SubContainerTransform.gameObject.SetActive(!isInvisible);

        private void AnimateHover()
        {
            cts?.SafeCancelAndDispose();
            cts = new CancellationTokenSource();
            HoverBackgroundTransform.localScale = Vector3.one;
            HoverBackgroundTransform.gameObject.SetActive(true);
            ContainerTransform.DOScale(hoveredScale, ANIMATION_TIME).SetEase(Ease.Flash).ToUniTask(cancellationToken: cts.Token);
            HoverBackgroundTransform.DOScale(Vector3.one, ANIMATION_TIME).SetEase(Ease.Flash).ToUniTask(cancellationToken: cts.Token);
        }

        private void AnimateExit()
        {
            cts?.SafeCancelAndDispose();
            cts = new CancellationTokenSource();
            ContainerTransform.DOScale(Vector3.one, ANIMATION_TIME).SetEase(Ease.Flash).ToUniTask(cancellationToken: cts.Token);
            HoverBackgroundTransform.DOScale(Vector3.zero, ANIMATION_TIME).SetEase(Ease.Flash)
                                    .OnComplete(()=>HoverBackgroundTransform.gameObject.SetActive(false)).ToUniTask(cancellationToken: cts.Token);
        }
    }
}
