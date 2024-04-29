using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Wearables;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Profiles
{
    public class Avatar
    {
        public const int MAX_EQUIPPED_EMOTES = 10;

        public BodyShape BodyShape { get; internal set; }

        internal readonly HashSet<URN> wearables = new ();

        internal readonly HashSet<string> forceRender = new ();

        internal readonly URN[] emotes = new URN[MAX_EQUIPPED_EMOTES];

        public IReadOnlyCollection<URN> Wearables => wearables;

        public IReadOnlyCollection<string> ForceRender => forceRender;
        /// <summary>
        ///     Each index represents the slot on which the emote is equipped
        /// </summary>
        public IReadOnlyList<URN> Emotes => emotes;
        public URLAddress FaceSnapshotUrl { get; internal set; }
        public URLAddress BodySnapshotUrl { get; internal set; }
        public Color EyesColor { get; internal set; }
        public Color HairColor { get; internal set; }
        public Color SkinColor { get; internal set; }

        internal Avatar() { }

        public Avatar(BodyShape bodyShape, HashSet<URN> wearables, Color eyesColor, Color hairColor, Color skinColor)
        {
            BodyShape = bodyShape;
            this.wearables = wearables;
            EyesColor = eyesColor;
            HairColor = hairColor;
            SkinColor = skinColor;

            FaceSnapshotUrl = URLAddress.EMPTY;
            BodySnapshotUrl = URLAddress.EMPTY;
        }
    }
}
