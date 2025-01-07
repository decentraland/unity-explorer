using DCL.InWorldCamera.CameraReelStorageService.Schemas;
using System;
using System.Collections.Generic;

namespace DCL.InWorldCamera.PhotoDetail
{
    /// <summary>
    ///     MVC parameter for the PhotoDetailController.
    /// </summary>
    public struct PhotoDetailParameter
    {
        //Currently loaded reels
        public readonly List<CameraReelResponseCompact> AllReels;
        //Index of the current reel
        public readonly int CurrentReelIndex;
        //If the reels are owned by the user (e.g. they are not if you are seeing a reel from another user's passport)
        public readonly bool UserOwnedReels;
        //Action to execute when the user wants to delete a reel (only if UserOwnedReels is true, otherwise an error will be returned by the backend)
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
