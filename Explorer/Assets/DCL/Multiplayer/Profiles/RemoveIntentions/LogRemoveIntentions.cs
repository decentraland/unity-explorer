using DCL.Diagnostics;
using DCL.Multiplayer.Profiles.Bunches;
using System;

namespace DCL.Multiplayer.Profiles.RemoveIntentions
{
    public class LogRemoveIntentions : IRemoveIntentions
    {
        private readonly IRemoveIntentions origin;
        private readonly Action<string> log;

        public LogRemoveIntentions(IRemoveIntentions origin) : this(origin, ReportHub.WithReport(ReportCategory.LIVEKIT).Log)
        {
        }

        public LogRemoveIntentions(IRemoveIntentions origin, Action<string> log)
        {
            this.origin = origin;
            this.log = log;
        }

        public OwnedBunch<RemoveIntention> Bunch()
        {
            OwnedBunch<RemoveIntention> bunch = origin.Bunch();
            log($"LogRemoveIntentions: bunch with count {bunch.Collection().Count}");
            return bunch;
        }
    }
}
