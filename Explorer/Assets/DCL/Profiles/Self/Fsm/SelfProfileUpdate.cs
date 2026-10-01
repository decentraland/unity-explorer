using DCL.Utility.Types;

namespace DCL.Profiles.Self
{
    public readonly partial struct SelfProfileModel
    {
        // Reasons a message is ignored. Constants only: the runtime logs the message and the model next to them,
        // which is enough to reconstruct the case without allocating.
        private const string IDENTITY_ALREADY_CURRENT = "the identity is already current";
        private const string NO_IDENTITY_TO_CLEAR = "there is no identity to clear";
        private const string NO_IDENTITY_TO_DEPLOY_FOR = "there is no identity to deploy the edited profile for";
        private const string FETCH_HAS_NO_IDENTITY = "there is no identity, the fetch was started for a previous one";
        private const string FETCH_FOR_ANOTHER_IDENTITY = "the fetch was started for another identity";
        private const string NO_FETCH_IN_FLIGHT = "no fetch is in flight";
        private const string DEPLOY_HAS_NO_IDENTITY = "there is no identity, the deploy was started for a previous one";
        private const string DEPLOY_FOR_ANOTHER_IDENTITY = "the deploy was started for another identity";
        private const string NO_DEPLOY_IN_FLIGHT = "no deploy is in flight";
        private const string DEPLOY_SUPERSEDED = "the deploy was superseded by a later edit";
        private const string NO_IDENTITY_TO_RETRY_FOR = "there is no identity to read the profile of";
        private const string LAST_READ_DID_NOT_FAIL = "the last read did not fail, there is nothing to retry";
        private const string ACTIVITY_ALREADY_IN_FLIGHT = "an activity is already in flight";

        /// <summary>
        ///     Pure transition of the self-profile FSM. No IO, no time, no shared state.
        /// </summary>
        public static (SelfProfileModel model, SelfProfileCmd cmd) Update(in SelfProfileModel model, in SelfProfileMsg msg) =>
            msg.Match(
                model,
                onIdentityChanged: static (model, address) => OnIdentityChanged(model, address),
                onIdentityCleared: static model => OnIdentityCleared(model),
                onFetchSucceeded: static (model, fetched) => OnFetchSucceeded(model, fetched),
                onFetchNotFound: static (model, address) => OnFetchNotFound(model, address),
                onFetchFailed: static (model, failed) => OnFetchFailed(model, failed),
                onProfileEdited: static (model, edited) => OnProfileEdited(model, edited),
                onDeploySucceeded: static (model, deployed) => OnDeploySucceeded(model, deployed),
                onDeployFailed: static (model, failed) => OnDeployFailed(model, failed),
                onRetryRequested: static model => OnRetryRequested(model)
            );

        private static (SelfProfileModel, SelfProfileCmd) OnIdentityChanged(in SelfProfileModel model, UserId address) =>
            model.Match(
                address,
                onNoIdentity: static address => (StartFetching(address), SelfProfileCmd.FromFetch(address)),
                onIdentified: static (address, current) => current.Address.Equals(address)
                    ? Ignored(FromIdentified(current), IDENTITY_ALREADY_CURRENT)
                    : (StartFetching(address), SelfProfileCmd.FromBatch(new[] { SelfProfileCmd.ResetLocalState(), SelfProfileCmd.FromFetch(address) }))
            );

        private static (SelfProfileModel, SelfProfileCmd) OnIdentityCleared(in SelfProfileModel model) =>
            model.Match(
                onNoIdentity: static () => Ignored(NoIdentity(), NO_IDENTITY_TO_CLEAR),
                onIdentified: static _ => (NoIdentity(), SelfProfileCmd.ResetLocalState())
            );

        private static (SelfProfileModel, SelfProfileCmd) OnFetchSucceeded(in SelfProfileModel model, in FetchSucceeded msg)
        {
            if (StaleFetchReason(model, msg.Address, out Identified current) is { } reason)
                return Ignored(model, reason);

            return (FromIdentified(current.With(ProfileKnowledge.FromKnown(msg.Profile), ProfileActivity.Idle())), SelfProfileCmd.FromPublish(msg.Profile));
        }

        private static (SelfProfileModel, SelfProfileCmd) OnFetchNotFound(in SelfProfileModel model, UserId address)
        {
            if (StaleFetchReason(model, address, out Identified current) is { } reason)
                return Ignored(model, reason);

            return (FromIdentified(current.With(ProfileKnowledge.Missing(), ProfileActivity.Idle())), SelfProfileCmd.None());
        }

        private static (SelfProfileModel, SelfProfileCmd) OnFetchFailed(in SelfProfileModel model, in FetchFailed msg)
        {
            if (StaleFetchReason(model, msg.Address, out Identified current) is { } reason)
                return Ignored(model, reason);

            // A trusted profile outlives a failed read.
            ProfileKnowledge knowledge = current.Knowledge.Match(
                msg.Failure,
                onUnknown: static failure => ProfileKnowledge.FromFailed(failure),
                onKnown: static (_, known) => ProfileKnowledge.FromKnown(known),
                onMissing: static failure => ProfileKnowledge.FromFailed(failure),
                onFailed: static (failure, _) => ProfileKnowledge.FromFailed(failure)
            );

            return (FromIdentified(current.With(knowledge, ProfileActivity.Idle())), SelfProfileCmd.None());
        }

        private static (SelfProfileModel, SelfProfileCmd) OnProfileEdited(in SelfProfileModel model, Profile edited) =>
            model.Match(
                edited,
                onNoIdentity: static _ => Ignored(NoIdentity(), NO_IDENTITY_TO_DEPLOY_FOR),
                onIdentified: static (edited, current) => StartDeploying(current, edited)
            );

        private static (SelfProfileModel, SelfProfileCmd) StartDeploying(in Identified current, Profile edited)
        {
            // A deploy already in flight is superseded; the revert point stays the knowledge from before the first edit.
            ProfileKnowledge before = current.Activity.Match(
                current.Knowledge,
                onIdle: static knowledge => knowledge,
                onFetching: static knowledge => knowledge,
                onDeploying: static (_, inFlight) => inFlight.Before
            );

            // The next version follows the trusted profile, which during a deploy is the pending edit.
            int version = current.Knowledge.IsKnown(out Profile known) ? known.Version + 1 : edited.Version + 1;

            ProfileActivity deploying = ProfileActivity.FromDeploying(new Deploying(edited, before));
            SelfProfileCmd deploy = SelfProfileCmd.FromDeploy(new DeployCmd(current.Address, edited, version));

            // Only a known profile is trusted locally before the catalyst confirms the edit.
            if (!before.IsKnown(out _))
                return (FromIdentified(current.With(before, deploying, Option<DeployFailure>.None)), deploy);

            return (FromIdentified(current.With(ProfileKnowledge.FromKnown(edited), deploying, Option<DeployFailure>.None)),
                SelfProfileCmd.FromBatch(new[] { SelfProfileCmd.FromPublish(edited), deploy }));
        }

        private static (SelfProfileModel, SelfProfileCmd) OnDeploySucceeded(in SelfProfileModel model, in DeploySucceeded msg)
        {
            if (StaleDeployReason(model, msg.Address, msg.Sent, out Identified current, out _) is { } reason)
                return Ignored(model, reason);

            return (FromIdentified(current.With(ProfileKnowledge.FromKnown(msg.Saved), ProfileActivity.Idle(), Option<DeployFailure>.None)), SelfProfileCmd.FromPublish(msg.Saved));
        }

        private static (SelfProfileModel, SelfProfileCmd) OnDeployFailed(in SelfProfileModel model, in DeployFailed msg)
        {
            if (StaleDeployReason(model, msg.Address, msg.Sent, out Identified current, out Deploying deploying) is { } reason)
                return Ignored(model, reason);

            var failure = Option<DeployFailure>.Some(new DeployFailure(msg.Sent, msg.Exception));
            SelfProfileModel reverted = FromIdentified(current.With(deploying.Before, ProfileActivity.Idle(), failure));

            // Only a known profile was published before the deploy, so only then is there something to republish.
            SelfProfileCmd cmd = deploying.Before.Match(
                onUnknown: static () => SelfProfileCmd.None(),
                onKnown: static previous => SelfProfileCmd.FromPublish(previous),
                onMissing: static () => SelfProfileCmd.None(),
                onFailed: static _ => SelfProfileCmd.None()
            );

            return (reverted, cmd);
        }

        private static (SelfProfileModel, SelfProfileCmd) OnRetryRequested(in SelfProfileModel model) =>
            model.Match(
                onNoIdentity: static () => Ignored(NoIdentity(), NO_IDENTITY_TO_RETRY_FOR),
                onIdentified: static current => Retry(current)
            );

        /// <summary>A retry re-reads only after a failed read; the failed knowledge stays until the new read answers.</summary>
        private static (SelfProfileModel, SelfProfileCmd) Retry(in Identified current)
        {
            if (!current.Activity.IsIdle())
                return Ignored(FromIdentified(current), ACTIVITY_ALREADY_IN_FLIGHT);

            if (!current.Knowledge.IsFailed(out _))
                return Ignored(FromIdentified(current), LAST_READ_DID_NOT_FAIL);

            return (FromIdentified(current.WithActivity(ProfileActivity.Fetching())), SelfProfileCmd.FromFetch(current.Address));
        }

        private static SelfProfileModel StartFetching(UserId address) =>
            FromIdentified(Identified.New(address).WithActivity(ProfileActivity.Fetching()));

        /// <summary>The model untouched, with the reason the message did not apply to it.</summary>
        private static (SelfProfileModel, SelfProfileCmd) Ignored(in SelfProfileModel model, string reason) =>
            (model, SelfProfileCmd.FromIgnore(reason));

        /// <summary>Null when a fetch result for the address belongs to the fetch in flight; otherwise why it does not.</summary>
        private static string? StaleFetchReason(in SelfProfileModel model, UserId address, out Identified current)
        {
            if (!model.IsIdentified(out current))
                return FETCH_HAS_NO_IDENTITY;

            if (!current.Address.Equals(address))
                return FETCH_FOR_ANOTHER_IDENTITY;

            if (!current.Activity.IsFetching())
                return NO_FETCH_IN_FLIGHT;

            return null;
        }

        /// <summary>Null when a deploy result for the sent profile belongs to the deploy in flight; otherwise why it does not.</summary>
        private static string? StaleDeployReason(in SelfProfileModel model, UserId address, Profile sent, out Identified current, out Deploying deploying)
        {
            deploying = default;

            if (!model.IsIdentified(out current))
                return DEPLOY_HAS_NO_IDENTITY;

            if (!current.Address.Equals(address))
                return DEPLOY_FOR_ANOTHER_IDENTITY;

            if (!current.Activity.IsDeploying(out deploying))
                return NO_DEPLOY_IN_FLIGHT;

            if (!ReferenceEquals(deploying.Pending, sent))
                return DEPLOY_SUPERSEDED;

            return null;
        }
    }
}
