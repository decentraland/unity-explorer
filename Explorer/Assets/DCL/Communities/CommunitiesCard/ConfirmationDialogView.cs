using Cysharp.Threading.Tasks;
using DG.Tweening;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Communities.CommunitiesCard
{
    public class ConfirmationDialogView : MonoBehaviour
    {
        public enum ConfirmationReason
        {
            LEAVE_COMMUNITY,
            KICK_USER,
            BAN_USER,
        }

        public enum ConfirmationResult
        {
            CONFIRM,
            CANCEL,
        }

        private const string LEAVE_COMMUNITY_TEXT_FORMAT = "Are you sure you want to leave '{0}'?";
        private const string KICK_MEMBER_TEXT_FORMAT = "Are you sure you want to kick '{0}' from {1}?";
        private const string BAN_MEMBER_TEXT_FORMAT = "Are you sure you want to ban '{0}' from {1}?";

        [field: SerializeField] public CanvasGroup ViewCanvasGroup { get; private set; }
        [field: SerializeField] public Button BackgroundButton { get; private set; }
        [field: SerializeField] public Button CancelButton { get; private set; }
        [field: SerializeField] public Button ConfirmButton { get; private set; }
        [field: SerializeField] public float FadeDuration { get; private set; } = 0.3f;
        [field: SerializeField] public TMP_Text MainText { get; private set; }
        [field: SerializeField] public Image MainImage { get; private set; }
        [field: SerializeField] public GameObject QuitImage { get; private set; }
        [field: SerializeField] public Image RimImage { get; private set; }

        [field: Header("Assets")]
        [field: SerializeField] public Sprite KickSprite { get; private set; }
        [field: SerializeField] public Sprite BanSprite { get; private set; }

        public async UniTask<ConfirmationResult> ShowConfirmationDialogAsync(ConfirmationReason reason,
            string communityName,
            string? userName = null,
            Sprite? communitySprite = null,
            bool showImageRim = false,
            CancellationToken ct = default)
        {
            gameObject.SetActive(true);
            CancelButton.gameObject.SetActive(true);
            ConfirmButton.gameObject.SetActive(true);

            RimImage.enabled = showImageRim;

            switch (reason)
            {
                case ConfirmationReason.LEAVE_COMMUNITY:
                    MainText.text = string.Format(LEAVE_COMMUNITY_TEXT_FORMAT, communityName);
                    MainImage.sprite = communitySprite;
                    break;
                case ConfirmationReason.KICK_USER:
                    MainText.text = string.Format(KICK_MEMBER_TEXT_FORMAT, userName, communityName);
                    MainImage.sprite = KickSprite;
                    break;
                case ConfirmationReason.BAN_USER:
                    MainText.text = string.Format(BAN_MEMBER_TEXT_FORMAT, userName, communityName);
                    MainImage.sprite = BanSprite;
                    break;
            }

            QuitImage.SetActive(reason == ConfirmationReason.LEAVE_COMMUNITY);

            await ViewCanvasGroup.DOFade(1f, FadeDuration).ToUniTask(cancellationToken: ct);
            ViewCanvasGroup.interactable = true;
            ViewCanvasGroup.blocksRaycasts = true;


            int index = await UniTask.WhenAny(CancelButton.OnClickAsync(ct), BackgroundButton.OnClickAsync(ct), ConfirmButton.OnClickAsync(ct));

            await ViewCanvasGroup.DOFade(0f, FadeDuration).ToUniTask(cancellationToken: ct);
            ViewCanvasGroup.interactable = false;
            ViewCanvasGroup.blocksRaycasts = false;

            CancelButton.gameObject.SetActive(false);
            ConfirmButton.gameObject.SetActive(false);

            return index > 1 ? ConfirmationResult.CONFIRM : ConfirmationResult.CANCEL;
        }

    }
}
