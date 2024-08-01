using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.AvatarRendering.AvatarShape;
using DCL.AvatarRendering.Emotes;
using DCL.Diagnostics;
using DCL.PerformanceAndDiagnostics.Analytics;
using ECS;
using ECS.Abstract;
using Segment.Serialization;

namespace DCL.Analytics.Systems
{
    [LogCategory(ReportCategory.ANALYTICS)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(AvatarGroup))]
    [UpdateBefore(typeof(CharacterEmoteSystem))]
    public partial class AnalyticsEmotesSystem : BaseUnityLoopSystem
    {
        private readonly IAnalyticsController analytics;

        private readonly IRealmData realmData;
        private readonly Entity playerEntity;

        private string lastEmoteId = string.Empty;

        public AnalyticsEmotesSystem(World world, IAnalyticsController analytics, IRealmData realmData, in Entity playerEntity) : base(world)
        {
            this.analytics = analytics;
            this.realmData = realmData;
            this.playerEntity = playerEntity;
        }

        protected override void Update(float t)
        {
            if (!realmData.Configured) return;

            if (World.TryGet(playerEntity, typeof(CharacterEmoteIntent), out object? intent))
            {
                var emoteIntent = (CharacterEmoteIntent)intent;

                if (emoteIntent.TriggerSource is TriggerSource.REMOTE or TriggerSource.PREVIEW)
                    return;

                if (lastEmoteId != emoteIntent.EmoteId)
                {
                    lastEmoteId = emoteIntent.EmoteId;
                    SendAnalytics(emoteIntent.EmoteId, !lastEmoteId.StartsWith("urn:"));
                }
            }
            else
                lastEmoteId = string.Empty;
        }

        private void SendAnalytics(string id, bool isBase)
        {
            analytics.Track(AnalyticsEvents.Wearables.USED_EMOTE, new JsonObject
            {
                { "item_id", id }, // Id of the item <contract-address>-<item_id>
                { "is_base", isBase }, // if the item is a base emote
                { "name", string.Empty },
                { "emote_index", -1 }, // Index where the emote was placed if the user equipped an emote.
            });
        }
    }
}
