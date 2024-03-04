using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Wearables;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Profiles
{
    public class Avatar
    {
        public BodyShape BodyShape { get; internal set; }

        internal readonly HashSet<URN> sharedWearables = new ();

        internal readonly HashSet<URN> uniqueWearables = new ();

        internal readonly HashSet<string> forceRender = new ();

        internal readonly Dictionary<string, Emote> emotes = new ();

        public IReadOnlyCollection<URN> SharedWearables => sharedWearables;
        public IReadOnlyCollection<URN> UniqueWearables => uniqueWearables;

        public IReadOnlyCollection<string> ForceRender => forceRender;
        public IReadOnlyDictionary<string, Emote> Emotes => emotes;
        public URLAddress FaceSnapshotUrl { get; internal set; }
        public URLAddress BodySnapshotUrl { get; internal set; }
        public Color EyesColor { get; internal set; }
        public Color HairColor { get; internal set; }
        public Color SkinColor { get; internal set; }

        internal Avatar() { }

        public Avatar(BodyShape bodyShape, HashSet<URN> sharedWearables, Color eyesColor, Color hairColor, Color skinColor)
        {
            BodyShape = bodyShape;
            this.sharedWearables = sharedWearables;
            EyesColor = eyesColor;
            HairColor = hairColor;
            SkinColor = skinColor;
        }
    }
}
