using Ipfs;
using System.IO;

namespace DCL.Ipfs
{
    public static class HashingExtensions
    {
        public static string IpfsHashV1(this byte[] content)
        {
            var sha2256MultiHash = MultiHash.ComputeHash(content);
            var cid = new Cid { Encoding = "base32", ContentType = "raw", Hash = sha2256MultiHash, Version = 1 };
            var hash = cid.ToString();
            return hash;
        }

        public static string IpfsHashV1(this Stream stream)
        {
            var sha2256MultiHash = MultiHash.ComputeHash(stream);
            var cid = new Cid { Encoding = "base32", ContentType = "raw", Hash = sha2256MultiHash, Version = 1 };
            var hash = cid.ToString();
            return hash;
        }
    }
}
