using CommunicationData.URLHelpers;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Profiles
{
    public class Avatar
    {
        public string BodyShape { get; }
        public IReadOnlyCollection<string> SharedWearables { get; }
        public IReadOnlyCollection<string> UniqueWearables { get; }
        public IReadOnlyCollection<string> ForceRender { get; }
        public Dictionary<string, Emote> Emotes { get; }
        public URLAddress FaceSnapshotUrl { get; }
        public URLAddress BodySnapshotUrl { get; }
        public Color EyesColor { get; }
        public Color HairColor { get; }
        public Color SkinColor { get; }

        public Avatar(string bodyShape, IReadOnlyCollection<string> sharedWearables, IReadOnlyCollection<string> uniqueWearables, IReadOnlyCollection<string> forceRender, Dictionary<string, Emote> emotes,
            URLAddress faceSnapshotUrl, URLAddress bodySnapshotUrl, Color eyesColor, Color hairColor, Color skinColor)
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
