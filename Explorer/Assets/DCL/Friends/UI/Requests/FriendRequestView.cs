using Cysharp.Threading.Tasks;
using DCL.Audio;
using DCL.UI;
using DG.Tweening;
using MVC;
using System;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Friends.UI.Requests
{
    public class FriendRequestView : ViewBase, IView
    {
        private const float FADE_ANIMATION_DURATION = 0.4f;
        private const float SCALE_ANIMATION_DURATION = 0.5f;

        public SendConfig send;
        public CancelConfig cancel;
        public ReceivedConfig received;
        public OperationConfirmedConfig sentConfirmed;
        public OperationConfirmedConfig cancelledConfirmed;
        public OperationConfirmedConfig rejectedConfirmed;
        public OperationConfirmedConfig acceptedConfirmed;

        public async UniTask PlayShowAnimationAsync(OperationConfirmedConfig config, CancellationToken ct)
        {
            config.Rays.rotation = Quaternion.identity;
            config.Root.transform.localScale = Vector3.zero;

            await config.CanvasGroup.DOFade(1, FADE_ANIMATION_DURATION)
                        .ToUniTask(cancellationToken: ct);

            await config.Root.transform.DOScale(Vector3.one, SCALE_ANIMATION_DURATION)
                        .SetEase(Ease.OutBounce)
                        .ToUniTask(cancellationToken: ct);

            if (config.Sound != null)
                UIAudioEventsBus.Instance.SendPlayAudioEvent(config.Sound);

            config.Rays.DORotate(new Vector3(0, 0, 360), 2f, RotateMode.FastBeyond360)
                  .SetEase(Ease.Linear)
                  .SetLoops(-1, LoopType.Restart)
                  .ToUniTask(cancellationToken: ct);
        }

        public async UniTask PlayHideAnimationAsync(OperationConfirmedConfig config, CancellationToken ct)
        {
            config.Root.transform.DOScale(Vector3.zero, SCALE_ANIMATION_DURATION / 2);

            await config.CanvasGroup.DOFade(0, FADE_ANIMATION_DURATION / 2)
                        .ToUniTask(cancellationToken: ct);
        }

        [Serializable]
        public struct SendConfig
        {
            public GameObject Root;
            public UserAndMutualFriendsConfig UserAndMutualFriendsConfig;
            public TMP_InputField MessageInput;
            public TMP_Text MessageCharacterCountText;
            public Button CancelButton;
            public Button SendButton;
            public Button CloseButton;
        }

        [Serializable]
        public struct CancelConfig
        {
            public GameObject Root;
            public UserAndMutualFriendsConfig UserAndMutualFriendsConfig;
            public TMP_InputField MessageInput;
            public GameObject MessageInputContainer;
            public Button PreCancelButton;
            public Button CancelButton;
            public Button BackButton;
            public Button CloseButton;
            public GameObject PreCancelToastContainer;
            public TMP_Text TimestampText;
        }

        [Serializable]
        public struct ReceivedConfig
        {
            public GameObject Root;
            public UserAndMutualFriendsConfig UserAndMutualFriendsConfig;
            public TMP_InputField MessageInput;
            public GameObject MessageInputContainer;
            public Button RejectButton;
            public Button AcceptButton;
            public Button BackButton;
            public Button CloseButton;
            public TMP_Text TimestampText;
        }

        [Serializable]
        public struct UserAndMutualFriendsConfig
        {
            public ImageView UserThumbnail;
            public TMP_Text UserName;
            public TMP_Text UserNameHash;
            public GameObject UserNameVerification;
            public GameObject MutualContainer;
            public MutualThumbnail[] MutualThumbnails;
            public TMP_Text MutalCountText;

            [Serializable]
            public struct MutualThumbnail
            {
                public GameObject Root;
                public ImageView Image;
            }
        }

        [Serializable]
        public struct OperationConfirmedConfig
        {
            public GameObject Root;
            public ImageView FriendThumbnail;
            public ImageView? MyThumbnail;
            public TMP_Text Label;
            public Button CloseButton;
            public Transform Rays;
            public CanvasGroup CanvasGroup;
            public AudioClipConfig? Sound;
        }
    }
}
