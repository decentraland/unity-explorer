﻿using System;
using System.Collections.Generic;
using System.Text;

namespace SocketIOClient.Messages
{
    public static class MessageFactory
    {
        private static IMessage CreateMessage(MessageType type)
        {
            switch (type)
            {
                case MessageType.Opened:
                    return new OpenedMessage();
                case MessageType.Ping:
                    return new PingMessage();
                case MessageType.Pong:
                    return new PongMessage();
                case MessageType.Connected:
                    return new ConnectedMessage();
                case MessageType.Disconnected:
                    return new DisconnectedMessage();
                case MessageType.EventMessage:
                    return new EventMessage();
                case MessageType.AckMessage:
                    return new ClientAckMessage();
                case MessageType.ErrorMessage:
                    return new ErrorMessage();
                case MessageType.BinaryMessage:
                    return new BinaryMessage();
                case MessageType.BinaryAckMessage:
                    return new ClientBinaryAckMessage();
                case MessageType.DefaultTextMessage:
                    return new DefaultTextMessage();
            }

            return null;
        }

        public static IMessage CreateMessage(EngineIO eio, string msg)
        {
            if (eio == EngineIO.WebSocketDefault)
            {
                IMessage result = CreateMessage(MessageType.DefaultTextMessage);

                if (result != null)
                {
                    result.EIO = eio;
                    result.Read(msg);
                    return result;
                }
            }

            Array enums = Enum.GetValues(typeof(MessageType));

            foreach (MessageType item in enums)
            {
                var prefix = ((int)item).ToString();

                if (msg.StartsWith(prefix))
                {
                    IMessage result = CreateMessage(item);

                    if (result != null)
                    {
                        result.EIO = eio;
                        result.Read(msg.Substring(prefix.Length));
                        return result;
                    }
                }
            }

            return null;
        }

        public static OpenedMessage CreateOpenedMessage(string msg)
        {
            var openedMessage = new OpenedMessage();

            if (msg[0] == '0')
            {
                openedMessage.EIO = EngineIO.V4;
                openedMessage.Read(msg.Substring(1));
            }
            else
            {
                openedMessage.EIO = EngineIO.V3;
                int index = msg.IndexOf(':');
                openedMessage.Read(msg.Substring(index + 2));
            }

            return openedMessage;
        }
    }
}
