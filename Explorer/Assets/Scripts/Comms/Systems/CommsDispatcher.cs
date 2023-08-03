using DCLCrypto;
using Decentraland.Kernel.Comms.Rfc5;
using Google.Protobuf;
using Google.Protobuf.Collections;
using System;
using System.Collections.Generic;
using UnityEngine;
using WebSocketSharp;
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
            Handshake,
            Connected
        }

        private WSCommsRoomState state = WSCommsRoomState.Disconnected;

        private AuthIdentity authIdentity;

        private WebSocket webSocket;

        // Data produced from Comms
        private Dictionary<uint, PeerInfo> peersIdentities = new ();
        private uint myAlias;

        public WSCommsRoom()
        {
            // TODO: Change how we create the AuthIdentity when we have Wallet Connect using Authenticator.InitializeAuthChain
            authIdentity = Authenticator.CreateRandomInsecureAuthIdentity();
        }

        public void Connect(string url)
        {
            webSocket = new WebSocket(url);

            webSocket.OnMessage += OnMessage;

            webSocket.OnOpen += OnOpen;

            webSocket.Connect();
        }

        private void OnOpen(object sender, EventArgs e)
        {
            state = WSCommsRoomState.Handshake;

            var joinMessage = new WsPacket()
            {
                PeerIdentification = new WsIdentification()
                {
                    Address = authIdentity.EthAddress
                }
            };

            webSocket.Send(joinMessage.ToByteArray());
        }

        private void OnMessage(object sender, MessageEventArgs e)
        {
            var packet = WsPacket.Parser.ParseFrom(e.RawData);

            Debug.Log($"Received: {packet.MessageCase}");

            switch (state)
            {
                case WSCommsRoomState.Handshake:
                    HandleHandshake(packet);
                    break;

                case WSCommsRoomState.Connected:
                    HandleConnectedMessages(packet);
                    break;
                default: break;
            }
        }

        private void HandleConnectedMessages(WsPacket packet)
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
                    break;

                case WsPacket.MessageOneofCase.PeerLeaveMessage:
                    break;

                case WsPacket.MessageOneofCase.PeerUpdateMessage:
                    var rfc4Packet = Rfc4.Packet.Parser.ParseFrom(packet.PeerUpdateMessage.Body);
                    HandleUpdateMessage(packet.PeerUpdateMessage.FromAlias, rfc4Packet);
                    break;

                case WsPacket.MessageOneofCase.PeerKicked:
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
                    break;
            }
        }

        private void HandleHandshake(WsPacket packet)
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

                    webSocket.Send(joinMessage.ToByteArray());
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

                    break;

                case WsPacket.MessageOneofCase.PeerJoinMessage:
                    peersIdentities.Add(packet.PeerJoinMessage.Alias, new PeerInfo()
                    {
                        alias = packet.PeerJoinMessage.Address,
                    });

                    break;

                case WsPacket.MessageOneofCase.PeerLeaveMessage:
                    peersIdentities.Remove(packet.PeerLeaveMessage.Alias);
                    break;

                case WsPacket.MessageOneofCase.PeerKicked:
                    Debug.LogError("We were kicked from the WS Rooms.");
                    peersIdentities.Clear();
                    state = WSCommsRoomState.Disconnected;
                    webSocket.Close();
                    break;

                default:
                    break;
            }
        }

        public void UpdatePlayerPosition(Rfc4.Position position)
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

            webSocket.Send(joinMessage.ToByteArray());
        }
    }
}
