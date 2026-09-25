using DCL.PlacesAPIService;
using NUnit.Framework;
using System;
using UnityEngine;

namespace DCL.Tests.Editor
{
    public class PlaceInfoShould
    {
        private const int USER_COUNT = 7;

        [Test]
        public void NullifyEmptyConnectedAddressesOnDeserialization()
        {
            var placeInfo = new PlacesData.PlaceInfo(Vector2Int.zero)
            {
                connected_addresses = Array.Empty<string>(),
                user_count = USER_COUNT,
            };

            placeInfo.OnAfterDeserialize();

            Assert.IsNull(placeInfo.connected_addresses);
            Assert.AreEqual(USER_COUNT, placeInfo.connected_addresses?.Length ?? placeInfo.user_count);
        }

        [Test]
        public void KeepPopulatedConnectedAddressesOnDeserialization()
        {
            var placeInfo = new PlacesData.PlaceInfo(Vector2Int.zero)
            {
                connected_addresses = new[] { "0x1", "0x2" },
                user_count = USER_COUNT,
            };

            placeInfo.OnAfterDeserialize();

            Assert.AreEqual(2, placeInfo.connected_addresses?.Length ?? placeInfo.user_count);
        }

        [Test]
        public void LeaveConnectedAddressesNullWhenAbsentFromResponse()
        {
            var json = $"{{\"ok\":true,\"total\":1,\"data\":[{{\"id\":\"place-id\",\"positions\":[\"0,0\"],\"base_position\":\"0,0\",\"user_count\":{USER_COUNT}}}]}}";

            var response = JsonUtility.FromJson<PlacesData.PlacesAPIResponse>(json);
            PlacesData.PlaceInfo placeInfo = response.data[0];

            Assert.IsNull(placeInfo.connected_addresses);
            Assert.AreEqual(USER_COUNT, placeInfo.connected_addresses?.Length ?? placeInfo.user_count);
        }

        [Test]
        public void ParseConnectedAddressesWhenPresentInResponse()
        {
            var json = $"{{\"ok\":true,\"total\":1,\"data\":[{{\"id\":\"place-id\",\"positions\":[\"0,0\"],\"base_position\":\"0,0\",\"user_count\":{USER_COUNT},\"connected_addresses\":[\"0x1\",\"0x2\"]}}]}}";

            var response = JsonUtility.FromJson<PlacesData.PlacesAPIResponse>(json);
            PlacesData.PlaceInfo placeInfo = response.data[0];

            Assert.AreEqual(2, placeInfo.connected_addresses?.Length ?? placeInfo.user_count);
        }
    }
}
