using System;
using System.Collections;
using System.Linq;
using System.Text;

namespace SAFE.SimulatedNetwork
{
    public class Prefix
    {
        public string Key { get; private set; }
        public BitArray Bits { get; private set; } = new BitArray(0);

        public Prefix(BitArray bits)
        {
            Bits = bits;
            SetKey();
        }

        public Prefix()
        {
            SetKey();
        }

        public Prefix ExtendLeft()
        {
            var newBits = new BitArray(Bits.Count + 1);// := make([]bool, Bits.Count+1)

            foreach (int i in Enumerable.Range(0, Bits.Count))
                newBits[i] = Bits[i];

            //newBits[newBits.Count - 1] = false;

            var left = new Prefix
            {
                Bits = newBits
            };

            left.SetKey();

            return left;
        }

        public Prefix ExtendRight()
        {
            var newBits = new BitArray(Bits.Count + 1); //:= make([]bool, len(p.bits)+1)

            foreach (int i in Enumerable.Range(0, Bits.Count))
                newBits[i] = Bits[i];

            newBits[newBits.Count - 1] = true;

            var right = new Prefix
            {
                Bits = newBits
            };

            right.SetKey();

            return right;
        }

        public Prefix Sibling()
        {
            var s = new Prefix
            {
                Bits = Bits
            };

            // TODO: Check if this is correct (temp solution to 0 count)
            if (s.Bits.Count > 0)
                s.Bits[s.Bits.Count - 1] = !s.Bits[s.Bits.Count - 1];
            s.SetKey();
            return s;
        }

        internal Prefix Parent()
        {
//            a:= Prefix{
//                  bits: p.bits[:len(p.bits) - 1], 
//  }

            var newBits = new BitArray(Math.Max(0, Bits.Count - 1));

            foreach (int i in Enumerable.Range(0, newBits.Count))
                newBits[i] = Bits[i];

            var a = new Prefix
            {
                Bits = newBits
            };
            a.SetKey();
	        return a;
        }

        int TotalBytes()
        {
            // int division does floor automatically
            var totalBytes = Bits.Count / 8;
	        // but totalBytes should be ceil so do that here
	        if (Bits.Count > 0 && Bits.Count % 8 != 0)
                totalBytes++;
            return totalBytes;
        }

        void SetKey()
        {
            //   var totalBytes = TotalBytes();
            //   // preallocate bytes to avoid append
            //   var bytes = new byte[totalBytes];// new BitArray(totalBytes);// make([]byte, totalBytes)
            //for (int i = 0; i < totalBytes; i++)
            //   {
            //       byte thisByte = 0;
            //       var startBit = i * 8;

            //       var endBit = (i + 1) * 8;

            //       for (int j = startBit; j < endBit; j++)
            //       {
            //           thisByte = (byte)(thisByte << 1);

            //           if (j < Bits.Count && Bits[j])
            //               thisByte++;
            //       }
            //       bytes[i] = thisByte;
            //   }
            //Key = BinaryString(new BitArray(bytes)); // strconv.Itoa(Bits.Count) + 
            Key = BinaryString(Bits);
        }

        public string BinaryString()
        {
            return BinaryString(Bits);
        }

        string BinaryString(BitArray bits)
        {
            var sb = new StringBuilder();
	        foreach (var b in bits)
            {
                if (Convert.ToBoolean(b))
                    sb.Append("1");
                else
                    sb.Append("0");
            }
            return sb.ToString();
        }

        public bool Equals(Prefix q)
        {
            return Key == q.Key;
        }

        public bool Matches(XorName x)
        {
	        if (Bits.Count > x.Bits.Count)
                return false;

            for (int i = 0; i < Bits.Count; i++)
            {
		        if (Bits[i] != x.Bits[i])
                {
                    return false;
		        }
	        }
            return true;
        }
    }
}
