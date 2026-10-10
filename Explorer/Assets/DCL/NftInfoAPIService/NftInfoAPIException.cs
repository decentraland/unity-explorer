using Cysharp.Threading.Tasks;
using System;

namespace DCL.NftInfoAPIService
{
    public class NftInfoAPIException : Exception
    {
        internal NftInfoAPIException(UnityWebRequestException exception, string message) : base(message, exception) { }
    }
}
