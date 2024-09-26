using DCL.Diagnostics;
using DCL.Multiplayer.Profiles.Bunches;
using System;

namespace DCL.Multiplayer.Profiles.RemoveIntentions
{
    public class LogRemoveIntentions : IRemoveIntentions
    {
        private readonly IRemoveIntentions origin;

        public LogRemoveIntentions(IRemoveIntentions origin)
        {
            this.origin = origin;
        }

        public OwnedBunch<RemoveIntention> Bunch()
        {
            OwnedBunch<RemoveIntention> bunch = origin.Bunch();
            ReportHub
               .WithReport(ReportCategory.LIVEKIT)
               .Log($"LogRemoveIntentions: bunch with count {bunch.Collection().Count}");
            return bunch;
        }
    }
}
