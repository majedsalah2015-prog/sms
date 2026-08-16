using System.Collections.Generic;

namespace Sms.Application.Grading
{
    /// <summary>Pure BR-GRA-001: which band a score % falls into. Bands are inclusive on both ends (e.g. 90-100, 80-89.99) — callers are responsible for defining non-overlapping ranges (see <see cref="AreNonOverlapping"/>).</summary>
    public static class ScaleBandResolver
    {
        public readonly struct Band
        {
            public Band(int id, decimal minPercent, decimal maxPercent)
            {
                Id = id;
                MinPercent = minPercent;
                MaxPercent = maxPercent;
            }

            public int Id { get; }

            public decimal MinPercent { get; }

            public decimal MaxPercent { get; }
        }

        public static int? Resolve(decimal scorePercent, IEnumerable<Band> bands)
        {
            foreach (var band in bands)
            {
                if (scorePercent >= band.MinPercent && scorePercent <= band.MaxPercent)
                {
                    return band.Id;
                }
            }

            return null;
        }

        /// <summary>BR-GRA validation rule: band ranges must not overlap.</summary>
        public static bool AreNonOverlapping(IReadOnlyList<Band> bands)
        {
            for (var i = 0; i < bands.Count; i++)
            {
                for (var j = i + 1; j < bands.Count; j++)
                {
                    if (bands[i].MinPercent <= bands[j].MaxPercent && bands[j].MinPercent <= bands[i].MaxPercent)
                    {
                        return false;
                    }
                }
            }

            return true;
        }
    }
}
