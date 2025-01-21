using DCL.UI;
using MVC;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Friends.UI.Requests
{
    public class FriendRequestView : ViewBase, IView
    {
        public SendConfig send;
        public CancelConfig cancel;
        public ReceiveWithMessageConfig receiveWithMessage;
        public ReceiveWithoutMessageConfig receiveWithoutMessage;

        [Serializable]
        public struct SendConfig
        {
            public GameObject Root;
            public UserAndMutualFriendsConfig UserAndMutualFriendsConfig;
            public InputField MessageInput;
            public TMP_Text MessageCharacterCountText;
            public Button CancelButton;
            public Button SendButton;
        }

        [Serializable]
        public struct CancelConfig
        {
            public GameObject Root;
            public UserAndMutualFriendsConfig UserAndMutualFriendsConfig;
            public InputField MessageInput;
            public TMP_Text MessageCharacterCountText;
            public Button CancelButton;
            public Button BackButton;
            public TMP_Text TimestampText;
        }

        [Serializable]
        public struct ReceiveWithMessageConfig
        {
            public InputField MessageInput;
            public ReceiveWithoutMessageConfig Config;
        }

        [Serializable]
        public struct ReceiveWithoutMessageConfig
        {
            public GameObject Root;
            public UserAndMutualFriendsConfig UserAndMutualFriendsConfig;
        }

        [Serializable]
        public struct UserAndMutualFriendsConfig
        {
            public ImageView UserThumbnail;
            public TMP_Text UserName;
            public TMP_Text UserNameHash;
            public GameObject UserNameVerification;
            public GameObject MutualThumbnailTemplate;
            public TMP_Text MutalCountText;
        }
    }
}
