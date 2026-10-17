namespace DCL.NftPrompt
{
    public partial class NftPromptController
    {
        public struct Params
        {
            public string Chain { get; }
            public string ContractAddress { get; }
            public string TokenId { get; }

            public Params(string chain, string contractAddress, string tokenId)
            {
                Chain = chain;
                ContractAddress = contractAddress;
                TokenId = tokenId;
            }
        }
    }
}
