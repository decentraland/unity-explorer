using Cysharp.Threading.Tasks;
using System;

namespace DCL.PlacesAPIService
{
    public class PlacesAPIException : Exception
    {
        internal PlacesAPIException(UnityWebRequestException exception, string message) : base(message, exception) { }

        internal PlacesAPIException(string context, string json) : base($"{context}\n{json}") { }

        internal PlacesAPIException(string message) : base(message) { }
    }
}
