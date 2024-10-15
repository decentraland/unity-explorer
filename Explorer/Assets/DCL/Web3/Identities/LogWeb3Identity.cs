using DCL.Diagnostics;
using DCL.Web3.Abstract;
using DCL.Web3.Accounts;
using DCL.Web3.Chains;
using System;
using UnityEngine;

namespace DCL.Web3.Identities
{
    public class LogWeb3Identity : IWeb3Identity
    {
        private readonly IWeb3Identity origin;

        public LogWeb3Identity(IWeb3Identity origin)
        {
            this.origin = origin;
        }

        public void Dispose()
        {
            origin.Dispose();
        }

        public Web3Address Address
        {
            get
            {
                ReportHub
                    .WithReport(ReportCategory.PROFILE)
                    .Log($"Web3Identity Address requested: {origin.Address}");
                return origin.Address;
            }
        }

        public DateTime Expiration
        {
            get
            {
                ReportHub
                    .WithReport(ReportCategory.PROFILE)
                    .Log($"Web3Identity Expiration requested: {origin.Expiration}");
                return origin.Expiration;
            }
        }

        public IWeb3Account EphemeralAccount => new LogWeb3Account(origin.EphemeralAccount);

        public bool IsExpired
        {
            get
            {
                ReportHub
                    .WithReport(ReportCategory.PROFILE)
                    .Log($"Web3Identity IsExpired requested: {origin.IsExpired}");
                return origin.IsExpired;
            }
        }

        public AuthChain AuthChain
        {
            get
            {
                ReportHub
                    .WithReport(ReportCategory.PROFILE)
                    .Log($"Web3Identity AuthChain requested: {origin.AuthChain}");
                return origin.AuthChain;
            }
        }

        public AuthChain Sign(string entityId)
        {
            ReportHub
                .WithReport(ReportCategory.PROFILE)
                .Log($"Web3Identity Sign requested: entity {entityId}");
            var result = origin.Sign(entityId);
            ReportHub
                .WithReport(ReportCategory.PROFILE)
                .Log($"Web3Identity Sign result: {result} for entity {entityId}");
            return result;
        }
    }
}
