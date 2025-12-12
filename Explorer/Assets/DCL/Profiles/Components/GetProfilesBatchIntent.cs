using CommunicationData.URLHelpers;
using ECS.StreamableLoading.Common.Components;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine.Pool;

namespace DCL.Profiles
{
    public struct GetProfilesBatchIntent : ILoadingIntention, IEquatable<GetProfilesBatchIntent>, IDisposable
    {
        public readonly HashSet<string> Ids;
        public readonly URLDomain Lambdas;
        public readonly ProfileTier.Kind Tier;

        public CommonLoadingArguments CommonArguments { get; set; }

        public GetProfilesBatchIntent(URLAddress address, URLDomain lambdas, ProfileTier.Kind tier, CancellationTokenSource cancellationTokenSource)
        {
            Ids = HashSetPool<string>.Get();
            CancellationTokenSource = cancellationTokenSource;
            Tier = tier;
            Lambdas = lambdas;

            // It will be repeated only once but the internal request will be repeated according to the special Catalyst related logic
            CommonArguments = new CommonLoadingArguments(address, attempts: 1);
        }

        public void Dispose() =>
            HashSetPool<string>.Release(Ids);

        public CancellationTokenSource CancellationTokenSource { get; }

        public bool Equals(GetProfilesBatchIntent other) =>
            false;

        public override bool Equals(object? obj) =>
            false;

        public override int GetHashCode() =>
            0;
    }

    /// <summary>
    ///     Everything is kept in the system itself => nothing to expose
    /// </summary>
    public readonly struct ProfilesBatchResult { }
}
