using CommunicationData.URLHelpers;
using System.Text.RegularExpressions;

namespace ECS.SceneLifeCycle.Realm
{
    public static class EnsExtensions
    {
        private static readonly Regex REGEX = new (@"^[a-zA-Z0-9.]+\.eth$");

        public static bool IsEns(this string str) =>
            REGEX.Match(str).Success;

        public static bool IsEns(this URLDomain domain) =>
            REGEX.Match(domain.Value).Success;
    }
}
