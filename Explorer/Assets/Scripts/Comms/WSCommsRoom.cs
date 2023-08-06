using DCLCrypto;
using Decentraland.Kernel.Comms.Rfc5;
using Google.Protobuf;
using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Rfc4 = Decentraland.Kernel.Comms.Rfc4;

namespace Comms.Systems
{
    public struct PeerInfo
    {
        public string alias;
        public Rfc4.Position position;
    }

    public class WSCommsRoom
    {
        private enum WSCommsRoomState
        {
            Disconnected,
            Connecting,
            Handshake,
            Connected
        }

        private WSCommsRoomState state = WSCommsRoomState.Disconnected;

        private AuthIdentity authIdentity;

        private ClientWebSocket webSocket;

        // Data produced from Comms
        private Dictionary<uint, PeerInfo> peersIdentities = new ();
        private uint myAlias;

        private CancellationTokenSource _cancellationTokenSource;
        private bool _ready = false;

        // Pre-cache received buffer
        private const int ARRAY_SIZE = 8192;
        byte[] receiveBuffer = new byte[ARRAY_SIZE];

        // Events
        public event EventHandler<string> OnChatMessage;

        public WSCommsRoom()
        {
            // TODO: Change how we create the AuthIdentity when we have Wallet Connect using Authenticator.InitializeAuthChain
            authIdentity = Authenticator.CreateRandomInsecureAuthIdentity();

            _cancellationTokenSource = new CancellationTokenSource();
        }

        public async UniTask Connect(Uri url)
        {
            webSocket = new ClientWebSocket();

            //Implementation of timeout of 5000 ms
            var cts = new CancellationTokenSource();
            cts.CancelAfter(5000);

            state = WSCommsRoomState.Connecting;

            await webSocket.ConnectAsync(url, cts.Token);
            _ready = true;
        }

        public bool IsConnected()
        {
            return webSocket is { State: WebSocketState.Open } && _ready;
        }

        private async UniTask<ArraySegment<byte>> ReceiveNextMessage()
        {
            var offset = 0;

            while (true)
            {
                var buffer = new ArraySegment<byte>(receiveBuffer, offset, 128);

                var result = await webSocket.ReceiveAsync(buffer,
                    _cancellationTokenSource.Token);

                if (result.Count <= 0) continue;

                offset += result.Count;

                if (result.EndOfMessage)
                    break;
            }

            return new ArraySegment<byte>(receiveBuffer, 0, offset);
        }

        public async UniTask ProcessNextMessage()
        {
            if (webSocket.State == WebSocketState.Open)
            {
                if (state == WSCommsRoomState.Connecting)
                {
                    await SendIdentification();
                    state = WSCommsRoomState.Handshake;
                }

                if (state is WSCommsRoomState.Handshake or WSCommsRoomState.Connected)
                {
                    var message = await ReceiveNextMessage();

                    var packet = WsPacket.Parser.ParseFrom(message.Array, 0, message.Count);

                    Debug.Log($"Received: {packet.MessageCase}");

                    switch (state)
                    {
                        case WSCommsRoomState.Handshake:
                            await HandleHandshake(packet);
                            break;

                        case WSCommsRoomState.Connected:
                            await HandleConnectedMessages(packet);
                            break;
                        default: break;
                    }
                }
            }
        }

        private async UniTask HandleConnectedMessages(WsPacket packet)
        {
            switch (packet.MessageCase)
            {
                case WsPacket.MessageOneofCase.ChallengeMessage:
                case WsPacket.MessageOneofCase.PeerIdentification:
                case WsPacket.MessageOneofCase.SignedChallengeForServer:
                case WsPacket.MessageOneofCase.WelcomeMessage:
                    Debug.LogError($"unexpected message {packet.MessageCase}");
                    break;

                case WsPacket.MessageOneofCase.PeerJoinMessage:
                    Debug.Log($"PeerJoinMessage {packet.PeerJoinMessage.Alias}");

                    peersIdentities.Add(packet.PeerJoinMessage.Alias, new PeerInfo()
                    {
                        alias = packet.PeerJoinMessage.Address,
                    });

                    break;

                case WsPacket.MessageOneofCase.PeerLeaveMessage:
                    Debug.Log($"PeerLeaveMessage {packet.PeerLeaveMessage.Alias}");
                    peersIdentities.Remove(packet.PeerLeaveMessage.Alias);
                    break;

                case WsPacket.MessageOneofCase.PeerKicked:
                    Debug.LogError("We were kicked from the WS Rooms.");
                    peersIdentities.Clear();
                    state = WSCommsRoomState.Disconnected;

                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Kicked",
                        _cancellationTokenSource.Token);

                    break;

                case WsPacket.MessageOneofCase.PeerUpdateMessage:
                    Debug.Log($"PeerUpdateMessage {packet.PeerUpdateMessage.FromAlias}");
                    var rfc4Packet = Rfc4.Packet.Parser.ParseFrom(packet.PeerUpdateMessage.Body);
                    HandleUpdateMessage(packet.PeerUpdateMessage.FromAlias, rfc4Packet);
                    break;

                default:
                    break;
            }
        }

        private void HandleUpdateMessage(uint alias, Rfc4.Packet packet)
        {
            switch (packet.MessageCase)
            {
                case Rfc4.Packet.MessageOneofCase.Chat:
                    Debug.Log($"Chat: {alias} {packet.Chat.Message}");
                    OnChatMessage?.Invoke(this, $"{alias.ToString()}> {packet.Chat.Message}");
                    break;
                case Rfc4.Packet.MessageOneofCase.Position:
                    if (peersIdentities.TryGetValue(alias, out PeerInfo value)) { value.position = packet.Position; }

                    break;
                case Rfc4.Packet.MessageOneofCase.Scene:

                    break;
                case Rfc4.Packet.MessageOneofCase.Voice:
                    // ignore, the comms it's going to be implemented throw LiveKit
                    break;
                case Rfc4.Packet.MessageOneofCase.ProfileRequest:
                case Rfc4.Packet.MessageOneofCase.ProfileResponse:
                case Rfc4.Packet.MessageOneofCase.ProfileVersion:
                    // TODO: Implement for the Profile
                    Debug.Log($"Profile Message {alias}: {packet.MessageCase}");
                    break;
            }
        }

        private async UniTask HandleHandshake(WsPacket packet)
        {
            switch (packet.MessageCase)
            {
                case WsPacket.MessageOneofCase.ChallengeMessage:
                    var challenge = packet.ChallengeMessage;
                    var signed = Authenticator.SignPayload(authIdentity, challenge.ChallengeToSign);

                    var joinMessage = new WsPacket()
                    {
                        SignedChallengeForServer = new WsSignedChallenge()
                        {
                            AuthChainJson = signed.ToJsonString()
                        }
                    };

                    await webSocket.SendAsync(joinMessage.ToByteArray(), WebSocketMessageType.Binary, true,
                        _cancellationTokenSource.Token);

                    break;

                case WsPacket.MessageOneofCase.WelcomeMessage:
                    myAlias = packet.WelcomeMessage.Alias;

                    foreach (var (key, peerAlias) in packet.WelcomeMessage.PeerIdentities)
                    {
                        peersIdentities.Add(key, new PeerInfo()
                        {
                            alias = peerAlias,
                        });
                    }

                    state = WSCommsRoomState.Connected;

                    break;

                default:
                    break;
            }
        }

        public async UniTask SendPlayerPosition(Rfc4.Position position)
        {
            var joinMessage = new WsPacket()
            {
                PeerUpdateMessage = new WsPeerUpdate()
                {
                    FromAlias = myAlias,
                    Body = position.ToByteString(),
                    Unreliable = false,
                }
            };

            await webSocket.SendAsync(joinMessage.ToByteArray(), WebSocketMessageType.Binary, true, _cancellationTokenSource.Token);
        }

        public async UniTask SendChat(string message)
        {
            var chatMessage = new Rfc4.Chat
            {
                Message = message,
                Timestamp = DateTime.Now.Ticks,
            };

            var joinMessage = new WsPacket()
            {
                PeerUpdateMessage = new WsPeerUpdate()
                {
                    FromAlias = myAlias,
                    Body = chatMessage.ToByteString(),
                    Unreliable = false,
                }
            };

            await webSocket.SendAsync(joinMessage.ToByteArray(), WebSocketMessageType.Binary, true, _cancellationTokenSource.Token);
        }

        private async UniTask SendIdentification()
        {
            var joinMessage = new WsPacket()
            {
                PeerIdentification = new WsIdentification()
                {
                    Address = authIdentity.EthAddress
                }
            };

            await webSocket.SendAsync(joinMessage.ToByteArray(), WebSocketMessageType.Binary, true, _cancellationTokenSource.Token);
        }
    }
}
