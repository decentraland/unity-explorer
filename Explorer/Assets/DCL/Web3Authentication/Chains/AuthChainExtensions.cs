using Newtonsoft.Json;

namespace DCL.Web3Authentication.Chains
{
    public static class AuthChainExtensions
    {
        public static string ToJson(this AuthChain authChain) =>
            JsonConvert.SerializeObject(authChain);

        public static string ToJson(this AuthLink link) =>
            JsonConvert.SerializeObject(link);
    }
}
