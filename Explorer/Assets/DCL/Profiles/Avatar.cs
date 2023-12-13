using CommunicationData.URLHelpers;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Profiles
{
    public class Avatar
    {
        public string BodyShape { get; internal set; }
        public IReadOnlyCollection<string> SharedWearables { get; internal set; }
        public IReadOnlyCollection<string> UniqueWearables { get; internal set; }
        public IReadOnlyCollection<string> ForceRender { get; internal set; }
        public IReadOnlyDictionary<string, Emote> Emotes { get; internal set; }
        public URLAddress FaceSnapshotUrl { get; internal set; }
        public URLAddress BodySnapshotUrl { get; internal set; }
        public Color EyesColor { get; internal set; }
        public Color HairColor { get; internal set; }
        public Color SkinColor { get; internal set; }

        public Avatar() { }

        public Avatar(string bodyShape,
            IReadOnlyCollection<string> sharedWearables,
            IReadOnlyCollection<string> uniqueWearables,
            IReadOnlyCollection<string> forceRender,
            Dictionary<string, Emote> emotes,
            URLAddress faceSnapshotUrl, URLAddress bodySnapshotUrl,
            Color eyesColor, Color hairColor, Color skinColor)
        {
            BodyShape = bodyShape;
            SharedWearables = sharedWearables;
            UniqueWearables = uniqueWearables;
            ForceRender = forceRender;
            Emotes = emotes;
            FaceSnapshotUrl = faceSnapshotUrl;
            BodySnapshotUrl = bodySnapshotUrl;
            EyesColor = eyesColor;
            HairColor = hairColor;
            SkinColor = skinColor;
        }
    }
}
