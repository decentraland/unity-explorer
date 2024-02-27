using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.CharacterMotion.Components;
using DCL.Multiplayer.Movement.MessageBusMock;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Random = UnityEngine.Random;

namespace DCL.Multiplayer.Movement.ECS
{
    public class MessagePipeMock
    {
        private readonly Queue<MessageMock> IncomingMessages = new ();

        public readonly MessagePipeSettings Settings;
        private readonly CharacterController playerCharacter;
        private readonly Entity playerEntity;
        private readonly World world;

        private readonly CancellationTokenSource cts;

        private MessageMock lastSend;
        public InterpolationType InterpolationType => Settings.InterpolationType;
        public int Count => IncomingMessages.Count;

        public MessagePipeMock(MessagePipeSettings settings, CharacterController playerCharacter, Entity playerEntity, World world)
        {
            this.Settings = settings;
            this.playerCharacter = playerCharacter;
            this.playerEntity = playerEntity;
            this.world = world;

            this.Settings.InboxCount = 0;
            this.Settings.PackageLost = 0;
            this.Settings.StartSending = false;

            this.Settings.startButton.Enable();
            this.Settings.packageLostButton.Enable();
            this.Settings.packageBlockButton.Enable();

            this.Settings.startButton.performed += _ => settings.StartSending = !settings.StartSending;
            this.Settings.packageLostButton.performed += _ => settings.PackageLost++;

            cts = new CancellationTokenSource();

            StartSendPackages(cts.Token).Forget();
        }

        public MessageMock Dequeue()
        {
            MessageMock message = IncomingMessages.Dequeue();
            Settings.InboxCount = IncomingMessages.Count;

            return message;
        }

        private async UniTask StartSendPackages(CancellationToken ctsToken)
        {
            while (!ctsToken.IsCancellationRequested)
            {
                if (!Settings.StartSending)
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(Settings.PackageSentRate), cancellationToken: ctsToken);
                    continue;
                }

                if (Settings.PackageLost > 0)
                    Settings.PackageLost--;
                else if (!Settings.packageBlockButton.IsPressed())
                {
                    Send(Time.unscaledTime, playerCharacter.transform.position, playerCharacter.velocity,
                        world.Get<CharacterAnimationComponent>(playerEntity),
                        world.Get<StunComponent>(playerEntity));

                    // PutMark(); send mark
                }

                await UniTask.Delay(TimeSpan.FromSeconds(Settings.PackageSentRate), cancellationToken: ctsToken);
            }
        }

        private void Send(float timestamp, Vector3 position, Vector3 velocity, CharacterAnimationComponent animState, StunComponent stun)
        {
            var message = new MessageMock
            {
                timestamp = timestamp,
                position = position,
                velocity = velocity,
                animState = animState.States,
                isStunned = stun.IsStunned,
            };

            UniTask.Delay(
                        TimeSpan.FromSeconds(Settings.Latency
                                             + (Settings.Latency * Random.Range(0, Settings.LatencyJitter))
                                             + (Settings.PackageSentRate * Random.Range(0, Settings.PackagesJitter))))
                   .ContinueWith(() =>
                    {
                        lastSend = message;
                        IncomingMessages.Enqueue(message);
                        Settings.InboxCount = IncomingMessages.Count;

                        // PutMark(newMessage, receivedMark, 0.11f); Received mark
                    })
                   .Forget();
        }
    }
}
