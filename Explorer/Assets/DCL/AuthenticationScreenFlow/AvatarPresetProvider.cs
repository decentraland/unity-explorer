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
        private static readonly Preset[] PRESETS =
        {
            new (
                BodyShape.FEMALE,
                new URN[]
                {
                    "urn:decentraland:off-chain:base-avatars:standard_hair",
                    "urn:decentraland:off-chain:base-avatars:f_eyes_00",
                    "urn:decentraland:off-chain:base-avatars:f_eyebrows_00",
                    "urn:decentraland:off-chain:base-avatars:f_mouth_02",
                    "urn:decentraland:off-chain:base-avatars:f_sweater",
                    "urn:decentraland:off-chain:base-avatars:f_jeans",
                    "urn:decentraland:off-chain:base-avatars:bun_shoes",
                },
                new (0.109804f, 0.109804f, 0.109804f),
                new (0.596078f, 0.372549f, 0.219608f),
                new (0.866667f, 0.694118f, 0.560784f)),
            new (
                BodyShape.FEMALE,
                new URN[]
                {
                    "urn:decentraland:off-chain:base-avatars:rasta",
                    "urn:decentraland:off-chain:base-avatars:eyes_08",
                    "urn:decentraland:off-chain:base-avatars:eyebrows_09",
                    "urn:decentraland:off-chain:base-avatars:mouth_01",
                    "urn:decentraland:off-chain:base-avatars:m_sweater_02",
                    "urn:decentraland:off-chain:base-avatars:swim_short",
                    "urn:decentraland:off-chain:base-avatars:m_greenflipflops",
                },
                new (0.749020f, 0.619608f, 0.352941f),
                new (0.486275f, 0.286275f, 0.086275f),
                new (0.239216f, 0.133333f, 0.086275f)),
            new (
                BodyShape.FEMALE,
                new URN[]
                {
                    "urn:decentraland:off-chain:base-avatars:tall_front_01",
                    "urn:decentraland:off-chain:base-avatars:f_eyes_04",
                    "urn:decentraland:off-chain:base-avatars:f_eyebrows_06",
                    "urn:decentraland:off-chain:base-avatars:f_mouth_00",
                    "urn:decentraland:off-chain:base-avatars:brown_sleveless_dress",
                    "urn:decentraland:off-chain:base-avatars:f_brown_skirt",
                    "urn:decentraland:off-chain:base-avatars:citycomfortableshoes",
                },
                new (1.000000f, 0.000000f, 0.772549f),
                new (1.000000f, 0.000000f, 0.772549f),
                new (0.674510f, 1.000000f, 0.988235f)),
            new (
                BodyShape.FEMALE,
                new URN[]
                {
                    "urn:decentraland:off-chain:base-avatars:curly_hair",
                    "urn:decentraland:off-chain:base-avatars:eyes_03",
                    "urn:decentraland:off-chain:base-avatars:eyebrows_00",
                    "urn:decentraland:off-chain:base-avatars:f_mouth_03",
                    "urn:decentraland:off-chain:base-avatars:f_body_swimsuit",
                    "urn:decentraland:off-chain:base-avatars:f_yoga_trousers",
                    "urn:decentraland:off-chain:base-avatars:espadrilles",
                    "urn:decentraland:off-chain:base-avatars:dcl_watch",
                    "urn:decentraland:off-chain:base-avatars:pink_gem_earring",
                },
                new (0.525490f, 0.376471f, 0.258824f),
                new (1.000000f, 0.745098f, 0.149020f),
                new (0.949020f, 0.760784f, 0.647059f)),
            new (
                BodyShape.FEMALE,
                new URN[]
                {
                    "urn:decentraland:off-chain:base-avatars:double_bun",
                    "urn:decentraland:off-chain:base-avatars:f_eyes_10",
                    "urn:decentraland:off-chain:base-avatars:f_eyebrows_03",
                    "urn:decentraland:off-chain:base-avatars:f_mouth_02",
                    "urn:decentraland:off-chain:base-avatars:black_top",
                    "urn:decentraland:off-chain:base-avatars:jean_shorts",
                    "urn:decentraland:off-chain:base-avatars:sport_colored_shoes",
                    "urn:decentraland:off-chain:base-avatars:cyclope",
                    "urn:decentraland:off-chain:base-avatars:dcl_watch",
                },
                new (0.674510f, 1.000000f, 0.737255f),
                new (0.674510f, 1.000000f, 0.737255f),
                new (0.866667f, 0.694118f, 0.560784f)),
            new (
                BodyShape.FEMALE,
                new URN[]
                {
                    "urn:decentraland:off-chain:base-avatars:hair_anime_01",
                    "urn:decentraland:off-chain:base-avatars:f_eyes_08",
                    "urn:decentraland:off-chain:base-avatars:f_eyebrows_02",
                    "urn:decentraland:off-chain:base-avatars:f_mouth_05",
                    "urn:decentraland:off-chain:base-avatars:school_shirt",
                    "urn:decentraland:off-chain:base-avatars:f_school_skirt",
                    "urn:decentraland:off-chain:base-avatars:schoolshoes",
                    "urn:decentraland:off-chain:base-avatars:blue_star_earring",
                },
                new (0.219608f, 0.486275f, 0.690196f),
                new (0.109804f, 0.109804f, 0.109804f),
                new (1.000000f, 0.894118f, 0.776471f)),
            new (
                BodyShape.MALE,
                new URN[]
                {
                    "urn:decentraland:off-chain:base-avatars:green_hoodie",
                    "urn:decentraland:off-chain:base-avatars:brown_pants",
                    "urn:decentraland:off-chain:base-avatars:sneakers",
                    "urn:decentraland:off-chain:base-avatars:casual_hair_01",
                    "urn:decentraland:off-chain:base-avatars:beard",
                },
                new (0.525490f, 0.380392f, 0.258824f),
                new (0.235294f, 0.129412f, 0.043137f),
                new (0.490196f, 0.364706f, 0.278431f)),
            new (
                BodyShape.MALE,
                new URN[]
                {
                    "urn:decentraland:off-chain:base-avatars:yellow_tshirt",
                    "urn:decentraland:off-chain:base-avatars:soccer_pants",
                    "urn:decentraland:off-chain:base-avatars:comfy_sport_sandals",
                    "urn:decentraland:off-chain:base-avatars:keanu_hair",
                    "urn:decentraland:off-chain:base-avatars:granpa_beard",
                },
                new (0.686275f, 0.772549f, 0.780392f),
                new (0.596078f, 0.372549f, 0.215686f),
                new (0.490196f, 0.364706f, 0.278431f)),
            new (
                BodyShape.MALE,
                new URN[]
                {
                    "urn:decentraland:off-chain:base-avatars:turtle_neck_sweater",
                    "urn:decentraland:off-chain:base-avatars:kilt",
                    "urn:decentraland:off-chain:base-avatars:m_mountainshoes.glb",
                    "urn:decentraland:off-chain:base-avatars:keanu_hair",
                    "urn:decentraland:off-chain:base-avatars:full_beard",
                },
                new (0.125490f, 0.701961f, 0.964706f),
                new (0.549020f, 0.125490f, 0.078431f),
                new (0.490196f, 0.364706f, 0.278431f)),
            new (
                BodyShape.MALE,
                new URN[]
                {
                    "urn:decentraland:off-chain:base-avatars:sleeveless_punk_shirt",
                    "urn:decentraland:off-chain:base-avatars:trash_jean",
                    "urn:decentraland:off-chain:base-avatars:citycomfortableshoes",
                    "urn:decentraland:off-chain:base-avatars:punk",
                    "urn:decentraland:off-chain:base-avatars:horseshoe_beard",
                    "urn:decentraland:off-chain:base-avatars:thunder_earring",
                },
                new (0.125490f, 0.701961f, 0.964706f),
                new (0.925490f, 0.909804f, 0.886275f),
                new (0.490196f, 0.364706f, 0.278431f)),
            new (
                BodyShape.MALE,
                new URN[]
                {
                    "urn:decentraland:off-chain:base-avatars:striped_pijama",
                    "urn:decentraland:off-chain:base-avatars:pijama_pants",
                    "urn:decentraland:off-chain:base-avatars:bear_slippers",
                    "urn:decentraland:off-chain:base-avatars:semi_bold",
                    "urn:decentraland:off-chain:base-avatars:mouth_04",
                },
                new (0.125490f, 0.701961f, 0.964706f),
                new (0.925490f, 0.909804f, 0.886275f),
                new (0.490196f, 0.364706f, 0.278431f)),
            new (
                BodyShape.MALE,
                new URN[]
                {
                    "urn:decentraland:off-chain:base-avatars:red_square_shirt",
                    "urn:decentraland:off-chain:base-avatars:brown_pants",
                    "urn:decentraland:off-chain:base-avatars:sneakers",
                    "urn:decentraland:off-chain:base-avatars:slicked_hair",
                    "urn:decentraland:off-chain:base-avatars:eyes_08",
                    "urn:decentraland:off-chain:base-avatars:punk_piercing",
                },
                new (0.125490f, 0.701961f, 0.964706f),
                new (1.000000f, 0.745098f, 0.156863f),
                new (0.490196f, 0.364706f, 0.278431f)),
        };

        private static readonly Dictionary<BodyShape, List<int>> INDICES_BY_BODY_SHAPE = BuildIndices();

        private readonly Dictionary<BodyShape, int> lastPickedByBodyShape = new ();

        public Avatar Next(BodyShape bodyShape)
        {
            List<int> candidates = INDICES_BY_BODY_SHAPE[bodyShape];
            int last = lastPickedByBodyShape.TryGetValue(bodyShape, out int previous) ? previous : -1;

            int picked = candidates[Random.Range(0, candidates.Count)];

            while (candidates.Count > 1 && picked == last)
                picked = candidates[Random.Range(0, candidates.Count)];

            lastPickedByBodyShape[bodyShape] = picked;

            return PRESETS[picked].ToAvatar();
        }

        private static Dictionary<BodyShape, List<int>> BuildIndices()
        {
            var indices = new Dictionary<BodyShape, List<int>>();

            for (var i = 0; i < PRESETS.Length; i++)
            {
                BodyShape bodyShape = PRESETS[i].BodyShape;

                if (!indices.TryGetValue(bodyShape, out List<int>? bucket))
                {
                    bucket = new List<int>();
                    indices[bodyShape] = bucket;
                }

                bucket.Add(i);
            }

            return indices;
        }

        private readonly struct Preset
        {
            public readonly BodyShape BodyShape;

            private readonly URN[] wearables;
            private readonly Color eyesColor;
            private readonly Color hairColor;
            private readonly Color skinColor;

            public Preset(BodyShape bodyShape, URN[] wearables, Color eyesColor, Color hairColor, Color skinColor)
            {
                BodyShape = bodyShape;
                this.wearables = wearables;
                this.eyesColor = eyesColor;
                this.hairColor = hairColor;
                this.skinColor = skinColor;
            }

            public Avatar ToAvatar() =>
                new (BodyShape, new HashSet<URN>(wearables), eyesColor, hairColor, skinColor);
        }
    }
}
