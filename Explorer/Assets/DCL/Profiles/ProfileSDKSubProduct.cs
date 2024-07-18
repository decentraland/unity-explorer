using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Wearables;
using DCL.ECSComponents;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Profiles
{
    /// <summary>
    ///     Contains data related to the Player needed for SDK only
    /// </summary>
    public class ProfileSDKSubProduct : IDirtyMarker
    {
        public AvatarSubProduct Avatar { get; } = new ();

        public bool IsDirty { get; set; }

        public string? Name { get; internal set; }

        public string? UserId { get; private set; }

        public bool HasConnectedWeb3 { get; private set; }

        public void OverrideWith(Profile profile)
        {
            Avatar.OverrideWith(profile.Avatar);

            Name = profile.Name;
            UserId = profile.UserId;
            HasConnectedWeb3 = profile.HasConnectedWeb3;

            IsDirty = true;
        }

        public class AvatarSubProduct
        {
            private readonly URN[] emotes = new URN[Profiles.Avatar.MAX_EQUIPPED_EMOTES];

            private readonly List<URN> wearables = new ();
            public BodyShape BodyShape { get; private set; }

            public Color EyesColor { get; private set; }
            public Color HairColor { get; private set; }
            public Color SkinColor { get; private set; }

            public IReadOnlyList<URN> Emotes => emotes;
            public IReadOnlyList<URN> Wearables => wearables;

            public void OverrideWith(Avatar avatar)
            {
                BodyShape = avatar.BodyShape;

                EyesColor = avatar.EyesColor;
                HairColor = avatar.HairColor;
                SkinColor = avatar.SkinColor;

                wearables.Clear();
                wearables.AddRange(avatar.Wearables);

                avatar.emotes.CopyTo(emotes, 0);
            }
        }
    }
}
