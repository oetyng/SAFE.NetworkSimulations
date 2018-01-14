
using System;

namespace SAFE.NetworkSimulation
{
    public class Settings
    {
        public int Seed;
        public int Netsize;
        public readonly int TotalEvents;
        public readonly double PctStep;
        
        public Settings()
        {
            Seed = 0;
            Netsize = 100000;
            TotalEvents = Netsize * 5;
            PctStep = TotalEvents / 100;
        }
    }

    public class Logger
    {
        public Action<string> Log;
        public Action<string> InlineLog;

        public Logger()
        {
            Log = Console.WriteLine;
            InlineLog = (s) => Console.Write($"\r{s}");
        }
    }
}