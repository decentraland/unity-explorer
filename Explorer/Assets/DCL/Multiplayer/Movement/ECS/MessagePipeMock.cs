using Cysharp.Threading.Tasks;
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
        public readonly Queue<MessageMock> IncomingMessages = new ();

        private readonly MessagePipeSettings settings;
        private readonly CharacterController playerCharacter;

        private readonly CancellationTokenSource cts;

        public MessagePipeMock(MessagePipeSettings settings, CharacterController playerCharacter)
        {
            this.settings = settings;
            this.playerCharacter = playerCharacter;

            this.settings.InboxCount = 0;
            this.settings.PackageLost = 0;
            this.settings.StartSending = false;

            this.settings.startButton.Enable();
            this.settings.packageLostButton.Enable();
            this.settings.packageBlockButton.Enable();

            this.settings.startButton.performed += _ => settings.StartSending = !settings.StartSending;
            this.settings.packageLostButton.performed += _ => settings.PackageLost++;

            cts = new CancellationTokenSource();

            StartSendPackages(cts.Token).Forget();
        }

        public InterpolationType InterpolationType => settings.InterpolationType;

        private async UniTask StartSendPackages(CancellationToken ctsToken)
        {
            while (!ctsToken.IsCancellationRequested)
            {
                if (!settings.StartSending)
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(settings.PackageSentRate), cancellationToken: ctsToken);
                    continue;
                }

                if (settings.PackageLost > 0)
                    settings.PackageLost--;
                else if (!this.settings.packageBlockButton.IsPressed())
                {
                    Send(Time.time, playerCharacter.transform.position, playerCharacter.velocity);
                    // PutMark(); send mark
                }

                await UniTask.Delay(TimeSpan.FromSeconds(settings.PackageSentRate), cancellationToken: ctsToken);
            }
        }

        private void Send(float timestamp, Vector3 position, Vector3 velocity)
        {
            var message = new MessageMock
            {
                timestamp = timestamp,
                position = position,
                velocity = velocity,
            };

            UniTask.Delay(
                        TimeSpan.FromSeconds(settings.Latency
                                             + (settings.Latency * Random.Range(0, settings.LatencyJitter))
                                             + (settings.PackageSentRate * Random.Range(0, settings.PackagesJitter))))
                   .ContinueWith(() =>
                    {
                        IncomingMessages.Enqueue(message);
                        settings.InboxCount = IncomingMessages.Count;

                        // PutMark(newMessage, receivedMark, 0.11f); Received mark
                    })
                   .Forget();
        }
    }
}
