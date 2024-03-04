using DCL.Diagnostics;
using DCL.Web3.Accounts;
using DCL.Web3.Chains;
using System;
using UnityEngine;

namespace DCL.Web3.Identities
{
    public class LogWeb3Identity : IWeb3Identity
    {
        private readonly IWeb3Identity origin;
        private readonly Action<string> log;

        public LogWeb3Identity(IWeb3Identity origin) : this(origin, ReportHub.WithReport(ReportCategory.PROFILE).Log) { }

        public LogWeb3Identity(IWeb3Identity origin, Action<string> log)
        {
            this.origin = origin;
            this.log = log;
        }

        public void Dispose()
        {
            origin.Dispose();
        }

        public Web3Address Address
        {
            get
            {
                log($"Web3Identity Address requested: {origin.Address}");
                return origin.Address;
            }
        }

        public DateTime Expiration
        {
            get
            {
                log($"Web3Identity Expiration requested: {origin.Expiration}");
                return origin.Expiration;
            }
        }

        public IWeb3Account EphemeralAccount => new LogWeb3Account(origin.EphemeralAccount, log);

        public bool IsExpired
        {
            get
            {
                log($"Web3Identity IsExpired requested: {origin.IsExpired}");
                return origin.IsExpired;
            }
        }

        public AuthChain AuthChain
        {
            get
            {
                log($"Web3Identity AuthChain requested: {origin.AuthChain}");
                return origin.AuthChain;
            }
        }

        public AuthChain Sign(string entityId)
        {
            log($"Web3Identity Sign requested: entity {entityId}");
            var result = origin.Sign(entityId);
            log($"Web3Identity Sign result: {result} for entity {entityId}");
            return result;
        }
    }
}
