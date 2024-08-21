using CommunicationData.URLHelpers;
using DCL.Optimization.PerformanceBudgeting;

namespace DCL.AvatarRendering.Emotes
{
    /// <summary>
    ///  Emotes cache, each implementation should be thread-safe.
    /// </summary>
    public interface IEmoteCache
    {
        bool TryGetEmote(URN urn, out IEmote emote);

        void Set(URN urn, IEmote emote);

        IEmote GetOrAddEmoteByDTO(EmoteDTO emoteDto, bool qualifiedForUnloading = true);

        void Unload(IPerformanceBudget frameTimeBudget);
    }
}
