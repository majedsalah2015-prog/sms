using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Sms.TestSupport
{
    /// <summary>
    /// DB/04 §6 / Implementation/05 perf gates: samples an operation N times and asserts its P95 against a
    /// budget (NF-P3 ≤ 2 s interactive, NF-P4 ≤ 1 s attendance save, NF-P5 ≤ 10 s standard report). Reports the
    /// distribution so a run's numbers survive in the test log; the assertion is the CI regression gate.
    /// </summary>
    public sealed class PerfGate
    {
        private readonly List<double> _samplesMs = new();

        public PerfGate(string name, TimeSpan budget)
        {
            Name = name;
            Budget = budget;
        }

        public string Name { get; }

        public TimeSpan Budget { get; }

        public IReadOnlyList<double> SamplesMs => _samplesMs;

        public double P50Ms => Percentile(50);

        public double P95Ms => Percentile(95);

        public double MaxMs => _samplesMs.Count == 0 ? 0 : _samplesMs.Max();

        public bool Passed => _samplesMs.Count > 0 && P95Ms <= Budget.TotalMilliseconds;

        public async Task SampleAsync(Func<Task> operation)
        {
            var sw = Stopwatch.StartNew();
            await operation();
            _samplesMs.Add(sw.Elapsed.TotalMilliseconds);
        }

        public async Task SampleAsync(int times, Func<int, Task> operation)
        {
            for (var i = 0; i < times; i++)
            {
                await SampleAsync(() => operation(i));
            }
        }

        /// <summary>Nearest-rank percentile over the recorded samples.</summary>
        public double Percentile(int p)
        {
            if (_samplesMs.Count == 0)
            {
                return 0;
            }

            var sorted = _samplesMs.OrderBy(x => x).ToList();
            var rank = (int)Math.Ceiling(p / 100.0 * sorted.Count) - 1;
            return sorted[Math.Clamp(rank, 0, sorted.Count - 1)];
        }

        public string Summary()
            => $"{Name}: n={_samplesMs.Count} p50={P50Ms:F1}ms p95={P95Ms:F1}ms max={MaxMs:F1}ms budget={Budget.TotalMilliseconds:F0}ms → {(Passed ? "PASS" : "FAIL")}";
    }
}
