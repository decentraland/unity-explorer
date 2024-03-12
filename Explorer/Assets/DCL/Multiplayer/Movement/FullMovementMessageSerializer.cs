using System;
using System.Text;
using UnityEngine;

namespace DCL.Multiplayer.Movement
{
    public static class FullMovementMessageSerializer
    {
        public static byte[] SerializeMessage(FullMovementMessage fullMovementMessage)
        {
            string? json = JsonUtility.ToJson(fullMovementMessage);
            return Encoding.UTF8.GetBytes(json);
        }

        public static FullMovementMessage? DeserializeMessage(ReadOnlySpan<byte> data)
        {
            try
            {
                string jsonString = Encoding.UTF8.GetString(data.ToArray());
                return JsonUtility.FromJson<FullMovementMessage>(jsonString);
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
