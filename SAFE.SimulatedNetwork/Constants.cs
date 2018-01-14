using System;

namespace SAFE.SimulatedNetwork
{
    public class Constants
    {
        public static Random prng = new Random(new Random().Next());

        public const int GroupSize = 8;
        public const int SplitBuffer = 3;
        public const int QuorumNumerator = 1;
        public const int QuorumDenominator = 2;
        public const int SplitSize = GroupSize + SplitBuffer;
    }
}