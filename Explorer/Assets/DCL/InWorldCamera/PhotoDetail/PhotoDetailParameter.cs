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
        public enum CallerContext
        {
            CameraReel,
            PlaceInfoPanel,
            Passport,
            CommunityCard
        }
        //Currently loaded reels
        public readonly List<CameraReelResponseCompact> AllReels;
        //Index of the current reel
        public readonly int CurrentReelIndex;
        //If the PhotoDetailUI was opened from a panel, that contains reels from other users. (Used to hide functionality)
        public readonly bool OpenedFromPublicBoard;
        public readonly CallerContext OpenedFrom;
        public readonly GalleryEventBus GalleryEventBus;
        //Action to execute when the user wants to delete a reel (only if UserOwnedReels is true, otherwise an error will be returned by the backend)
        public event Action<CameraReelResponseCompact> ReelDeleteIntention;
        //Hides reel from list, used when reel is displayed from passport view, and user sets reel to private, which
        //is not displayed in passport gallery.
        public event Action<CameraReelResponseCompact> HideReelFromListIntention;

        public PhotoDetailParameter(List<CameraReelResponseCompact> allReels, int currentReelIndex, bool openedFromPublicBoard, 
            CallerContext openedFrom, Action<CameraReelResponseCompact> reelDeleteAction, 
            Action<CameraReelResponseCompact> hideReelFromListIntention, GalleryEventBus galleryEventBus)
        {
            this.AllReels = allReels;
            this.CurrentReelIndex = currentReelIndex;
            this.OpenedFromPublicBoard = openedFromPublicBoard;
            this.OpenedFrom = openedFrom;
            ReelDeleteIntention = reelDeleteAction;
            HideReelFromListIntention = hideReelFromListIntention;
            GalleryEventBus = galleryEventBus;
        }

        public void ExecuteDeleteAction(int index) =>
            ReelDeleteIntention?.Invoke(AllReels[index]);
        
        public void ExecuteHideReelFromListAction(int index) =>
            HideReelFromListIntention?.Invoke(AllReels[index]);
    }
}
