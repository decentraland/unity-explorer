using CommunicationData.URLHelpers;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Profiles
{
    public class Avatar
    {
        public string BodyShape { get; set; }
        public HashSet<string> SharedWearables { get; set; }
        public HashSet<string> UniqueWearables { get; set; }
        public HashSet<string> ForceRender { get; set; }
        public Dictionary<string, Emote> Emotes { get; set; }
        public URLAddress FaceSnapshotUrl { get; set; }
        public URLAddress BodySnapshotUrl { get; set; }
        public Color EyesColor { get; set; }
        public Color HairColor { get; set; }
        public Color SkinColor { get; set; }
    }
}
