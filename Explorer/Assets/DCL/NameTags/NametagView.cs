using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using Utility;
using Random = UnityEngine.Random;
using Vector2 = UnityEngine.Vector2;
using DG.Tweening;

namespace DCL.Nametags
{
    public class NametagView : MonoBehaviour
    {
        [field: SerializeField]
        public TMP_Text Username { get; private set; }

        [field: SerializeField]
        public TMP_Text MessageContent { get; private set; }

        [field: SerializeField]
        public SpriteRenderer Background { get; private set; }

        [field: SerializeField]
        public RectTransform MessageContentRectTransform { get; private set; }

        [field: SerializeField]
        internal AnimationCurve backgroundEaseAnimationCurve { get; private set; }

        [field: SerializeField]
        public float FixedWidth { get; private set; }
        private const float MARGIN_OFFSET_WIDTH = 0.5f;
        private const float MARGIN_OFFSET_HEIGHT = 0.7f;
        private static readonly Vector2 MESSAGE_CONTENT_ANCHORED_POSITION = new (0,MARGIN_OFFSET_HEIGHT / 3);

        private bool isBubbleExpanded = false;

        private void Start()
        {
            SetUsername(StringUtils.GenerateRandomString(Random.Range(3,10)));
            GenerateRandomMsgsAsync().Forget();
        }

        public void SetUsername(string username)
        {
            Username.text = username;
            Username.rectTransform.sizeDelta = new Vector2(Username.preferredWidth, Username.preferredHeight + 0.15f);
            Background.size = new Vector2(Username.preferredWidth + 0.2f, Username.preferredHeight + 0.15f);

            //Animate("really long string really long string really long string really long string really long string really long string really long string ");
        }

        private async UniTaskVoid GenerateRandomMsgsAsync()
        {
            do
            {
                StartChatBubbleFlowAsync(StringUtils.GenerateRandomString(Random.Range(0,250))).Forget();
                await UniTask.Delay(Random.Range(5000,10000));
            }
            while (true);
        }

        public void SetChatMessage(string chatMessage)
        {
            StartChatBubbleFlowAsync(chatMessage).Forget();
        }

        private async UniTaskVoid StartChatBubbleFlowAsync(string chatMessage)
        {
            if(!isBubbleExpanded)
                AnimateIn(chatMessage);

            await UniTask.Delay(4000);
            AnimateOut();
        }

        private void AnimateIn(string messageContent)
        {
            MessageContent.gameObject.SetActive(true);
            isBubbleExpanded = true;

            Vector2 preferredSize = MessageContent.GetPreferredValues(messageContent, FixedWidth, 0);
            preferredSize.x = MessageContentRectTransform.sizeDelta.x;
            MessageContentRectTransform.sizeDelta = preferredSize;
            MessageContentRectTransform.anchoredPosition = MESSAGE_CONTENT_ANCHORED_POSITION;
            MessageContent.text = messageContent;
            preferredSize.x = MessageContentRectTransform.sizeDelta.x + MARGIN_OFFSET_WIDTH;
            preferredSize.y += MARGIN_OFFSET_HEIGHT;

            MessageContent.DOFade(1, 0.2f);
            Username.rectTransform.DOAnchorPos(new Vector2((-preferredSize.x / 2) + (Username.preferredWidth / 2) + (MARGIN_OFFSET_WIDTH / 2), MessageContentRectTransform.sizeDelta.y + (MARGIN_OFFSET_HEIGHT / 3)), 0.2f);
            DOTween.To(() => Background.size, x=> Background.size = x, preferredSize, 1).SetEase(backgroundEaseAnimationCurve);

        }



        private void AnimateOut()
        {
            isBubbleExpanded = false;

            Username.rectTransform.DOAnchorPos(Vector2.zero, 0.2f);
            MessageContent.DOFade(0, 0.2f).OnComplete(()=>MessageContent.gameObject.SetActive(false));
            DOTween.To(() => Background.size, x=> Background.size = x, new Vector2(Username.preferredWidth + 0.2f, Username.preferredHeight + 0.15f), 1).SetEase(backgroundEaseAnimationCurve);
        }

    }
}
