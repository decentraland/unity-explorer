using DCL.InWorldCamera.CameraReelStorageService.Schemas;
using System;
using System.Collections.Generic;

namespace DCL.InWorldCamera.PhotoDetail
{
    public struct PhotoDetailParameter
    {
        public readonly List<CameraReelResponseCompact> AllReels;
        public readonly int CurrentReelIndex;
        public readonly bool UserOwnedReels;
        public event Action<CameraReelResponseCompact> ReelDeleteIntention;

        public PhotoDetailParameter(List<CameraReelResponseCompact> allReels, int currentReelIndex, bool userOwnedReels, Action<CameraReelResponseCompact> reelDeleteAction)
        {
            this.AllReels = allReels;
            this.CurrentReelIndex = currentReelIndex;
            this.UserOwnedReels = userOwnedReels;
            ReelDeleteIntention = reelDeleteAction;
        }

        public void ExecuteDeleteAction(int index) =>
            ReelDeleteIntention?.Invoke(AllReels[index]);
    }
}
