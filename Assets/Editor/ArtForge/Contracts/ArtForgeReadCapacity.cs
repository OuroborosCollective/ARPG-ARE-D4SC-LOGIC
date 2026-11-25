using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AshenCrypt.ArtForge
{
    // Editor transport admission only. Never part of the canonical graph or simulation.
    public static class ArtForgeReadCapacity
    {
        public static bool TryAdmit<T>(IDictionary<string, T> reads, int limit, Func<T, Task> task,
            Func<T, long> started, Action<string, T> forget)
        {
            if (reads.Count < limit) return true;
            // A cancelled token is not evidence that its worker stopped. Reclaim
            // exactly one terminal task, preferring the oldest unclaimed result.
            string selected = null;
            T candidate = default(T);
            long oldest = long.MaxValue;
            foreach (var pair in reads)
            {
                if (!task(pair.Value).IsCompleted) continue;
                long stamp = started(pair.Value);
                if (selected != null && (stamp > oldest ||
                    (stamp == oldest && StringComparer.Ordinal.Compare(pair.Key, selected) >= 0))) continue;
                selected = pair.Key; candidate = pair.Value; oldest = stamp;
            }
            if (selected == null) return false;
            forget(selected, candidate);
            return reads.Count < limit;
        }
    }
}
