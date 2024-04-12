using DCL.Diagnostics;
using System;

namespace DCL.Web3.Identities
{
    public class LogWeb3IdentityCache : IWeb3IdentityCache
    {
        private readonly IWeb3IdentityCache origin;
        private readonly Action<string> log;

        public LogWeb3IdentityCache(IWeb3IdentityCache origin) : this(origin, ReportHub.WithReport(ReportCategory.PROFILE).Log)
        {
        }

        public LogWeb3IdentityCache(IWeb3IdentityCache origin, Action<string> log)
        {
            this.origin = origin;
            this.log = log;
        }

        public void Dispose()
        {
            log("Web3IdentityCache Dispose requested");
            origin.Dispose();
        }

        public IWeb3Identity? Identity
        {
            get
            {
                log("Web3IdentityCache Identity value get requested");
                var value = origin.Identity;
                return value == null
                    ? null
                    : new LogWeb3Identity(value, log);
            }

            set
            {
                log("Web3IdentityCache Identity value set requested");
                origin.Identity = value;
            }
        }

        public void Clear()
        {
            log("Web3IdentityCache Clear requested");
            origin.Clear();
        }
    }
}
