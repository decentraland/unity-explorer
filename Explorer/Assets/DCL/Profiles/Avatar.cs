using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Wearables;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Profiles
{
    public class Avatar
    {
        public BodyShape BodyShape { get; internal set; }

        internal readonly HashSet<string> sharedWearables = new ();

        internal readonly HashSet<string> uniqueWearables = new ();

        internal readonly Dictionary<string, Emote> emotes = new ();

        public IReadOnlyCollection<string> SharedWearables => sharedWearables;
        public IReadOnlyCollection<string> UniqueWearables => uniqueWearables;

        public IReadOnlyCollection<string> ForceRender { get; internal set; } = null!;
        public IReadOnlyDictionary<string, Emote> Emotes => emotes;
        public URLAddress FaceSnapshotUrl { get; internal set; }
        public URLAddress BodySnapshotUrl { get; internal set; }
        public Color EyesColor { get; internal set; }
        public Color HairColor { get; internal set; }
        public Color SkinColor { get; internal set; }

        internal Avatar() { }

        public Avatar(BodyShape bodyShape, HashSet<string> sharedWearables, Color eyesColor, Color hairColor, Color skinColor)
        {
            BodyShape = bodyShape;
            this.sharedWearables = sharedWearables;
            EyesColor = eyesColor;
            HairColor = hairColor;
            SkinColor = skinColor;
        }
    }
}
