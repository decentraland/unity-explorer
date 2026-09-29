using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Loading.Components;
using System.Collections.Generic;
using UnityEngine;
using Avatar = DCL.Profiles.Avatar;
using Random = UnityEngine.Random;

namespace DCL.AuthenticationScreenFlow
{
    public class AvatarPresetProvider
    {
        private static readonly Preset[] FEMALE_PRESETS =
        {
            new (
                new URN[]
                {
                    "urn:decentraland:off-chain:base-avatars:eyebrows_01",
                    "urn:decentraland:off-chain:base-avatars:mouth_02",
                    "urn:decentraland:off-chain:base-avatars:eyes_08",
                    "urn:decentraland:off-chain:base-avatars:f_m_sandals",
                    "urn:decentraland:off-chain:base-avatars:pony_tail",
                    "urn:decentraland:off-chain:base-avatars:brown_sleveless_dress",
                    "urn:decentraland:off-chain:base-avatars:kilt",
                    "urn:decentraland:off-chain:base-avatars:pearls_earring",
                    "urn:decentraland:off-chain:base-avatars:cord_bracelet",
                },
                new (0.086275f, 0.117647f, 0.376471f),
                new (0.945098f, 0.745098f, 0.317647f),
                new (0.886275f, 0.713725f, 0.564706f)),
            new (
                new URN[]
                {
                    "urn:decentraland:off-chain:base-avatars:cord_bracelet",
                    "urn:decentraland:off-chain:base-avatars:hair_anime_01",
                    "urn:decentraland:off-chain:base-avatars:f_eyes_08",
                    "urn:decentraland:off-chain:base-avatars:blue_star_earring",
                    "urn:decentraland:off-chain:base-avatars:school_shirt",
                    "urn:decentraland:off-chain:base-avatars:f_school_skirt",
                    "urn:decentraland:off-chain:base-avatars:ruby_blue_loafer",
                    "urn:decentraland:off-chain:base-avatars:eyebrows_09",
                    "urn:decentraland:off-chain:base-avatars:f_mouth_05",
                },
                new (0.168627f, 0.035294f, 0.019608f),
                new (0.141176f, 0.141176f, 0.137255f),
                new (0.886275f, 0.713725f, 0.564706f)),
            new (
                new URN[]
                {
                    "urn:decentraland:off-chain:base-avatars:cord_bracelet",
                    "urn:decentraland:off-chain:base-avatars:hair_anime_01",
                    "urn:decentraland:off-chain:base-avatars:eyebrows_09",
                    "urn:decentraland:off-chain:base-avatars:mouth_02",
                    "urn:decentraland:off-chain:base-avatars:pink_gem_earring",
                    "urn:decentraland:off-chain:base-avatars:f_red_elegant_jacket",
                    "urn:decentraland:off-chain:base-avatars:ruby_red_loafer",
                    "urn:decentraland:off-chain:base-avatars:f_capris",
                    "urn:decentraland:off-chain:base-avatars:eyes_15",
                },
                new (0.168627f, 0.035294f, 0.019608f),
                new (0.435294f, 0.003922f, 0.039216f),
                new (0.501961f, 0.333333f, 0.188235f)),
            new (
                new URN[]
                {
                    "urn:decentraland:off-chain:base-avatars:f_eyebrows_07",
                    "urn:decentraland:off-chain:base-avatars:f_mouth_08",
                    "urn:decentraland:off-chain:base-avatars:eyes_01",
                    "urn:decentraland:off-chain:base-avatars:slicked_hair",
                    "urn:decentraland:off-chain:base-avatars:f_skull_earring",
                    "urn:decentraland:off-chain:base-avatars:black_sun_glasses",
                    "urn:decentraland:off-chain:base-avatars:cord_bracelet",
                    "urn:decentraland:off-chain:base-avatars:citycomfortableshoes",
                    "urn:decentraland:off-chain:base-avatars:trash_jean",
                    "urn:decentraland:off-chain:base-avatars:black_top",
                },
                new (0.262745f, 0.262745f, 0.262745f),
                new (0.180392f, 0.172549f, 0.172549f),
                new (0.772549f, 0.509804f, 0.282353f)),
        };

        private static readonly Preset[] MALE_PRESETS =
        {
            new (
                new URN[]
                {
                    "urn:decentraland:off-chain:base-avatars:crocs",
                    "urn:decentraland:off-chain:base-avatars:f_short_colored_leggins",
                    "urn:decentraland:off-chain:base-avatars:mouth_07",
                    "urn:decentraland:off-chain:base-avatars:eyes_21",
                    "urn:decentraland:off-chain:base-avatars:modern_hair",
                    "urn:decentraland:off-chain:base-avatars:yellow_tshirt",
                    "urn:decentraland:off-chain:base-avatars:square_earring",
                    "urn:decentraland:off-chain:base-avatars:dcl_watch",
                    "urn:decentraland:off-chain:base-avatars:retro_sunglasses",
                },
                new (0.231373f, 0.141176f, 0.050980f),
                new (0.850980f, 0.313725f, 0.109804f),
                new (0.764706f, 0.549020f, 0.443137f)),
            new (
                new URN[]
                {
                    "urn:decentraland:off-chain:base-avatars:f_eyes_02",
                    "urn:decentraland:off-chain:base-avatars:f_mouth_02",
                    "urn:decentraland:off-chain:base-avatars:curtained_hair",
                    "urn:decentraland:off-chain:base-avatars:classic_shoes",
                    "urn:decentraland:off-chain:base-avatars:cord_bracelet",
                    "urn:decentraland:off-chain:base-avatars:toruspiercing",
                    "urn:decentraland:off-chain:base-avatars:green_square_shirt",
                    "urn:decentraland:off-chain:base-avatars:f_short_blue_jeans",
                },
                new (0.231373f, 0.141176f, 0.050980f),
                new (0.035294f, 0.262745f, 0.007843f),
                new (0.556863f, 0.439216f, 0.313725f)),
            new (
                new URN[]
                {
                    "urn:decentraland:off-chain:base-avatars:semi_afro",
                    "urn:decentraland:off-chain:base-avatars:eyes_01",
                    "urn:decentraland:off-chain:base-avatars:lincoln_beard",
                    "urn:decentraland:off-chain:base-avatars:mouth_05",
                    "urn:decentraland:off-chain:base-avatars:sport_jacket",
                    "urn:decentraland:off-chain:base-avatars:grey_joggers",
                    "urn:decentraland:off-chain:base-avatars:punk_piercing",
                    "urn:decentraland:off-chain:base-avatars:sport_colored_shoes",
                },
                new (0.231373f, 0.141176f, 0.050980f),
                new (0.231373f, 0.129412f, 0.090196f),
                new (0.415686f, 0.286275f, 0.176471f)),
            new (
                new URN[]
                {
                    "urn:decentraland:off-chain:base-avatars:f_eyes_08",
                    "urn:decentraland:off-chain:base-avatars:mouth_05",
                    "urn:decentraland:off-chain:base-avatars:full_beard",
                    "urn:decentraland:off-chain:base-avatars:punk",
                    "urn:decentraland:off-chain:base-avatars:eyebrows_12",
                    "urn:decentraland:off-chain:base-avatars:aviatorstyle",
                    "urn:decentraland:off-chain:base-avatars:black_glove",
                    "urn:decentraland:off-chain:base-avatars:sleeveless_punk_shirt",
                    "urn:decentraland:off-chain:base-avatars:distressed_black_jeans",
                    "urn:decentraland:off-chain:base-avatars:citycomfortableshoes",
                    "urn:decentraland:off-chain:base-avatars:punk_piercing",
                },
                new (0.168627f, 0.035294f, 0.019608f),
                new (0.141176f, 0.141176f, 0.137255f),
                new (0.886275f, 0.713725f, 0.564706f)),
        };

        private int lastIndex = -1;

        public Avatar Next(BodyShape bodyShape)
        {
            Preset[] presets = PresetsFor(bodyShape);

            int index = Random.Range(0, presets.Length);

            if (index == lastIndex)
                index = (index + 1) % presets.Length;

            lastIndex = index;

            return presets[index].ToAvatar(bodyShape);
        }

        public Avatar Get(BodyShape bodyShape, int slot)
        {
            Preset[] presets = PresetsFor(bodyShape);

            int index = ((slot % presets.Length) + presets.Length) % presets.Length;
            lastIndex = index;

            return presets[index].ToAvatar(bodyShape);
        }

        private static Preset[] PresetsFor(BodyShape bodyShape) =>
            bodyShape.Equals(BodyShape.FEMALE) ? FEMALE_PRESETS : MALE_PRESETS;

        private readonly struct Preset
        {
            private readonly URN[] wearables;
            private readonly Color eyesColor;
            private readonly Color hairColor;
            private readonly Color skinColor;

            public Preset(URN[] wearables, Color eyesColor, Color hairColor, Color skinColor)
            {
                this.wearables = wearables;
                this.eyesColor = eyesColor;
                this.hairColor = hairColor;
                this.skinColor = skinColor;
            }

            public Avatar ToAvatar(BodyShape bodyShape) =>
                new (bodyShape, new HashSet<URN>(wearables), eyesColor, hairColor, skinColor);
        }
    }
}
