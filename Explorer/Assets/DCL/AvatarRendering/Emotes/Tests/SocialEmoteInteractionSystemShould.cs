using Arch.Core;
using DCL.Multiplayer.Connections.Rooms;
using DCL.Multiplayer.Emotes;
using DCL.Multiplayer.Movement;
using DCL.Multiplayer.Profiles.Bunches;
using DCL.Multiplayer.Profiles.Tables;
using DCL.Optimization.Multithreading;
using ECS.LifeCycle.Components;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using System.Collections.Generic;

namespace DCL.AvatarRendering.Emotes.Tests
{
    public class SocialEmoteInteractionSystemShould : UnitySystemTestBase<SocialEmoteInteractionSystem>
    {
        private const uint INTERACTION_ID = 7;
        private const string EMOTE_URN = "urn:decentraland:off-chain:base-emotes:high-five";
        private const string INITIATOR = "0xinitiator";
        private const string RECEIVER = "0xreceiver";
        private const string BYSTANDER = "0xbystander";
        private const double TIMESTAMP = 1.0;

        private readonly MutexSync sync = new ();
        private readonly HashSet<RemoteSocialEmoteIntention> inbox = new ();
        private readonly List<RemoteSocialEmoteIntention> retried = new ();

        private EntityParticipantTable participantTable = null!;
        private Entity initiator;
        private Entity receiver;
        private Entity bystander;

        [SetUp]
        public void Setup()
        {
            inbox.Clear();
            retried.Clear();

            IEmotesMessageBus messageBus = Substitute.For<IEmotesMessageBus>();
            messageBus.SocialEmoteIntentions().Returns(_ => new OwnedBunch<RemoteSocialEmoteIntention>(sync, inbox));
            messageBus.When(bus => bus.SaveForRetry(Arg.Any<RemoteSocialEmoteIntention>())).Do(call => retried.Add(call.Arg<RemoteSocialEmoteIntention>()));

            participantTable = new EntityParticipantTable();
            initiator = RegisterRemoteAvatar(INITIATOR);
            receiver = RegisterRemoteAvatar(RECEIVER);
            bystander = RegisterRemoteAvatar(BYSTANDER);

            system = new SocialEmoteInteractionSystem(world, participantTable, messageBus);
        }

        [Test]
        public void StartAnInteractionOnTheInitiator()
        {
            Feed(Start(target: RECEIVER));
            system.Update(0f);

            SocialEmoteInteractionComponent interaction = world.Get<SocialEmoteInteractionComponent>(initiator);
            Assert.That(interaction.InteractionId, Is.EqualTo(INTERACTION_ID));
            Assert.That(interaction.Role, Is.EqualTo(SocialEmoteRole.Initiator));
            Assert.That(interaction.Phase, Is.EqualTo(SocialEmotePhase.Started));
            Assert.That(interaction.TargetWalletId, Is.EqualTo(RECEIVER));
            Assert.That(interaction.PartnerWalletId, Is.Empty);
            Assert.That(interaction.OutcomeIndex, Is.EqualTo(RemoteSocialEmoteIntention.NO_OUTCOME));
            Assert.That(retried, Is.Empty);
        }

        [Test]
        public void PairTheReceiverWithTheInitiatorOnOutcome()
        {
            Feed(Start());
            system.Update(0f);

            Feed(Outcome(RECEIVER, 2), Outcome(INITIATOR, 2));
            system.Update(0f);

            SocialEmoteInteractionComponent initiatorInteraction = world.Get<SocialEmoteInteractionComponent>(initiator);
            Assert.That(initiatorInteraction.Phase, Is.EqualTo(SocialEmotePhase.Outcome));
            Assert.That(initiatorInteraction.OutcomeIndex, Is.EqualTo(2));
            Assert.That(initiatorInteraction.PartnerWalletId, Is.EqualTo(RECEIVER));

            SocialEmoteInteractionComponent receiverInteraction = world.Get<SocialEmoteInteractionComponent>(receiver);
            Assert.That(receiverInteraction.Role, Is.EqualTo(SocialEmoteRole.Receiver));
            Assert.That(receiverInteraction.Phase, Is.EqualTo(SocialEmotePhase.Outcome));
            Assert.That(receiverInteraction.OutcomeIndex, Is.EqualTo(2));
            Assert.That(receiverInteraction.PartnerWalletId, Is.EqualTo(INITIATOR));
            Assert.That(retried, Is.Empty);
        }

        [Test]
        public void RetryAnOutcomeThatArrivesBeforeTheStart()
        {
            Feed(Outcome(RECEIVER, 1));
            system.Update(0f);

            Assert.That(world.Has<SocialEmoteInteractionComponent>(receiver), Is.False);
            Assert.That(retried, Has.Count.EqualTo(1));

            Feed(Start(), retried[0]);
            system.Update(0f);

            Assert.That(world.Get<SocialEmoteInteractionComponent>(receiver).PartnerWalletId, Is.EqualTo(INITIATOR));
            Assert.That(world.Get<SocialEmoteInteractionComponent>(initiator).PartnerWalletId, Is.EqualTo(RECEIVER));
        }

        [Test]
        public void DropAnOutcomeWhoseStartCanNoLongerArrive()
        {
            ref InterpolationComponent interpolation = ref world.Get<InterpolationComponent>(receiver);
            interpolation.Time = SocialEmoteInteractionSystem.REACTION_TIMEOUT + 2f;

            Feed(Outcome(RECEIVER, 1));
            system.Update(0f);

            Assert.That(world.Has<SocialEmoteInteractionComponent>(receiver), Is.False);
            Assert.That(retried, Is.Empty);
        }

        [Test]
        public void IgnoreARepeatedStart()
        {
            Feed(Start());
            system.Update(0f);
            Feed(Outcome(RECEIVER, 0));
            system.Update(0f);

            Feed(Start());
            system.Update(0f);

            Assert.That(world.Get<SocialEmoteInteractionComponent>(initiator).PartnerWalletId, Is.EqualTo(RECEIVER));
            Assert.That(world.Get<SocialEmoteInteractionComponent>(receiver).Phase, Is.EqualTo(SocialEmotePhase.Outcome));
        }

        [Test]
        public void LetTheFirstReactionWin()
        {
            Feed(Start());
            system.Update(0f);

            Feed(Outcome(RECEIVER, 0));
            system.Update(0f);
            Feed(Outcome(BYSTANDER, 1));
            system.Update(0f);

            Assert.That(world.Get<SocialEmoteInteractionComponent>(initiator).PartnerWalletId, Is.EqualTo(RECEIVER));
            Assert.That(world.Has<SocialEmoteInteractionComponent>(bystander), Is.False);
            Assert.That(retried, Is.Empty);
        }

        [Test]
        public void FinishAnUnansweredStartOnTimeout()
        {
            Feed(Start());
            system.Update(0f);

            system.Update(SocialEmoteInteractionSystem.REACTION_TIMEOUT - 0.5f);
            Assert.That(world.Get<SocialEmoteInteractionComponent>(initiator).Phase, Is.EqualTo(SocialEmotePhase.Started));

            system.Update(1f);
            system.Update(0f);
            Assert.That(world.Has<SocialEmoteInteractionComponent>(initiator), Is.False);
        }

        [Test]
        public void KeepAnAnsweredInteractionPastTheReactionTimeout()
        {
            Feed(Start());
            system.Update(0f);
            Feed(Outcome(RECEIVER, 0), Outcome(INITIATOR, 0));
            system.Update(0f);

            system.Update(SocialEmoteInteractionSystem.REACTION_TIMEOUT * 2f);

            Assert.That(world.Get<SocialEmoteInteractionComponent>(initiator).Phase, Is.EqualTo(SocialEmotePhase.Outcome));
            Assert.That(world.Get<SocialEmoteInteractionComponent>(receiver).Phase, Is.EqualTo(SocialEmotePhase.Outcome));
        }

        [Test]
        public void FinishBothParticipantsWhenOneIsDeleted()
        {
            Feed(Start());
            system.Update(0f);
            Feed(Outcome(RECEIVER, 0), Outcome(INITIATOR, 0));
            system.Update(0f);

            world.Add(receiver, new DeleteEntityIntention());
            system.Update(0f);
            system.Update(0f);

            Assert.That(world.Has<SocialEmoteInteractionComponent>(receiver), Is.False);
            Assert.That(world.Has<SocialEmoteInteractionComponent>(initiator), Is.False);
        }

        [Test]
        public void ReplaceThePreviousInteractionOfTheInitiator()
        {
            Feed(Start());
            system.Update(0f);
            Feed(Outcome(RECEIVER, 0), Outcome(INITIATOR, 0));
            system.Update(0f);

            Feed(Start(INTERACTION_ID + 1));
            system.Update(0f);
            system.Update(0f);

            SocialEmoteInteractionComponent interaction = world.Get<SocialEmoteInteractionComponent>(initiator);
            Assert.That(interaction.InteractionId, Is.EqualTo(INTERACTION_ID + 1));
            Assert.That(interaction.Phase, Is.EqualTo(SocialEmotePhase.Started));
            Assert.That(interaction.PartnerWalletId, Is.Empty);
            Assert.That(world.Has<SocialEmoteInteractionComponent>(receiver), Is.False);
        }

        private Entity RegisterRemoteAvatar(string walletId)
        {
            Entity entity = world.Create(new InterpolationComponent { Start = new NetworkMovementMessage { timestamp = 0 }, Time = 0f });
            participantTable.Register(walletId, entity, RoomSource.Pulse);
            return entity;
        }

        private void Feed(params RemoteSocialEmoteIntention[] intentions)
        {
            retried.Clear();
            inbox.UnionWith(intentions);
        }

        private static RemoteSocialEmoteIntention Start(uint interactionId = INTERACTION_ID, string target = "") =>
            new (interactionId, EMOTE_URN, INITIATOR, INITIATOR, target, RemoteSocialEmoteIntention.NO_OUTCOME, TIMESTAMP);

        private static RemoteSocialEmoteIntention Outcome(string walletId, int outcomeIndex, uint interactionId = INTERACTION_ID) =>
            new (interactionId, EMOTE_URN, walletId, INITIATOR, string.Empty, outcomeIndex, TIMESTAMP);
    }
}
