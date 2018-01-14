using System;

namespace SAFE.NetworkSimulation
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("@mav's SAFENetwork simulations \n  -- rewritten in C# by @oetyng");

            var config = Configure();
            var simulation = GetSimulation(config);

            simulation.Start();

            Console.ReadKey();
        }

        static Simulation GetSimulation((Settings, Logger) config)
        {
            var (settings, logger) = config;
            var choice = GetInt("Select simulation: \n1. Google Attack \n2. Targeted Google Attack \n3. Section size \n", (i) => i > 0 && i <= 3 );
            switch(choice)
            {
                case 1:
                    return new GoogleAttack(settings, logger);
                case 2:
                    return new TargetedGoogleAttack(settings, logger);
                case 3:
                    return new SectionSize(settings, logger);
                default:
                    throw new NotImplementedException();
            }
        }

        static (Settings, Logger) Configure()
        {
            var netSize = GetInt("Netsize: ", (i) => i > 0); // network must be at least 1 node
            var seed = GetInt("Seed: ", (i) => true); // no condition on seed

            var settings = new Settings
            {
                Netsize = netSize,
                Seed = seed
            };

            return (settings, new Logger());
        }

        static int GetInt(string msg, Func<int, bool> condition)
        {
            while (true)
            {
                Console.Write(msg);

                if (int.TryParse(Console.ReadLine(), out int netSize) && condition(netSize))
                    return netSize;
            }
        }
    }
}
