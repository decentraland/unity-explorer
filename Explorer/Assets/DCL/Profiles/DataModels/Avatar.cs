using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Loading.Components;
using DCL.Utilities.Extensions;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Profiles
{
    public class Avatar
    {
        public const int MAX_EQUIPPED_EMOTES = 10;

        internal readonly HashSet<URN> wearables = new ();

        internal readonly HashSet<string> forceRender = new ();

        internal readonly URN[] emotes = new URN[MAX_EQUIPPED_EMOTES];

        public BodyShape BodyShape { get; internal set; }

        public IReadOnlyCollection<URN> Wearables => wearables;

        public IReadOnlyCollection<string> ForceRender => forceRender;

        /// <summary>
        ///     Each index represents the slot on which the emote is equipped,
        ///     The slot can be unequipped - in this case it will contain `null` URN, it's a valid case
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

        public bool IsEmotesWheelEmpty()
        {
            foreach (URN urn in Emotes)
                if (!urn.IsNullOrEmpty())
                    return false;

            return true;
        }

        public bool IsSameAvatar(Avatar? other)
        {
            if (other == null)
                return false;

            return BodyShape.Equals(other.BodyShape)
                   && wearables.SetEquals(other.wearables)
                   && emotes.EqualsContentInOrder(other.emotes)
                   && forceRender.SetEquals(other.forceRender)
                   && HairColor.Equals(other.HairColor)
                   && EyesColor.Equals(other.EyesColor)
                   && SkinColor.Equals(other.SkinColor);
        }

        public void Clear()
        {
            wearables.Clear();
            forceRender.Clear();

            for (var i = 0; i < emotes.Length; i++)
                emotes[i] = "";

            BodyShape = default(BodyShape);
            EyesColor = default(Color);
            HairColor = default(Color);
            SkinColor = default(Color);
            SkinColor = default(Color);
            BodySnapshotUrl = default(URLAddress);
            FaceSnapshotUrl = default(URLAddress);
        }
    }
}
