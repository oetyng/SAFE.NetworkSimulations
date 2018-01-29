using System;
using System.Collections;
using System.Collections.Generic;

namespace SAFE.SimulatedNetwork
{
    public class Vault
    {
        public XorName Name { get; private set; }
        public Prefix Prefix { get; private set; }
        public int Age { get; private set; }
        public bool IsAttacker { get; set; }
        
        public Vault()
        {
            Name = new XorName();
		    Age = 1;
        }

        public void SetPrefix(Prefix p)
        {
            Prefix = p;
        }

        public void IncrementAge()
        {
            Age++;
        }

        public bool IsAdult()
        {
            return Age > 4;
        }

        // @mav:
        // There should never be a prefix longer than the vault name length (vault name length is constant at 256 bits). 
        // This would mean there’s approx 2^256 sections in the network which is pretty massive.
        public void RenameWithPrefix(Prefix p)
        {
            if (p.Bits.Count > Name.Bits.Count)
                Console.WriteLine("Warning: prefix bit count longer than name bit count!");

            var newBits = Name.Bits.Count >= p.Bits.Count ? 
                new BitArray(Name.Bits) : new BitArray(p.Bits);

            for (int i = 0; i < p.Bits.Count; i++)
                newBits.Set(i, p.Bits[i]);

            Name = new XorName(newBits);
        }       
    }

    public class OldestFirst : List<Vault>
    {
        public OldestFirst(List<Vault> vaults)
            : base(vaults)
        {
            this.Sort(new AgeBasedComparer());
        }

        class AgeBasedComparer : IComparer<Vault>
        {
            public int Compare(Vault a, Vault b)
            {
                if ((a.Age == b.Age))
                    return ResolveAgeTiebreaker(a, b);
                if ((a.Age > b.Age)) // oldest first
                    return -1;

                return 1;
            }
        }

        // TODO: Confirm this one actually works as original version.
        static int ResolveAgeTiebreaker(Vault vi, Vault vj)
        {
            // ties in age are resolved by XOR their public keys together and find the
            // one XOR closest to it
            // see https://forum.safedev.org/t/data-chains-deeper-dive/1209
            // in this case the vault xorname is used as the public key
            var x = vi.Name.Address.Xor(vj.Name.Address);
            var xi = vi.Name.Address.Xor(x);
            var xj = vj.Name.Address.Xor(x);
            //x.Xor(vi.Name.bigint, vj.Name.bigint)
            //   xi := big.NewInt(0)
            //xi.Xor(vi.Name.bigint, x)
            //xj := big.NewInt(0)
            //xj.Xor(vj.Name.bigint, x)
            // if xi is larger than xj then i should be lower in the sort order
            // than j since i is further away.
            //return xi.CompareTo(xj) == 1;
            return xi.CompareTo(xj);
        }

        //public void Swap(int i, int j)
        //{
        //    var temp = this[i];
        //    var temp2 = this[j];
        //    this[j] = temp;
        //    this[i] = temp2;
        //}

        //public bool Less(int i, int j)
        //{
        //    if (this[i].Age == this[j].Age)
        //        return ResolveAgeTiebreaker(this[i], this[j]);

        //    return this[i].Age > this[j].Age;
        //}
    }
}