using DCL.Diagnostics;
using Nethereum.Signer;
using System;
using UnityEngine;

namespace DCL.Web3.Accounts
{
    public class LogWeb3Account : IWeb3Account, IEthKeyOwner
    {
        private readonly IWeb3Account origin;
        private readonly Action<string> log;

        public LogWeb3Account(IWeb3Account origin) : this(origin, ReportHub.WithReport(ReportCategory.PROFILE).Log) { }

        public LogWeb3Account(IWeb3Account origin, Action<string> log)
        {
            this.origin = origin;
            this.log = log;
        }

        public Web3Address Address
        {
            get
            {
                log($"Web3Account Address requested: {origin.Address}");
                return origin.Address;
            }
        }

        public string Sign(string message)
        {
            log($"Web3Account Sign requested: {message}");
            string result = origin.Sign(message);
            log($"Web3Account Sign result: {result} for {message}");
            return result;
        }

        public bool Verify(string message, string signature)
        {
            log($"Web3Account Verify requested: {message} with {signature}");
            bool result = origin.Verify(message, signature);
            log($"Web3Account Verify result: {result} for {message} with {signature}");
            return result;
        }

        public EthECKey Key => origin is IEthKeyOwner ethKeyOwner
            ? ethKeyOwner.Key
            : throw new InvalidOperationException("The origin is not an IEthKeyOwner");
    }
}
