using DCL.Multiplayer.Movement.MessageBusMock;
using System;
using System.Text;
using UnityEngine;

namespace DCL.Multiplayer.Movement.ECS.System
{
    public static class MessageMockSerializer
    {
        public static byte[] SerializeMessage(MessageMock message)
        {
            string? json = JsonUtility.ToJson(message);
            return Encoding.UTF8.GetBytes(json);
        }

        public static MessageMock DeserializeMessage(ReadOnlySpan<byte> data)
        {
            string jsonString = Encoding.UTF8.GetString(data.ToArray());
            return JsonUtility.FromJson<MessageMock>(jsonString);
        }
    }
}
