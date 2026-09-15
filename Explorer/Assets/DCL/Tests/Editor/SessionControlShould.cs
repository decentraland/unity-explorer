using DCL.Web3.Identities;
using NUnit.Framework;
using System;
using Cysharp.Threading.Tasks;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using Google.Protobuf;
using Decentraland.Kernel.Comms.V3;
using Newtonsoft.Json.Linq;

namespace DCL.Tests.Editor
{
    public class SessionControlShould
    {
        [TestCase(SessionControl.Status.Superseded)]
        [TestCase(SessionControl.Status.Banned)]
        [TestCase(SessionControl.Status.Failed)]
        [TestCase(SessionControl.Status.Unknown)]
        public void RejectLateAssignmentsAndRecoveryAfterStopping(SessionControl.Status reason)
        {
            using var cache = new MemoryWeb3IdentityCache();
            var session = SessionControl.For(cache);
            int generation = session.Generation;
            session.Stop(generation, reason);
            Assert.IsFalse(session.AcceptAssignment(generation));
            Assert.IsFalse(session.CanRecover(generation));
            Assert.IsFalse(session.CanListen(generation));
        }

        [Test]
        public void BoundPendingEvenWhenStatusIsRepeated()
        {
            using var cache = new MemoryWeb3IdentityCache();
            var session = SessionControl.For(cache);
            DateTime now = DateTime.UtcNow;
            session.Pending(session.Generation, 1000, now);
            session.Pending(session.Generation, 1000, now.AddSeconds(29));
            Assert.IsFalse(session.CanRecover(session.Generation));
            session.CheckWatchdog(now.AddSeconds(31));
            Assert.AreEqual(SessionControl.Status.Failed, session.Current);
        }

        [Test]
        public void ResolvePendingOnlyWithAssignment()
        {
            using var cache = new MemoryWeb3IdentityCache();
            var session = SessionControl.For(cache);
            session.Pending(session.Generation, 1000, DateTime.UtcNow.AddSeconds(-2));
            Assert.IsTrue(session.CanRecoverTransport(session.Generation));
            Assert.IsFalse(session.CanRecover(session.Generation));
            Assert.IsTrue(session.AcceptAssignment(session.Generation));
            Assert.IsTrue(session.CanRecover(session.Generation));
        }

        [Test]
        public void RequireDifferentEphemeralKeyAfterExplicitReconnect()
        {
            using var oldIdentity = new IWeb3Identity.Random();
            using var newIdentity = new IWeb3Identity.Random();
            using var cache = new MemoryWeb3IdentityCache();
            cache.Identity = oldIdentity;
            var session = SessionControl.For(cache);
            int oldGeneration = session.Generation;
            session.Stop(oldGeneration, SessionControl.Status.Superseded);
            Assert.IsTrue(session.BeginReauthentication());
            cache.Clear();
            cache.Identity = oldIdentity;
            Assert.AreEqual(SessionControl.Status.Authenticating, session.Current);
            Assert.IsFalse(session.CanRecover(session.Generation));
            cache.Identity = newIdentity;
            Assert.IsTrue(session.CanRecover(session.Generation));
            session.Stop(oldGeneration, SessionControl.Status.Superseded);
            Assert.AreEqual(SessionControl.Status.Active, session.Current);
            Assert.IsFalse(session.AcceptAssignment(oldGeneration));
        }

        [Test]
        public void DenyReconnectActionForBan()
        {
            using var cache = new MemoryWeb3IdentityCache();
            var session = SessionControl.For(cache);
            session.Stop(session.Generation, SessionControl.Status.Banned);
            Assert.IsFalse(session.BeginReauthentication());
        }

        [Test]
        public void LoadExistingPopupWithAllStatusBindings()
        {
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/DCL/UI/DuplicateIdentityPopup/DuplicateIdentityWindow.prefab");
            var view = prefab.GetComponent<UI.DuplicateIdentityPopup.DuplicateIdentityWindowView>();
            Assert.IsNotNull(view.Title);
            Assert.IsNotNull(view.Description);
            Assert.IsNotNull(view.ActionLabel);
            Assert.IsNotNull(view.ExitButton);
        }

        [Test]
        public void FenceRoomOperationAcrossPendingAndSuccessfulAssignment()
        {
            using var cache = new MemoryWeb3IdentityCache();
            var session = SessionControl.For(cache);
            int generation = session.Generation;
            int revision = session.Revision;
            Assert.IsTrue(session.CanCompleteOperation(generation, revision));
            session.Pending(generation, 1000, DateTime.UtcNow);
            session.AcceptAssignment(generation);
            Assert.IsFalse(session.CanCompleteOperation(generation, revision));
            Assert.IsTrue(session.CanCompleteOperation(generation, session.Revision));
        }

        [Test]
        public void DecodeAndReencodeCanonicalProtocolGoldens()
        {
            string path = Path.Combine(UnityEngine.Application.dataPath, "../TestResources/iteration-2/session-control.json");
            foreach (JToken fixture in JArray.Parse(File.ReadAllText(path)))
            {
                string hex = fixture["hex"]!.Value<string>()!;
                var bytes = new byte[hex.Length / 2];
                for (var i = 0; i < bytes.Length; i++) bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
                IMessage message = fixture["type"]!.Value<string>() switch
                {
                    "ServerPacket" => ServerPacket.Parser.ParseFrom(bytes),
                    "KickedMessage" => KickedMessage.Parser.ParseFrom(bytes),
                    "SessionStatusMessage" => SessionStatusMessage.Parser.ParseFrom(bytes),
                    _ => throw new InvalidOperationException("Unknown fixture message type"),
                };
                CollectionAssert.AreEqual(bytes, message.ToByteArray(), fixture["name"]!.Value<string>());
                if (message is ServerPacket { MessageCase: ServerPacket.MessageOneofCase.SessionStatus } packet)
                    Assert.IsTrue(Enum.IsDefined(typeof(SessionStatus), packet.SessionStatus.State));
            }
        }

        [Test]
        public void RejectUnreasonablePendingDuration()
        {
            using var cache = new MemoryWeb3IdentityCache();
            var session = SessionControl.For(cache);
            session.Pending(session.Generation, uint.MaxValue, DateTime.UtcNow);
            Assert.AreEqual(SessionControl.Status.Failed, session.Current);
        }

        [Test]
        public void FailWhenAllAssignmentControlPacketsAreLost()
        {
            using var cache = new MemoryWeb3IdentityCache();
            var session = SessionControl.For(cache);
            DateTime now = DateTime.UtcNow;
            session.AwaitAssignment(session.Generation, now);
            Assert.IsTrue(session.CanRecover(session.Generation), "Initial Pulse authentication must remain possible");
            session.AwaitAssignment(session.Generation, now.AddSeconds(29));
            session.CheckWatchdog(now.AddSeconds(31));
            Assert.AreEqual(SessionControl.Status.Failed, session.Current);
        }

        [Test]
        public async Task ClearCachedIdentityBeforeInvokingAuthenticationAndRejectDisplacedKey()
        {
            using var identity = new IWeb3Identity.Random();
            using var cache = new MemoryWeb3IdentityCache();
            cache.Identity = identity;
            var session = SessionControl.For(cache);
            session.Stop(session.Generation, SessionControl.Status.Superseded);
            var invoked = false;
            try
            {
                await session.ReauthenticateAsync(_ =>
                {
                    invoked = true;
                    Assert.IsNull(cache.Identity);
                    Assert.IsFalse(session.CanRecover(session.Generation));
                    cache.Identity = identity;
                    return UniTask.CompletedTask;
                }, CancellationToken.None);
                Assert.Fail("The displaced key must not be accepted");
            }
            catch (InvalidOperationException) { }
            Assert.IsTrue(invoked);
            Assert.AreEqual(SessionControl.Status.Failed, session.Current);
        }

        [Test]
        public async Task ResumeOnlyAfterAuthenticationSuppliesDifferentKey()
        {
            using var oldIdentity = new IWeb3Identity.Random();
            using var newIdentity = new IWeb3Identity.Random();
            using var cache = new MemoryWeb3IdentityCache();
            cache.Identity = oldIdentity;
            var session = SessionControl.For(cache);
            int oldGeneration = session.Generation;
            session.Stop(oldGeneration, SessionControl.Status.Superseded);
            await session.ReauthenticateAsync(_ =>
            {
                cache.Identity = newIdentity;
                return UniTask.CompletedTask;
            }, CancellationToken.None);
            Assert.AreNotEqual(oldGeneration, session.Generation);
            Assert.IsTrue(session.CanRecover(session.Generation));
            Assert.IsFalse(session.AcceptAssignment(oldGeneration));
        }
    }
}
