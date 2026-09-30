namespace DCL.Profiles.Self
{
    public readonly partial struct SelfProfileModel
    {
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
                onDeployFailed: static (model, failed) => OnDeployFailed(model, failed)
            );

        private static (SelfProfileModel, SelfProfileCmd) OnIdentityChanged(in SelfProfileModel model, UserId address) =>
            model.Match(
                address,
                onNoIdentity: static address => (StartFetching(address), SelfProfileCmd.FromFetch(address)),
                onIdentified: static (address, current) => current.Address.Equals(address)
                    ? (FromIdentified(current), SelfProfileCmd.None())
                    : (StartFetching(address), SelfProfileCmd.FromBatch(new[] { SelfProfileCmd.ResetLocalState(), SelfProfileCmd.FromFetch(address) }))
            );

        private static (SelfProfileModel, SelfProfileCmd) OnIdentityCleared(in SelfProfileModel model) =>
            model.Match(
                onNoIdentity: static () => (NoIdentity(), SelfProfileCmd.None()),
                onIdentified: static _ => (NoIdentity(), SelfProfileCmd.ResetLocalState())
            );

        private static (SelfProfileModel, SelfProfileCmd) OnFetchSucceeded(in SelfProfileModel model, in FetchSucceeded msg) =>
            IsFetchingFor(model, msg.Address, out Identified current)
                ? (FromIdentified(current.With(ProfileKnowledge.FromKnown(msg.Profile), ProfileActivity.Idle())), SelfProfileCmd.FromPublish(msg.Profile))
                : Unchanged(model);

        private static (SelfProfileModel, SelfProfileCmd) OnFetchNotFound(in SelfProfileModel model, UserId address) =>
            IsFetchingFor(model, address, out Identified current)
                ? (FromIdentified(current.With(ProfileKnowledge.Missing(), ProfileActivity.Idle())), SelfProfileCmd.None())
                : Unchanged(model);

        private static (SelfProfileModel, SelfProfileCmd) OnFetchFailed(in SelfProfileModel model, in FetchFailed msg)
        {
            if (!IsFetchingFor(model, msg.Address, out Identified current))
                return Unchanged(model);

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
                onNoIdentity: static _ => (NoIdentity(), SelfProfileCmd.None()),
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

            ProfileActivity deploying = ProfileActivity.FromDeploying(new Deploying(edited, before));
            SelfProfileCmd deploy = SelfProfileCmd.FromDeploy(new DeployCmd(current.Address, edited));

            // Only a known profile is trusted locally before the catalyst confirms the edit.
            if (!before.IsKnown(out _))
                return (FromIdentified(current.With(before, deploying)), deploy);

            return (FromIdentified(current.With(ProfileKnowledge.FromKnown(edited), deploying)), SelfProfileCmd.FromBatch(new[] { SelfProfileCmd.FromPublish(edited), deploy }));
        }

        private static (SelfProfileModel, SelfProfileCmd) OnDeploySucceeded(in SelfProfileModel model, in DeploySucceeded msg) =>
            IsDeployingFor(model, msg.Address, msg.Sent, out Identified current, out _)
                ? (FromIdentified(current.With(ProfileKnowledge.FromKnown(msg.Saved), ProfileActivity.Idle())), SelfProfileCmd.FromPublish(msg.Saved))
                : Unchanged(model);

        private static (SelfProfileModel, SelfProfileCmd) OnDeployFailed(in SelfProfileModel model, in DeployFailed msg)
        {
            if (!IsDeployingFor(model, msg.Address, msg.Sent, out Identified current, out Deploying deploying))
                return Unchanged(model);

            SelfProfileModel reverted = FromIdentified(current.With(deploying.Before, ProfileActivity.Idle()));

            // Only a known profile was published before the deploy, so only then is there something to republish.
            SelfProfileCmd cmd = deploying.Before.Match(
                onUnknown: static () => SelfProfileCmd.None(),
                onKnown: static previous => SelfProfileCmd.FromPublish(previous),
                onMissing: static () => SelfProfileCmd.None(),
                onFailed: static _ => SelfProfileCmd.None()
            );

            return (reverted, cmd);
        }

        private static SelfProfileModel StartFetching(UserId address) =>
            FromIdentified(Identified.New(address).WithActivity(ProfileActivity.Fetching()));

        private static (SelfProfileModel, SelfProfileCmd) Unchanged(in SelfProfileModel model) =>
            (model, SelfProfileCmd.None());

        private static bool IsFetchingFor(in SelfProfileModel model, UserId address, out Identified current) =>
            model.IsIdentified(out current)
            && current.Address.Equals(address)
            && current.Activity.IsFetching();

        private static bool IsDeployingFor(in SelfProfileModel model, UserId address, Profile sent, out Identified current, out Deploying deploying)
        {
            deploying = default;

            return model.IsIdentified(out current)
                   && current.Address.Equals(address)
                   && current.Activity.IsDeploying(out deploying)
                   && ReferenceEquals(deploying.Pending, sent);
        }
    }
}
