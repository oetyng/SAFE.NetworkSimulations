using SAFE.SimulatedNetwork;
using System;

namespace SAFE.NetworkSimulation
{
    public class GoogleAttack : Simulation
    {
        public GoogleAttack(Settings settings, Logger logger)
            : base(settings, logger)
        { }

        public override void Start()
        { 
            try
            {
                var network = BuildNetwork(); // build network
                var attackVaultCount = Attack(network); // attack the network until the attacker owns a section
                Report(network, attackVaultCount); // report results
            }
            catch(Exception ex)
            {
                _log(ex.Message + ex.StackTrace);
            }
        }

        int Attack(Network network)
        {
            _log($"{network.TotalVaults()} vaults before attack");

            var attackVaultCount = 0;

            while (true)
            {
                // logging
                if (attackVaultCount % 1000 == 0)
                    _log($"{attackVaultCount} attacking vaults added\r");

                // add an attacking vault
                var disallowed = true;
                var attacker = default(Vault);

                while (disallowed)
                {
                    attacker = new Vault { IsAttacker = true };
                    disallowed = network.AddVault(attacker);
                }

                if (!network.Sections.ContainsKey(attacker.Prefix.Key))
                    continue;

                attackVaultCount++;

                // check if attack has worked
                var section = network.Sections[attacker.Prefix.Key];

                if (section.IsAttacked() /* modification => */ && attacker.Prefix.Key != "")
                    break;
                // TODO edge case: if section just split it may have
                // caused the sibling section to be attacked so
                // should check the sibling section
                // add one normal vault for every ten attacking
                if (attackVaultCount % 10 == 0)
                {
                    disallowed = true;

                    while (disallowed)
                    {
                        var v = new Vault();
                        disallowed = network.AddVault(v);
                    }
                }
                // remove a non-attacking vault for every ten attacking
                if (attackVaultCount % 10 == 0)
                {
                    var e = network.GetRandomVault();
                    while (e.IsAttacker /* modification => */ && e.Prefix.Key != "")
                        e = network.GetRandomVault();

                    network.RemoveVault(e);
                }
            }

            return attackVaultCount;
        }

        void Report(Network network, int attackVaultCount)
        {
            _log($"Results for: {nameof(GoogleAttack)}");
            ReportAttackerCount(network, attackVaultCount); // basic reporting
            ReportSectionSizeDistribution(network);
            ReportSectionAgeDistribution(network);
            // addtitional specific reporting
            // ..
        }
    }
}
