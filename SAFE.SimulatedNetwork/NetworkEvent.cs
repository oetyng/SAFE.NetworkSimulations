using Org.BouncyCastle.Math;
using System.Collections.Generic;

namespace SAFE.SimulatedNetwork
{
    public class NetworkEvent
    {
        public BigInteger Hash { get; set; }
        public List<Section> NewSections { get; set; } = new List<Section>();
        public Vault VaultToRelocate { get; set; }

        public const int networkeventHashBits = 256;
        public const int networkeventHashBytes = networkeventHashBits / 8;

        public static readonly byte[] LargestHashValue = new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF };

        public NetworkEvent()
        {
            // create a hash from prng
            var bytes = new byte[networkeventHashBytes];
            Constants.prng.NextBytes(bytes);
            Hash = new BigInteger(bytes);
        }

        // calculates x = b.hash % divisor and returns x == 0
        public bool HashModIsZero(BigInteger divisor)
        {
            var x = Hash.Mod(divisor);
            //x.Mod(Hash, divisor)
            return x.CompareTo(new BigInteger("0")) == 0;
        }
    }
}
