using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using Newtonsoft.Json;
using UnityEngine;

// ReSharper disable InconsistentNaming
namespace Data
{
    /// <summary>
    /// One animation of a social emote: the clip each armature plays, whether it loops and the audio
    /// file it plays. The page passes it as the <c>socialEmote</c> option, and an emote's outcomes come
    /// in the same shape. Wire format of <c>SocialEmoteAnimation</c> in @dcl/schemas, ADR-287:
    /// https://adr.decentraland.org/adr/ADR-287
    /// </summary>
    [Serializable]
    public class SocialEmoteAnimation
    {
        public string title;
        public bool loop;
        public string audio;
        public EmoteClip Armature;
        public EmoteClip Armature_Other;
        public EmoteClip Armature_Prop;

        [CanBeNull] public string AvatarAnimation => NonEmpty(Armature?.animation);

        [CanBeNull] public string OtherAnimation => NonEmpty(Armature_Other?.animation);

        [CanBeNull] public string PropAnimation => NonEmpty(Armature_Prop?.animation);

        /// <summary>
        /// The option as the page wrote it, or null when it is not a social emote animation: invalid
        /// JSON, or no clip for the avatar.
        /// </summary>
        [CanBeNull]
        public static SocialEmoteAnimation FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;

            try
            {
                var animation = JsonConvert.DeserializeObject<SocialEmoteAnimation>(json);
                return animation?.AvatarAnimation != null ? animation : null;
            }
            catch (JsonException e)
            {
                Debug.LogWarning($"Invalid socialEmote [{json}]: {e.Message}");
                return null;
            }
        }

        public string ToJson() =>
            JsonConvert.SerializeObject(this, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });

        /// <summary>
        /// The animation an emote plays: none unless it is social, then <paramref name="chosen"/> if the
        /// page chose one, or the outcome at <paramref name="randomIndex"/>.
        /// </summary>
        [CanBeNull]
        public static SocialEmoteAnimation Resolve([CanBeNull] SocialEmoteAnimation[] outcomes,
            [CanBeNull] SocialEmoteAnimation chosen, int randomIndex)
        {
            if (outcomes is not { Length: > 0 }) return null;

            return chosen ?? outcomes[Mathf.Clamp(randomIndex, 0, outcomes.Length - 1)];
        }

        public static SocialEmoteAnimation FromOutcome(OutcomeGroup outcome) =>
            new()
            {
                title = outcome.title,
                loop = outcome.loop,
                audio = outcome.audio,
                Armature = outcome.clips?.Armature,
                Armature_Other = outcome.clips?.Armature_Other,
                Armature_Prop = outcome.clips?.Armature_Prop,
            };

        /// <summary>
        /// Which of the emote file's clips each armature plays, by index, -1 for none. A clip named
        /// exactly as the animation wins; otherwise the first whose name starts with it, as the Babylon
        /// preview matches them. The avatar's prefix also starts its other avatar's clip
        /// (<c>X_Avatar</c>, <c>X_AvatarOther</c>), so the avatar never takes a clip another armature
        /// claims.
        /// </summary>
        public (int avatar, int other, int prop) SelectClips(IReadOnlyList<string> clipNames)
        {
            var other = FindClip(clipNames, OtherAnimation, null, null);
            var prop = FindClip(clipNames, PropAnimation, OtherAnimation, null);
            var avatar = FindClip(clipNames, AvatarAnimation, OtherAnimation, PropAnimation);

            return (avatar, other, prop);
        }

        private static int FindClip(IReadOnlyList<string> clipNames, [CanBeNull] string animation,
            [CanBeNull] string excludedPrefix, [CanBeNull] string otherExcludedPrefix)
        {
            if (animation == null) return -1;

            for (var i = 0; i < clipNames.Count; i++)
            {
                if (string.Equals(clipNames[i], animation, StringComparison.OrdinalIgnoreCase)) return i;
            }

            for (var i = 0; i < clipNames.Count; i++)
            {
                var name = clipNames[i];

                if (name.StartsWith(animation, StringComparison.OrdinalIgnoreCase)
                    && !StartsWith(name, excludedPrefix)
                    && !StartsWith(name, otherExcludedPrefix))
                    return i;
            }

            return -1;
        }

        private static bool StartsWith(string name, [CanBeNull] string prefix) =>
            prefix != null && name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);

        [CanBeNull]
        private static string NonEmpty([CanBeNull] string value) => string.IsNullOrEmpty(value) ? null : value;
    }

    [Serializable]
    public class EmoteClip
    {
        public string animation;
    }

    /// <summary>
    /// The clip a social emote plays while it waits for someone to join. Wire format of
    /// <c>StartAnimation</c>, ADR-287.
    /// </summary>
    [Serializable]
    public class StartAnimation
    {
        public bool loop;
        public string audio;
        public EmoteClip Armature;
        public EmoteClip Armature_Prop;
    }

    /// <summary>
    /// One way a social emote can end, with the clips both avatars and the prop play. Wire format of
    /// <c>OutcomeGroup</c>, ADR-287.
    /// </summary>
    [Serializable]
    public class OutcomeGroup
    {
        public string title;
        public bool loop;
        public string audio;
        public OutcomeClips clips;
    }

    [Serializable]
    public class OutcomeClips
    {
        public EmoteClip Armature;
        public EmoteClip Armature_Other;
        public EmoteClip Armature_Prop;
    }
}
