using System.Text.RegularExpressions;
using UnityEngine;

namespace DCL.UserInAppInitializationFlow.StartupOperations
{
    public static class RealmHelper
    {
        public static bool TryParseParcelFromString(string? positionString, out Vector2Int parcel)
        {
            parcel = Vector2Int.zero;

            if (string.IsNullOrEmpty(positionString)) return false;

            var matches = new Regex(@"-*\d+").Matches(positionString);

            if (matches.Count > 1)
            {
                parcel.x = int.Parse(matches[0].Value);
                parcel.y = int.Parse(matches[1].Value);
                return true;
            }

            return false;
        }
    }
}