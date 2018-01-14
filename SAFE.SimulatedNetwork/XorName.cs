
using Org.BouncyCastle.Math;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace SAFE.SimulatedNetwork
{
    public class XorName
    {
        BitArray _bits;
        public BitArray Bits
        {
            get
            {
                if (_bits == null)
                {
                    _bits = new BitArray(Address.BitCount);
                    for (int i = 0; i < Address.BitCount; i++)
                        _bits.Set(i, Address.TestBit(i));
                    //_bits = new BitArray(Address.ToByteArray());
                }
                    
                return _bits;
            }
        }

        public BigInteger Address { get; set; }

        public XorName(BitArray bits)
        {
            //byte[] array = new byte[bits.Count];
            //bits.CopyTo(array, 0);
            //Address = new BigInteger(array);

            Address = new BigInteger("0");
            for (int i = 0; i < bits.Count; i++)
            {
                if (bits[i])
                    Address = Address.SetBit(i);
            }
        }

        public XorName()
            : this(GenerateBitArray())
        { }

        const int xornameBits = 256;
        static BitArray GenerateBitArray()
        {
            // create a name from prng
            var nameBits = new BitArray(256);

            for (int i = 0; i < xornameBits; i++)
            {
                var bit = Constants.prng.Next(2);
                if (bit == 0)
                    nameBits[i] = false;
                else if (bit == 1)
                    nameBits[i] = true;
                else
                {
                    Debug.WriteLine("Warning: NewXorName generated a number not 0 or 1");
                }
            }

            return nameBits;
        }

        //public XorName()
        //{
        //    Address = new BigInteger(Random());
        //}

        public XorName(string name)
        {
            Address = new BigInteger(SHA256(name));
        }

        static Random _rand = Constants.prng;

        static readonly Dictionary<char, string> hexCharacterToBinary = new Dictionary<char, string>
        {
            { '0', "0000" },
            { '1', "0001" },
            { '2', "0010" },
            { '3', "0011" },
            { '4', "0100" },
            { '5', "0101" },
            { '6', "0110" },
            { '7', "0111" },
            { '8', "1000" },
            { '9', "1001" },
            { 'a', "1010" },
            { 'b', "1011" },
            { 'c', "1100" },
            { 'd', "1101" },
            { 'e', "1110" },
            { 'f', "1111" }
        };

        public static string Random()
        {
            return Random(256);
        }

        public static string Random(int bits)
        {
            var hex = RandomSHA256Hex(bits);
            var binary = HexStringToBinary(hex);
            return binary;
        }

        public static string SHA256(string name)
        {
            var hex = SHA256Hex(name);
            var binary = HexStringToBinary(hex);
            return binary;
        }

        static string HexStringToBinary(string hex)
        {
            StringBuilder result = new StringBuilder();
            foreach (char c in hex)
            {
                // This will crash for non-hex characters. You might want to handle that differently.
                result.Append(hexCharacterToBinary[char.ToLower(c)]);
            }
            return result.ToString();
        }

        static string RandomSHA256Base64(int bits)
        {
            var byteArray = new byte[bits / 8];
            _rand.NextBytes(byteArray);

            string hash;
            using (SHA256CryptoServiceProvider sha256 = new SHA256CryptoServiceProvider())
            {
                hash = Convert.ToBase64String(sha256.ComputeHash(byteArray));
            }
            return hash;
        }

        static string SHA256Hex(string name)
        {
            var byteArray = Encoding.UTF8.GetBytes(name);
            return SHA256Hex(byteArray);
        }

        static string SHA256Hex(byte[] byteArray)
        {
            string hash;
            using (SHA256CryptoServiceProvider sha1 = new SHA256CryptoServiceProvider())
            {
                hash = BitConverter.ToString(sha1.ComputeHash(byteArray)).Replace("-", "");
            }

            return hash;
        }

        static string RandomSHA256Hex(int bits)
        {
            var byteArray = new byte[bits / 8];
            _rand.NextBytes(byteArray);

            return SHA256Hex(byteArray);
        }
    }
}