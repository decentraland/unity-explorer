using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Wearables.Components;
using UnityEngine;

namespace DCL.Backpack.Gifting.Models
{
    public struct WearableViewModel : IGiftableItemViewModel
    {
        public IWearable source { get; set; }
        public string Urn { get; }
        public ThumbnailState ThumbnailState { get; set; }
        public Sprite? Thumbnail { get; }

        public IWearable Source => source;

        /// <summary>
        ///     Public constructor for creating the initial view model from the data model.
        ///     It always starts in the 'NotLoaded' state.
        /// </summary>
        public WearableViewModel(IWearable source)
        {
            this.source = source;
            Urn = source.GetUrn();
            ThumbnailState = ThumbnailState.NotLoaded;
            Thumbnail = null;
        }

        /// <summary>
        ///     Private constructor used internally by WithState to create modified copies.
        /// </summary>
        private WearableViewModel(IWearable source, ThumbnailState state, Sprite? thumbnail)
        {
            this.source = source;
            Urn = source.GetUrn();
            ThumbnailState = state;
            Thumbnail = thumbnail;
        }

        /// <summary>
        ///     Returns a new instance of the ViewModel with an updated state and optional sprite.
        /// </summary>
        public WearableViewModel WithState(ThumbnailState newState, Sprite? newSprite = null)
        {
            return new WearableViewModel(source, newState, newSprite ?? Thumbnail);
        }
    }
}