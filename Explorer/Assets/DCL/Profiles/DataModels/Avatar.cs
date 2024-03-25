using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Wearables;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Profiles
{
    public class Avatar
    {
        public BodyShape BodyShape { get; internal set; }

        internal readonly HashSet<URN> wearables = new ();

        internal readonly HashSet<string> forceRender = new ();

        internal readonly Dictionary<string, Emote> emotes = new ();

        public IReadOnlyCollection<URN> Wearables => wearables;

        public IReadOnlyCollection<string> ForceRender => forceRender;
        public IReadOnlyDictionary<string, Emote> Emotes => emotes;
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
        }
    }
}
