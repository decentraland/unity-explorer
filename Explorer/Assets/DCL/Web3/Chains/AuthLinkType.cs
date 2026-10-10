using System;

namespace DCL.Web3.Chains
{
    [Serializable]

    // ReSharper disable InconsistentNaming
    public enum AuthLinkType
    {
        SIGNER = 0,
        ECDSA_EPHEMERAL = 1,
        ECDSA_SIGNED_ENTITY = 2,
        /**
         * See https://github.com/ethereum/EIPs/issues/1654
         */
        ECDSA_EIP_1654_EPHEMERAL,
        /**
         * See https://github.com/ethereum/EIPs/issues/1654
         */
        ECDSA_EIP_1654_SIGNED_ENTITY,
    }
}
