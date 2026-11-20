namespace DCL.MarketplaceCredits.Purchase
{
    /// <summary>
    ///     A contract that accepts Decentraland native meta-transactions: its address and the EIP-712 domain it
    ///     verifies signatures against. <see cref="IsClassic" /> selects the older variant, which signs a
    ///     `functionSignature` field and executes with the signature split into r, s and v (MANA and the
    ///     CollectionStore); the others sign `functionData` and take the signature whole.
    /// </summary>
    public readonly struct MetaTxContract
    {
        public readonly string Address;
        public readonly string Eip712Name;
        public readonly string Eip712Version;
        public readonly bool IsClassic;

        public MetaTxContract(string address, string eip712Name, string eip712Version, bool isClassic)
        {
            Address = address;
            Eip712Name = eip712Name;
            Eip712Version = eip712Version;
            IsClassic = isClassic;
        }
    }
}
