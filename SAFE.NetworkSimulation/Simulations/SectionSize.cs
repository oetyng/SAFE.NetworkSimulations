using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SAFE.NetworkSimulation
{
    // Uses the first simulation code.
    public class SectionSize : Simulation
    {
        //# a container for the overall simulated network
        //# Sections[prefix] = [name1, name2, ...]
        public ConcurrentDictionary<string, List<string>> Sections { get; set; } = new ConcurrentDictionary<string, List<string>>();
        //# retain node names to quickly know the total size of the network
        //# and make removal of nodes deterministic (first in first out)
        public List<string> Nodes { get; set; } = new List<string>();

        //# prefixes for each node are cached as nodePrefixes[name] = prefix
        //# This helps performance.
        public ConcurrentDictionary<string, string> NodePrefixes { get; set; } = new ConcurrentDictionary<string, string>();

        //# Stats are kept in this dict and updated throughout the simulation
        public Dictionary<string, int> Stats { get; set; } = new Dictionary<string, int>
        {
            { "splits", 0 },
            { "merges", 0 },
            { "largestSectionSize", 0 },
            { "largestMergeSections", 0 },
            { "largestMergeNodes", 0 },
        };

        //# Vault names are derived from the vault id as sha256(id)
        //# and salted with prevVaultName so network with startId 0 is
        //# very different to network with startId 1
        //int _startId = 2;

        //# The total number of join events to simulate
        //decimal _totalEvents = 1000000;
        //# If the network is larger than this, nodes will be removed until the size is
        //# back down to this number.
        //int _netsize = 100000;

        int _minsize = 8;
        int _sectionbuffer = 3;
        int _splitsize = 0; // minsize+sectionbuffer;
        
        public SectionSize(Settings settings, Logger logger)
            : base(settings, logger)
        {
            _splitsize = _minsize + _sectionbuffer;
        }

        public override void Start()
        {
            try
            {
                BuildNetworkSimple(); // var network = BuildNetwork() build network
                Report(); // report results
            }
            catch (Exception ex)
            {
                _log(ex.Message + ex.StackTrace);
            }
        }

        void BuildNetworkSimple()
        {
            var prevVaultName = string.Empty;
            //# work out final vault id
            var endId = _settings.TotalEvents + _settings.Seed;
            //# iterate over vault ids
            for (int i = _settings.Seed; i < endId; i++)
            {
                //# add new node to the network
                var newVaultName = SimulatedNetwork.XorName.SHA256(i + prevVaultName);
                AddVault(newVaultName);
                //# start also removing nodes once the network is full size
                while (Nodes.Count > _settings.Netsize)
                    RemoveVault(Nodes[0]);
                prevVaultName = newVaultName;
                //# log progress every so often
                if (i % 1000 == 0)
                {
                    var pct = ((i - _settings.Seed) / (double)_settings.TotalEvents) * 100;
                    _inlineLog($"{pct} % complete        ");
                }
            }
        }

        void Report()
        {
            //# work out distribution of section sizes
            var sizes = new Dictionary<int, int>();
            var prefixes = Sections.Keys
                .OrderBy(x => x)
                .ToList();
            foreach (var prefix in prefixes)
            {
                var section = Sections[prefix];
                var size = section.Count;
                if (!sizes.ContainsKey(size))
                    sizes[size] = 0;
                sizes[size] += 1;
            }
            var totalSections = (decimal)prefixes.Count;
            //# report distribution of section sizes
            var distBuilder = new StringBuilder();
            distBuilder.AppendLine("size count percent");
            distBuilder.AppendLine("------------------");
            var keys = sizes.Keys.OrderBy(x => x);
            Func<decimal, decimal, decimal, string> rowFormat = (a, b, c) => $"{a,-4} {b,5} {c, 7}";
            foreach (var k in keys)
            {
                var pct = Math.Round(sizes[k] / totalSections * 100m, 3);
                var rowStr = rowFormat(k, sizes[k], pct);
                distBuilder.AppendLine(rowStr);
            }

            var distribution = distBuilder.ToString();

            //# report stats
            var statsBuilder = new StringBuilder();
            foreach (var stat in Stats)
                statsBuilder.AppendLine($"{stat.Key}: {stat.Value}");

            var stats = statsBuilder.ToString();

            _log(stats);
            _log(distribution);
        }

        void AddVault(string name)
        {
            Nodes.Add(name);

            var prefixes = Sections.Keys;
            var prefixLengths = prefixes.Select(p => p.Length).ToList();
            var maxPrefixLength = 0;
            if (prefixLengths.Count > 0)
                maxPrefixLength = prefixLengths.Max();
            var longestPrefix = name.Substring(0, maxPrefixLength + 2);
            while (!prefixes.Contains(longestPrefix) && longestPrefix.Length > 0)
                longestPrefix = longestPrefix.Substring(0, longestPrefix.Length - 1);

            if (!Sections.ContainsKey(longestPrefix))
                Sections[longestPrefix] = new List<string>();
            //# add node to section
            var section = Sections[longestPrefix];
            section.Add(name);
            NodePrefixes[name] = longestPrefix;
            //# split section if needed
            //# try new prefixes
            var leftPrefix = longestPrefix + "0";
            var rightPrefix = longestPrefix + "1";
            var left = new List<string>();
            var right = new List<string>();

            foreach (var node in section)
            {
                if (node.StartsWith(leftPrefix))
                    left.Add(node);
                else if (node.StartsWith(rightPrefix))
                    right.Add(node);
                else
                    Warn($"PREFIX MISMATCH: Original Prefix {longestPrefix} ; Name {node}");
            }
            if (left.Count >= _splitsize && right.Count >= _splitsize)
            {
                Stats["splits"] += 1;
                //# set the new sections
                Sections[leftPrefix] = left;
                Sections[rightPrefix] = right;
                //# cache the new prefix for each node
                foreach (var node in left)
                    NodePrefixes[node] = leftPrefix;
                foreach (var node in right)
                    NodePrefixes[node] = rightPrefix;
                //# remove the old section
                Sections.TryRemove(longestPrefix, out var removed);

                //# track largest section size statistic
                var leftSectionSize = Sections[leftPrefix].Count;
                if (leftSectionSize > Stats["largestSectionSize"])
                    Stats["largestSectionSize"] = leftSectionSize;
                var rightSectionSize = Sections[rightPrefix].Count;
                if (rightSectionSize > Stats["largestSectionSize"])
                    Stats["largestSectionSize"] = rightSectionSize;
            }
            else
            {
                //# track largest section size statistic
                var newSectionSize = Sections[longestPrefix].Count;
                if (newSectionSize > Stats["largestSectionSize"])
                    Stats["largestSectionSize"] = newSectionSize;
            }
        }

        void RemoveVault(string name)
        {
            //# Remove node from cache
            Nodes.Remove(name);
            //# find section for node
            if (!NodePrefixes.ContainsKey(name))
            {
                Warn($"MISSING PREFIX CACHE DURING REMOVE: {name}");
                return;
            }

            var prefix = NodePrefixes[name];
            if (!Sections.ContainsKey(prefix))
            {
                Warn($"NO SECTION FOR PREFIX DURING REMOVE: {prefix}");
                return;
            }

            var section = Sections[prefix].ToList();
            if (!section.Contains(name))
            {
                var key = Sections.Where(g => g.Value.Contains(name)).ToList();
                Warn($"INCORRECT PREFIX {prefix} CACHED DURING REMOVE {name}");
                return;
            }
            //# remove node from section
            section.Remove(name);
            //# if merge is not needed, update section in network
            if (section.Count >= _minsize)
                Sections[prefix] = section;
            else
            {
                Stats["merges"] += 1;
                var lastBit = prefix.Last();
                var siblingBit = lastBit == '1' ? "0" : "1";
                var newPrefix = prefix.Substring(0, prefix.Length - 1);
                var siblingPrefix = newPrefix + siblingBit;
                var mergedSections = 0;
                var mergedNodes = 0;
                var removablePrefixes = new List<string>();
                //# merge all sections starting with complementPrefix
                foreach (var pair in Sections)
                {
                    var siblingOrChildPrefix = pair.Key;
                    if (!siblingOrChildPrefix.StartsWith(siblingPrefix))
                        continue;
                    var siblingSection = Sections[siblingOrChildPrefix];
                    mergedSections += 1;
                    mergedNodes += siblingSection.Count;
                    section = section.Concat(siblingSection).ToList();
                    removablePrefixes.Add(siblingOrChildPrefix);
                }
                //# remove merged sections
                foreach (var removablePrefix in removablePrefixes)
                    Sections.TryRemove(removablePrefix, out var r1);
                //# track stats for merging
                if (mergedSections > Stats["largestMergeSections"])
                    Stats["largestMergeSections"] = mergedSections;
                if (mergedNodes > Stats["largestMergeNodes"])
                    Stats["largestMergeNodes"] = mergedNodes;
                //# create merged section
                Sections[newPrefix] = section;
                //# cache the new prefix for each node
                foreach (var node in section)
                    NodePrefixes[node] = newPrefix;
                //# remove the old section
                Sections.TryRemove(prefix, out var r2);
            }
        }

        void Warn(string p)
        {
            _log(p);
        }
    }
}