using DCL.Utility.Types;

namespace DCL.Profiles.Self
{
    public readonly partial struct SelfProfileModel
    {
        // Reasons a message is ignored; constants, so ignoring allocates nothing.
        private const string IDENTITY_ALREADY_CURRENT = "the identity is already current";
        private const string NO_IDENTITY_TO_CLEAR = "there is no identity to clear";
        private const string FETCH_HAS_NO_IDENTITY = "there is no identity, the fetch was started for a previous one";
        private const string FETCH_FOR_ANOTHER_IDENTITY = "the fetch was started for another identity";
        private const string NO_FETCH_IN_FLIGHT = "no fetch is in flight";
        private const string DEPLOY_HAS_NO_IDENTITY = "there is no identity, the deploy was started for a previous one";
        private const string DEPLOY_FOR_ANOTHER_IDENTITY = "the deploy was started for another identity";
        private const string NO_DEPLOY_IN_FLIGHT = "no deploy is in flight";
        private const string DEPLOY_SUPERSEDED = "the deploy was superseded by a later edit";

        /// <summary>Pure transition of the self-profile FSM.</summary>
        public static (SelfProfileModel model, SelfProfileCmd cmd) Update(in SelfProfileModel model, in SelfProfileMsg msg) =>
            msg.Match(
                model,
                onIdentityChanged: static (model, address) => OnIdentityChanged(model, address),
                onIdentityCleared: static model => OnIdentityCleared(model),
                onFetchSucceeded: static (model, fetched) => OnFetchSucceeded(model, fetched),
                onFetchNotFound: static (model, address) => OnFetchNotFound(model, address),
                onFetchFailed: static (model, failed) => OnFetchFailed(model, failed),
                onDeployProfileOnEditRequested: static (model, request) => OnDeployProfileOnEditRequested(model, request),
                onDeploySucceeded: static (model, deployed) => OnDeploySucceeded(model, deployed),
                onDeployFailed: static (model, failed) => OnDeployFailed(model, failed),
                onProfileReadRequested: static (model, id) => OnProfileReadRequested(model, id),
                onRequestClosed: static (model, id) => (model.WithoutRequest(id), SelfProfileCmd.None())
            );

        private static (SelfProfileModel, SelfProfileCmd) OnIdentityChanged(in SelfProfileModel model, UserId address) =>
            model.Session.Match(
                (model, address),
                onNoIdentity: static ctx => (ctx.model.WithSession(StartFetching(ctx.address, default)), SelfProfileCmd.FromFetch(ctx.address)),
                onIdentified: static (ctx, current) => current.Address.Equals(ctx.address)
                    ? Ignored(ctx.model, IDENTITY_ALREADY_CURRENT)
                    : (ctx.model.WithSession(StartFetching(ctx.address, current.PendingReads))
                          .WithDeployResults(PendingDeploys(current), ProfileDeployResult.FromError(ProfileDeployError.NoIdentity)),
                        SelfProfileCmd.FromBatch(new[] { SelfProfileCmd.ResetLocalState(), SelfProfileCmd.FromFetch(ctx.address) }))
            );

        private static (SelfProfileModel, SelfProfileCmd) OnIdentityCleared(in SelfProfileModel model) =>
            model.Session.Match(
                model,
                onNoIdentity: static model => Ignored(model, NO_IDENTITY_TO_CLEAR),
                onIdentified: static (model, current) => (model.WithSession(SelfProfileSession.NoIdentity())
                                                               .WithReadResults(current.PendingReads, ProfileReadResult.FromError(ProfileReadError.NoIdentity))
                                                               .WithDeployResults(PendingDeploys(current), ProfileDeployResult.FromError(ProfileDeployError.NoIdentity)),
                    SelfProfileCmd.ResetLocalState())
            );

        private static (SelfProfileModel, SelfProfileCmd) OnFetchSucceeded(in SelfProfileModel model, in FetchSucceeded msg)
        {
            if (StaleFetchReason(model, msg.Address, out Identified current) is { } reason)
                return Ignored(model, reason);

            return SettleReads(model, current.With(ProfileKnowledge.FromKnown(msg.Profile), ProfileActivity.Idle()), SelfProfileCmd.FromPublish(msg.Profile));
        }

        private static (SelfProfileModel, SelfProfileCmd) OnFetchNotFound(in SelfProfileModel model, UserId address)
        {
            if (StaleFetchReason(model, address, out Identified current) is { } reason)
                return Ignored(model, reason);

            return SettleReads(model, current.With(ProfileKnowledge.Missing(), ProfileActivity.Idle()), SelfProfileCmd.None());
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

            return SettleReads(model, current.With(knowledge, ProfileActivity.Idle()), SelfProfileCmd.None());
        }

        private static (SelfProfileModel, SelfProfileCmd) OnProfileReadRequested(in SelfProfileModel model, RequestId id) =>
            model.Session.Match(
                (model: model.WithLastRequest(id), id),
                onNoIdentity: static ctx => (ctx.model.WithReadResult(ctx.id, ProfileReadResult.FromError(ProfileReadError.NoIdentity)), SelfProfileCmd.None()),
                onIdentified: static (ctx, current) => Read(ctx.model, current, ctx.id)
            );

        /// <summary>
        ///     A read is answered at once from settled knowledge; otherwise it waits on the fetch in flight, or starts one
        ///     when nothing is in flight, which is the one refetch after a failed fetch.
        /// </summary>
        private static (SelfProfileModel, SelfProfileCmd) Read(in SelfProfileModel model, in Identified current, RequestId id)
        {
            if (current.Knowledge.IsKnown(out Profile? known))
                return (model.WithReadResult(id, ProfileReadResult.FromOk(known)), SelfProfileCmd.None());

            if (current.Knowledge.IsMissing())
                return (model.WithReadResult(id, ProfileReadResult.FromError(ProfileReadError.NotFound)), SelfProfileCmd.None());

            Identified queued = current.WithPendingReads(current.PendingReads.Add(id));

            if (!current.Activity.IsIdle())
                return (model.WithSession(SelfProfileSession.FromIdentified(queued)), SelfProfileCmd.None());

            return (model.WithSession(SelfProfileSession.FromIdentified(queued.WithActivity(ProfileActivity.Fetching()))), SelfProfileCmd.FromFetch(current.Address));
        }

        private static (SelfProfileModel, SelfProfileCmd) OnDeployProfileOnEditRequested(in SelfProfileModel model, in DeployRequest request) =>
            model.Session.Match(
                (model: model.WithLastRequest(request.Id), request),
                onNoIdentity: static ctx => (ctx.model.WithDeployResult(ctx.request.Id, ProfileDeployResult.FromError(ProfileDeployError.NoIdentity)), SelfProfileCmd.None()),
                onIdentified: static (ctx, current) => Deploy(ctx.model, current, ctx.request)
            );

        /// <summary>
        ///     An edit identical to the deploy in flight joins that deploy and shares its outcome; one identical to the confirmed
        ///     profile is answered <c>NothingChanged</c>; any other edit starts a deploy.
        /// </summary>
        private static (SelfProfileModel, SelfProfileCmd) Deploy(in SelfProfileModel model, in Identified current, in DeployRequest request)
        {
            if (current.Activity.IsDeploying(out Deploying inFlight))
            {
                if (!request.Edited.IsSameProfile(inFlight.Pending))
                    return StartDeploying(model, current, request);

                var joined = new Deploying(inFlight.Pending, inFlight.Before, inFlight.Requests.Add(request.Id));
                return (model.WithSession(SelfProfileSession.FromIdentified(current.WithActivity(ProfileActivity.FromDeploying(joined)))), SelfProfileCmd.None());
            }

            if (current.Knowledge.IsKnown(out Profile? known) && request.Edited.IsSameProfile(known))
                return (model.WithDeployResult(request.Id, ProfileDeployResult.FromError(ProfileDeployError.NothingChanged)), SelfProfileCmd.None());

            return StartDeploying(model, current, request);
        }

        private static (SelfProfileModel, SelfProfileCmd) StartDeploying(in SelfProfileModel model, in Identified current, in DeployRequest request)
        {
            Profile edited = request.Edited;

            // A deploy already in flight is superseded: its requests move to the new deploy and the revert point stays
            // the knowledge from before the first edit.
            ProfileKnowledge before = current.Activity.Match(
                current.Knowledge,
                onIdle: static knowledge => knowledge,
                onFetching: static knowledge => knowledge,
                onDeploying: static (_, inFlight) => inFlight.Before
            );

            RequestIds requests = PendingDeploys(current).Add(request.Id);

            // The next version follows the trusted profile, which during a deploy is the pending edit.
            int version = current.Knowledge.IsKnown(out Profile? known) ? known.Version + 1 : edited.Version + 1;

            ProfileActivity deploying = ProfileActivity.FromDeploying(new Deploying(edited, before, requests));
            SelfProfileCmd deploy = SelfProfileCmd.FromDeploy(new DeployCmd(current.Address, edited, version));

            // Only a known profile is trusted locally before the catalyst confirms the edit.
            if (!before.IsKnown(out _))
                return (model.WithSession(SelfProfileSession.FromIdentified(current.With(before, deploying))), deploy);

            return (model.WithSession(SelfProfileSession.FromIdentified(current.With(ProfileKnowledge.FromKnown(edited), deploying))),
                SelfProfileCmd.FromBatch(new[] { SelfProfileCmd.FromPublish(edited), deploy }));
        }

        private static (SelfProfileModel, SelfProfileCmd) OnDeploySucceeded(in SelfProfileModel model, in DeploySucceeded msg)
        {
            if (StaleDeployReason(model, msg.Address, msg.Sent, out Identified current, out Deploying deploying) is { } reason)
                return Ignored(model, reason);

            SelfProfileModel answered = model.WithDeployResults(deploying.Requests, ProfileDeployResult.FromOk(msg.Saved));
            return SettleReads(answered, current.With(ProfileKnowledge.FromKnown(msg.Saved), ProfileActivity.Idle()), SelfProfileCmd.FromPublish(msg.Saved));
        }

        private static (SelfProfileModel, SelfProfileCmd) OnDeployFailed(in SelfProfileModel model, in DeployFailed msg)
        {
            if (StaleDeployReason(model, msg.Address, msg.Sent, out Identified current, out Deploying deploying) is { } reason)
                return Ignored(model, reason);

            // Only a known profile was published before the deploy, so only then is there something to republish.
            SelfProfileCmd republish = deploying.Before.Match(
                onUnknown: static () => SelfProfileCmd.None(),
                onKnown: static previous => SelfProfileCmd.FromPublish(previous),
                onMissing: static () => SelfProfileCmd.None(),
                onFailed: static _ => SelfProfileCmd.None()
            );

            SelfProfileModel answered = model.WithDeployResults(deploying.Requests, ProfileDeployResult.FromError(ProfileDeployError.DeployFailed));
            return SettleReads(answered, current.With(deploying.Before, ProfileActivity.Idle()), republish);
        }

        /// <summary>
        ///     Answers the pending reads when the knowledge is settled. Unknown knowledge with nothing in flight starts a fetch for them.
        /// </summary>
        private static (SelfProfileModel, SelfProfileCmd) SettleReads(in SelfProfileModel model, in Identified current, in SelfProfileCmd cmd)
        {
            if (current.PendingReads.Count == 0)
                return (model.WithSession(SelfProfileSession.FromIdentified(current)), cmd);

            Option<ProfileReadResult> answer = current.Knowledge.Match(
                onUnknown: static () => Option<ProfileReadResult>.None,
                onKnown: static known => Option<ProfileReadResult>.Some(ProfileReadResult.FromOk(known)),
                onMissing: static () => Option<ProfileReadResult>.Some(ProfileReadResult.FromError(ProfileReadError.NotFound)),
                onFailed: static _ => Option<ProfileReadResult>.Some(ProfileReadResult.FromError(ProfileReadError.FetchFailed))
            );

            if (answer.Has)
                return (model.WithSession(SelfProfileSession.FromIdentified(current.WithPendingReads(default))).WithReadResults(current.PendingReads, answer.Value), cmd);

            if (current.Activity.IsIdle())
                return (model.WithSession(SelfProfileSession.FromIdentified(current.WithActivity(ProfileActivity.Fetching()))), Then(cmd, SelfProfileCmd.FromFetch(current.Address)));

            return (model.WithSession(SelfProfileSession.FromIdentified(current)), cmd);
        }

        private static SelfProfileSession StartFetching(UserId address, RequestIds pendingReads) =>
            SelfProfileSession.FromIdentified(Identified.New(address).WithActivity(ProfileActivity.Fetching()).WithPendingReads(pendingReads));

        private static RequestIds PendingDeploys(in Identified current) =>
            current.Activity.IsDeploying(out Deploying deploying) ? deploying.Requests : default;

        /// <summary>The model untouched, with the reason the message did not apply to it.</summary>
        private static (SelfProfileModel, SelfProfileCmd) Ignored(in SelfProfileModel model, string reason) =>
            (model, SelfProfileCmd.FromIgnore(reason));

        /// <summary>Both commands in order, without wrapping a <c>None</c>.</summary>
        private static SelfProfileCmd Then(in SelfProfileCmd first, in SelfProfileCmd second)
        {
            if (second.IsNone())
                return first;

            if (first.IsNone())
                return second;

            return SelfProfileCmd.FromBatch(new[] { first, second });
        }

        /// <summary>Null when a fetch result for the address belongs to the fetch in flight; otherwise why it does not.</summary>
        private static string? StaleFetchReason(in SelfProfileModel model, UserId address, out Identified current)
        {
            if (!model.Session.IsIdentified(out current))
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

            if (!model.Session.IsIdentified(out current))
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
