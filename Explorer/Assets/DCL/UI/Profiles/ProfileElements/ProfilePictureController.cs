using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Profiles;
using DCL.UI.Profiles;
using DCL.Web3;
using MVC;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.UI.ProfileElements
{
    public readonly struct ProfileThumbnailData
    {
        public readonly Color Color;
        public readonly URLAddress FaceSnapshotUrl;
        public readonly Web3Address UserAddress;
        public ProfileThumbnailData(URLAddress faceSnapshotUrl, Color color, Web3Address userAddress)
        {
            FaceSnapshotUrl = faceSnapshotUrl;
            Color = color;
            UserAddress = userAddress;
        }
    }

}
