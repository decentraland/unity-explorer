using Arch.Core;
using DCL.CharacterMotion.Components;
using System;
using UnityEngine;

namespace ECS.SceneLifeCycle
{
    public static class TeleportUtils
    {
        private const string TRAM_LINE_TITLE = "Tram Line";
        private const string LONG_ROAD_TITLE = "Long Road";

        public static bool IsRoad(ReadOnlySpan<char> originalJson)
        {
            ReadOnlySpan<char> span = ExtractTitleValue(originalJson);

            return span.SequenceEqual(TRAM_LINE_TITLE.AsSpan())
                || span.SequenceEqual(LONG_ROAD_TITLE.AsSpan());
        }

        private static ReadOnlySpan<char> ExtractTitleValue(ReadOnlySpan<char> json)
        {
            int titleIndex = json.IndexOf(@"""title"":");

            if (titleIndex == -1)
                return ReadOnlySpan<char>.Empty;

            // Move to the start of the title value (after "title": ")
            int valueStartIndex = json[titleIndex..].IndexOf(':') + 1;
            ReadOnlySpan<char> valueSpan = json.Slice(titleIndex + valueStartIndex);

            int openQuoteIndex = valueSpan.IndexOf('"');

            if (openQuoteIndex == -1)
                return ReadOnlySpan<char>.Empty;

            int closeQuoteIndex = valueSpan[(openQuoteIndex + 1)..].IndexOf('"');

            if (closeQuoteIndex == -1)
                return ReadOnlySpan<char>.Empty;

            return valueSpan.Slice(openQuoteIndex + 1, closeQuoteIndex);
        }

        public static PlayerTeleportingState GetTeleportParcel(World world, Entity playerEntity)
        {
            var teleportParcel = new PlayerTeleportingState();

            if (world.TryGet(playerEntity, out PlayerTeleportIntent playerTeleportIntent))
            {
                teleportParcel.IsTeleporting = true;
                teleportParcel.Parcel = playerTeleportIntent.Parcel;
            }

            if (world.TryGet(playerEntity, out PlayerTeleportIntent.JustTeleported justTeleported))
            {
                teleportParcel.IsTeleporting = true;
                teleportParcel.Parcel = justTeleported.Parcel;
            }

            return teleportParcel;
        }

        public struct PlayerTeleportingState
        {
            public Vector2Int Parcel;
            public bool IsTeleporting;
        }
    }
}
