using Cysharp.Threading.Tasks;
using DCL.Audio;
using DCL.RewardPanel;
using DCL.UI.ProfileElements;
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
        public SendConfig send;
        public CancelConfig cancel;
        public ReceivedConfig received;
        public OperationConfirmedConfig sentConfirmed;
        public OperationConfirmedConfig cancelledConfirmed;
        public OperationConfirmedConfig rejectedConfirmed;
        public OperationConfirmedConfig acceptedConfirmed;

        public async UniTask PlayShowAnimationAsync(OperationConfirmedConfig config, CancellationToken ct)
        {
            await config.BackgroundRaysAnimation.ShowAnimationAsync(ct);

            if (config.Sound != null)
                UIAudioEventsBus.Instance.SendPlayAudioEvent(config.Sound);
        }

        public async UniTask PlayHideAnimationAsync(OperationConfirmedConfig config, CancellationToken ct) =>
            await config.BackgroundRaysAnimation.HideAnimationAsync(ct);

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
            public ProfilePictureView UserThumbnail;
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
                public ProfilePictureView Image;
            }
        }

        [Serializable]
        public struct OperationConfirmedConfig
        {
            public GameObject Root;
            public ProfilePictureView FriendThumbnail;
            public ProfilePictureView? MyThumbnail;
            public TMP_Text Label;
            public Button CloseButton;
            public AudioClipConfig? Sound;
            public RewardBackgroundRaysAnimation BackgroundRaysAnimation;
        }
    }
}
