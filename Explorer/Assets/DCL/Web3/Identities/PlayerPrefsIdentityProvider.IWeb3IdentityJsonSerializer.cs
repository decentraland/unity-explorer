namespace DCL.Web3.Identities
{
    public partial class PlayerPrefsIdentityProvider
    {
        public interface IWeb3IdentityJsonSerializer
        {
            IWeb3Identity? Deserialize(string json);

            string Serialize(IWeb3Identity identity);
        }
    }
}
