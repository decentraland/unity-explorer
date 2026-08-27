using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Emotes;
using DCL.ECSComponents;

namespace DCL.Multiplayer.SDK.Components
{
    public struct AvatarEmoteCommandComponent : IDirtyMarker
    {
        /// <summary>
        ///     One-shot events transferred from the global world, consumed (appended + cleared) by
        ///     WriteAvatarEmoteCommandSystem.
        /// </summary>
        public EmoteStartEvent StartEvent;
        public EmoteStopEvent StopEvent;

        /// <summary>
        ///     Replay snapshot of the last started emote: used only by WriteAvatarEmoteCommandSystem.Initialize()
        ///     to re-append the started state while the emote is still playing. Stops are never replayed.
        /// </summary>
        public URN PlayingEmote;
        public bool LoopingEmote;
        public bool IsPlaying;

        public bool IsDirty { get; set; }
    }
}
