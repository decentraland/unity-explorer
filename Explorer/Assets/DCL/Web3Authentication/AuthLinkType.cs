using System;

namespace DCL.Web3Authentication
{
    [Serializable]

    // ReSharper disable InconsistentNaming
    public enum AuthLinkType
    {
        SIGNER,
        ECDSA_EPHEMERAL,
        ECDSA_SIGNED_ENTITY,
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
