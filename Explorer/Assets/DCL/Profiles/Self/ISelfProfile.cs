using Cysharp.Threading.Tasks;
using System;
using System.Threading;

namespace DCL.Profiles.Self
{
    /// <summary>Thread-safe</summary>
    public interface ISelfProfile : IDisposable
    {
        SelfProfileModel CurrentProfileSnapshot { get; }

        /// <summary>
        ///     Waits until the current identity's profile is resolved. A failed read is retried once per call.
        /// </summary>
        UniTask<ProfileReadResult> ProfileAsync(CancellationToken ct);

        /// <summary>
        ///     Cancelling the token stops waiting; the deploy itself runs to its end. // TODO can we cancel the deploy itself?
        /// </summary>
        UniTask<ProfileDeployResult> DeployProfileAsync(Profile edited, CancellationToken ct);
    }
}
