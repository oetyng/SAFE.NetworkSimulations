using Org.BouncyCastle.Math;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace SAFE.SimulatedNetwork
{
    public class Section
    {
        public Vault VaultToRelocate { get; set; }
        public Prefix Prefix { get; internal set; }
        public List<Vault> Vaults { get; set; } = new List<Vault>();
        
        // Returns a slice of sections since as vaults age they may cascade into
        // multiple sections.
        public static NetworkEvent NewSection(Prefix prefix, List<Vault> vaults)
        {
            var section = new Section
            {
                Prefix = prefix,
            };

	        // add each existing vault to new section
	        foreach (var vault in vaults)
            {
                vault.SetPrefix(section.Prefix);
                section.Vaults.Add(vault);
            }
            // split into two sections if needed.
            // there is no vault relocation here.
            if (section.ShouldSplit())
                return section.Split();

            // return the section as a network event.
            // there is a vault relocation here.
            var netEvt = new NetworkEvent
            {
                NewSections = new List<Section> { section }
            };

            var toRelocate = section.VaultForRelocation(netEvt);

            if (toRelocate != null)
                netEvt.VaultToRelocate = toRelocate;

            return netEvt;
        }
        
        public (NetworkEvent, bool) AddVault(Vault v)
        {
            // disallow more than one node aged 1 per section if the section is
            // TODO currently disabled, should be reenabled
            // complete (all elders are adults)
            // see https://github.com/fizyk20/ageing_sim/blob/53829350daa372731c9b8080488b2a75c72f60bb/src/network/section.rs#L198
            var isDisallowed = false;
            //if v.Age == 1 && s.hasVaultAgedOne() && s.isComplete() {
            //	isDisallowed = true
            //	return nil, isDisallowed
            //}
            v.SetPrefix(Prefix);
            Vaults.Add(v);
	        // split into two sections if needed
	        // details are handled by network upon returning two new sections
	        if (ShouldSplit())
                return (Split(), isDisallowed);

            // no split so return zero new sections
            // but a new vault added triggers a network event which may lead to vault
            // relocation
            var ne = new NetworkEvent();

            var r = VaultForRelocation(ne);
	        if (r != null)
                ne.VaultToRelocate = r;

            return (ne, isDisallowed);
        }

        public NetworkEvent RemoveVault(Vault v)
        {
	        // remove from section
            Vaults.Remove(v);
            // merge is handled by network using NetworkEvent ne
            // which includes a vault relocation
            var ne = new NetworkEvent();
            var r = VaultForRelocation(ne);
            if (r != null)
                ne.VaultToRelocate = r;
            return ne;
        }

        NetworkEvent Split()
        {
            var leftPrefix = Prefix.ExtendLeft();
            var rightPrefix = Prefix.ExtendRight();
            var left = new List<Vault>();
            var right = new List<Vault>();
            foreach (var v in Vaults)
            {
                if (leftPrefix.Matches(v.Name))
                    left.Add(v);
                else if (rightPrefix.Matches(v.Name))
                    right.Add(v);
                else { }
                    //Debug.WriteLine("Warning: Split has vault that doesn't match extended prefix");
            }
            var ne0 = NewSection(leftPrefix, left);
            var ne1 = NewSection(rightPrefix, right);
            var ne = new NetworkEvent
            {
                NewSections = ne0.NewSections.Concat(ne1.NewSections).ToList()
            };

            return ne;
        }

        bool ShouldSplit()
        {
            var left = LeftAdultCount();
            var right = RightAdultCount();
            return left >= Constants.SplitSize && right >= Constants.SplitSize;
        }

        bool IsComplete()
        {
            // GROUP_SIZE peers with age >4 in a section
            return TotalAdults() >= Constants.GroupSize;
        }

        bool HasVaultAgedOne()
        {
	        foreach (var v in Vaults)
            {
                if (v.Age == 1)
                    return true;
            }
            return false;
        }
        
        List<Vault> Elders()
        {
            // get elders
            // see https://forum.safedev.org/t/data-chains-deeper-dive/1209
            // the GROUP_SIZE oldest peers in the section
            // tiebreakers are handled by the sort algorithm
            
            Vaults = new OldestFirst(Vaults).ToList();
            // if there aren't enough vaults, use all of them
            var elders = Vaults;
	        // otherwise get the GroupSize oldest vaults
	        if (Vaults.Count > Constants.GroupSize)
                elders = Vaults.Take(Constants.GroupSize).ToList();
            return elders;
        }

        public bool IsAttacked()
        {
            // check if enough attacking elders to control quorum
            // and if attackers control 50% of the age
            // see https://github.com/maidsafe/rfcs/blob/master/text/0045-node-ageing/0045-node-ageing.md#consensus-measurement
            // A group consensus will require >50% of nodes and >50% of the age of the whole group.
            var elders = Elders();
            var totalVotes = elders.Count;

            var totalAge = 0;
            var attackingVotes = 0;
            var attackingAge = 0;
	        foreach (var v in elders)
            {
                totalAge += v.Age;
		        if (v.IsAttacker)
                {
                    attackingVotes++;
                    attackingAge += v.Age;
                }
            }
            // use integer arithmetic to check quorum
            // see https://github.com/maidsafe/routing/blob/da462bfebfd47dd16cb0c7523359d219bb097a3e/src/lib.rs#L213
            var votesAttacked = attackingVotes * Constants.QuorumDenominator > totalVotes * Constants.QuorumNumerator;
            // compare ages
            var ageAttacked = attackingAge * Constants.QuorumDenominator > totalAge * Constants.QuorumNumerator;
            return votesAttacked && ageAttacked;
        }

        public Vault GetRandomVault()
        {
            var totalVaults = Vaults.Count;
	        if (totalVaults == 0)
            {
                Debug.WriteLine("Warning: GetRandomVault for section with no vaults");
                return null;
            }
            var i = Constants.prng.Next(totalVaults);
            return Vaults[i];
        }

        Vault VaultForRelocation(NetworkEvent ne)
        {
            // find vault to relocate based on a randomly generated 'event hash'
            // see https://forum.safedev.org/t/data-chains-deeper-dive/1209
            // As we receive/form a valid block of Live for non-infant peers, we take
            // the Hash of the event H. Then if H % 2^age == 0 for any peer (sorted by
            // age ascending) in our section, we relocate this node to the neighbour
            // that has the lowest number of peers.
            var oldestAge = 0;
            var smallestTiebreaker = new BigInteger(NetworkEvent.LargestHashValue);

            Vault v = default(Vault);
	        foreach (var w in Vaults)
            {
                if (w.Age < oldestAge)
                    continue;
                else if (w.Age > oldestAge)
                {
                    // calculate divisor as 2^age
                    var divisor = new BigInteger("1");
                    divisor = divisor.ShiftLeft(w.Age);
                    //divisor.Lsh(divisor, uint(w.Age));
    
                    if (ne.HashModIsZero(divisor))
                    {
                        oldestAge = w.Age;
                        v = w;
                        // track xordistance for potential future tiebreaker
                        var xordistance = w.Name.Address.Xor(ne.Hash);
                        smallestTiebreaker = xordistance;
                    }
                }
                else if (w.Age == oldestAge)
                {
                    // calculate divisor as 2^age
                    var divisor = new BigInteger("1");
                    divisor = divisor.ShiftLeft(w.Age);
                    //divisor.Lsh(divisor, uint(w.Age))
    
                    if (ne.HashModIsZero(divisor))
                    {
                        // tiebreaker
                        // If there are multiple peers of the same age then XOR their
                        // public keys together and find the one XOR closest to it.
                        // TODO this isn't done correctly, since it only XORs the two
                        // keys when it should XOR all keys of this age.
                        var xordistance = w.Name.Address.Xor(ne.Hash);
                        //xordistance.Xor(w.Name.bigint, ne.hash)

                        if (xordistance.CompareTo(smallestTiebreaker) == -1)
                        {
                            smallestTiebreaker = xordistance;
                            v = w;
                        }
                    }
                }
            }
            return v;
        }

        public bool ShouldMerge()
        {
            return TotalAdults() <= Constants.GroupSize;
        }

        public int TotalAdults()
        {
            return Vaults.Count(v => v.IsAdult());
        }

        int TotalElders()
        {
            return Elders().Count;
        }

        int LeftVaultCount()
        {
            var leftPrefix = Prefix.ExtendLeft();
	        return VaultCountForExtendedPrefix(leftPrefix);
        }

        int RightVaultCount()
        {
            var rightPrefix = Prefix.ExtendRight();
            return VaultCountForExtendedPrefix(rightPrefix);
        }

        int VaultCountForExtendedPrefix(Prefix p)
        {
            return Vaults.Count(v => p.Matches(v.Name));
        }

        int LeftAdultCount()
        {
            var leftPrefix = Prefix.ExtendLeft();
            return AdultCountForExtendedPrefix(leftPrefix);
        }

        int RightAdultCount()
        {
            var rightPrefix = Prefix.ExtendRight();
            return AdultCountForExtendedPrefix(rightPrefix);
        }

        int AdultCountForExtendedPrefix(Prefix p)
        {
            return Vaults.Count(v => v.IsAdult() && p.Matches(v.Name));
        }
    }
}
