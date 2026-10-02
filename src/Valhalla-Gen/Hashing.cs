using System.Text;

namespace Valhalla.Gen;

// Must match ValhallaHash.h bit-for-bit; generated code static_asserts every emitted hash against the C++ side.
static class Fnv
{
    const ulong OffsetBasis = 0xcbf29ce484222325UL;
    const ulong Prime = 0x100000001b3UL;

    public static ulong Hash(string text)
    {
        ulong hash = OffsetBasis;
        foreach (byte b in Encoding.UTF8.GetBytes(text))
        {
            hash ^= b;
            hash *= Prime;
        }
        return hash;
    }
}

// Lookup math must match ValhallaChd.h; generated code static_asserts each type's slot against it.
sealed class ChdTable
{
    const ulong BucketSeed = 0x243f6a8885a308d3UL;
    const ulong SlotSeed = 0x13198a2e03707344UL;
    const ulong DisplacementStep = 0x9e3779b97f4a7c15UL;
    public const uint EmptySlot = 0xffffffffu;

    public uint BucketCount { get; private set; }
    public uint SlotCount { get; private set; }
    public uint[] Displacements { get; private set; } = [];
    // SlotToKey[slot] = index into the key list, or EmptySlot.
    public uint[] SlotToKey { get; private set; } = [];

    public static ulong Mix(ulong value)
    {
        value ^= value >> 30;
        value *= 0xbf58476d1ce4e5b9UL;
        value ^= value >> 27;
        value *= 0x94d049bb133111ebUL;
        value ^= value >> 31;
        return value;
    }

    public static uint BucketOf(ulong key, uint bucketCount) => (uint)(Mix(key ^ BucketSeed) % bucketCount);

    public static uint SlotOf(ulong key, uint displacement, uint slotCount) =>
        (uint)(Mix(key ^ (SlotSeed + displacement * DisplacementStep)) % slotCount);

    public static ChdTable Build(IReadOnlyList<ulong> keys)
    {
        var table = new ChdTable();
        int n = keys.Count;
        if (n == 0) return table;

        uint bucketCount = (uint)(n / 2 + 1);
        for (uint slotCount = (uint)n; slotCount <= (uint)n * 2; slotCount++)
        {
            if (TryBuild(keys, bucketCount, slotCount, out var displacements, out var slotToKey))
            {
                table.BucketCount = bucketCount;
                table.SlotCount = slotCount;
                table.Displacements = displacements;
                table.SlotToKey = slotToKey;
                return table;
            }
        }
        throw new InvalidOperationException("CHD construction failed; key set likely contains duplicates.");
    }

    static bool TryBuild(IReadOnlyList<ulong> keys, uint bucketCount, uint slotCount, out uint[] displacements, out uint[] slotToKey)
    {
        displacements = new uint[bucketCount];
        slotToKey = Enumerable.Repeat(EmptySlot, (int)slotCount).ToArray();

        var buckets = Enumerable.Range(0, (int)bucketCount).Select(_ => new List<int>()).ToArray();
        for (int i = 0; i < keys.Count; i++) buckets[BucketOf(keys[i], bucketCount)].Add(i);

        foreach (int bucket in Enumerable.Range(0, (int)bucketCount).OrderByDescending(b => buckets[b].Count).ThenBy(b => b))
        {
            var members = buckets[bucket];
            if (members.Count == 0) continue;

            bool placed = false;
            var claimed = new List<uint>(members.Count);
            for (uint disp = 0; disp < 1_000_000 && !placed; disp++)
            {
                claimed.Clear();
                bool clash = false;
                foreach (int k in members)
                {
                    uint slot = SlotOf(keys[k], disp, slotCount);
                    if (slotToKey[slot] != EmptySlot || claimed.Contains(slot)) { clash = true; break; }
                    claimed.Add(slot);
                }
                if (clash) continue;

                for (int m = 0; m < members.Count; m++) slotToKey[claimed[m]] = (uint)members[m];
                displacements[bucket] = disp;
                placed = true;
            }
            if (!placed) return false;
        }
        return true;
    }
}
