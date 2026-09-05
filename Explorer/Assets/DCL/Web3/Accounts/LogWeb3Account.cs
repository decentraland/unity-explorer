using DCL.Diagnostics;
using DCL.Web3.Abstract;
using Nethereum.Signer;
using System;
using UnityEngine;

namespace DCL.Web3.Accounts
{
    public class LogWeb3Account : IWeb3Account
    {
        private readonly IWeb3Account origin;

        public LogWeb3Account(IWeb3Account origin)
        {
            this.origin = origin;
        }

        public Web3Address Address
        {
            get
            {
                ReportHub
                   .WithReport(ReportCategory.PROFILE)
                   .Log($"Web3Account Address requested: {origin.Address}");
                return origin.Address;
            }
        }

        public string PrivateKey
        {
            get
            {
                ReportHub
                   .WithReport(ReportCategory.PROFILE)
                   .Log($"Web3Account PrivateKey requested: {origin.PrivateKey}");
                return origin.PrivateKey;
            }
        }

        public string Sign(string message)
        {
            ReportHub
               .WithReport(ReportCategory.PROFILE)
               .Log($"Web3Account Sign requested: {message}");
            string result = origin.Sign(message);
            ReportHub
               .WithReport(ReportCategory.PROFILE)
               .Log($"Web3Account Sign result: {result} for {message}");
            return result;
        }

        public bool Verify(string message, string signature)
        {
            ReportHub
               .WithReport(ReportCategory.PROFILE)
               .Log($"Web3Account Verify requested: {message} with {signature}");
            bool result = origin.Verify(message, signature);
            ReportHub
               .WithReport(ReportCategory.PROFILE)
               .Log($"Web3Account Verify result: {result} for {message} with {signature}");
            return result;
        }
    }
}
