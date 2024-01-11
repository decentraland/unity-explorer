using System;

namespace DCL.Web3
{
    [Serializable]
    public struct EthApiRequest
    {
        public string method;
        public object[] @params;
    }
}
