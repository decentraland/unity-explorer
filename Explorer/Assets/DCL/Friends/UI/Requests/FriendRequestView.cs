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
        public ReceivedConfig received;
        public OperationConfirmedConfig sentConfirmed;
        public OperationConfirmedConfig cancelledConfirmed;
        public OperationConfirmedConfig rejectedConfirmed;
        public OperationConfirmedConfig acceptedConfirmed;

        [Serializable]
        public struct SendConfig
        {
            public GameObject Root;
            public UserAndMutualFriendsConfig UserAndMutualFriendsConfig;
            public TMP_InputField MessageInput;
            public TMP_Text MessageCharacterCountText;
            public Button CancelButton;
            public Button SendButton;
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
        }
    }
}
