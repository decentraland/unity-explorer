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
            this.wearables.Clear();
            this.forceRender.Clear();

            for (var i = 0; i < this.emotes.Length; i++)
                this.emotes[i] = "";

            this.BodyShape = default(BodyShape);
            this.EyesColor = default(Color);
            this.HairColor = default(Color);
            this.SkinColor = default(Color);
            this.SkinColor = default(Color);
            this.BodySnapshotUrl = default(URLAddress);
            this.FaceSnapshotUrl = default(URLAddress);
        }

#if UNITY_EDITOR

        /// <summary>
        /// Fills one of the emote slots of the avatar's profile with an emote URN, if it is not already in the list. If all slots are already filled, the last one will be replaced with the provided URN.
        /// This method is used by editor tools or for debugging, it must never be used in production.
        /// </summary>
        /// <param name="emoteURN">The URN of the emote to add.</param>
        public void AddEmote(URN emoteURN)
        {
            int i = 0;

            for (; i < emotes.Length; ++i)
            {
                if (emotes[i].IsNullOrEmpty())
                    emotes[i] = emoteURN;
                else if (emotes[i] == emoteURN)
                    return;
            }

            if (i == emotes.Length)
                emotes[0] = emoteURN;
        }

#endif
    }
}
