using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace SAFE.SimulatedNetwork
{
    public class Network
    {
        public Dictionary<string, Section> Sections;
        public int TotalMerges;
        public int TotalSplits;
        public int TotalJoins;
        public int TotalDepartures;
        public int TotalRelocations;
        public List<int> NeighbourhoodHops;

        public Network()
        {
            Sections = new Dictionary<string, Section>();
            NeighbourhoodHops = new List<int>();
        }

        public Network(int seed)
            : this()
        {
            Constants.prng = new Random(seed);
        }

        public Section GetRandomSection()
        {
            var x = new XorName();
            var p = GetPrefixForXorname(x);
            var s = Sections[p.Key];
            return s;
        }
        
        public bool AddVault(Vault v)
        {
            // track stats
            TotalJoins++;
            // get prefix for vault
            var prefix = GetPrefixForXorname(v.Name);

            Section section = default(Section);

            // get the section for this prefix
            if (!Sections.ContainsKey(prefix.Key))
            {
                var blankPrefix = new Prefix();

                var netEvt = Section.NewSection(blankPrefix, new List<Vault>());
                if (netEvt != null)
                {
                    foreach (var s in netEvt.NewSections)
                    {
                        section = s;
                        Sections[section.Prefix.Key] = section;
                    }
                }
            }
            else
                section = Sections[prefix.Key];

            // add the vault to the section
            var (ne, disallowed) = section.AddVault(v);
            // if there was a split
            if (ne != null && ne.NewSections.Count > 0)
            {
                TotalSplits++;
                // add new sections
                foreach (var s in ne.NewSections)
                    Sections[s.Prefix.Key] = s;
                // remove old section
                Sections.Remove(section.Prefix.Key);
            }
            // relocate vault if there is one to relocate
            if (ne != null && ne.VaultToRelocate != null)
                RelocateVault(ne);

            return disallowed;
        }

        public void RemoveVault(Vault v)
        {
            TotalDepartures++;

            if (!Sections.ContainsKey(v.Prefix.Key))
            {
                Debug.WriteLine("Warning: No section for removeVault");
                return;
            }

            Section section = Sections[v.Prefix.Key];

            // remove the vault from the section
            var ne = section.RemoveVault(v);
            // merge if needed
            if (section.ShouldMerge() && HasMoreThanOneSection())
            {
                TotalMerges++;

                var parentPrefix = section.Prefix.Parent();
                // get sibling prefix
                var siblingPrefix = v.Prefix.Sibling();
                // get sibling vaults
                var parentVaults = section.Vaults.ToList();

                if (Sections.ContainsKey(siblingPrefix.Key))
                {
                    // merge sibling
                    var siblingVaults = Sections[siblingPrefix.Key].Vaults;

                    parentVaults = parentVaults.Concat(siblingVaults).ToList();

                    Sections.Remove(siblingPrefix.Key);
                }
                else
                {
                    // get child vaults
                    var childPrefixes = GetChildPrefixes(siblingPrefix);

                    foreach (var childPrefix in childPrefixes)
                    {
                        // merge child vault
                        var childVaults = Sections[childPrefix.Key].Vaults;
                        parentVaults = parentVaults.Concat(childVaults).ToList();
                        Sections.Remove(childPrefix.Key);
                    }
                }
                // remove the merged section
                Sections.Remove(section.Prefix.Key);
                // create the new section

                ne = Section.NewSection(parentPrefix, parentVaults);

                if (ne != null)
                {
                    foreach (var s in ne.NewSections)
                        Sections[s.Prefix.Key] = s;
                }
            }
            else if (ne != null && ne.VaultToRelocate != null)
            {
                // if there is no merge but there is a vault to relocate,
                // relocate the vault
                RelocateVault(ne);
            }
        }

        void RelocateVault(NetworkEvent ne)
        {
            // track stats for relocations
            TotalRelocations++;
            // find the neighbour with shortest prefix or fewest vaults
            // default to the existing section, useful for zero-length prefix
            var smallestNeighbour = Sections[ne.VaultToRelocate.Prefix.Key];
            var minNeighbourPrefix = UInt32.MaxValue;
            var minNeighbourVaults = UInt32.MaxValue;
            // get all neighbours
            for (int i = 0; i < ne.VaultToRelocate.Prefix.Bits.Count; i++)
            {
                // clone the prefix but flip the ith bit of the prefix
                var neighbourPrefix = new Prefix();

                for (int j = 0; j < ne.VaultToRelocate.Prefix.Bits.Count; j++)
                {
                    var isZero = !(bool)ne.VaultToRelocate.Prefix.Bits[j];

                    if (j == i)
                        isZero = !isZero;
                    if (isZero)
                        neighbourPrefix = neighbourPrefix.ExtendLeft();
                    else
                        neighbourPrefix = neighbourPrefix.ExtendRight();
                }
                // get neighbouring prefixes from the network for this prefix
                // and repeat until we arrive at the 'best' neighbour prefix
                var prevNeighbourPrefix = new Prefix();

                while (!neighbourPrefix.Equals(prevNeighbourPrefix))
                {
                    // track previous neighbour prefix
                    prevNeighbourPrefix = neighbourPrefix;
                    // get potential new neighbour prefixes
                    var neighbourPrefixes = GetMatchingPrefixes(neighbourPrefix);
                    // check if these neighbours contain the 'best' neighbour
                    // prioritise sections with shorter prefixes and having less nodes to balance the network
                    foreach (var p in neighbourPrefixes)
                    {
                        var s = Sections[p.Key];

                        if (p.Bits.Count < minNeighbourPrefix)
                        {
                            // prefer shorter prefixes
                            neighbourPrefix = p;
                            minNeighbourPrefix = (uint)p.Bits.Count;
                            smallestNeighbour = s;
                        }
                        else if (p.Bits.Count == minNeighbourPrefix)
                        {
                            // prefer less vaults if prefix length is same
                            if (s.Vaults.Count < minNeighbourVaults)
                            {
                                neighbourPrefix = p;
                                minNeighbourVaults = (uint)s.Vaults.Count;
                                smallestNeighbour = s;
                            }
                            else if (s.Vaults.Count == minNeighbourVaults)
                            {
                                // TODO tiebreaker for equal sized neighbours
                                // see https://forum.safedev.org/t/data-chains-deeper-dive/1209
                                // If all neighbours have the same number of peers we relocate
                                // to the section closest to the H above (that is not us)
                            }
                        }
                    }
                }
            }
            // track neighbourhood hops by comparing how many bits differ
            // between the new and the old prefix.
            var neighbourhoodHops = 0;
            var prefixLength = smallestNeighbour.Prefix.Key.Length;
            if (ne.VaultToRelocate.Prefix.Key.Length < prefixLength)
                prefixLength = ne.VaultToRelocate.Prefix.Key.Length;

            for (int i = 0; i < prefixLength; i++)
            {
                var newBit = smallestNeighbour.Prefix.Key[i];
                var oldBit = ne.VaultToRelocate.Prefix.Key[i];
                if (newBit != oldBit)
                    neighbourhoodHops++;
            }

            NeighbourhoodHops.Add(neighbourhoodHops);
            // remove vault from current section (includes merge if needed)
            RemoveVault(ne.VaultToRelocate);
            // adjust vault name to match the neighbour section prefix
            ne.VaultToRelocate.RenameWithPrefix(smallestNeighbour.Prefix);
            // age the relocated vault
            ne.VaultToRelocate.IncrementAge();
            // relocate the vault to the smallest neighbour (includes split if needed)
            var disallowed = AddVault(ne.VaultToRelocate);

            if (disallowed)
                Debug.WriteLine("Warning: disallowed relocated vault");
        }

        // Needs to be deterministic but also random.
        // Iterating over keys of a map is not deterministic
        public Vault GetRandomVault()
        {
            var s = GetRandomSection();
            return s.GetRandomVault();
        }

        // Returns the parent, prefix, or children that matches this prefix on the network
        public List<Prefix> GetMatchingPrefixes(Prefix prefix)
        {
            var prefixes = new List<Prefix>();
            var testPrefix = new Prefix();
            
            // find possible parents
	        if (Sections.ContainsKey(testPrefix.Key))
                prefixes.Add(testPrefix);
	        for (int i = 0; i< prefix.Bits.Count; i++)
            {
                if (!prefix.Bits[i])
                    testPrefix = testPrefix.ExtendLeft();
                else
                    testPrefix = testPrefix.ExtendRight();
		        
		        if (Sections.ContainsKey(testPrefix.Key))
                    prefixes.Add(testPrefix); // TODO can probably break here?
	        }
            // get child prefixes if no parent found
            if (prefixes.Count == 0)
                prefixes = GetChildPrefixes(prefix);
            return prefixes;
        }

        List<Prefix> GetChildPrefixes(Prefix prefix)
        {
            var prefixes = new List<Prefix>();
            var leftPrefix = prefix.ExtendLeft();
            var rightPrefix = prefix.ExtendRight();
            var leftExists = Sections.ContainsKey(leftPrefix.Key);
            var rightExists = Sections.ContainsKey(rightPrefix.Key);
            if (leftExists && rightExists)
                prefixes.AddRange(new[] { leftPrefix, rightPrefix });
            else if (leftExists)
            {
                prefixes.Add(leftPrefix);
                prefixes.AddRange(GetChildPrefixes(rightPrefix));
            }
            else if (rightExists)
            {
                prefixes.Add(rightPrefix);
                prefixes.AddRange(GetChildPrefixes(leftPrefix));
            }
            else if (prefix.Bits.Count < 256)
            {
                prefixes.AddRange(GetChildPrefixes(leftPrefix));
                prefixes.AddRange(GetChildPrefixes(rightPrefix));
            }
            else
                Debug.WriteLine("Warning: No children exist for prefix");
            return prefixes;
        }

        Prefix GetPrefixForXorname(XorName x)
        {
            var prefix = new Prefix();

	        while (!Sections.ContainsKey(prefix.Key) && prefix.Bits.Count < x.Bits.Count)
            {
                // get the next bit of the xorname prefix
                // extend the prefix depending on the bit of the xorname
                if (!x.Bits[prefix.Bits.Count])
                    prefix = prefix.ExtendLeft();
                else
                    prefix = prefix.ExtendRight();
            }
	        if (!Sections.ContainsKey(prefix.Key) && HasMoreThanOneVault())
            {
                Debug.WriteLine("Warning: No prefix for xorname");
                return new Prefix();
	        }
            return prefix;
        }

        public Dictionary<int, int> ReportPrefixsLengths()
        {
            var prefixLenghts = new Dictionary<int, int>();
            foreach (var pair in Sections)
            {
                var key = pair.Key;
                var prefix = pair.Value.Prefix;
                if (!prefixLenghts.ContainsKey(prefix.Bits.Count))
                    prefixLenghts[prefix.Bits.Count] = 0;
                prefixLenghts[prefix.Bits.Count]++;
            }

            return prefixLenghts;
        }

        public List<KeyValuePair<int, int>> ReportAges()
        {
            var ages = new Dictionary<int, int>();
	        foreach (var pair in Sections)
            {
                var p = pair.Key;
		        foreach (var v in pair.Value.Vaults)
                {
                    if (!ages.ContainsKey(v.Age))
                        ages[v.Age] = 0;
                    ages[v.Age]++;
                }
            }

            return ages.Select(x => x)
                .OrderBy(c => c.Key)
                .ToList();
        }

        public List<KeyValuePair<int, int>> ReportAdults()
        {
            var adultsMap = new Dictionary<int, int>();
            foreach (var pair in Sections)
            {
                var section = pair.Value;
                var adults = section.TotalAdults();
                // track distribution
                if (!adultsMap.ContainsKey(adults))
                    adultsMap[adults] = 0;
                adultsMap[adults]++;
            }

            return adultsMap.Select(x => x)
                .OrderBy(c => c.Key)
                .ToList();
        }

        public int TotalVaults()
        {
            return Sections.Values.Sum(c => c.Vaults.Count);
        }

        public int TotalSections()
        {
            return Sections.Count;
        }

        bool HasMoreThanOneVault()
        {
            var vaults = 0;
            foreach (var pair in Sections)
            {
                vaults += pair.Value.Vaults.Count;
                if (vaults > 1)
                    return true;
            }
            return false;
        }

        bool HasMoreThanOneSection()
        {
            var sections = 0;
	        foreach (var pair in Sections)
            {
                sections++;
                if (sections > 1)
                    return true;
            }
            return false;
        }

        bool HasOneSection()
        {
            var sections = 0;
            foreach (var pair in Sections)
            {
                sections++;
                if (sections > 1)
                    return false;
            }
            return sections == 1;
        }
    }
}