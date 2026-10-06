using DCL.Utility.Types;
using REnum;
using System;

namespace DCL.Profiles.Self
{
    /// <summary>Session of the self-profile FSM while an identity is present: the address with its knowledge and activity.</summary>
    public readonly struct Identified : IEquatable<Identified>
    {
        public readonly UserId Address;
        public readonly ProfileKnowledge Knowledge;
        public readonly ProfileActivity Activity;

        /// <summary>Reads waiting for the knowledge to settle.</summary>
        public readonly RequestIds PendingReads;

        public Identified(UserId address, ProfileKnowledge knowledge, ProfileActivity activity)
            : this(address, knowledge, activity, default) { }

        public Identified(UserId address, ProfileKnowledge knowledge, ProfileActivity activity, RequestIds pendingReads)
        {
            Address = address;
            Knowledge = knowledge;
            Activity = activity;
            PendingReads = pendingReads;
        }

        /// <summary>A fresh identity: nothing known, nothing in flight.</summary>
        public static Identified New(UserId address) =>
            new (address, ProfileKnowledge.Unknown(), ProfileActivity.Idle());

        public Identified WithActivity(ProfileActivity activity) =>
            new (Address, Knowledge, activity, PendingReads);

        public Identified WithPendingReads(RequestIds pendingReads) =>
            new (Address, Knowledge, Activity, pendingReads);

        public Identified With(ProfileKnowledge knowledge, ProfileActivity activity) =>
            new (Address, knowledge, activity, PendingReads);

        /// <summary>True while the request waits among the pending reads or on the deploy in flight.</summary>
        public bool Holds(RequestId id) =>
            PendingReads.Contains(id) || (Activity.IsDeploying(out Deploying deploying) && deploying.Requests.Contains(id));

        /// <summary>The same session without the request among the pending reads or the requests of the deploy in flight.</summary>
        public Identified WithoutRequest(RequestId id)
        {
            ProfileActivity activity = Activity.IsDeploying(out Deploying deploying)
                ? ProfileActivity.FromDeploying(new Deploying(deploying.Pending, deploying.Version, deploying.Before, deploying.Requests.Remove(id)))
                : Activity;

            return new Identified(Address, Knowledge, activity, PendingReads.Remove(id));
        }

        public bool Equals(Identified other) =>
            Address.Equals(other.Address) && Knowledge.Equals(other.Knowledge) && Activity.Equals(other.Activity) && PendingReads.Equals(other.PendingReads);

        public override bool Equals(object? obj) =>
            obj is Identified other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(Address, Knowledge, Activity, PendingReads);

        public override string ToString() =>
            PendingReads.Count == 0
                ? $"{Address.Value} knowledge {Knowledge} activity {Activity}"
                : $"{Address.Value} knowledge {Knowledge} activity {Activity} pending reads {PendingReads}";
    }

    /// <summary>
    ///     <c>NoIdentity</c> = no wallet is signed in, so there is nothing to know and nothing to do;
    ///     <c>Identified</c> = the address plus what is known about its profile and what is in flight.
    /// </summary>
    [REnum(EnumUnderlyingType.Byte)]
    [REnumFieldEmpty("NoIdentity")]
    [REnumField(typeof(Identified))]
    public readonly partial struct SelfProfileSession { }

    /// <summary>
    ///     Immutable model of the self-profile FSM: the session plus the answered requests, kept until closed. Requests apply
    ///     in id order and the bounded lists drop their oldest entry, so an applied request it no longer <see cref="Holds"/> was dropped.
    /// </summary>
    public readonly partial struct SelfProfileModel
    {
        public readonly SelfProfileSession Session;
        public readonly RequestResults<ProfileReadResult> ReadResults;
        public readonly RequestResults<ProfileDeployResult> DeployResults;

        /// <summary>The id of the last request applied.</summary>
        public readonly RequestId LastRequest;

        public SelfProfileModel(SelfProfileSession session)
            : this(session, default, default, default) { }

        public SelfProfileModel(SelfProfileSession session, RequestResults<ProfileReadResult> readResults, RequestResults<ProfileDeployResult> deployResults, RequestId lastRequest)
        {
            Session = session;
            ReadResults = readResults;
            DeployResults = deployResults;
            LastRequest = lastRequest;
        }

        /// <summary>The trusted profile of the current identity, when there is one; during a deploy this is the pending edit.</summary>
        public Option<Profile> KnownProfile =>
            Session.IsIdentified(out Identified identified) && identified.Knowledge.IsKnown(out Profile? known)
                ? Option<Profile>.Some(known)
                : Option<Profile>.None;

        /// <summary>The profile the catalyst holds, when known: during a deploy this is the knowledge from before the edit.</summary>
        public Option<Profile> ConfirmedProfile
        {
            get
            {
                if (!Session.IsIdentified(out Identified identified))
                    return Option<Profile>.None;

                ProfileKnowledge confirmed = identified.Activity.IsDeploying(out Deploying deploying) ? deploying.Before : identified.Knowledge;
                return confirmed.IsKnown(out Profile? known) ? Option<Profile>.Some(known) : Option<Profile>.None;
            }
        }

        public static SelfProfileModel NoIdentity() =>
            new (SelfProfileSession.NoIdentity());

        public static SelfProfileModel FromIdentified(in Identified identified) =>
            new (SelfProfileSession.FromIdentified(identified));

        public bool IsIdentified(out Identified identified) =>
            Session.IsIdentified(out identified);

        /// <summary>True while the request waits for an answer or its answer waits to be taken.</summary>
        public bool Holds(RequestId id) =>
            ReadResults.Contains(id)
            || DeployResults.Contains(id)
            || Session.Match(id, onNoIdentity: static _ => false, onIdentified: static (id, current) => current.Holds(id));

        public SelfProfileModel WithSession(in SelfProfileSession session) =>
            new (session, ReadResults, DeployResults, LastRequest);

        public SelfProfileModel WithLastRequest(RequestId id) =>
            new (Session, ReadResults, DeployResults, id);

        public SelfProfileModel WithReadResult(RequestId id, ProfileReadResult result) =>
            new (Session, ReadResults.With(id, result), DeployResults, LastRequest);

        public SelfProfileModel WithReadResults(RequestIds ids, ProfileReadResult result)
        {
            RequestResults<ProfileReadResult> results = ReadResults;

            for (var i = 0; i < ids.Count; i++)
                results = results.With(ids[i], result);

            return new SelfProfileModel(Session, results, DeployResults, LastRequest);
        }

        public SelfProfileModel WithDeployResult(RequestId id, ProfileDeployResult result) =>
            new (Session, ReadResults, DeployResults.With(id, result), LastRequest);

        public SelfProfileModel WithDeployResults(RequestIds ids, ProfileDeployResult result)
        {
            RequestResults<ProfileDeployResult> results = DeployResults;

            for (var i = 0; i < ids.Count; i++)
                results = results.With(ids[i], result);

            return new SelfProfileModel(Session, ReadResults, results, LastRequest);
        }

        /// <summary>The model without any trace of the request: its result, or its place among the waiting requests.</summary>
        public SelfProfileModel WithoutRequest(RequestId id)
        {
            SelfProfileSession session = Session.Match(
                id,
                onNoIdentity: static _ => SelfProfileSession.NoIdentity(),
                onIdentified: static (id, current) => SelfProfileSession.FromIdentified(current.WithoutRequest(id))
            );

            return new SelfProfileModel(session, ReadResults.Without(id), DeployResults.Without(id), LastRequest);
        }

        public override string ToString() =>
            $"{Session} read results {ReadResults} deploy results {DeployResults} last request {LastRequest}";
    }
}
