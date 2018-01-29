using SAFE.SimulatedNetwork;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SAFE.NetworkSimulation
{
    public abstract class Simulation
    {
        protected Settings _settings;
        protected Action<string> _log;
        protected Action<string> _inlineLog;

        public Simulation(Settings settings, Logger logger)
        {
            _settings = settings;
            _log = logger.Log;
            _inlineLog = logger.InlineLog;
        }

        public abstract void Start();

        public Network BuildNetwork()
        {
            // Create initial network
            _log("Building initial network");

            var network = new Network(_settings.Seed);

            for (int i = 0; i < _settings.TotalEvents; i++)
            {
                // logging
                if (i % _settings.PctStep == 0)
                {
                    var progress = (int)(i / (double)_settings.TotalEvents * 100.0);
                    _inlineLog($"{progress} % complete          ");
                }
                // create a new vault
                var v = new Vault();
                var disallowed = network.AddVault(v);

                while (disallowed)
                {
                    v = new Vault();
                    disallowed = network.AddVault(v);
                }

                // remove existing vaults until network is back to capacity
                while (network.TotalVaults() > _settings.Netsize)
                {
                    var e = network.GetRandomVault();
                    network.RemoveVault(e);
                }
            }

            _log("\n100% complete\n");

            return network;
        }

        protected void ReportAttackerCount(Network network, int attackVaultCount)
        {
            // report
            _log($"{attackVaultCount} attacking vaults added to own a section");

            _log($"{network.TotalVaults()} vaults after attack");

            _log($"{network.TotalSections()} sections after attack");

            var pctOwned = attackVaultCount / (double)network.TotalVaults() * 100.0;

            _log($"{pctOwned} percent of total network owned by attacker");

            //var total = network.Sections.Values.SelectMany(s => s.Vaults).Count();
            //var attackers = network.Sections.Values.SelectMany(s => s.Vaults).Count(v => v.IsAttacker);
            //pctOwned = attackers / (double)total * 100.0;
            //_log($"{pctOwned} percent of total network owned by attacker");
        }

        protected void ReportSectionSizeDistribution(Network network)
        {
            //# work out distribution of section sizes
            var sizes = new Dictionary<int, int>();
            var prefixes = network.Sections.Keys
                .OrderBy(x => x)
                .ToList();
            foreach (var prefix in prefixes)
            {
                var section = network.Sections[prefix];
                var size = section.Vaults.Count;
                if (!sizes.ContainsKey(size))
                    sizes[size] = 0;
                sizes[size]++;
            }
            var totalSections = (decimal)prefixes.Count;
            //# report distribution of section sizes
            var distBuilder = new StringBuilder();
            distBuilder.AppendLine("size count percent");
            distBuilder.AppendLine("------------------");
            var keys = sizes.Keys.OrderBy(x => x);
            Func<decimal, decimal, decimal, string> rowFormat = (a, b, c) => $"{a,-4} {b,5} {c,7}";
            foreach (var k in keys)
            {
                var pct = Math.Round(sizes[k] / totalSections * 100m, 3);
                var rowStr = rowFormat(k, sizes[k], pct);
                distBuilder.AppendLine(rowStr);
            }

            var distribution = distBuilder.ToString();
            
            _log(distribution);
        }

        protected void ReportSectionAgeDistribution(Network network)
        {
            var ages = network.ReportAges();
            _log("age  vaults");
            foreach (var age in ages)
                _log($"{age.Key}  {age.Value}");

            var adults = network.ReportAdults();
            _log("adults  sections");
            foreach (var adult in adults)
                _log($"{adult.Key}  {adult.Value}");
        }

        protected void ReportPrefixLength(Network network)
        {
            var lengths = network.ReportPrefixsLengths();
            _log("length  count");
            foreach (var len in lengths)
                _log($"{len.Key}  {len.Value}");
        }
    }
}
