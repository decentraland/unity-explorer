using System;

namespace DCL.Web3
{
    [Serializable]
    public struct EthApiRequest
    {
        public long id;
        public string method;
        public object[] @params;
    }
}
