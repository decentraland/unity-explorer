using DCL.Diagnostics;
using System;

namespace DCL.Web3.Identities
{
    public class LogWeb3IdentityCache : IWeb3IdentityCache
    {
        private readonly IWeb3IdentityCache origin;

        public LogWeb3IdentityCache(IWeb3IdentityCache origin)
        {
            this.origin = origin;
        }

        public void Dispose()
        {
            ReportHub
               .WithReport(ReportCategory.PROFILE)
               .Log("Web3IdentityCache Dispose requested");
            origin.Dispose();
        }

        public IWeb3Identity? Identity
        {
            get
            {
                ReportHub
                   .WithReport(ReportCategory.PROFILE)
                   .Log("Web3IdentityCache Identity value get requested");
                var value = origin.Identity;
                return value == null
                    ? null
                    : new LogWeb3Identity(value);
            }

            set
            {
                ReportHub
                   .WithReport(ReportCategory.PROFILE)
                   .Log("Web3IdentityCache Identity value set requested");
                origin.Identity = value;
            }
        }

        public void Clear()
        {
            ReportHub
               .WithReport(ReportCategory.PROFILE)
               .Log("Web3IdentityCache Clear requested");
            origin.Clear();
        }
    }
}
