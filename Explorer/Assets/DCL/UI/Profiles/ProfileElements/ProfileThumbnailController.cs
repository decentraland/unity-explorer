using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Profiles;
using DCL.UI.Profiles;
using DCL.Web3;
using MVC;
using System.Threading;
using UnityEngine;

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

    public class ProfileThumbnailController : SimpleController <ProfileThumbnailView, ProfileThumbnailData, Color>
    {
        private readonly IProfileThumbnailCache profileThumbnailCache;


        public ProfileThumbnailController(
            ProfileThumbnailView view,
            ProfileThumbnailData data,
            IProfileThumbnailCache profileThumbnailCache) : base(view, data)
        {
            this.profileThumbnailCache = profileThumbnailCache;
        }

        protected override Color ProcessData(ProfileThumbnailData data) =>
            inputData.Color;

        public override void UpdateView()
        {
            base.UpdateView();
            UpdateThumbnailAsync().Forget();
        }

        private async UniTaskVoid UpdateThumbnailAsync()
        {
            await viewInstance.ThumbnailImageView.LoadThumbnailSafeAsync(profileThumbnailCache, inputData.UserAddress, inputData.FaceSnapshotUrl, new CancellationToken() );
        }

        public override void Dispose()
        {
        }
    }
}
