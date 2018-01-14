using SAFE.SimulatedNetwork;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace SAFE.NetworkSimulation
{
    public class TargetedGoogleAttack : Simulation
    {
        public TargetedGoogleAttack(Settings settings, Logger logger)
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
            catch (Exception ex)
            {
                _log(ex.Message + ex.StackTrace);
            }
        }

        // Attack the network until the attacker owns a section
        // by adding vaults to a specific prefix
        // which will be relocated only to the neighbours which is
        // a fairly small subsection of the network
        int Attack(Network network)
        {
            _log($"{network.TotalVaults()} vaults before attack");

            // TODO should set prefixBitCount to the current length of the
            // section prefix length. 64 is a sort-of suitable compromise
            // which is valid for networks with up to about 2^64 sections
            var seed = new XorName(); // will have a random address
            var prefixBits = new BitArray(64);
            for (int i = 0; i < prefixBits.Count; i++) // TODO vault / prefix abstraction seems wrong here, too messy
                prefixBits.Set(i, seed.Bits.Get(i));
            var attackPrefix = new Prefix(prefixBits);

            var attackVaultCount = 0;

            while (true)
            {
                // logging
                if (attackVaultCount % 1000 == 0)
                    _log($"{attackVaultCount} attacking vaults added\r");

                // add an attacking vault
                var disallowed = true;
                var attacker = default(Vault);

                while (disallowed) //TODO: fix this: if disallowing is enabled, this will always be true if true once, since we're trying to add to same section every time..
                {
                    attacker = new Vault { IsAttacker = true };
                    attacker.RenameWithPrefix(attackPrefix); // set vault to use the attack prefix
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

        new void Report(Network network, int attackVaultCount)
        {
            _log($"Results for: {nameof(TargetedGoogleAttack)}");
            ReportAttackerCount(network, attackVaultCount); // basic reporting
            ReportSectionSizeDistribution(network);
            ReportSectionAgeDistribution(network);
            // addtitional specific reporting
            // ..
        }
    }
}
