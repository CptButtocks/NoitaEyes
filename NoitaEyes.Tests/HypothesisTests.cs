using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NoitaEyes.Engine;
using Xunit;
using Xunit.Abstractions;

namespace NoitaEyes.Tests;

public sealed class HypothesisTests
{
    private readonly ITestOutputHelper _output;

    public HypothesisTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public static IEnumerable<object[]> Cases => HypothesisCatalog.Cases.Select(c => new object[] { c });

    [Theory]
    [MemberData(nameof(Cases))]
    [Trait("Category", "Hypothesis")]
    public void EvaluateHypothesis(HypothesisCase hypothesis)
    {
        var context = new HypothesisContext();
        var result = hypothesis.Evaluate(context);

        if (hypothesis.Expectation == HypothesisExpectation.Pending)
        {
            _output.WriteLine($"{hypothesis.Name}: pending ({result.Summary})");
            return;
        }

        var expectedPass = hypothesis.Expectation == HypothesisExpectation.Pass;
        if (expectedPass)
        {
            Assert.True(result.Passed, $"{hypothesis.Name}: {result.Summary}");
        }
        else
        {
            Assert.False(result.Passed, $"{hypothesis.Name}: expected to fail ({result.Summary})");
        }
    }
}

public sealed record HypothesisCase(
    string Name,
    string Description,
    HypothesisExpectation Expectation,
    Func<HypothesisContext, HypothesisResult> Evaluate
);

public sealed record HypothesisResult(bool Passed, string Summary, IReadOnlyDictionary<string, double>? Metrics = null);

public enum HypothesisExpectation
{
    Pass,
    Fail,
    Pending
}

public sealed class HypothesisContext
{
    private readonly EyePuzzleEngine _engine = new();
    private IReadOnlyList<EyeMessage>? _messages;
    private IReadOnlyList<EyePuzzleLayout>? _layouts;
    private IReadOnlyDictionary<int, IReadOnlyList<EyeTrigramToken>>? _weaveTrigrams;
    private IReadOnlyList<EyeTrigramToken>? _allWeaveTrigrams;

    public EyePuzzleEngine Engine => _engine;

    public IReadOnlyList<EyeMessage> Messages => _messages ??= _engine.ListMessages();

    public IReadOnlyList<EyePuzzleLayout> Layouts => _layouts ??= Messages.Select(m => _engine.Generate(m.Id)).ToArray();

    public IReadOnlyDictionary<int, IReadOnlyList<EyeTrigramToken>> WeaveTrigrams =>
        _weaveTrigrams ??= Messages.ToDictionary(m => m.Id, m => TrigramWeaver.BuildWeaveTrigrams(m, TrigramScheme.Canonical));

    public IReadOnlyList<EyeTrigramToken> AllWeaveTrigrams =>
        _allWeaveTrigrams ??= WeaveTrigrams.Values.SelectMany(t => t).ToArray();
}

public static class HypothesisCatalog
{
    public static IReadOnlyList<HypothesisCase> Cases { get; } = new List<HypothesisCase>
    {
        new(
            "Weave trigrams cover range 0-82",
            "Using the canonical weave scheme, trigram values span 0..82 across all messages.",
            HypothesisExpectation.Pass,
            context =>
            {
                var values = TrigramAnalysis.UniqueValues(context.AllWeaveTrigrams);

                var min = values.Count == 0 ? 0 : values.Min();
                var max = values.Count == 0 ? 0 : values.Max();
                var passed = values.Count == 83 && min == 0 && max == 82;
                var summary = $"Unique values: {values.Count}, min {min}, max {max}.";

                return new HypothesisResult(passed, summary, new Dictionary<string, double>
                {
                    ["unique"] = values.Count,
                    ["min"] = min,
                    ["max"] = max,
                });
            }
        ),
        new(
            "Canonical weave scheme is unique",
            "Only one permutation pair yields contiguous 0..82 values across all messages.",
            HypothesisExpectation.Pass,
            context =>
            {
                var matches = new List<TrigramScheme>();
                foreach (var scheme in TrigramScheme.AllPermutations())
                {
                    var values = new HashSet<int>();
                    foreach (var message in context.Messages)
                    {
                        var trigrams = TrigramWeaver.BuildWeaveTrigrams(message, scheme);
                        foreach (var trigram in trigrams)
                        {
                            values.Add(trigram.Base10Value);
                        }
                    }

                    var min = values.Count == 0 ? 0 : values.Min();
                    var max = values.Count == 0 ? 0 : values.Max();
                    if (values.Count == 83 && min == 0 && max == 82)
                    {
                        matches.Add(scheme);
                    }
                }

                var passed = matches.Count == 1;
                var summary = $"Schemes matching 0..82: {matches.Count}.";
                return new HypothesisResult(passed, summary, new Dictionary<string, double>
                {
                    ["matches"] = matches.Count,
                });
            }
        ),
        new(
            "Eye count divisible by three",
            "Each message has an eye count divisible by three.",
            HypothesisExpectation.Pass,
            context =>
            {
                var failures = new List<int>();
                foreach (var message in context.Messages)
                {
                    var eyeCount = message.Lines.Sum(l => l.Length);
                    if (eyeCount % 3 != 0)
                    {
                        failures.Add(message.Id);
                    }
                }

                var passed = failures.Count == 0;
                var summary = failures.Count == 0
                    ? "All messages divisible by three."
                    : $"Messages failing divisibility: {string.Join(", ", failures)}.";
                return new HypothesisResult(passed, summary);
            }
        ),
        new(
            "Trigram counts match reported lengths",
            "Weave trigram counts match the known per-message lengths.",
            HypothesisExpectation.Pass,
            context =>
            {
                var expected = new Dictionary<int, int>
                {
                    [0] = 99,
                    [1] = 103,
                    [2] = 118,
                    [3] = 102,
                    [4] = 137,
                    [5] = 124,
                    [6] = 119,
                    [7] = 120,
                    [8] = 114,
                };

                var failures = new List<string>();
                foreach (var (id, trigrams) in context.WeaveTrigrams)
                {
                    var count = trigrams.Count;
                    if (!expected.TryGetValue(id, out var expectedCount) || count != expectedCount)
                    {
                        failures.Add($"{id}:{count}");
                    }
                }

                var passed = failures.Count == 0;
                var summary = failures.Count == 0
                    ? "All counts match."
                    : $"Mismatched counts: {string.Join(", ", failures)}.";
                return new HypothesisResult(passed, summary);
            }
        ),
        new(
            "Unique trigram counts match reported values",
            "Per-message unique trigram counts match published counts.",
            HypothesisExpectation.Pass,
            context =>
            {
                var expected = new Dictionary<int, int>
                {
                    [0] = 57,
                    [1] = 57,
                    [2] = 62,
                    [3] = 61,
                    [4] = 67,
                    [5] = 65,
                    [6] = 62,
                    [7] = 68,
                    [8] = 63,
                };

                var failures = new List<string>();
                foreach (var (id, trigrams) in context.WeaveTrigrams)
                {
                    var unique = trigrams.Select(t => t.Base10Value).Distinct().Count();
                    if (!expected.TryGetValue(id, out var expectedCount) || unique != expectedCount)
                    {
                        failures.Add($"{id}:{unique}");
                    }
                }

                var passed = failures.Count == 0;
                var summary = failures.Count == 0
                    ? "All unique counts match."
                    : $"Mismatched unique counts: {string.Join(", ", failures)}.";
                return new HypothesisResult(passed, summary);
            }
        ),
        new(
            "Total trigram count is 1036",
            "Total number of trigrams across all messages equals 1036.",
            HypothesisExpectation.Pass,
            context =>
            {
                var total = context.WeaveTrigrams.Values.Sum(t => t.Count);
                var passed = total == 1036;
                var summary = $"Total trigrams: {total}.";
                return new HypothesisResult(passed, summary, new Dictionary<string, double>
                {
                    ["total"] = total,
                });
            }
        ),
        new(
            "First trigrams match reported values",
            "First trigram values match published sequence (by message id order).",
            HypothesisExpectation.Pass,
            context =>
            {
                var expected = new Dictionary<int, int>
                {
                    [0] = 50,
                    [1] = 80,
                    [2] = 36,
                    [3] = 76,
                    [4] = 63,
                    [5] = 34,
                    [6] = 27,
                    [7] = 77,
                    [8] = 33,
                };

                var failures = new List<string>();
                foreach (var (id, trigrams) in context.WeaveTrigrams)
                {
                    if (trigrams.Count == 0)
                    {
                        failures.Add($"{id}:empty");
                        continue;
                    }

                    var first = trigrams[0].Base10Value;
                    if (!expected.TryGetValue(id, out var expectedValue) || first != expectedValue)
                    {
                        failures.Add($"{id}:{first}");
                    }
                }

                var passed = failures.Count == 0;
                var summary = failures.Count == 0
                    ? "All starting trigrams match."
                    : $"Mismatched starts: {string.Join(", ", failures)}.";
                return new HypothesisResult(passed, summary);
            }
        ),
        new(
            "Message trigram sums match reported values",
            "Per-message trigram sums match published values.",
            HypothesisExpectation.Pass,
            context =>
            {
                var expected = new Dictionary<int, int>
                {
                    [0] = 4040,
                    [1] = 4124,
                    [2] = 4754,
                    [3] = 4295,
                    [4] = 5656,
                    [5] = 4748,
                    [6] = 5385,
                    [7] = 4936,
                    [8] = 4545,
                };

                var failures = new List<string>();
                foreach (var (id, trigrams) in context.WeaveTrigrams)
                {
                    var sum = trigrams.Sum(t => t.Base10Value);
                    if (!expected.TryGetValue(id, out var expectedSum) || sum != expectedSum)
                    {
                        failures.Add($"{id}:{sum}");
                    }
                }

                var passed = failures.Count == 0;
                var summary = failures.Count == 0
                    ? "All sums match."
                    : $"Mismatched sums: {string.Join(", ", failures)}.";
                return new HypothesisResult(passed, summary);
            }
        ),
        new(
            "ABAB sum pattern for East-only messages",
            "E1, E3, and E5 trigram sums follow an ABAB pattern (4040, 5656, 4545).",
            HypothesisExpectation.Pass,
            context =>
            {
                var expected = new Dictionary<int, int>
                {
                    [0] = 4040,
                    [4] = 5656,
                    [8] = 4545,
                };

                var failures = new List<string>();
                foreach (var (id, expectedSum) in expected)
                {
                    var sum = context.WeaveTrigrams[id].Sum(t => t.Base10Value);
                    if (sum != expectedSum)
                    {
                        failures.Add($"{id}:{sum}");
                    }
                }

                var passed = failures.Count == 0;
                var summary = failures.Count == 0
                    ? "ABAB sums match for E1, E3, E5."
                    : $"ABAB sum mismatches: {string.Join(", ", failures)}.";
                return new HypothesisResult(passed, summary);
            }
        ),
        new(
            "No two-digit prime factors in trigram sums",
            "No message sum has a two-digit prime factor.",
            HypothesisExpectation.Pass,
            context =>
            {
                var primes = new[]
                {
                    11, 13, 17, 19, 23, 29, 31, 37, 41, 43,
                    47, 53, 59, 61, 67, 71, 73, 79, 83, 89, 97,
                };

                var failures = new List<string>();
                foreach (var (id, trigrams) in context.WeaveTrigrams)
                {
                    var sum = trigrams.Sum(t => t.Base10Value);
                    if (primes.Any(p => sum % p == 0))
                    {
                        failures.Add($"{id}:{sum}");
                    }
                }

                var passed = failures.Count == 0;
                var summary = failures.Count == 0
                    ? "No two-digit prime factors found."
                    : $"Prime-factor failures: {string.Join(", ", failures)}.";
                return new HypothesisResult(passed, summary);
            }
        ),
        new(
            "First two trigrams have GCD > 1",
            "The first two trigrams of each message are not coprime.",
            HypothesisExpectation.Pass,
            context =>
            {
                var failures = new List<int>();
                foreach (var (id, trigrams) in context.WeaveTrigrams)
                {
                    if (trigrams.Count < 2)
                    {
                        failures.Add(id);
                        continue;
                    }

                    var gcd = Gcd(trigrams[0].Base10Value, trigrams[1].Base10Value);
                    if (gcd == 1)
                    {
                        failures.Add(id);
                    }
                }

                var passed = failures.Count == 0;
                var summary = failures.Count == 0
                    ? "All first-pair GCDs > 1."
                    : $"GCD failures: {string.Join(", ", failures)}.";
                return new HypothesisResult(passed, summary);
            }
        ),
        new(
            "Repeat gap counts (0,1,3) match reported",
            "Gap counts between repeated trigrams match reported values (gap0=0, gap1=5, gap3=26).",
            HypothesisExpectation.Pass,
            context =>
            {
                var totalCounts = new Dictionary<int, int>();
                foreach (var trigrams in context.WeaveTrigrams.Values)
                {
                    var gaps = TrigramAnalysis.ComputeRepeatGapCounts(trigrams, gapIsBetweenCount: true);
                    foreach (var (gap, count) in gaps)
                    {
                        totalCounts[gap] = totalCounts.TryGetValue(gap, out var existing) ? existing + count : count;
                    }
                }

                totalCounts.TryGetValue(0, out var gap0);
                totalCounts.TryGetValue(1, out var gap1);
                totalCounts.TryGetValue(3, out var gap3);

                var passed = gap0 == 0 && gap1 == 5 && gap3 == 26;
                var summary = $"gap0={gap0}, gap1={gap1}, gap3={gap3}.";
                return new HypothesisResult(passed, summary, new Dictionary<string, double>
                {
                    ["gap0"] = gap0,
                    ["gap1"] = gap1,
                    ["gap3"] = gap3,
                });
            }
        ),
        new(
            "Transition graph metrics",
            "Validate node/edge counts, sinks/sources, and SCC coverage for the transition graph.",
            HypothesisExpectation.Pass,
            context =>
            {
                var analysis = TrigramAnalysis.AnalyzeTransitionGraph(context.AllWeaveTrigrams, hubThreshold: 10);

                var passed = analysis.NodeCount == 83
                    && analysis.EdgeCount == 858
                    && analysis.TotalTransitions == 1035
                    && analysis.Sources.Count == 0
                    && analysis.Sinks.Count == 0
                    && analysis.LargestComponentSize == 83
                    && analysis.StronglyConnectedComponents.Count == 1;

                var summary = $"nodes={analysis.NodeCount}, edges={analysis.EdgeCount}, transitions={analysis.TotalTransitions}, " +
                              $"sources={analysis.Sources.Count}, sinks={analysis.Sinks.Count}, " +
                              $"largestScc={analysis.LargestComponentSize}, sccCount={analysis.StronglyConnectedComponents.Count}.";

                return new HypothesisResult(passed, summary, new Dictionary<string, double>
                {
                    ["nodes"] = analysis.NodeCount,
                    ["edges"] = analysis.EdgeCount,
                    ["transitions"] = analysis.TotalTransitions,
                    ["sources"] = analysis.Sources.Count,
                    ["sinks"] = analysis.Sinks.Count,
                    ["largestScc"] = analysis.LargestComponentSize,
                    ["sccCount"] = analysis.StronglyConnectedComponents.Count,
                });
            }
        ),
        new(
            "Transition graph hubs",
            "Top hub nodes (out-degree) match expected set.",
            HypothesisExpectation.Pass,
            context =>
            {
                var analysis = TrigramAnalysis.AnalyzeTransitionGraph(context.AllWeaveTrigrams, hubThreshold: 10);
                var top = analysis.Hubs.Take(10).ToArray();
                var expected = new[] { 26, 60, 54, 64, 5, 13, 21, 25, 11, 47 };

                var passed = top.SequenceEqual(expected) && analysis.Hubs.Count == 49;
                var summary = $"hubs={analysis.Hubs.Count}, top10={string.Join(",", top)}.";
                return new HypothesisResult(passed, summary, new Dictionary<string, double>
                {
                    ["hubs"] = analysis.Hubs.Count,
                });
            }
        ),
        new(
            "Transition clusters (weight >=2)",
            "Edges seen at least twice form 23 undirected clusters (largest = 59).",
            HypothesisExpectation.Pass,
            context =>
            {
                var analysis = TrigramAnalysis.AnalyzeTransitionClusters(context.AllWeaveTrigrams, minEdgeWeight: 2);

                var passed = analysis.ClusterCount == 23
                    && analysis.LargestClusterSize == 59
                    && analysis.SingletonCount == 20;

                var summary = $"clusters={analysis.ClusterCount}, largest={analysis.LargestClusterSize}, " +
                              $"singletons={analysis.SingletonCount}.";

                return new HypothesisResult(passed, summary, new Dictionary<string, double>
                {
                    ["clusters"] = analysis.ClusterCount,
                    ["largest"] = analysis.LargestClusterSize,
                    ["singletons"] = analysis.SingletonCount,
                });
            }
        ),
        new(
            "Transition clusters (weight >=3)",
            "Edges seen at least three times form 48 undirected clusters (largest = 34).",
            HypothesisExpectation.Pass,
            context =>
            {
                var analysis = TrigramAnalysis.AnalyzeTransitionClusters(context.AllWeaveTrigrams, minEdgeWeight: 3);

                var passed = analysis.ClusterCount == 48
                    && analysis.LargestClusterSize == 34
                    && analysis.SingletonCount == 45;

                var summary = $"clusters={analysis.ClusterCount}, largest={analysis.LargestClusterSize}, " +
                              $"singletons={analysis.SingletonCount}.";

                return new HypothesisResult(passed, summary, new Dictionary<string, double>
                {
                    ["clusters"] = analysis.ClusterCount,
                    ["largest"] = analysis.LargestClusterSize,
                    ["singletons"] = analysis.SingletonCount,
                });
            }
        ),
        new(
            "Largest cluster members (weight >=2)",
            "Largest cluster members for transitions with weight >= 2.",
            HypothesisExpectation.Pass,
            context =>
            {
                var analysis = TrigramAnalysis.AnalyzeTransitionClusters(context.AllWeaveTrigrams, minEdgeWeight: 2);
                var expected = new[]
                {
                    0, 1, 2, 3, 5, 8, 9, 11, 13, 14, 15, 16, 17, 18, 19, 20, 21, 24, 25, 26,
                    28, 29, 30, 31, 32, 34, 36, 40, 41, 42, 43, 44, 46, 47, 48, 49, 52, 54,
                    55, 57, 59, 60, 61, 62, 63, 64, 65, 66, 67, 68, 70, 71, 73, 75, 76, 78,
                    79, 80, 81,
                };

                var largest = analysis.Clusters.Count == 0 ? Array.Empty<int>() : analysis.Clusters[0].ToArray();
                var passed = largest.SequenceEqual(expected);
                var summary = $"largestSize={analysis.LargestClusterSize}, members={string.Join(",", largest)}.";
                return new HypothesisResult(passed, summary, new Dictionary<string, double>
                {
                    ["largest"] = analysis.LargestClusterSize,
                });
            }
        ),
        new(
            "Largest cluster members (weight >=3)",
            "Largest cluster members for transitions with weight >= 3.",
            HypothesisExpectation.Pass,
            context =>
            {
                var analysis = TrigramAnalysis.AnalyzeTransitionClusters(context.AllWeaveTrigrams, minEdgeWeight: 3);
                var expected = new[]
                {
                    1, 2, 3, 5, 8, 9, 13, 14, 15, 17, 18, 19, 21, 24, 29, 32, 36, 40, 42,
                    47, 48, 49, 54, 55, 59, 60, 61, 62, 66, 68, 70, 75, 78, 81,
                };

                var largest = analysis.Clusters.Count == 0 ? Array.Empty<int>() : analysis.Clusters[0].ToArray();
                var passed = largest.SequenceEqual(expected);
                var summary = $"largestSize={analysis.LargestClusterSize}, members={string.Join(",", largest)}.";
                return new HypothesisResult(passed, summary, new Dictionary<string, double>
                {
                    ["largest"] = analysis.LargestClusterSize,
                });
            }
        ),
        new(
            "Largest cluster members (weight >=4)",
            "Largest cluster members for transitions with weight >= 4.",
            HypothesisExpectation.Pass,
            context =>
            {
                var analysis = TrigramAnalysis.AnalyzeTransitionClusters(context.AllWeaveTrigrams, minEdgeWeight: 4);
                var expected = new[]
                {
                    2, 5, 13, 29, 40, 49, 54, 60, 62, 66, 75,
                };

                var largest = analysis.Clusters.Count == 0 ? Array.Empty<int>() : analysis.Clusters[0].ToArray();
                var passed = largest.SequenceEqual(expected);
                var summary = $"largestSize={analysis.LargestClusterSize}, members={string.Join(",", largest)}.";
                return new HypothesisResult(passed, summary, new Dictionary<string, double>
                {
                    ["largest"] = analysis.LargestClusterSize,
                });
            }
        ),
        new(
            "West vs East-only clusters (weight >=2)",
            "Compare largest clusters between West-paired messages and the East-only message.",
            HypothesisExpectation.Pass,
            context =>
            {
                var westIds = new[] { 1, 3, 5, 7 };
                var eastOnlyIds = new[] { 8 };

                var westTrigrams = westIds.SelectMany(id => context.WeaveTrigrams[id]).ToArray();
                var eastTrigrams = eastOnlyIds.SelectMany(id => context.WeaveTrigrams[id]).ToArray();

                var westAnalysis = TrigramAnalysis.AnalyzeTransitionClusters(westTrigrams, minEdgeWeight: 2);
                var eastAnalysis = TrigramAnalysis.AnalyzeTransitionClusters(eastTrigrams, minEdgeWeight: 2);

                var expectedWest = new[]
                {
                    0, 5, 9, 13, 17, 19, 25, 31, 34, 43, 46, 47, 48, 49, 54, 57, 62, 66, 67, 75,
                };
                var expectedEast = new[] { 5, 49 };

                var westLargest = westAnalysis.Clusters.Count == 0 ? Array.Empty<int>() : westAnalysis.Clusters[0].ToArray();
                var eastLargest = eastAnalysis.Clusters.Count == 0 ? Array.Empty<int>() : eastAnalysis.Clusters[0].ToArray();
                var intersection = westLargest.Intersect(eastLargest).OrderBy(v => v).ToArray();

                var passed = westLargest.SequenceEqual(expectedWest)
                    && eastLargest.SequenceEqual(expectedEast)
                    && intersection.SequenceEqual(expectedEast);

                var summary = $"westLargest={string.Join(",", westLargest)}; eastLargest={string.Join(",", eastLargest)}.";
                return new HypothesisResult(passed, summary, new Dictionary<string, double>
                {
                    ["westLargest"] = westAnalysis.LargestClusterSize,
                    ["eastLargest"] = eastAnalysis.LargestClusterSize,
                });
            }
        ),
        new(
            "West vs East-only clusters (weight >=3)",
            "West retains a small core at weight >= 3; East-only transitions become singletons.",
            HypothesisExpectation.Pass,
            context =>
            {
                var westIds = new[] { 1, 3, 5, 7 };
                var eastOnlyIds = new[] { 8 };

                var westTrigrams = westIds.SelectMany(id => context.WeaveTrigrams[id]).ToArray();
                var eastTrigrams = eastOnlyIds.SelectMany(id => context.WeaveTrigrams[id]).ToArray();

                var westAnalysis = TrigramAnalysis.AnalyzeTransitionClusters(westTrigrams, minEdgeWeight: 3);
                var eastAnalysis = TrigramAnalysis.AnalyzeTransitionClusters(eastTrigrams, minEdgeWeight: 3);

                var expectedWest = new[] { 5, 49, 54, 66, 75 };
                var westLargest = westAnalysis.Clusters.Count == 0 ? Array.Empty<int>() : westAnalysis.Clusters[0].ToArray();

                var passed = westLargest.SequenceEqual(expectedWest)
                    && eastAnalysis.ClusterCount == 63
                    && eastAnalysis.LargestClusterSize == 1
                    && eastAnalysis.SingletonCount == 63;

                var summary = $"westLargest={string.Join(",", westLargest)}; eastClusters={eastAnalysis.ClusterCount}, " +
                              $"eastLargest={eastAnalysis.LargestClusterSize}.";
                return new HypothesisResult(passed, summary, new Dictionary<string, double>
                {
                    ["westLargest"] = westAnalysis.LargestClusterSize,
                    ["eastClusters"] = eastAnalysis.ClusterCount,
                });
            }
        ),
        new(
            "East vs West paired clusters (weight >=2)",
            "Compare largest clusters between East-paired (E1..E4) and West-paired (W1..W4) messages.",
            HypothesisExpectation.Pass,
            context =>
            {
                var westIds = new[] { 1, 3, 5, 7 };
                var eastIds = new[] { 0, 2, 4, 6 };

                var westTrigrams = westIds.SelectMany(id => context.WeaveTrigrams[id]).ToArray();
                var eastTrigrams = eastIds.SelectMany(id => context.WeaveTrigrams[id]).ToArray();

                var westAnalysis = TrigramAnalysis.AnalyzeTransitionClusters(westTrigrams, minEdgeWeight: 2);
                var eastAnalysis = TrigramAnalysis.AnalyzeTransitionClusters(eastTrigrams, minEdgeWeight: 2);

                var expectedWest = new[]
                {
                    0, 5, 9, 13, 17, 19, 25, 31, 34, 43, 46, 47, 48, 49, 54, 57, 62, 66, 67, 75,
                };
                var expectedEast = new[]
                {
                    1, 2, 3, 5, 8, 13, 14, 15, 24, 26, 29, 32, 40, 42, 48, 49, 54, 60, 61, 62,
                    64, 65, 66, 70, 75, 78, 81,
                };

                var westLargest = westAnalysis.Clusters.Count == 0 ? Array.Empty<int>() : westAnalysis.Clusters[0].ToArray();
                var eastLargest = eastAnalysis.Clusters.Count == 0 ? Array.Empty<int>() : eastAnalysis.Clusters[0].ToArray();

                var passed = westAnalysis.ClusterCount == 62
                    && westAnalysis.LargestClusterSize == 20
                    && westAnalysis.SingletonCount == 59
                    && eastAnalysis.ClusterCount == 52
                    && eastAnalysis.LargestClusterSize == 27
                    && eastAnalysis.SingletonCount == 47
                    && westLargest.SequenceEqual(expectedWest)
                    && eastLargest.SequenceEqual(expectedEast);

                var summary = $"westClusters={westAnalysis.ClusterCount}, westLargest={westAnalysis.LargestClusterSize}; " +
                              $"eastClusters={eastAnalysis.ClusterCount}, eastLargest={eastAnalysis.LargestClusterSize}.";

                return new HypothesisResult(passed, summary, new Dictionary<string, double>
                {
                    ["westClusters"] = westAnalysis.ClusterCount,
                    ["eastClusters"] = eastAnalysis.ClusterCount,
                });
            }
        ),
        new(
            "East vs West paired clusters (weight >=3)",
            "East-paired collapses to a 2-node core at weight >= 3; West-paired retains a 5-node core.",
            HypothesisExpectation.Pass,
            context =>
            {
                var westIds = new[] { 1, 3, 5, 7 };
                var eastIds = new[] { 0, 2, 4, 6 };

                var westTrigrams = westIds.SelectMany(id => context.WeaveTrigrams[id]).ToArray();
                var eastTrigrams = eastIds.SelectMany(id => context.WeaveTrigrams[id]).ToArray();

                var westAnalysis = TrigramAnalysis.AnalyzeTransitionClusters(westTrigrams, minEdgeWeight: 3);
                var eastAnalysis = TrigramAnalysis.AnalyzeTransitionClusters(eastTrigrams, minEdgeWeight: 3);

                var expectedWest = new[] { 5, 49, 54, 66, 75 };
                var expectedEast = new[] { 5, 66 };

                var westLargest = westAnalysis.Clusters.Count == 0 ? Array.Empty<int>() : westAnalysis.Clusters[0].ToArray();
                var eastLargest = eastAnalysis.Clusters.Count == 0 ? Array.Empty<int>() : eastAnalysis.Clusters[0].ToArray();

                var passed = westAnalysis.ClusterCount == 78
                    && westAnalysis.LargestClusterSize == 5
                    && westAnalysis.SingletonCount == 76
                    && eastAnalysis.ClusterCount == 82
                    && eastAnalysis.LargestClusterSize == 2
                    && eastAnalysis.SingletonCount == 81
                    && westLargest.SequenceEqual(expectedWest)
                    && eastLargest.SequenceEqual(expectedEast);

                var summary = $"westClusters={westAnalysis.ClusterCount}, westLargest={westAnalysis.LargestClusterSize}; " +
                              $"eastClusters={eastAnalysis.ClusterCount}, eastLargest={eastAnalysis.LargestClusterSize}.";

                return new HypothesisResult(passed, summary, new Dictionary<string, double>
                {
                    ["westClusters"] = westAnalysis.ClusterCount,
                    ["eastClusters"] = eastAnalysis.ClusterCount,
                });
            }
        ),
        new(
            "Core cluster orientation counts (weight >=2)",
            "Counts of core-cluster occurrences (largest cluster, weight >=2) by message and orientation.",
            HypothesisExpectation.Pass,
            context =>
            {
                var core = new HashSet<int>
                {
                    0, 1, 2, 3, 5, 8, 9, 11, 13, 14, 15, 16, 17, 18, 19, 20, 21, 24, 25, 26,
                    28, 29, 30, 31, 32, 34, 36, 40, 41, 42, 43, 44, 46, 47, 48, 49, 52, 54,
                    55, 57, 59, 60, 61, 62, 63, 64, 65, 66, 67, 68, 70, 71, 73, 75, 76, 78,
                    79, 80, 81,
                };

                var expected = new Dictionary<int, (int Total, int Down, int Up)>
                {
                    [0] = (85, 41, 44),
                    [1] = (93, 48, 45),
                    [2] = (95, 48, 47),
                    [3] = (83, 42, 41),
                    [4] = (107, 53, 54),
                    [5] = (96, 46, 50),
                    [6] = (92, 48, 44),
                    [7] = (93, 47, 46),
                    [8] = (90, 48, 42),
                };

                var failures = new List<string>();
                var total = 0;
                var downTotal = 0;
                var upTotal = 0;

                foreach (var message in context.Messages)
                {
                    if (!expected.TryGetValue(message.Id, out var expectedCounts))
                    {
                        failures.Add($"{message.Id}:missing");
                        continue;
                    }

                    var occurrences = TrigramWeaver.BuildWeaveOccurrences(message, TrigramScheme.Canonical);
                    var coreOccurrences = occurrences.Where(o => core.Contains(o.Base10Value)).ToArray();
                    var totalCount = coreOccurrences.Length;
                    var downCount = coreOccurrences.Count(o => o.Orientation == TrigramOrientation.Down);
                    var upCount = coreOccurrences.Count(o => o.Orientation == TrigramOrientation.Up);

                    total += totalCount;
                    downTotal += downCount;
                    upTotal += upCount;

                    if (totalCount != expectedCounts.Total || downCount != expectedCounts.Down || upCount != expectedCounts.Up)
                    {
                        failures.Add($"{message.Id}:{totalCount}/{downCount}/{upCount}");
                    }
                }

                var passed = failures.Count == 0 && total == 834 && downTotal == 421 && upTotal == 413;
                var summary = failures.Count == 0
                    ? $"total={total}, down={downTotal}, up={upTotal}."
                    : $"Mismatches: {string.Join(", ", failures)}.";

                return new HypothesisResult(passed, summary, new Dictionary<string, double>
                {
                    ["total"] = total,
                    ["down"] = downTotal,
                    ["up"] = upTotal,
                });
            }
        ),
        new(
            "Core cluster row-pair distribution (weight >=2)",
            "Row-pair counts for core-cluster occurrences (largest cluster, weight >=2).",
            HypothesisExpectation.Pass,
            context =>
            {
                var core = new HashSet<int>
                {
                    0, 1, 2, 3, 5, 8, 9, 11, 13, 14, 15, 16, 17, 18, 19, 20, 21, 24, 25, 26,
                    28, 29, 30, 31, 32, 34, 36, 40, 41, 42, 43, 44, 46, 47, 48, 49, 52, 54,
                    55, 57, 59, 60, 61, 62, 63, 64, 65, 66, 67, 68, 70, 71, 73, 75, 76, 78,
                    79, 80, 81,
                };

                var expected = new Dictionary<int, Dictionary<int, int>>
                {
                    [0] = new Dictionary<int, int> { [0] = 25, [2] = 23, [4] = 19, [6] = 18 },
                    [1] = new Dictionary<int, int> { [0] = 26, [2] = 26, [4] = 21, [6] = 20 },
                    [2] = new Dictionary<int, int> { [0] = 25, [2] = 23, [4] = 17, [6] = 20, [8] = 10 },
                    [3] = new Dictionary<int, int> { [0] = 22, [2] = 21, [4] = 23, [6] = 17 },
                    [4] = new Dictionary<int, int> { [0] = 23, [2] = 21, [4] = 18, [6] = 21, [8] = 18, [10] = 6 },
                    [5] = new Dictionary<int, int> { [0] = 21, [2] = 21, [4] = 18, [6] = 22, [8] = 14 },
                    [6] = new Dictionary<int, int> { [0] = 23, [2] = 23, [4] = 16, [6] = 20, [8] = 10 },
                    [7] = new Dictionary<int, int> { [0] = 24, [2] = 18, [4] = 22, [6] = 16, [8] = 13 },
                    [8] = new Dictionary<int, int> { [0] = 24, [2] = 18, [4] = 21, [6] = 18, [8] = 9 },
                };

                var failures = new List<string>();
                foreach (var message in context.Messages)
                {
                    if (!expected.TryGetValue(message.Id, out var expectedCounts))
                    {
                        failures.Add($"{message.Id}:missing");
                        continue;
                    }

                    var occurrences = TrigramWeaver.BuildWeaveOccurrences(message, TrigramScheme.Canonical);
                    var coreOccurrences = occurrences.Where(o => core.Contains(o.Base10Value));
                    var actualCounts = coreOccurrences
                        .GroupBy(o => o.RowPair)
                        .ToDictionary(g => g.Key, g => g.Count());

                    foreach (var (rowPair, count) in expectedCounts)
                    {
                        if (!actualCounts.TryGetValue(rowPair, out var actual) || actual != count)
                        {
                            failures.Add($"{message.Id}:{rowPair}={actual}");
                        }
                    }

                    var extra = actualCounts.Keys.Except(expectedCounts.Keys).OrderBy(v => v).ToArray();
                    if (extra.Length > 0)
                    {
                        failures.Add($"{message.Id}:extra({string.Join(",", extra)})");
                    }
                }

                var passed = failures.Count == 0;
                var summary = failures.Count == 0
                    ? "Row-pair counts match."
                    : $"Mismatches: {string.Join(", ", failures)}.";

                return new HypothesisResult(passed, summary);
            }
        ),
        new(
            "East vs West row-pair distribution (weight >=2)",
            "Row-pair counts for core-cluster occurrences aggregated by East-paired vs West-paired subsets.",
            HypothesisExpectation.Pass,
            context =>
            {
                var core = new HashSet<int>
                {
                    0, 1, 2, 3, 5, 8, 9, 11, 13, 14, 15, 16, 17, 18, 19, 20, 21, 24, 25, 26,
                    28, 29, 30, 31, 32, 34, 36, 40, 41, 42, 43, 44, 46, 47, 48, 49, 52, 54,
                    55, 57, 59, 60, 61, 62, 63, 64, 65, 66, 67, 68, 70, 71, 73, 75, 76, 78,
                    79, 80, 81,
                };

                var eastIds = new[] { 0, 2, 4, 6 };
                var westIds = new[] { 1, 3, 5, 7 };

                var expectedEast = new Dictionary<int, int>
                {
                    [0] = 96,
                    [2] = 90,
                    [4] = 70,
                    [6] = 79,
                    [8] = 38,
                    [10] = 6,
                };

                var expectedWest = new Dictionary<int, int>
                {
                    [0] = 93,
                    [2] = 86,
                    [4] = 84,
                    [6] = 75,
                    [8] = 27,
                };

                var failures = new List<string>();

                var eastOccurrences = eastIds
                    .SelectMany(id => TrigramWeaver.BuildWeaveOccurrences(
                        context.Messages.First(m => m.Id == id),
                        TrigramScheme.Canonical))
                    .Where(o => core.Contains(o.Base10Value))
                    .ToArray();

                var westOccurrences = westIds
                    .SelectMany(id => TrigramWeaver.BuildWeaveOccurrences(
                        context.Messages.First(m => m.Id == id),
                        TrigramScheme.Canonical))
                    .Where(o => core.Contains(o.Base10Value))
                    .ToArray();

                var eastCounts = eastOccurrences
                    .GroupBy(o => o.RowPair)
                    .ToDictionary(g => g.Key, g => g.Count());

                var westCounts = westOccurrences
                    .GroupBy(o => o.RowPair)
                    .ToDictionary(g => g.Key, g => g.Count());

                foreach (var (rowPair, count) in expectedEast)
                {
                    if (!eastCounts.TryGetValue(rowPair, out var actual) || actual != count)
                    {
                        failures.Add($"east:{rowPair}={actual}");
                    }
                }

                foreach (var (rowPair, count) in expectedWest)
                {
                    if (!westCounts.TryGetValue(rowPair, out var actual) || actual != count)
                    {
                        failures.Add($"west:{rowPair}={actual}");
                    }
                }

                var extraEast = eastCounts.Keys.Except(expectedEast.Keys).OrderBy(v => v).ToArray();
                if (extraEast.Length > 0)
                {
                    failures.Add($"eastExtra({string.Join(",", extraEast)})");
                }

                var extraWest = westCounts.Keys.Except(expectedWest.Keys).OrderBy(v => v).ToArray();
                if (extraWest.Length > 0)
                {
                    failures.Add($"westExtra({string.Join(",", extraWest)})");
                }

                var eastTotal = eastCounts.Values.Sum();
                var westTotal = westCounts.Values.Sum();
                var passed = failures.Count == 0 && eastTotal == 379 && westTotal == 365;

                var summary = failures.Count == 0
                    ? $"eastTotal={eastTotal}, westTotal={westTotal}."
                    : $"Mismatches: {string.Join(", ", failures)}.";

                return new HypothesisResult(passed, summary, new Dictionary<string, double>
                {
                    ["eastTotal"] = eastTotal,
                    ["westTotal"] = westTotal,
                });
            }
        ),
        new(
            "Core cluster cell coverage (weight >=2)",
            "Core-cluster placements map to unique glyph coordinates per message.",
            HypothesisExpectation.Pass,
            context =>
            {
                var core = new HashSet<int>
                {
                    0, 1, 2, 3, 5, 8, 9, 11, 13, 14, 15, 16, 17, 18, 19, 20, 21, 24, 25, 26,
                    28, 29, 30, 31, 32, 34, 36, 40, 41, 42, 43, 44, 46, 47, 48, 49, 52, 54,
                    55, 57, 59, 60, 61, 62, 63, 64, 65, 66, 67, 68, 70, 71, 73, 75, 76, 78,
                    79, 80, 81,
                };

                var expected = new Dictionary<int, int>
                {
                    [0] = 255,
                    [1] = 279,
                    [2] = 285,
                    [3] = 249,
                    [4] = 321,
                    [5] = 288,
                    [6] = 276,
                    [7] = 279,
                    [8] = 270,
                };

                var failures = new List<string>();
                foreach (var message in context.Messages)
                {
                    if (!expected.TryGetValue(message.Id, out var expectedCount))
                    {
                        failures.Add($"{message.Id}:missing");
                        continue;
                    }

                    var placements = TrigramWeaver.BuildWeavePlacements(message, TrigramScheme.Canonical);
                    var corePlacements = placements.Where(p => core.Contains(p.Base10Value)).ToArray();
                    var positions = new HashSet<(int Row, int Col)>();

                    foreach (var placement in corePlacements)
                    {
                        positions.Add((placement.VertexA.Row, placement.VertexA.Column));
                        positions.Add((placement.VertexB.Row, placement.VertexB.Column));
                        positions.Add((placement.VertexC.Row, placement.VertexC.Column));
                    }

                    var uniqueCount = positions.Count;
                    var totalVertices = corePlacements.Length * 3;

                    if (uniqueCount != expectedCount || totalVertices != expectedCount)
                    {
                        failures.Add($"{message.Id}:{uniqueCount}/{totalVertices}");
                    }
                }

                var passed = failures.Count == 0;
                var summary = failures.Count == 0
                    ? "Unique core cell coverage matches expected totals."
                    : $"Mismatches: {string.Join(", ", failures)}.";

                return new HypothesisResult(passed, summary);
            }
        ),
        new(
            "Core cluster row distribution (weight >=2)",
            "Row counts for unique core-cluster glyph coordinates per message.",
            HypothesisExpectation.Pass,
            context =>
            {
                var core = new HashSet<int>
                {
                    0, 1, 2, 3, 5, 8, 9, 11, 13, 14, 15, 16, 17, 18, 19, 20, 21, 24, 25, 26,
                    28, 29, 30, 31, 32, 34, 36, 40, 41, 42, 43, 44, 46, 47, 48, 49, 52, 54,
                    55, 57, 59, 60, 61, 62, 63, 64, 65, 66, 67, 68, 70, 71, 73, 75, 76, 78,
                    79, 80, 81,
                };

                var expected = new Dictionary<int, Dictionary<int, int>>
                {
                    [0] = new Dictionary<int, int> { [0] = 37, [1] = 38, [2] = 33, [3] = 36, [4] = 28, [5] = 29, [6] = 28, [7] = 26 },
                    [1] = new Dictionary<int, int> { [0] = 39, [1] = 39, [2] = 39, [3] = 39, [4] = 32, [5] = 31, [6] = 31, [7] = 29 },
                    [2] = new Dictionary<int, int> { [0] = 38, [1] = 37, [2] = 34, [3] = 35, [4] = 27, [5] = 24, [6] = 29, [7] = 31, [8] = 15, [9] = 15 },
                    [3] = new Dictionary<int, int> { [0] = 33, [1] = 33, [2] = 32, [3] = 31, [4] = 35, [5] = 34, [6] = 25, [7] = 26 },
                    [4] = new Dictionary<int, int> { [0] = 35, [1] = 34, [2] = 33, [3] = 30, [4] = 26, [5] = 28, [6] = 31, [7] = 32, [8] = 26, [9] = 28, [10] = 9, [11] = 9 },
                    [5] = new Dictionary<int, int> { [0] = 31, [1] = 32, [2] = 33, [3] = 30, [4] = 26, [5] = 28, [6] = 31, [7] = 35, [8] = 21, [9] = 21 },
                    [6] = new Dictionary<int, int> { [0] = 35, [1] = 34, [2] = 33, [3] = 36, [4] = 25, [5] = 23, [6] = 30, [7] = 30, [8] = 17, [9] = 13 },
                    [7] = new Dictionary<int, int> { [0] = 36, [1] = 36, [2] = 27, [3] = 27, [4] = 34, [5] = 32, [6] = 24, [7] = 24, [8] = 19, [9] = 20 },
                    [8] = new Dictionary<int, int> { [0] = 36, [1] = 36, [2] = 28, [3] = 26, [4] = 31, [5] = 32, [6] = 29, [7] = 25, [8] = 14, [9] = 13 },
                };

                var failures = new List<string>();
                foreach (var message in context.Messages)
                {
                    if (!expected.TryGetValue(message.Id, out var expectedCounts))
                    {
                        failures.Add($"{message.Id}:missing");
                        continue;
                    }

                    var placements = TrigramWeaver.BuildWeavePlacements(message, TrigramScheme.Canonical);
                    var corePlacements = placements.Where(p => core.Contains(p.Base10Value)).ToArray();
                    var positions = new HashSet<(int Row, int Col)>();

                    foreach (var placement in corePlacements)
                    {
                        positions.Add((placement.VertexA.Row, placement.VertexA.Column));
                        positions.Add((placement.VertexB.Row, placement.VertexB.Column));
                        positions.Add((placement.VertexC.Row, placement.VertexC.Column));
                    }

                    var rowCounts = positions
                        .GroupBy(pos => pos.Row)
                        .ToDictionary(group => group.Key, group => group.Count());

                    foreach (var (row, count) in expectedCounts)
                    {
                        if (!rowCounts.TryGetValue(row, out var actual) || actual != count)
                        {
                            failures.Add($"{message.Id}:{row}={actual}");
                        }
                    }

                    var extra = rowCounts.Keys.Except(expectedCounts.Keys).OrderBy(v => v).ToArray();
                    if (extra.Length > 0)
                    {
                        failures.Add($"{message.Id}:extra({string.Join(",", extra)})");
                    }
                }

                var passed = failures.Count == 0;
                var summary = failures.Count == 0
                    ? "Row distributions match expected counts."
                    : $"Mismatches: {string.Join(", ", failures)}.";

                return new HypothesisResult(passed, summary);
            }
        ),
        new(
            "East/West pairing vs row-pair similarity (weight >=2)",
            "Nearest West row-pair profile for each East message does not consistently match its paired West.",
            HypothesisExpectation.Pass,
            context =>
            {
                var core = new HashSet<int>
                {
                    0, 1, 2, 3, 5, 8, 9, 11, 13, 14, 15, 16, 17, 18, 19, 20, 21, 24, 25, 26,
                    28, 29, 30, 31, 32, 34, 36, 40, 41, 42, 43, 44, 46, 47, 48, 49, 52, 54,
                    55, 57, 59, 60, 61, 62, 63, 64, 65, 66, 67, 68, 70, 71, 73, 75, 76, 78,
                    79, 80, 81,
                };

                var messagesById = context.Messages.ToDictionary(m => m.Id);
                var eastIds = new[] { 0, 2, 4, 6 };
                var westIds = new[] { 1, 3, 5, 7 };

                var expectedNearest = new Dictionary<int, (int WestId, int Dist)>
                {
                    [0] = (1, 8),
                    [2] = (5, 13),
                    [4] = (5, 13),
                    [6] = (5, 12),
                };

                var failures = new List<string>();
                var pairMatches = 0;
                var mappings = new List<string>();

                foreach (var eastId in eastIds)
                {
                    var eastCounts = GetRowPairCounts(messagesById[eastId], core);

                    var best = westIds
                        .Select(westId => (WestId: westId, Dist: L1Distance(eastCounts, GetRowPairCounts(messagesById[westId], core))))
                        .OrderBy(result => result.Dist)
                        .ThenBy(result => result.WestId)
                        .First();

                    if (!expectedNearest.TryGetValue(eastId, out var expected) ||
                        expected.WestId != best.WestId ||
                        expected.Dist != best.Dist)
                    {
                        failures.Add($"{eastId}->{best.WestId}:{best.Dist}");
                    }

                    var pairedWest = eastId + 1;
                    if (best.WestId == pairedWest)
                    {
                        pairMatches++;
                    }

                    mappings.Add($"{eastId}->{best.WestId}:{best.Dist}");
                }

                var passed = failures.Count == 0 && pairMatches == 2;
                var summary = failures.Count == 0
                    ? $"pairedMatches={pairMatches}/4, mappings={string.Join(", ", mappings)}."
                    : $"Mismatches: {string.Join(", ", failures)}; mappings={string.Join(", ", mappings)}.";

                return new HypothesisResult(passed, summary, new Dictionary<string, double>
                {
                    ["pairedMatches"] = pairMatches,
                });
            }
        ),
        new(
            "Universal transition 66->5 at index 1",
            "The most frequent transition (66->5) appears exactly once at index 1 on row-pair 0 in every message.",
            HypothesisExpectation.Pass,
            context =>
            {
                var failures = new List<string>();
                foreach (var message in context.Messages)
                {
                    var transitions = GetTransitions(message);
                    var hits = transitions.Where(t => t.From == 66 && t.To == 5).ToArray();
                    if (hits.Length != 1 || hits[0].Index != 1 || hits[0].RowPair != 0)
                    {
                        var details = hits.Length == 0
                            ? "none"
                            : string.Join(", ", hits.Select(h => $"{h.Index}@{h.RowPair}"));
                        failures.Add($"{message.Id}:{details}");
                    }
                }

                var passed = failures.Count == 0;
                var summary = failures.Count == 0
                    ? "All messages contain 66->5 at index 1 (row-pair 0)."
                    : $"Mismatches: {string.Join(", ", failures)}.";
                return new HypothesisResult(passed, summary);
            }
        ),
        new(
            "Early chain 5->49->75->54 in messages 3-8",
            "Messages 3-8 share the transition chain 5->49->75->54 at indices 2-4 (row-pair 0).",
            HypothesisExpectation.Pass,
            context =>
            {
                var failures = new List<string>();
                var chainMessages = new HashSet<int> { 3, 4, 5, 6, 7, 8 };

                foreach (var message in context.Messages)
                {
                    var transitions = GetTransitions(message);
                    var hits5_49 = transitions.Where(t => t.From == 5 && t.To == 49).ToArray();
                    var hits49_75 = transitions.Where(t => t.From == 49 && t.To == 75).ToArray();
                    var hits75_54 = transitions.Where(t => t.From == 75 && t.To == 54).ToArray();

                    if (chainMessages.Contains(message.Id))
                    {
                        var first5_49 = hits5_49.FirstOrDefault();
                        var first49_75 = hits49_75.FirstOrDefault();
                        var first75_54 = hits75_54.FirstOrDefault();

                        if (hits49_75.Length != 1 || first49_75.Index != 3 || first49_75.RowPair != 0)
                        {
                            failures.Add($"{message.Id}:49->75");
                        }

                        if (first5_49.Index != 2 || first5_49.RowPair != 0)
                        {
                            failures.Add($"{message.Id}:5->49");
                        }

                        if (first75_54.Index != 4 || first75_54.RowPair != 0)
                        {
                            failures.Add($"{message.Id}:75->54");
                        }

                        if (message.Id == 8)
                        {
                            var expected5_49 = new HashSet<(int Index, int RowPair)>
                            {
                                (2, 0),
                                (36, 2),
                            };
                            var expected75_54 = new HashSet<(int Index, int RowPair)>
                            {
                                (4, 0),
                                (67, 4),
                            };

                            var actual5_49 = hits5_49.Select(h => (h.Index, h.RowPair)).ToHashSet();
                            var actual75_54 = hits75_54.Select(h => (h.Index, h.RowPair)).ToHashSet();

                            if (!expected5_49.SetEquals(actual5_49) || !expected75_54.SetEquals(actual75_54))
                            {
                                failures.Add($"{message.Id}:extra");
                            }
                        }
                        else
                        {
                            if (hits5_49.Length != 1 || hits75_54.Length != 1)
                            {
                                failures.Add($"{message.Id}:counts");
                            }
                        }
                    }
                    else
                    {
                        if (hits5_49.Length != 0 || hits49_75.Length != 0 || hits75_54.Length != 0)
                        {
                            failures.Add($"{message.Id}:unexpected");
                        }
                    }
                }

                var passed = failures.Count == 0;
                var summary = failures.Count == 0
                    ? "Chain positions and counts match expected pattern."
                    : $"Mismatches: {string.Join(", ", failures)}.";
                return new HypothesisResult(passed, summary);
            }
        ),
        new(
            "Transition 62->13 early in messages 0-2",
            "Transition 62->13 appears at index 4 (row-pair 0) in messages 0-2 and twice later in message 3.",
            HypothesisExpectation.Pass,
            context =>
            {
                var failures = new List<string>();
                foreach (var message in context.Messages)
                {
                    var transitions = GetTransitions(message);
                    var hits = transitions.Where(t => t.From == 62 && t.To == 13).ToArray();

                    if (message.Id <= 2)
                    {
                        if (hits.Length != 1 || hits[0].Index != 4 || hits[0].RowPair != 0)
                        {
                            failures.Add($"{message.Id}:early");
                        }
                    }
                    else if (message.Id == 3)
                    {
                        var expected = new HashSet<(int Index, int RowPair)>
                        {
                            (35, 2),
                            (66, 4),
                        };
                        var actual = hits.Select(h => (h.Index, h.RowPair)).ToHashSet();
                        if (!expected.SetEquals(actual))
                        {
                            failures.Add($"{message.Id}:late");
                        }
                    }
                    else
                    {
                        if (hits.Length != 0)
                        {
                            failures.Add($"{message.Id}:unexpected");
                        }
                    }
                }

                var passed = failures.Count == 0;
                var summary = failures.Count == 0
                    ? "62->13 positions match expected distribution."
                    : $"Mismatches: {string.Join(", ", failures)}.";
                return new HypothesisResult(passed, summary);
            }
        ),
        new(
            "Aligned East/West substitution conflicts (anchored)",
            "Anchored alignments (using [66,5]) show conflicting trigram mappings between East and West messages.",
            HypothesisExpectation.Pass,
            context =>
            {
                var pairs = new (int EastId, int WestId)[]
                {
                    (0, 1),
                    (2, 3),
                    (4, 5),
                    (6, 7),
                };

                var anchor = new[] { 66, 5 };
                var results = new List<string>();
                var totalConflicts = 0;
                var totalMappings = 0;
                var expectedPairDetails = new Dictionary<(int, int), (int Conflicts, int Mappings, int Score, int Matches, int Gaps)>
                {
                    [(0, 1)] = (21, 56, 32, 46, 10),
                    [(2, 3)] = (24, 57, -71, 19, 36),
                    [(4, 5)] = (29, 64, -80, 23, 37),
                    [(6, 7)] = (32, 61, -37, 30, 15),
                };
                var detailFailures = new List<string>();

                foreach (var (eastId, westId) in pairs)
                {
                    var east = context.WeaveTrigrams[eastId].Select(t => t.Base10Value).ToArray();
                    var west = context.WeaveTrigrams[westId].Select(t => t.Base10Value).ToArray();

                    var anchorA = IndexOfSubsequence(east, anchor);
                    var anchorB = IndexOfSubsequence(west, anchor);
                    if (anchorA < 0 || anchorB < 0)
                    {
                        results.Add($"{eastId}->{westId}:anchor-missing");
                        continue;
                    }

                    var alignment = SequenceAlignment.AlignAnchored(east, west, anchorA, anchorB, anchor.Length);
                    var mapping = new Dictionary<int, HashSet<int>>();

                    foreach (var step in alignment.Steps)
                    {
                        if (step.ValueA.HasValue && step.ValueB.HasValue)
                        {
                            var from = step.ValueA.Value;
                            var to = step.ValueB.Value;
                            if (!mapping.TryGetValue(from, out var set))
                            {
                                set = new HashSet<int>();
                                mapping[from] = set;
                            }

                            set.Add(to);
                        }
                    }

                    var conflicts = mapping.Count(kvp => kvp.Value.Count > 1);
                    totalConflicts += conflicts;
                    totalMappings += mapping.Count;

                    results.Add($"{eastId}->{westId}:conflicts={conflicts}, mappings={mapping.Count}, score={alignment.Score}, " +
                                $"matches={alignment.MatchCount}, gaps={alignment.GapCount}");

                    if (expectedPairDetails.TryGetValue((eastId, westId), out var expected) &&
                        (expected.Conflicts != conflicts ||
                         expected.Mappings != mapping.Count ||
                         expected.Score != alignment.Score ||
                         expected.Matches != alignment.MatchCount ||
                         expected.Gaps != alignment.GapCount))
                    {
                        detailFailures.Add($"{eastId}->{westId}");
                    }
                }

                var summary = $"totalConflicts={totalConflicts}, totalMappings={totalMappings}, details={string.Join(" | ", results)}.";
                var passed = totalConflicts == 106 && totalMappings == 238 && detailFailures.Count == 0;
                return new HypothesisResult(passed, summary, new Dictionary<string, double>
                {
                    ["totalConflicts"] = totalConflicts,
                    ["totalMappings"] = totalMappings,
                });
            }
        ),
        new(
            "Periodic substitution by index mod N (anchored)",
            "Test periodic substitution by index mod N across anchored East/West alignments.",
            HypothesisExpectation.Pass,
            context =>
            {
                var pairs = new (int EastId, int WestId)[]
                {
                    (0, 1),
                    (2, 3),
                    (4, 5),
                    (6, 7),
                };

                var anchor = new[] { 66, 5 };
                var results = new List<string>();
                var best = (Mod: 0, Conflicts: int.MaxValue, Mappings: 0);

                for (var mod = 2; mod <= 14; mod++)
                {
                    var totalConflicts = 0;
                    var totalMappings = 0;

                    foreach (var (eastId, westId) in pairs)
                    {
                        var east = context.WeaveTrigrams[eastId].Select(t => t.Base10Value).ToArray();
                        var west = context.WeaveTrigrams[westId].Select(t => t.Base10Value).ToArray();

                        var anchorA = IndexOfSubsequence(east, anchor);
                        var anchorB = IndexOfSubsequence(west, anchor);
                        if (anchorA < 0 || anchorB < 0)
                        {
                            continue;
                        }

                        var alignment = SequenceAlignment.AlignAnchored(east, west, anchorA, anchorB, anchor.Length);
                        var (conflicts, mappings) = ComputePeriodicConflicts(alignment, mod);
                        totalConflicts += conflicts;
                        totalMappings += mappings;
                    }

                    results.Add($"mod{mod}:conflicts={totalConflicts},mappings={totalMappings}");

                    if (totalConflicts < best.Conflicts)
                    {
                        best = (mod, totalConflicts, totalMappings);
                    }
                }

                var summary = $"best=mod{best.Mod} conflicts={best.Conflicts} mappings={best.Mappings}; " +
                              $"all={string.Join(" | ", results)}.";
                var passed = best.Mod == 12 && best.Conflicts == 16 && best.Mappings == 395;

                return new HypothesisResult(passed, summary, new Dictionary<string, double>
                {
                    ["bestMod"] = best.Mod,
                    ["bestConflicts"] = best.Conflicts,
                });
            }
        ),
        new(
            "Periodic substitution sweep with baseline (anchored)",
            "Sweep mod 2..60 with normalized conflict rates and shuffled baseline.",
            HypothesisExpectation.Pass,
            context =>
            {
                var pairs = new (int EastId, int WestId)[]
                {
                    (0, 1),
                    (2, 3),
                    (4, 5),
                    (6, 7),
                };

                var anchor = new[] { 66, 5 };
                var alignments = BuildAlignmentPairs(context, pairs, anchor);

                const int minMod = 2;
                const int maxMod = 60;
                const int baselineSamples = 200;
                var rng = new Random(1337);
                var entries = new List<string>();

                var bestByRate = (Mod: 0, Rate: double.MaxValue);
                var bestByDelta = (Mod: 0, Delta: double.MinValue);

                for (var mod = minMod; mod <= maxMod; mod++)
                {
                    var (conflicts, mappings) = ComputeConflicts(alignments, mod);
                    var actualRate = mappings == 0 ? 0 : conflicts / (double)mappings;

                    var sum = 0.0;
                    var min = int.MaxValue;
                    var max = 0;
                    for (var sample = 0; sample < baselineSamples; sample++)
                    {
                        var baselineConflicts = ComputeConflictsShuffled(alignments, mod, rng);
                        sum += baselineConflicts;
                        if (baselineConflicts < min) min = baselineConflicts;
                        if (baselineConflicts > max) max = baselineConflicts;
                    }

                    var meanConflicts = sum / baselineSamples;
                    var meanRate = mappings == 0 ? 0 : meanConflicts / mappings;
                    var delta = meanRate - actualRate;

                    if (actualRate < bestByRate.Rate)
                    {
                        bestByRate = (mod, actualRate);
                    }

                    if (delta > bestByDelta.Delta)
                    {
                        bestByDelta = (mod, delta);
                    }

                    entries.Add($"mod{mod}:conf={conflicts},map={mappings},rate={actualRate:F4}," +
                                 $"baseMean={meanConflicts:F2},baseRate={meanRate:F4},baseMin={min},baseMax={max}");
                }

                var summary = $"bestRate=mod{bestByRate.Mod} rate={bestByRate.Rate:F4}; " +
                              $"bestDelta=mod{bestByDelta.Mod} delta={bestByDelta.Delta:F4}; " +
                              $"entries={string.Join(" | ", entries)}.";

                var passed = bestByRate.Mod == 57 && bestByDelta.Mod == 8;
                return new HypothesisResult(passed, summary, new Dictionary<string, double>
                {
                    ["bestRateMod"] = bestByRate.Mod,
                    ["bestDeltaMod"] = bestByDelta.Mod,
                });
            }
        ),
        new(
            "Periodic substitution sweep with baseline (anchor 5-49-75-54)",
            "Sweep mod 2..60 using anchor [5,49,75,54] with normalized conflict rates and shuffled baseline.",
            HypothesisExpectation.Pass,
            context =>
            {
                var pairs = new (int EastId, int WestId)[]
                {
                    (0, 1),
                    (2, 3),
                    (4, 5),
                    (6, 7),
                };

                var anchor = new[] { 5, 49, 75, 54 };
                var alignments = BuildAlignmentPairs(context, pairs, anchor);

                const int minMod = 2;
                const int maxMod = 60;
                const int baselineSamples = 200;
                var rng = new Random(2026);
                var entries = new List<string>();

                var bestByRate = (Mod: 0, Rate: double.MaxValue);
                var bestByDelta = (Mod: 0, Delta: double.MinValue);

                for (var mod = minMod; mod <= maxMod; mod++)
                {
                    var (conflicts, mappings) = ComputeConflicts(alignments, mod);
                    var actualRate = mappings == 0 ? 0 : conflicts / (double)mappings;

                    var sum = 0.0;
                    var min = int.MaxValue;
                    var max = 0;
                    for (var sample = 0; sample < baselineSamples; sample++)
                    {
                        var baselineConflicts = ComputeConflictsShuffled(alignments, mod, rng);
                        sum += baselineConflicts;
                        if (baselineConflicts < min) min = baselineConflicts;
                        if (baselineConflicts > max) max = baselineConflicts;
                    }

                    var meanConflicts = sum / baselineSamples;
                    var meanRate = mappings == 0 ? 0 : meanConflicts / mappings;
                    var delta = meanRate - actualRate;

                    if (actualRate < bestByRate.Rate)
                    {
                        bestByRate = (mod, actualRate);
                    }

                    if (delta > bestByDelta.Delta)
                    {
                        bestByDelta = (mod, delta);
                    }

                    entries.Add($"mod{mod}:conf={conflicts},map={mappings},rate={actualRate:F4}," +
                                 $"baseMean={meanConflicts:F2},baseRate={meanRate:F4},baseMin={min},baseMax={max}");
                }

                var summary = $"bestRate=mod{bestByRate.Mod} rate={bestByRate.Rate:F4}; " +
                              $"bestDelta=mod{bestByDelta.Mod} delta={bestByDelta.Delta:F4}; " +
                              $"entries={string.Join(" | ", entries)}.";

                var passed = bestByRate.Mod == 48 && bestByDelta.Mod == 8;
                return new HypothesisResult(passed, summary, new Dictionary<string, double>
                {
                    ["bestRateMod"] = bestByRate.Mod,
                    ["bestDeltaMod"] = bestByDelta.Mod,
                });
            }
        ),
        new(
            "Markov predictability (in-sample)",
            "Order-1 and order-2 Markov models on the full dataset.",
            HypothesisExpectation.Pass,
            context =>
            {
                var stats = ComputeMarkovStats(context.Messages);

                var passed = stats.BaselineCorrect == 26
                    && stats.BaselineTotal == 1027
                    && stats.Order1Correct == 192
                    && stats.Order1Total == 1027
                    && stats.Order2Correct == 949
                    && stats.Order2Total == 1018
                    && stats.Order1Contexts == 83
                    && stats.Order2Contexts == 842
                    && stats.Order2Deterministic == 783;

                var summary = $"baseline={stats.BaselineCorrect}/{stats.BaselineTotal}, " +
                              $"order1={stats.Order1Correct}/{stats.Order1Total}, " +
                              $"order2={stats.Order2Correct}/{stats.Order2Total}, " +
                              $"contexts1={stats.Order1Contexts}, contexts2={stats.Order2Contexts}.";

                return new HypothesisResult(passed, summary, new Dictionary<string, double>
                {
                    ["baseline"] = stats.BaselineCorrect,
                    ["order1"] = stats.Order1Correct,
                    ["order2"] = stats.Order2Correct,
                });
            }
        ),
        new(
            "Markov predictability (leave-one-out)",
            "Order-1/2 Markov models trained on all but one message and evaluated on the held-out message.",
            HypothesisExpectation.Pass,
            context =>
            {
                var stats = ComputeLeaveOneOutMarkovStats(context.Messages);

                var passed = stats.Order1Correct == 96
                    && stats.Order1Seen == 1027
                    && stats.Order1Total == 1027
                    && stats.Order2Correct == 158
                    && stats.Order2Seen == 269
                    && stats.Order2Total == 1018;

                var summary = $"order1={stats.Order1Correct}/{stats.Order1Seen} (total {stats.Order1Total}), " +
                              $"order2={stats.Order2Correct}/{stats.Order2Seen} (total {stats.Order2Total}).";

                return new HypothesisResult(passed, summary, new Dictionary<string, double>
                {
                    ["order1"] = stats.Order1Correct,
                    ["order2"] = stats.Order2Correct,
                });
            }
        ),
        new(
            "Markov predictability (leave-one-out, smoothed)",
            "Interpolated n-gram model with add-k smoothing evaluated on held-out messages.",
            HypothesisExpectation.Pass,
            context =>
            {
                var stats = ComputeLeaveOneOutSmoothedStats(context.Messages, k: 0.5, lambda2: 0.6, lambda1: 0.3, lambda0: 0.1);

                var passed = stats.Order1Correct == 94
                    && stats.Order1Total == 1027
                    && stats.Order2Correct == 174
                    && stats.Order2Total == 1018;

                var summary = $"order1={stats.Order1Correct}/{stats.Order1Total}, " +
                              $"order2={stats.Order2Correct}/{stats.Order2Total} " +
                              $"(k={stats.K}, l2={stats.Lambda2:0.##}, l1={stats.Lambda1:0.##}, l0={stats.Lambda0:0.##}).";

                return new HypothesisResult(passed, summary, new Dictionary<string, double>
                {
                    ["order1"] = stats.Order1Correct,
                    ["order2"] = stats.Order2Correct,
                });
            }
        ),
        new(
            "Markov predictability (smoothed, East/West subsets)",
            "Compare smoothed leave-one-out predictability on East-paired vs West-paired subsets.",
            HypothesisExpectation.Pass,
            context =>
            {
                var eastPairedIds = new HashSet<int> { 0, 2, 4, 6 };
                var westPairedIds = new HashSet<int> { 1, 3, 5, 7 };
                var eastAllIds = new HashSet<int> { 0, 2, 4, 6, 8 };

                var eastPaired = context.Messages.Where(m => eastPairedIds.Contains(m.Id)).ToArray();
                var westPaired = context.Messages.Where(m => westPairedIds.Contains(m.Id)).ToArray();
                var eastAll = context.Messages.Where(m => eastAllIds.Contains(m.Id)).ToArray();

                var eastStats = ComputeLeaveOneOutSmoothedStats(eastPaired, k: 0.5, lambda2: 0.6, lambda1: 0.3, lambda0: 0.1);
                var westStats = ComputeLeaveOneOutSmoothedStats(westPaired, k: 0.5, lambda2: 0.6, lambda1: 0.3, lambda0: 0.1);
                var eastAllStats = ComputeLeaveOneOutSmoothedStats(eastAll, k: 0.5, lambda2: 0.6, lambda1: 0.3, lambda0: 0.1);

                var passed = eastStats.Order1Correct == 17
                    && eastStats.Order1Total == 469
                    && eastStats.Order2Correct == 62
                    && eastStats.Order2Total == 465
                    && westStats.Order1Correct == 21
                    && westStats.Order1Total == 445
                    && westStats.Order2Correct == 20
                    && westStats.Order2Total == 441
                    && eastAllStats.Order1Correct == 33
                    && eastAllStats.Order1Total == 582
                    && eastAllStats.Order2Correct == 97
                    && eastAllStats.Order2Total == 577;

                var summary = $"east={eastStats.Order1Correct}/{eastStats.Order1Total}, " +
                              $"{eastStats.Order2Correct}/{eastStats.Order2Total}; " +
                              $"west={westStats.Order1Correct}/{westStats.Order1Total}, " +
                              $"{westStats.Order2Correct}/{westStats.Order2Total}; " +
                              $"eastAll={eastAllStats.Order1Correct}/{eastAllStats.Order1Total}, " +
                              $"{eastAllStats.Order2Correct}/{eastAllStats.Order2Total}.";

                return new HypothesisResult(passed, summary, new Dictionary<string, double>
                {
                    ["eastOrder2"] = eastStats.Order2Correct,
                    ["westOrder2"] = westStats.Order2Correct,
                });
            }
        ),
        new(
            "Motif-segmented headers",
            "Use early motifs to segment a header and validate shared tail templates.",
            HypothesisExpectation.Pass,
            context =>
            {
                var motifB = new[] { 5, 49, 75, 54 };
                var motifC = new[] { 62, 13 };
                var tailB = new[] { 66, 5, 49, 75, 54 };
                var tailC = new[] { 66, 5, 48, 62, 13 };

                var failures = new List<string>();
                var countB = 0;
                var countC = 0;

                foreach (var message in context.Messages)
                {
                    var seq = context.WeaveTrigrams[message.Id].Select(t => t.Base10Value).ToArray();
                    var b = IndexOfSubsequence(seq, motifB);
                    var c = IndexOfSubsequence(seq, motifC);

                    var motif = "";
                    var start = -1;
                    var length = 0;
                    if (b >= 0 && (c < 0 || b <= c))
                    {
                        motif = "B";
                        start = b;
                        length = motifB.Length;
                        countB++;
                    }
                    else if (c >= 0)
                    {
                        motif = "C";
                        start = c;
                        length = motifC.Length;
                        countC++;
                    }
                    else
                    {
                        failures.Add($"{message.Id}:anchor");
                        continue;
                    }

                    var end = start + length - 1;
                    var headerLength = end + 1;
                    if (headerLength != 6)
                    {
                        failures.Add($"{message.Id}:len{headerLength}");
                    }

                    var tail = seq.Skip(1).Take(5).ToArray();
                    if (motif == "B")
                    {
                        if (start != 2 || !tail.SequenceEqual(tailB))
                        {
                            failures.Add($"{message.Id}:tailB");
                        }
                    }
                    else
                    {
                        if (start != 4 || !tail.SequenceEqual(tailC))
                        {
                            failures.Add($"{message.Id}:tailC");
                        }
                    }
                }

                var passed = failures.Count == 0 && countB == 6 && countC == 3;
                var summary = failures.Count == 0
                    ? $"anchors ok (B={countB}, C={countC})."
                    : $"failures: {string.Join(", ", failures)}.";

                return new HypothesisResult(passed, summary, new Dictionary<string, double>
                {
                    ["motifB"] = countB,
                    ["motifC"] = countC,
                });
            }
        ),
        new(
            "Body row-pair counts (post-header)",
            "Row-pair distributions for the body (indices 6..end) after motif-based header segmentation.",
            HypothesisExpectation.Pass,
            context =>
            {
                var expected = new Dictionary<int, Dictionary<int, int>>
                {
                    [0] = new Dictionary<int, int> { [0] = 20, [2] = 26, [4] = 26, [6] = 21 },
                    [1] = new Dictionary<int, int> { [0] = 20, [2] = 26, [4] = 26, [6] = 25 },
                    [2] = new Dictionary<int, int> { [0] = 20, [2] = 26, [4] = 26, [6] = 26, [8] = 14 },
                    [3] = new Dictionary<int, int> { [0] = 20, [2] = 26, [4] = 26, [6] = 24 },
                    [4] = new Dictionary<int, int> { [0] = 20, [2] = 26, [4] = 26, [6] = 26, [8] = 26, [10] = 7 },
                    [5] = new Dictionary<int, int> { [0] = 20, [2] = 26, [4] = 26, [6] = 26, [8] = 20 },
                    [6] = new Dictionary<int, int> { [0] = 20, [2] = 26, [4] = 26, [6] = 26, [8] = 15 },
                    [7] = new Dictionary<int, int> { [0] = 20, [2] = 26, [4] = 26, [6] = 26, [8] = 16 },
                    [8] = new Dictionary<int, int> { [0] = 20, [2] = 26, [4] = 26, [6] = 26, [8] = 10 },
                };

                var failures = new List<string>();
                foreach (var message in context.Messages)
                {
                    var counts = GetBodyRowPairCounts(message, out _, out _);
                    if (!expected.TryGetValue(message.Id, out var expectedCounts))
                    {
                        failures.Add($"{message.Id}:missing");
                        continue;
                    }

                    var keys = expectedCounts.Keys.Concat(counts.Keys).Distinct().OrderBy(v => v);
                    foreach (var key in keys)
                    {
                        var actual = counts.TryGetValue(key, out var value) ? value : 0;
                        var expectedValue = expectedCounts.TryGetValue(key, out var expectedCount) ? expectedCount : 0;
                        if (actual != expectedValue)
                        {
                            failures.Add($"{message.Id}:{key}={actual}");
                        }
                    }
                }

                var passed = failures.Count == 0;
                var summary = failures.Count == 0
                    ? "All body row-pair counts match."
                    : $"Mismatches: {string.Join(", ", failures)}.";

                return new HypothesisResult(passed, summary);
            }
        ),
        new(
            "Body row-pair nearest neighbors",
            "Nearest neighbor by L1 distance does not align cleanly with header motif groups.",
            HypothesisExpectation.Pass,
            context =>
            {
                var motifB = new HashSet<int> { 3, 4, 5, 6, 7, 8 };
                var motifC = new HashSet<int> { 0, 1, 2 };

                var counts = context.Messages.ToDictionary(
                    m => m.Id,
                    m => GetBodyRowPairCounts(m, out _, out _));

                var ids = counts.Keys.OrderBy(v => v).ToArray();
                var nearest = new Dictionary<int, int>();

                foreach (var id in ids)
                {
                    var best = int.MaxValue;
                    var bestId = -1;
                    foreach (var other in ids)
                    {
                        if (other == id)
                        {
                            continue;
                        }

                        var dist = L1Distance(counts[id], counts[other]);
                        if (dist < best)
                        {
                            best = dist;
                            bestId = other;
                        }
                    }

                    nearest[id] = bestId;
                }

                var expected = new Dictionary<int, int>
                {
                    [0] = 3,
                    [1] = 3,
                    [2] = 6,
                    [3] = 1,
                    [4] = 5,
                    [5] = 7,
                    [6] = 2,
                    [7] = 6,
                    [8] = 2,
                };

                var failures = new List<string>();
                foreach (var (id, expectedNeighbor) in expected)
                {
                    if (!nearest.TryGetValue(id, out var actual) || actual != expectedNeighbor)
                    {
                        failures.Add($"{id}->{actual}");
                    }
                }

                var sameGroup = nearest.Count(kvp =>
                    (motifB.Contains(kvp.Key) && motifB.Contains(kvp.Value)) ||
                    (motifC.Contains(kvp.Key) && motifC.Contains(kvp.Value)));

                var passed = failures.Count == 0 && sameGroup == 3;
                var summary = failures.Count == 0
                    ? $"nearest neighbors ok; same-group={sameGroup}/9."
                    : $"nearest neighbor mismatches: {string.Join(", ", failures)}.";

                return new HypothesisResult(passed, summary, new Dictionary<string, double>
                {
                    ["sameGroup"] = sameGroup,
                });
            }
        ),
        new(
            "Body row-pair 8 prevalence",
            "Row-pair 8 counts are higher in motif-B bodies; row-pair 10 appears only once.",
            HypothesisExpectation.Pass,
            context =>
            {
                var motifB = new HashSet<int> { 3, 4, 5, 6, 7, 8 };
                var motifC = new HashSet<int> { 0, 1, 2 };

                var totalB = 0;
                var totalC = 0;
                var rowPair10Messages = new List<int>();

                foreach (var message in context.Messages)
                {
                    var counts = GetBodyRowPairCounts(message, out _, out _);
                    if (counts.TryGetValue(8, out var count8))
                    {
                        if (motifB.Contains(message.Id))
                        {
                            totalB += count8;
                        }
                        else if (motifC.Contains(message.Id))
                        {
                            totalC += count8;
                        }
                    }

                    if (counts.TryGetValue(10, out var count10) && count10 > 0)
                    {
                        rowPair10Messages.Add(message.Id);
                    }
                }

                var passed = totalB == 87 && totalC == 14 && rowPair10Messages.SequenceEqual(new[] { 4 });
                var summary = $"row8 B={totalB}, C={totalC}; row10={string.Join(",", rowPair10Messages)}.";

                return new HypothesisResult(passed, summary, new Dictionary<string, double>
                {
                    ["row8B"] = totalB,
                    ["row8C"] = totalC,
                    ["row10Count"] = rowPair10Messages.Count,
                });
            }
        ),
        new(
            "Body row-pair transitions (post-header)",
            "Row-pair transition counts for the body after header segmentation.",
            HypothesisExpectation.Pass,
            context =>
            {
                var expected = new Dictionary<int, Dictionary<(int From, int To), int>>
                {
                    [0] = new Dictionary<(int From, int To), int>
                    {
                        [(2, 2)] = 25,
                        [(4, 4)] = 25,
                        [(6, 6)] = 20,
                        [(0, 0)] = 19,
                        [(0, 2)] = 1,
                        [(2, 4)] = 1,
                        [(4, 6)] = 1,
                    },
                    [1] = new Dictionary<(int From, int To), int>
                    {
                        [(2, 2)] = 25,
                        [(4, 4)] = 25,
                        [(6, 6)] = 24,
                        [(0, 0)] = 19,
                        [(0, 2)] = 1,
                        [(2, 4)] = 1,
                        [(4, 6)] = 1,
                    },
                    [2] = new Dictionary<(int From, int To), int>
                    {
                        [(2, 2)] = 25,
                        [(4, 4)] = 25,
                        [(6, 6)] = 25,
                        [(0, 0)] = 19,
                        [(8, 8)] = 13,
                        [(0, 2)] = 1,
                        [(2, 4)] = 1,
                        [(4, 6)] = 1,
                        [(6, 8)] = 1,
                    },
                    [3] = new Dictionary<(int From, int To), int>
                    {
                        [(2, 2)] = 25,
                        [(4, 4)] = 25,
                        [(6, 6)] = 23,
                        [(0, 0)] = 19,
                        [(0, 2)] = 1,
                        [(2, 4)] = 1,
                        [(4, 6)] = 1,
                    },
                    [4] = new Dictionary<(int From, int To), int>
                    {
                        [(2, 2)] = 25,
                        [(4, 4)] = 25,
                        [(6, 6)] = 25,
                        [(8, 8)] = 25,
                        [(0, 0)] = 19,
                        [(10, 10)] = 6,
                        [(0, 2)] = 1,
                        [(2, 4)] = 1,
                        [(4, 6)] = 1,
                        [(6, 8)] = 1,
                        [(8, 10)] = 1,
                    },
                    [5] = new Dictionary<(int From, int To), int>
                    {
                        [(2, 2)] = 25,
                        [(4, 4)] = 25,
                        [(6, 6)] = 25,
                        [(0, 0)] = 19,
                        [(8, 8)] = 19,
                        [(0, 2)] = 1,
                        [(2, 4)] = 1,
                        [(4, 6)] = 1,
                        [(6, 8)] = 1,
                    },
                    [6] = new Dictionary<(int From, int To), int>
                    {
                        [(2, 2)] = 25,
                        [(4, 4)] = 25,
                        [(6, 6)] = 25,
                        [(0, 0)] = 19,
                        [(8, 8)] = 14,
                        [(0, 2)] = 1,
                        [(2, 4)] = 1,
                        [(4, 6)] = 1,
                        [(6, 8)] = 1,
                    },
                    [7] = new Dictionary<(int From, int To), int>
                    {
                        [(2, 2)] = 25,
                        [(4, 4)] = 25,
                        [(6, 6)] = 25,
                        [(0, 0)] = 19,
                        [(8, 8)] = 15,
                        [(0, 2)] = 1,
                        [(2, 4)] = 1,
                        [(4, 6)] = 1,
                        [(6, 8)] = 1,
                    },
                    [8] = new Dictionary<(int From, int To), int>
                    {
                        [(2, 2)] = 25,
                        [(4, 4)] = 25,
                        [(6, 6)] = 25,
                        [(0, 0)] = 19,
                        [(8, 8)] = 9,
                        [(0, 2)] = 1,
                        [(2, 4)] = 1,
                        [(4, 6)] = 1,
                        [(6, 8)] = 1,
                    },
                };

                var failures = new List<string>();
                foreach (var message in context.Messages)
                {
                    var transitions = GetBodyRowPairTransitions(message);
                    if (!expected.TryGetValue(message.Id, out var expectedTransitions))
                    {
                        failures.Add($"{message.Id}:missing");
                        continue;
                    }

                    var keys = expectedTransitions.Keys.Concat(transitions.Keys).Distinct().OrderBy(k => k.From).ThenBy(k => k.To);
                    foreach (var key in keys)
                    {
                        var actual = transitions.TryGetValue(key, out var value) ? value : 0;
                        var expectedValue = expectedTransitions.TryGetValue(key, out var expectedCount) ? expectedCount : 0;
                        if (actual != expectedValue)
                        {
                            failures.Add($"{message.Id}:{key.From}->{key.To}={actual}");
                        }
                    }
                }

                var passed = failures.Count == 0;
                var summary = failures.Count == 0
                    ? "All body transition counts match."
                    : $"Mismatches: {string.Join(", ", failures)}.";

                return new HypothesisResult(passed, summary);
            }
        ),
        new(
            "Body row-pair transition totals",
            "Global transition totals across all bodies.",
            HypothesisExpectation.Pass,
            context =>
            {
                var totals = new Dictionary<(int From, int To), int>();
                foreach (var message in context.Messages)
                {
                    var transitions = GetBodyRowPairTransitions(message);
                    foreach (var (key, count) in transitions)
                    {
                        totals[key] = totals.TryGetValue(key, out var existing) ? existing + count : count;
                    }
                }

                var expected = new Dictionary<(int From, int To), int>
                {
                    [(2, 2)] = 225,
                    [(4, 4)] = 225,
                    [(6, 6)] = 217,
                    [(0, 0)] = 171,
                    [(8, 8)] = 95,
                    [(0, 2)] = 9,
                    [(2, 4)] = 9,
                    [(4, 6)] = 9,
                    [(6, 8)] = 6,
                    [(10, 10)] = 6,
                    [(8, 10)] = 1,
                };

                var failures = new List<string>();
                foreach (var key in expected.Keys.Concat(totals.Keys).Distinct().OrderBy(k => k.From).ThenBy(k => k.To))
                {
                    var actual = totals.TryGetValue(key, out var value) ? value : 0;
                    var expectedValue = expected.TryGetValue(key, out var expectedCount) ? expectedCount : 0;
                    if (actual != expectedValue)
                    {
                        failures.Add($"{key.From}->{key.To}={actual}");
                    }
                }

                var passed = failures.Count == 0;
                var summary = failures.Count == 0
                    ? "Global transition totals match."
                    : $"Mismatches: {string.Join(", ", failures)}.";

                return new HypothesisResult(passed, summary);
            }
        ),
        new(
            "Body row-pair runs",
            "Run-length encoding of row-pair sequences in the body.",
            HypothesisExpectation.Pass,
            context =>
            {
                var expected = new Dictionary<int, (int Value, int Length)[]>
                {
                    [0] = new[] { (0, 20), (2, 26), (4, 26), (6, 21) },
                    [1] = new[] { (0, 20), (2, 26), (4, 26), (6, 25) },
                    [2] = new[] { (0, 20), (2, 26), (4, 26), (6, 26), (8, 14) },
                    [3] = new[] { (0, 20), (2, 26), (4, 26), (6, 24) },
                    [4] = new[] { (0, 20), (2, 26), (4, 26), (6, 26), (8, 26), (10, 7) },
                    [5] = new[] { (0, 20), (2, 26), (4, 26), (6, 26), (8, 20) },
                    [6] = new[] { (0, 20), (2, 26), (4, 26), (6, 26), (8, 15) },
                    [7] = new[] { (0, 20), (2, 26), (4, 26), (6, 26), (8, 16) },
                    [8] = new[] { (0, 20), (2, 26), (4, 26), (6, 26), (8, 10) },
                };

                var failures = new List<string>();
                foreach (var message in context.Messages)
                {
                    var runs = GetBodyRowPairRuns(message);
                    if (!expected.TryGetValue(message.Id, out var expectedRuns))
                    {
                        failures.Add($"{message.Id}:missing");
                        continue;
                    }

                    if (runs.Count != expectedRuns.Length)
                    {
                        failures.Add($"{message.Id}:runs{runs.Count}");
                        continue;
                    }

                    for (var i = 0; i < runs.Count; i++)
                    {
                        if (runs[i].Value != expectedRuns[i].Value || runs[i].Length != expectedRuns[i].Length)
                        {
                            failures.Add($"{message.Id}:{runs[i].Value}x{runs[i].Length}");
                            break;
                        }
                    }
                }

                var passed = failures.Count == 0;
                var summary = failures.Count == 0
                    ? "All run sequences match."
                    : $"Mismatches: {string.Join(", ", failures)}.";

                return new HypothesisResult(passed, summary);
            }
        ),
        new(
            "Body run-length correlations",
            "Correlate body run lengths (row-pairs 6/8/10) with body length, sum, and unique counts.",
            HypothesisExpectation.Pass,
            context =>
            {
                var expected = new Dictionary<int, (int BodyLen, int Sum, int Unique, int Run6, int Run8, int Run10)>
                {
                    [0] = (93, 4040, 57, 21, 0, 0),
                    [1] = (97, 4124, 57, 25, 0, 0),
                    [2] = (112, 4754, 62, 26, 14, 0),
                    [3] = (96, 4295, 61, 24, 0, 0),
                    [4] = (131, 5656, 67, 26, 26, 7),
                    [5] = (118, 4748, 65, 26, 20, 0),
                    [6] = (113, 5385, 62, 26, 15, 0),
                    [7] = (114, 4936, 68, 26, 16, 0),
                    [8] = (108, 4545, 63, 26, 10, 0),
                };

                var failures = new List<string>();
                var bodyLens = new List<double>();
                var sums = new List<double>();
                var uniques = new List<double>();
                var run6s = new List<double>();
                var run8s = new List<double>();
                var run10s = new List<double>();

                foreach (var message in context.Messages.OrderBy(m => m.Id))
                {
                    var metrics = GetBodyMetrics(message);
                    if (!expected.TryGetValue(message.Id, out var expectedMetrics))
                    {
                        failures.Add($"{message.Id}:missing");
                        continue;
                    }

                    if (metrics.BodyLength != expectedMetrics.BodyLen
                        || metrics.Sum != expectedMetrics.Sum
                        || metrics.Unique != expectedMetrics.Unique
                        || metrics.Run6 != expectedMetrics.Run6
                        || metrics.Run8 != expectedMetrics.Run8
                        || metrics.Run10 != expectedMetrics.Run10)
                    {
                        failures.Add($"{message.Id}:values");
                    }

                    bodyLens.Add(metrics.BodyLength);
                    sums.Add(metrics.Sum);
                    uniques.Add(metrics.Unique);
                    run6s.Add(metrics.Run6);
                    run8s.Add(metrics.Run8);
                    run10s.Add(metrics.Run10);
                }

                var correlations = new Dictionary<string, double>
                {
                    ["run6-bodyLen"] = Math.Round(Correlation(run6s, bodyLens), 4),
                    ["run8-bodyLen"] = Math.Round(Correlation(run8s, bodyLens), 4),
                    ["run10-bodyLen"] = Math.Round(Correlation(run10s, bodyLens), 4),
                    ["run6-sum"] = Math.Round(Correlation(run6s, sums), 4),
                    ["run8-sum"] = Math.Round(Correlation(run8s, sums), 4),
                    ["run10-sum"] = Math.Round(Correlation(run10s, sums), 4),
                    ["run6-unique"] = Math.Round(Correlation(run6s, uniques), 4),
                    ["run8-unique"] = Math.Round(Correlation(run8s, uniques), 4),
                    ["run10-unique"] = Math.Round(Correlation(run10s, uniques), 4),
                };

                var expectedCorr = new Dictionary<string, double>
                {
                    ["run6-bodyLen"] = 0.7219,
                    ["run8-bodyLen"] = 0.9885,
                    ["run10-bodyLen"] = 0.6745,
                    ["run6-sum"] = 0.6548,
                    ["run8-sum"] = 0.8916,
                    ["run10-sum"] = 0.6412,
                    ["run6-unique"] = 0.6778,
                    ["run8-unique"] = 0.8538,
                    ["run10-unique"] = 0.4407,
                };

                foreach (var (key, expectedValue) in expectedCorr)
                {
                    if (!correlations.TryGetValue(key, out var actual) || Math.Abs(actual - expectedValue) > 0.0001)
                    {
                        failures.Add($"{key}:{actual}");
                    }
                }

                var passed = failures.Count == 0;
                var summary = failures.Count == 0
                    ? "Correlations match."
                    : $"Mismatches: {string.Join(", ", failures)}.";

                return new HypothesisResult(passed, summary, correlations);
            }
        ),
        new(
            "Body run lengths by group",
            "Compare row-pair 8/10 run lengths by East/West and header motif groups.",
            HypothesisExpectation.Pass,
            context =>
            {
                var eastIds = new HashSet<int> { 0, 2, 4, 6, 8 };
                var westIds = new HashSet<int> { 1, 3, 5, 7 };

                var metrics = new Dictionary<int, (BodyMetrics Metrics, char Motif)>();
                foreach (var message in context.Messages)
                {
                    var seq = context.WeaveTrigrams[message.Id].Select(t => t.Base10Value).ToArray();
                    if (!TryGetHeaderEndIndex(seq, out _, out var motif))
                    {
                        continue;
                    }

                    metrics[message.Id] = (GetBodyMetrics(message), motif);
                }

                var east = AggregateGroup(metrics.Where(kvp => eastIds.Contains(kvp.Key)).Select(kvp => kvp.Value.Metrics));
                var west = AggregateGroup(metrics.Where(kvp => westIds.Contains(kvp.Key)).Select(kvp => kvp.Value.Metrics));
                var motifB = AggregateGroup(metrics.Where(kvp => kvp.Value.Motif == 'B').Select(kvp => kvp.Value.Metrics));
                var motifC = AggregateGroup(metrics.Where(kvp => kvp.Value.Motif == 'C').Select(kvp => kvp.Value.Metrics));

                var passed = east.TotalCount == 5
                    && east.Run8Total == 65
                    && east.Run10Total == 7
                    && east.Run8NonZero == 4
                    && east.Run10NonZero == 1
                    && west.TotalCount == 4
                    && west.Run8Total == 36
                    && west.Run10Total == 0
                    && west.Run8NonZero == 2
                    && west.Run10NonZero == 0
                    && motifB.TotalCount == 6
                    && motifB.Run8Total == 87
                    && motifB.Run10Total == 7
                    && motifB.Run8NonZero == 5
                    && motifB.Run10NonZero == 1
                    && motifC.TotalCount == 3
                    && motifC.Run8Total == 14
                    && motifC.Run10Total == 0
                    && motifC.Run8NonZero == 1
                    && motifC.Run10NonZero == 0;

                var summary = $"east run8={east.Run8Total}, run10={east.Run10Total}; " +
                              $"west run8={west.Run8Total}, run10={west.Run10Total}; " +
                              $"motifB run8={motifB.Run8Total}, run10={motifB.Run10Total}; " +
                              $"motifC run8={motifC.Run8Total}, run10={motifC.Run10Total}.";

                return new HypothesisResult(passed, summary, new Dictionary<string, double>
                {
                    ["eastRun8"] = east.Run8Total,
                    ["westRun8"] = west.Run8Total,
                    ["motifBRun8"] = motifB.Run8Total,
                    ["motifCRun8"] = motifC.Run8Total,
                });
            }
        ),
        new(
            "Row-pair 8/10 block value coverage",
            "Row-pair 8 blocks cover 101 values (57 unique); row-pair 10 has 7 unique values.",
            HypothesisExpectation.Pass,
            context =>
            {
                var values8 = new List<int>();
                var values10 = new List<int>();
                foreach (var message in context.Messages)
                {
                    values8.AddRange(GetRowPairBlockValues(message, rowPair: 8));
                    values10.AddRange(GetRowPairBlockValues(message, rowPair: 10));
                }

                var total8 = values8.Count;
                var unique8 = values8.Distinct().Count();
                var total10 = values10.Count;
                var unique10 = values10.Distinct().Count();

                var passed = total8 == 101 && unique8 == 57 && total10 == 7 && unique10 == 7;
                var summary = $"row8 total={total8}, unique={unique8}; row10 total={total10}, unique={unique10}.";

                return new HypothesisResult(passed, summary, new Dictionary<string, double>
                {
                    ["row8Total"] = total8,
                    ["row8Unique"] = unique8,
                    ["row10Total"] = total10,
                    ["row10Unique"] = unique10,
                });
            }
        ),
        new(
            "Row-pair 8/10 block transitions",
            "Row-pair 8 transitions are all unique (95 total); row-pair 10 transitions are all unique (6 total).",
            HypothesisExpectation.Pass,
            context =>
            {
                var transitions8 = new Dictionary<(int From, int To), int>();
                var transitions10 = new Dictionary<(int From, int To), int>();

                foreach (var message in context.Messages)
                {
                    MergeTransitions(transitions8, GetRowPairBlockTransitions(message, rowPair: 8));
                    MergeTransitions(transitions10, GetRowPairBlockTransitions(message, rowPair: 10));
                }

                var total8 = transitions8.Values.Sum();
                var total10 = transitions10.Values.Sum();
                var max8 = transitions8.Count == 0 ? 0 : transitions8.Values.Max();
                var max10 = transitions10.Count == 0 ? 0 : transitions10.Values.Max();
                var self8 = transitions8.Count(kvp => kvp.Key.From == kvp.Key.To);
                var self10 = transitions10.Count(kvp => kvp.Key.From == kvp.Key.To);

                var passed = transitions8.Count == 95
                    && total8 == 95
                    && max8 == 1
                    && transitions10.Count == 6
                    && total10 == 6
                    && max10 == 1
                    && self8 == 0
                    && self10 == 0;

                var summary = $"row8 transitions={transitions8.Count}, max={max8}; " +
                              $"row10 transitions={transitions10.Count}, max={max10}.";

                return new HypothesisResult(passed, summary, new Dictionary<string, double>
                {
                    ["row8Transitions"] = transitions8.Count,
                    ["row10Transitions"] = transitions10.Count,
                });
            }
        ),
        new(
            "Body block column overlays",
            "Row-pair body blocks map to contiguous column ranges in glyph coordinates.",
            HypothesisExpectation.Pass,
            context =>
            {
                var expected = new Dictionary<int, BodyBlockRange[]>
                {
                    [0] = new[]
                    {
                        new BodyBlockRange(0, 9, 38, 30, true, 9, 38, 30, true),
                        new BodyBlockRange(2, 0, 38, 39, true, 0, 38, 39, true),
                        new BodyBlockRange(4, 0, 38, 39, true, 0, 38, 39, true),
                        new BodyBlockRange(6, 0, 31, 32, true, 0, 30, 31, true),
                    },
                    [1] = new[]
                    {
                        new BodyBlockRange(0, 9, 38, 30, true, 9, 38, 30, true),
                        new BodyBlockRange(2, 0, 38, 39, true, 0, 38, 39, true),
                        new BodyBlockRange(4, 0, 38, 39, true, 0, 38, 39, true),
                        new BodyBlockRange(6, 0, 37, 38, true, 0, 36, 37, true),
                    },
                    [2] = new[]
                    {
                        new BodyBlockRange(0, 9, 38, 30, true, 9, 38, 30, true),
                        new BodyBlockRange(2, 0, 38, 39, true, 0, 38, 39, true),
                        new BodyBlockRange(4, 0, 38, 39, true, 0, 38, 39, true),
                        new BodyBlockRange(6, 0, 38, 39, true, 0, 38, 39, true),
                        new BodyBlockRange(8, 0, 20, 21, true, 0, 20, 21, true),
                    },
                    [3] = new[]
                    {
                        new BodyBlockRange(0, 9, 38, 30, true, 9, 38, 30, true),
                        new BodyBlockRange(2, 0, 38, 39, true, 0, 38, 39, true),
                        new BodyBlockRange(4, 0, 38, 39, true, 0, 38, 39, true),
                        new BodyBlockRange(6, 0, 35, 36, true, 0, 35, 36, true),
                    },
                    [4] = new[]
                    {
                        new BodyBlockRange(0, 9, 38, 30, true, 9, 38, 30, true),
                        new BodyBlockRange(2, 0, 38, 39, true, 0, 38, 39, true),
                        new BodyBlockRange(4, 0, 38, 39, true, 0, 38, 39, true),
                        new BodyBlockRange(6, 0, 38, 39, true, 0, 38, 39, true),
                        new BodyBlockRange(8, 0, 38, 39, true, 0, 38, 39, true),
                        new BodyBlockRange(10, 0, 10, 11, true, 0, 9, 10, true),
                    },
                    [5] = new[]
                    {
                        new BodyBlockRange(0, 9, 38, 30, true, 9, 38, 30, true),
                        new BodyBlockRange(2, 0, 38, 39, true, 0, 38, 39, true),
                        new BodyBlockRange(4, 0, 38, 39, true, 0, 38, 39, true),
                        new BodyBlockRange(6, 0, 38, 39, true, 0, 38, 39, true),
                        new BodyBlockRange(8, 0, 29, 30, true, 0, 29, 30, true),
                    },
                    [6] = new[]
                    {
                        new BodyBlockRange(0, 9, 38, 30, true, 9, 38, 30, true),
                        new BodyBlockRange(2, 0, 38, 39, true, 0, 38, 39, true),
                        new BodyBlockRange(4, 0, 38, 39, true, 0, 38, 39, true),
                        new BodyBlockRange(6, 0, 38, 39, true, 0, 38, 39, true),
                        new BodyBlockRange(8, 0, 22, 23, true, 0, 21, 22, true),
                    },
                    [7] = new[]
                    {
                        new BodyBlockRange(0, 9, 38, 30, true, 9, 38, 30, true),
                        new BodyBlockRange(2, 0, 38, 39, true, 0, 38, 39, true),
                        new BodyBlockRange(4, 0, 38, 39, true, 0, 38, 39, true),
                        new BodyBlockRange(6, 0, 38, 39, true, 0, 38, 39, true),
                        new BodyBlockRange(8, 0, 23, 24, true, 0, 23, 24, true),
                    },
                    [8] = new[]
                    {
                        new BodyBlockRange(0, 9, 38, 30, true, 9, 38, 30, true),
                        new BodyBlockRange(2, 0, 38, 39, true, 0, 38, 39, true),
                        new BodyBlockRange(4, 0, 38, 39, true, 0, 38, 39, true),
                        new BodyBlockRange(6, 0, 38, 39, true, 0, 38, 39, true),
                        new BodyBlockRange(8, 0, 14, 15, true, 0, 14, 15, true),
                    },
                };

                var failures = new List<string>();
                foreach (var message in context.Messages)
                {
                    var blocks = GetBodyBlockRanges(message);
                    if (!expected.TryGetValue(message.Id, out var expectedBlocks))
                    {
                        failures.Add($"{message.Id}:missing");
                        continue;
                    }

                    if (blocks.Count != expectedBlocks.Length)
                    {
                        failures.Add($"{message.Id}:count{blocks.Count}");
                        continue;
                    }

                    for (var i = 0; i < blocks.Count; i++)
                    {
                        var actual = blocks[i];
                        var exp = expectedBlocks[i];
                        if (actual.RowPair != exp.RowPair
                            || actual.TopMin != exp.TopMin
                            || actual.TopMax != exp.TopMax
                            || actual.TopCount != exp.TopCount
                            || actual.BottomMin != exp.BottomMin
                            || actual.BottomMax != exp.BottomMax
                            || actual.BottomCount != exp.BottomCount
                            || actual.TopContiguous != exp.TopContiguous
                            || actual.BottomContiguous != exp.BottomContiguous)
                        {
                            failures.Add($"{message.Id}:row{actual.RowPair}");
                            break;
                        }
                    }
                }

                var passed = failures.Count == 0;
                var summary = failures.Count == 0
                    ? "All body block ranges match and are contiguous."
                    : $"Mismatches: {string.Join(", ", failures)}.";

                return new HypothesisResult(passed, summary);
            }
        ),
        new(
            "Cell entropy/consensus map",
            "Per-cell entropy highlights a fixed header scaffold with variable lead cells.",
            HypothesisExpectation.Pass,
            context =>
            {
                var stats = ComputeCellEntropyStats(context.Messages).ToArray();
                var failures = new List<string>();

                if (stats.Length != 411)
                {
                    failures.Add($"total:{stats.Length}");
                }

                var expectedCoverage = new Dictionary<int, int>
                {
                    [1] = 39,
                    [2] = 12,
                    [3] = 3,
                    [4] = 3,
                    [5] = 12,
                    [6] = 33,
                    [7] = 3,
                    [8] = 9,
                    [9] = 297,
                };

                var actualCoverage = stats
                    .GroupBy(s => s.Coverage)
                    .ToDictionary(g => g.Key, g => g.Count());

                foreach (var (coverage, expectedCount) in expectedCoverage)
                {
                    if (!actualCoverage.TryGetValue(coverage, out var actualCount) || actualCount != expectedCount)
                    {
                        failures.Add($"coverage:{coverage}:{actualCount}");
                    }
                }

                foreach (var coverage in actualCoverage.Keys.Except(expectedCoverage.Keys))
                {
                    failures.Add($"coverageExtra:{coverage}");
                }

                var fixedCells = stats
                    .Where(s => s.Coverage == 9 && s.Entropy < 1e-9 && s.MaxValues.Length == 1)
                    .ToDictionary(s => (s.Row, s.Col), s => s.MaxValues[0]);

                var expectedFixed = new Dictionary<(int Row, int Col), int>
                {
                    [(0, 2)] = 1,
                    [(0, 3)] = 0,
                    [(0, 4)] = 1,
                    [(1, 1)] = 3,
                    [(1, 2)] = 2,
                    [(1, 3)] = 0,
                    [(1, 4)] = 4,
                    [(1, 5)] = 1,
                };

                if (fixedCells.Count != expectedFixed.Count)
                {
                    failures.Add($"fixedCount:{fixedCells.Count}");
                }

                foreach (var (key, expectedValue) in expectedFixed)
                {
                    if (!fixedCells.TryGetValue(key, out var actualValue) || actualValue != expectedValue)
                    {
                        failures.Add($"fixed:{key.Row},{key.Col}:{actualValue}");
                    }
                }

                var header = stats
                    .Where(s => (s.Row == 0 || s.Row == 1) && s.Col <= 8 && s.Coverage >= 6)
                    .ToArray();

                if (header.Length != 18)
                {
                    failures.Add($"headerCount:{header.Length}");
                }

                var headerFixed = header.Count(s => s.Entropy < 1e-9);
                if (headerFixed != 8)
                {
                    failures.Add($"headerFixed:{headerFixed}");
                }

                var headerAvg = Math.Round(header.Average(s => s.Entropy), 3);
                if (Math.Abs(headerAvg - 0.654) > 0.0001)
                {
                    failures.Add($"headerAvg:{headerAvg:0.000}");
                }

                var headerHigh = header
                    .Where(s => s.Entropy > 1.5)
                    .Select(s => (s.Row, s.Col))
                    .ToHashSet();
                var expectedHigh = new HashSet<(int Row, int Col)>
                {
                    (0, 0),
                    (0, 1),
                    (1, 0),
                };

                if (headerHigh.Count != expectedHigh.Count || !headerHigh.SetEquals(expectedHigh))
                {
                    failures.Add($"headerHigh:{headerHigh.Count}");
                }

                var passed = failures.Count == 0;
                var summary = failures.Count == 0
                    ? "Cell coverage and fixed header scaffold match expected map."
                    : $"Mismatches: {string.Join(", ", failures)}.";

                return new HypothesisResult(passed, summary, new Dictionary<string, double>
                {
                    ["totalCells"] = stats.Length,
                    ["coverage9"] = actualCoverage.TryGetValue(9, out var cov9) ? cov9 : 0,
                    ["fixedCells"] = fixedCells.Count,
                    ["headerAvgEntropy"] = headerAvg,
                });
            }
        ),
        new(
            "Cell East/West divergence map",
            "Per-cell value-set overlap (Jaccard) across East vs West messages with full coverage.",
            HypothesisExpectation.Pass,
            context =>
            {
                var eastIds = new HashSet<int> { 0, 2, 4, 6, 8 };
                var westIds = new HashSet<int> { 1, 3, 5, 7 };

                var east = ComputeCellValueSets(context.Messages, eastIds);
                var west = ComputeCellValueSets(context.Messages, westIds);

                var full = new List<(int Row, int Col, CellValueSet East, CellValueSet West, double Jaccard)>();
                foreach (var (key, eastSet) in east)
                {
                    if (!west.TryGetValue(key, out var westSet))
                    {
                        continue;
                    }

                    if (eastSet.Coverage == eastIds.Count && westSet.Coverage == westIds.Count)
                    {
                        var jaccard = ComputeJaccard(eastSet.Values, westSet.Values).Jaccard;
                        full.Add((key.Row, key.Col, eastSet, westSet, jaccard));
                    }
                }

                var failures = new List<string>();
                if (full.Count != 297)
                {
                    failures.Add($"full:{full.Count}");
                }

                var histogram = full
                    .GroupBy(cell => Math.Round(cell.Jaccard, 3))
                    .ToDictionary(g => g.Key, g => g.Count());

                var expectedHistogram = new Dictionary<double, int>
                {
                    [0.000] = 2,
                    [0.200] = 17,
                    [0.250] = 31,
                    [0.333] = 10,
                    [0.400] = 25,
                    [0.500] = 75,
                    [0.600] = 14,
                    [0.667] = 51,
                    [0.750] = 31,
                    [1.000] = 41,
                };

                foreach (var (key, expectedCount) in expectedHistogram)
                {
                    if (!histogram.TryGetValue(key, out var actualCount) || actualCount != expectedCount)
                    {
                        failures.Add($"hist:{key:0.000}:{actualCount}");
                    }
                }

                foreach (var key in histogram.Keys.Except(expectedHistogram.Keys))
                {
                    failures.Add($"histExtra:{key:0.000}");
                }

                var zeroCells = full
                    .Where(cell => Math.Abs(cell.Jaccard) < 1e-9)
                    .ToDictionary(
                        cell => (cell.Row, cell.Col),
                        cell => (East: cell.East.Values, West: cell.West.Values));

                var expectedZero = new Dictionary<(int Row, int Col), (int[] East, int[] West)>
                {
                    [(7, 1)] = (new[] { 2, 3 }, new[] { 1 }),
                    [(7, 17)] = (new[] { 0, 1 }, new[] { 2, 3 }),
                };

                if (zeroCells.Count != expectedZero.Count)
                {
                    failures.Add($"zeroCount:{zeroCells.Count}");
                }

                foreach (var (key, expected) in expectedZero)
                {
                    if (!zeroCells.TryGetValue(key, out var actual))
                    {
                        failures.Add($"zero:{key.Row},{key.Col}:missing");
                        continue;
                    }

                    if (!actual.East.SequenceEqual(expected.East) || !actual.West.SequenceEqual(expected.West))
                    {
                        failures.Add($"zero:{key.Row},{key.Col}:mismatch");
                    }
                }

                var jaccardOne = full.Count(cell => Math.Abs(cell.Jaccard - 1.0) < 1e-9);
                if (jaccardOne != 41)
                {
                    failures.Add($"jaccard1:{jaccardOne}");
                }

                var passed = failures.Count == 0;
                var summary = failures.Count == 0
                    ? "East/West per-cell overlap matches expected divergence profile."
                    : $"Mismatches: {string.Join(", ", failures)}.";

                return new HypothesisResult(passed, summary, new Dictionary<string, double>
                {
                    ["fullCoverageCells"] = full.Count,
                    ["jaccardZero"] = zeroCells.Count,
                    ["jaccardOne"] = jaccardOne,
                });
            }
        ),
        new(
            "Row-pair 8 column substitution (paired)",
            "Check if paired East/West row-pair 8 columns admit a consistent per-column substitution.",
            HypothesisExpectation.Pass,
            context =>
            {
                var sequences = new Dictionary<int, int[]>();
                foreach (var message in context.Messages)
                {
                    var seq = GetRowPairColumnSequence(message, rowPair: 8);
                    if (seq.Length > 0)
                    {
                        sequences[message.Id] = seq;
                    }
                }

                var pairs = new List<(int EastId, int WestId)>
                {
                    (0, 1),
                    (2, 3),
                    (4, 5),
                    (6, 7),
                };

                var stats = ComputeColumnSubstitutionStats(sequences, pairs, useAlignment: false);
                var passed = stats.PairCount == 2
                    && stats.AlignedPositions == 52
                    && stats.Mappings == 51
                    && stats.Conflicts == 1;
                var summary = $"pairs={stats.PairCount}, aligned={stats.AlignedPositions}, " +
                              $"mappings={stats.Mappings}, conflicts={stats.Conflicts}.";

                if (stats.ConflictDetails.Count != 1)
                {
                    passed = false;
                }
                else
                {
                    var detail = stats.ConflictDetails[0];
                    if (detail.Index != 11
                        || detail.EastValue != 5
                        || !detail.WestValues.SequenceEqual(new[] { 11, 21 }))
                    {
                        passed = false;
                    }
                }

                return new HypothesisResult(passed, summary, new Dictionary<string, double>
                {
                    ["pairs"] = stats.PairCount,
                    ["aligned"] = stats.AlignedPositions,
                    ["mappings"] = stats.Mappings,
                    ["conflicts"] = stats.Conflicts,
                });
            }
        ),
        new(
            "Row-pair 8 column substitution (paired, aligned)",
            "Check if aligned row-pair 8 columns reduce East/West substitution conflicts.",
            HypothesisExpectation.Pass,
            context =>
            {
                var sequences = new Dictionary<int, int[]>();
                foreach (var message in context.Messages)
                {
                    var seq = GetRowPairColumnSequence(message, rowPair: 8);
                    if (seq.Length > 0)
                    {
                        sequences[message.Id] = seq;
                    }
                }

                var pairs = new List<(int EastId, int WestId)>
                {
                    (0, 1),
                    (2, 3),
                    (4, 5),
                    (6, 7),
                };

                var stats = ComputeColumnSubstitutionStats(sequences, pairs, useAlignment: true);
                var passed = stats.PairCount == 2
                    && stats.AlignedPositions == 47
                    && stats.Mappings == 46
                    && stats.Conflicts == 1;
                var summary = $"pairs={stats.PairCount}, aligned={stats.AlignedPositions}, " +
                              $"mappings={stats.Mappings}, conflicts={stats.Conflicts}.";

                if (stats.ConflictDetails.Count != 1)
                {
                    passed = false;
                }
                else
                {
                    var detail = stats.ConflictDetails[0];
                    if (detail.Index != 13
                        || detail.EastValue != 5
                        || !detail.WestValues.SequenceEqual(new[] { 5, 11 }))
                    {
                        passed = false;
                    }
                }

                return new HypothesisResult(passed, summary, new Dictionary<string, double>
                {
                    ["pairs"] = stats.PairCount,
                    ["aligned"] = stats.AlignedPositions,
                    ["mappings"] = stats.Mappings,
                    ["conflicts"] = stats.Conflicts,
                });
            }
        ),
        new(
            "Row-pair 8 column substitution (paired, stateful prev)",
            "Check if adding previous East value resolves row-pair 8 substitution conflicts.",
            HypothesisExpectation.Pass,
            context =>
            {
                var sequences = new Dictionary<int, int[]>();
                foreach (var message in context.Messages)
                {
                    var seq = GetRowPairColumnSequence(message, rowPair: 8);
                    if (seq.Length > 0)
                    {
                        sequences[message.Id] = seq;
                    }
                }

                var pairs = new List<(int EastId, int WestId)>
                {
                    (0, 1),
                    (2, 3),
                    (4, 5),
                    (6, 7),
                };

                var stats = ComputeColumnSubstitutionStatsWithPrev(sequences, pairs, useAlignment: false);
                var passed = stats.PairCount == 2
                    && stats.AlignedPositions == 52
                    && stats.Mappings == 52
                    && stats.Conflicts == 0;
                var summary = $"pairs={stats.PairCount}, aligned={stats.AlignedPositions}, " +
                              $"mappings={stats.Mappings}, conflicts={stats.Conflicts}.";

                if (stats.ConflictDetails.Count != 0)
                {
                    passed = false;
                }

                return new HypothesisResult(passed, summary, new Dictionary<string, double>
                {
                    ["pairs"] = stats.PairCount,
                    ["aligned"] = stats.AlignedPositions,
                    ["mappings"] = stats.Mappings,
                    ["conflicts"] = stats.Conflicts,
                });
            }
        ),
        new(
            "Row-pair 8 column substitution (paired, stateful prevWest)",
            "Check if adding previous West value resolves row-pair 8 substitution conflicts.",
            HypothesisExpectation.Pass,
            context =>
            {
                var sequences = new Dictionary<int, int[]>();
                foreach (var message in context.Messages)
                {
                    var seq = GetRowPairColumnSequence(message, rowPair: 8);
                    if (seq.Length > 0)
                    {
                        sequences[message.Id] = seq;
                    }
                }

                var pairs = new List<(int EastId, int WestId)>
                {
                    (0, 1),
                    (2, 3),
                    (4, 5),
                    (6, 7),
                };

                var stats = ComputeColumnSubstitutionStatsWithPrevWest(sequences, pairs, useAlignment: false);
                var passed = stats.PairCount == 2
                    && stats.AlignedPositions == 52
                    && stats.Mappings == 52
                    && stats.Conflicts == 0;
                var summary = $"pairs={stats.PairCount}, aligned={stats.AlignedPositions}, " +
                              $"mappings={stats.Mappings}, conflicts={stats.Conflicts}.";

                if (stats.ConflictDetails.Count != 0)
                {
                    passed = false;
                }

                return new HypothesisResult(passed, summary, new Dictionary<string, double>
                {
                    ["pairs"] = stats.PairCount,
                    ["aligned"] = stats.AlignedPositions,
                    ["mappings"] = stats.Mappings,
                    ["conflicts"] = stats.Conflicts,
                });
            }
        ),
        new(
            "Row-pair 8 column substitution (paired, stateful prevEast+prevWest)",
            "Check if adding previous East+West values resolves row-pair 8 substitution conflicts.",
            HypothesisExpectation.Pass,
            context =>
            {
                var sequences = new Dictionary<int, int[]>();
                foreach (var message in context.Messages)
                {
                    var seq = GetRowPairColumnSequence(message, rowPair: 8);
                    if (seq.Length > 0)
                    {
                        sequences[message.Id] = seq;
                    }
                }

                var pairs = new List<(int EastId, int WestId)>
                {
                    (0, 1),
                    (2, 3),
                    (4, 5),
                    (6, 7),
                };

                var stats = ComputeColumnSubstitutionStatsWithPrevBoth(sequences, pairs, useAlignment: false);
                var passed = stats.PairCount == 2
                    && stats.AlignedPositions == 52
                    && stats.Mappings == 52
                    && stats.Conflicts == 0;
                var summary = $"pairs={stats.PairCount}, aligned={stats.AlignedPositions}, " +
                              $"mappings={stats.Mappings}, conflicts={stats.Conflicts}.";

                if (stats.ConflictDetails.Count != 0)
                {
                    passed = false;
                }

                return new HypothesisResult(passed, summary, new Dictionary<string, double>
                {
                    ["pairs"] = stats.PairCount,
                    ["aligned"] = stats.AlignedPositions,
                    ["mappings"] = stats.Mappings,
                    ["conflicts"] = stats.Conflicts,
                });
            }
        ),
        new(
            "Row-pair 8 column substitution (all pairs, stateful prevEast, aligned)",
            "Apply stateful prevEast mapping to all East/West pairs using approximate alignment.",
            HypothesisExpectation.Pass,
            context =>
            {
                var sequences = new Dictionary<int, int[]>();
                foreach (var message in context.Messages)
                {
                    var seq = GetRowPairColumnSequence(message, rowPair: 8);
                    if (seq.Length > 0)
                    {
                        sequences[message.Id] = seq;
                    }
                }

                var eastIds = new[] { 0, 2, 4, 6, 8 };
                var westIds = new[] { 1, 3, 5, 7 };
                var pairs = new List<(int EastId, int WestId)>();
                foreach (var east in eastIds)
                {
                    foreach (var west in westIds)
                    {
                        pairs.Add((east, west));
                    }
                }

                var stats = ComputeColumnSubstitutionStatsWithPrev(sequences, pairs, useAlignment: true);
                var passed = stats.PairCount == 8
                    && stats.AlignedPositions == 147
                    && stats.Mappings == 147
                    && stats.Conflicts == 0;
                var summary = $"pairs={stats.PairCount}, aligned={stats.AlignedPositions}, " +
                              $"mappings={stats.Mappings}, conflicts={stats.Conflicts}.";

                if (stats.ConflictDetails.Count != 0)
                {
                    passed = false;
                }

                return new HypothesisResult(passed, summary, new Dictionary<string, double>
                {
                    ["pairs"] = stats.PairCount,
                    ["aligned"] = stats.AlignedPositions,
                    ["mappings"] = stats.Mappings,
                    ["conflicts"] = stats.Conflicts,
                });
            }
        ),
        new(
            "Row-pair 8 stateful decode (all pairs, leave-one-out)",
            "Predict West row-pair 8 values from East using a prevEast stateful mapping (leave-one-out).",
            HypothesisExpectation.Pass,
            context =>
            {
                var sequences = new Dictionary<int, int[]>();
                foreach (var message in context.Messages)
                {
                    var seq = GetRowPairColumnSequence(message, rowPair: 8);
                    if (seq.Length > 0)
                    {
                        sequences[message.Id] = seq;
                    }
                }

                var eastIds = new[] { 0, 2, 4, 6, 8 };
                var westIds = new[] { 1, 3, 5, 7 };
                var pairs = new List<(int EastId, int WestId)>();
                foreach (var east in eastIds)
                {
                    foreach (var west in westIds)
                    {
                        pairs.Add((east, west));
                    }
                }

                var stats = EvaluateStatefulDecode(sequences, pairs, useAlignment: true);
                var passed = stats.PairCount == 8
                    && stats.AlignedPositions == 147
                    && stats.CoveredPositions == 26
                    && stats.Correct == 6;
                var summary = $"pairs={stats.PairCount}, aligned={stats.AlignedPositions}, " +
                              $"covered={stats.CoveredPositions}, correct={stats.Correct}.";

                return new HypothesisResult(passed, summary, new Dictionary<string, double>
                {
                    ["pairs"] = stats.PairCount,
                    ["aligned"] = stats.AlignedPositions,
                    ["covered"] = stats.CoveredPositions,
                    ["correct"] = stats.Correct,
                });
            }
        ),
        new(
            "Row-pair 8 stateful decode (motif filtered, leave-one-out)",
            "Predict West row-pair 8 values from East using prevEast mapping trained on same header motif.",
            HypothesisExpectation.Pass,
            context =>
            {
                var sequences = new Dictionary<int, int[]>();
                var motifs = new Dictionary<int, char>();
                foreach (var message in context.Messages)
                {
                    motifs[message.Id] = GetHeaderMotifType(message);
                    var seq = GetRowPairColumnSequence(message, rowPair: 8);
                    if (seq.Length > 0)
                    {
                        sequences[message.Id] = seq;
                    }
                }

                var eastIds = new[] { 0, 2, 4, 6, 8 };
                var westIds = new[] { 1, 3, 5, 7 };
                var pairs = new List<(int EastId, int WestId)>();
                foreach (var east in eastIds)
                {
                    foreach (var west in westIds)
                    {
                        pairs.Add((east, west));
                    }
                }

                var stats = EvaluateStatefulDecodeByMotif(sequences, pairs, motifs, useAlignment: true);
                var passed = stats.PairCount == 8
                    && stats.AlignedPositions == 147
                    && stats.CoveredPositions == 24
                    && stats.Correct == 6;
                var summary = $"pairs={stats.PairCount}, aligned={stats.AlignedPositions}, " +
                              $"covered={stats.CoveredPositions}, correct={stats.Correct}.";

                return new HypothesisResult(passed, summary, new Dictionary<string, double>
                {
                    ["pairs"] = stats.PairCount,
                    ["aligned"] = stats.AlignedPositions,
                    ["covered"] = stats.CoveredPositions,
                    ["correct"] = stats.Correct,
                });
            }
        ),
        new(
            "Row-pair 8 stateful decode (run position, leave-one-out)",
            "Predict West row-pair 8 values using prevEast mapping keyed by original column index.",
            HypothesisExpectation.Pass,
            context =>
            {
                var sequences = new Dictionary<int, int[]>();
                foreach (var message in context.Messages)
                {
                    var seq = GetRowPairColumnSequence(message, rowPair: 8);
                    if (seq.Length > 0)
                    {
                        sequences[message.Id] = seq;
                    }
                }

                var eastIds = new[] { 0, 2, 4, 6, 8 };
                var westIds = new[] { 1, 3, 5, 7 };
                var pairs = new List<(int EastId, int WestId)>();
                foreach (var east in eastIds)
                {
                    foreach (var west in westIds)
                    {
                        pairs.Add((east, west));
                    }
                }

                var stats = EvaluateStatefulDecodeWithRunPosition(sequences, pairs, useAlignment: true);
                var passed = stats.PairCount == 8
                    && stats.AlignedPositions == 147
                    && stats.CoveredPositions == 114
                    && stats.Correct == 24;
                var summary = $"pairs={stats.PairCount}, aligned={stats.AlignedPositions}, " +
                              $"covered={stats.CoveredPositions}, correct={stats.Correct}.";

                return new HypothesisResult(passed, summary, new Dictionary<string, double>
                {
                    ["pairs"] = stats.PairCount,
                    ["aligned"] = stats.AlignedPositions,
                    ["covered"] = stats.CoveredPositions,
                    ["correct"] = stats.Correct,
                });
            }
        ),
        new(
            "Row-pair 8 stateful decode (run position buckets)",
            "Bucketed accuracy for run-position decoding (index <10, <20, >=20).",
            HypothesisExpectation.Pass,
            context =>
            {
                var sequences = new Dictionary<int, int[]>();
                foreach (var message in context.Messages)
                {
                    var seq = GetRowPairColumnSequence(message, rowPair: 8);
                    if (seq.Length > 0)
                    {
                        sequences[message.Id] = seq;
                    }
                }

                var eastIds = new[] { 0, 2, 4, 6, 8 };
                var westIds = new[] { 1, 3, 5, 7 };
                var pairs = new List<(int EastId, int WestId)>();
                foreach (var east in eastIds)
                {
                    foreach (var west in westIds)
                    {
                        pairs.Add((east, west));
                    }
                }

                var stats = EvaluateStatefulDecodeWithRunPositionBuckets(sequences, pairs, useAlignment: true);
                var passed = stats.PairCount == 8
                    && stats.AlignedPositions == 147
                    && stats.CoveredPositions == 114
                    && stats.Correct == 24
                    && stats.Buckets[0].Aligned == 67
                    && stats.Buckets[0].Covered == 54
                    && stats.Buckets[0].Correct == 16
                    && stats.Buckets[1].Aligned == 64
                    && stats.Buckets[1].Covered == 58
                    && stats.Buckets[1].Correct == 8
                    && stats.Buckets[2].Aligned == 16
                    && stats.Buckets[2].Covered == 2
                    && stats.Buckets[2].Correct == 0;
                var summary = $"pairs={stats.PairCount}, aligned={stats.AlignedPositions}, " +
                              $"covered={stats.CoveredPositions}, correct={stats.Correct}; " +
                              $"b0={stats.Buckets[0].Covered}/{stats.Buckets[0].Aligned}/{stats.Buckets[0].Correct}, " +
                              $"b1={stats.Buckets[1].Covered}/{stats.Buckets[1].Aligned}/{stats.Buckets[1].Correct}, " +
                              $"b2={stats.Buckets[2].Covered}/{stats.Buckets[2].Aligned}/{stats.Buckets[2].Correct}.";

                return new HypothesisResult(passed, summary, new Dictionary<string, double>
                {
                    ["pairs"] = stats.PairCount,
                    ["aligned"] = stats.AlignedPositions,
                    ["covered"] = stats.CoveredPositions,
                    ["correct"] = stats.Correct,
                    ["b0Aligned"] = stats.Buckets[0].Aligned,
                    ["b0Covered"] = stats.Buckets[0].Covered,
                    ["b0Correct"] = stats.Buckets[0].Correct,
                    ["b1Aligned"] = stats.Buckets[1].Aligned,
                    ["b1Covered"] = stats.Buckets[1].Covered,
                    ["b1Correct"] = stats.Buckets[1].Correct,
                    ["b2Aligned"] = stats.Buckets[2].Aligned,
                    ["b2Covered"] = stats.Buckets[2].Covered,
                    ["b2Correct"] = stats.Buckets[2].Correct,
                });
            }
        ),
        new(
            "Row-pair 8 stateful decode (prev2, early columns)",
            "Second-order stateful decode limited to early columns (index < 10).",
            HypothesisExpectation.Pass,
            context =>
            {
                var sequences = new Dictionary<int, int[]>();
                foreach (var message in context.Messages)
                {
                    var seq = GetRowPairColumnSequence(message, rowPair: 8);
                    if (seq.Length > 0)
                    {
                        sequences[message.Id] = seq;
                    }
                }

                var eastIds = new[] { 0, 2, 4, 6, 8 };
                var westIds = new[] { 1, 3, 5, 7 };
                var pairs = new List<(int EastId, int WestId)>();
                foreach (var east in eastIds)
                {
                    foreach (var west in westIds)
                    {
                        pairs.Add((east, west));
                    }
                }

                var stats = EvaluateStatefulDecodePrev2Early(sequences, pairs, useAlignment: true, maxIndexExclusive: 10);
                var passed = stats.PairCount == 8
                    && stats.Aligned == 67
                    && stats.Covered == 54
                    && stats.Correct == 16;
                var summary = $"pairs={stats.PairCount}, aligned={stats.Aligned}, " +
                              $"covered={stats.Covered}, correct={stats.Correct}.";

                return new HypothesisResult(passed, summary, new Dictionary<string, double>
                {
                    ["pairs"] = stats.PairCount,
                    ["aligned"] = stats.Aligned,
                    ["covered"] = stats.Covered,
                    ["correct"] = stats.Correct,
                });
            }
        ),
        new(
            "Row-pair 8 early decode vs shuffled baseline",
            "Compare early-column decode accuracy to a shuffled West baseline.",
            HypothesisExpectation.Pass,
            context =>
            {
                var sequences = new Dictionary<int, int[]>();
                foreach (var message in context.Messages)
                {
                    var seq = GetRowPairColumnSequence(message, rowPair: 8);
                    if (seq.Length > 0)
                    {
                        sequences[message.Id] = seq;
                    }
                }

                var eastIds = new[] { 0, 2, 4, 6, 8 };
                var westIds = new[] { 1, 3, 5, 7 };
                var pairs = new List<(int EastId, int WestId)>();
                foreach (var east in eastIds)
                {
                    foreach (var west in westIds)
                    {
                        pairs.Add((east, west));
                    }
                }

                var stats = EvaluateStatefulDecodePrev1EarlyWithBaseline(
                    sequences,
                    pairs,
                    useAlignment: true,
                    maxIndexExclusive: 10,
                    samples: 200,
                    seed: 1337);
                var passed = stats.PairCount == 8
                    && stats.Aligned == 67
                    && stats.Covered == 54
                    && stats.Correct == 16
                    && Math.Abs(stats.BaselineMeanCorrect - 4.39) < 0.01;

                var summary = $"pairs={stats.PairCount}, aligned={stats.Aligned}, " +
                              $"covered={stats.Covered}, correct={stats.Correct}, " +
                              $"baselineMean={stats.BaselineMeanCorrect:0.###}.";

                return new HypothesisResult(passed, summary, new Dictionary<string, double>
                {
                    ["pairs"] = stats.PairCount,
                    ["aligned"] = stats.Aligned,
                    ["covered"] = stats.Covered,
                    ["correct"] = stats.Correct,
                    ["baselineMean"] = stats.BaselineMeanCorrect,
                });
            }
        ),
        new(
            "Row-pair column motifs (length 3/4)",
            "Column-ordered row-pair sequences in blocks 8/10 show no shared 3- or 4-grams across messages.",
            HypothesisExpectation.Pass,
            context =>
            {
                var expected8 = new Dictionary<int, int>
                {
                    [2] = 21,
                    [4] = 39,
                    [5] = 30,
                    [6] = 22,
                    [7] = 24,
                    [8] = 15,
                };

                var expected10 = new Dictionary<int, int>
                {
                    [4] = 10,
                };

                var failures = new List<string>();
                var sequences8 = new Dictionary<int, int[]>();
                var sequences10 = new Dictionary<int, int[]>();

                foreach (var message in context.Messages)
                {
                    var seq8 = GetRowPairColumnSequence(message, rowPair: 8);
                    var seq10 = GetRowPairColumnSequence(message, rowPair: 10);
                    if (seq8.Length > 0)
                    {
                        sequences8[message.Id] = seq8;
                    }

                    if (seq10.Length > 0)
                    {
                        sequences10[message.Id] = seq10;
                    }
                }

                foreach (var (id, length) in expected8)
                {
                    if (!sequences8.TryGetValue(id, out var seq) || seq.Length != length)
                    {
                        failures.Add($"row8:{id}");
                    }
                }

                foreach (var id in sequences8.Keys.Except(expected8.Keys))
                {
                    failures.Add($"row8extra:{id}");
                }

                foreach (var (id, length) in expected10)
                {
                    if (!sequences10.TryGetValue(id, out var seq) || seq.Length != length)
                    {
                        failures.Add($"row10:{id}");
                    }
                }

                foreach (var id in sequences10.Keys.Except(expected10.Keys))
                {
                    failures.Add($"row10extra:{id}");
                }

                var shared8Len3 = CountSharedMotifs(sequences8, 3);
                var shared8Len4 = CountSharedMotifs(sequences8, 4);
                var shared10Len3 = CountSharedMotifs(sequences10, 3);
                var shared10Len4 = CountSharedMotifs(sequences10, 4);

                if (shared8Len3 != 0) failures.Add($"row8len3:{shared8Len3}");
                if (shared8Len4 != 0) failures.Add($"row8len4:{shared8Len4}");
                if (shared10Len3 != 0) failures.Add($"row10len3:{shared10Len3}");
                if (shared10Len4 != 0) failures.Add($"row10len4:{shared10Len4}");

                var passed = failures.Count == 0;
                var summary = failures.Count == 0
                    ? "No shared 3/4-gram motifs in row-pair 8/10 column sequences."
                    : $"Mismatches: {string.Join(", ", failures)}.";

                return new HypothesisResult(passed, summary, new Dictionary<string, double>
                {
                    ["row8len3"] = shared8Len3,
                    ["row8len4"] = shared8Len4,
                    ["row10len3"] = shared10Len3,
                    ["row10len4"] = shared10Len4,
                });
            }
        ),
        new(
            "Row-pair column motifs (length 5/6)",
            "Column-ordered row-pair sequences in blocks 8/10 show no shared 5- or 6-grams across messages.",
            HypothesisExpectation.Pass,
            context =>
            {
                var sequences8 = new Dictionary<int, int[]>();
                var sequences10 = new Dictionary<int, int[]>();

                foreach (var message in context.Messages)
                {
                    var seq8 = GetRowPairColumnSequence(message, rowPair: 8);
                    var seq10 = GetRowPairColumnSequence(message, rowPair: 10);
                    if (seq8.Length > 0)
                    {
                        sequences8[message.Id] = seq8;
                    }

                    if (seq10.Length > 0)
                    {
                        sequences10[message.Id] = seq10;
                    }
                }

                var shared8Len5 = CountSharedMotifs(sequences8, 5);
                var shared8Len6 = CountSharedMotifs(sequences8, 6);
                var shared10Len5 = CountSharedMotifs(sequences10, 5);
                var shared10Len6 = CountSharedMotifs(sequences10, 6);

                var failures = new List<string>();
                if (shared8Len5 != 0) failures.Add($"row8len5:{shared8Len5}");
                if (shared8Len6 != 0) failures.Add($"row8len6:{shared8Len6}");
                if (shared10Len5 != 0) failures.Add($"row10len5:{shared10Len5}");
                if (shared10Len6 != 0) failures.Add($"row10len6:{shared10Len6}");

                var passed = failures.Count == 0;
                var summary = failures.Count == 0
                    ? "No shared 5/6-gram motifs in row-pair 8/10 column sequences."
                    : $"Mismatches: {string.Join(", ", failures)}.";

                return new HypothesisResult(passed, summary, new Dictionary<string, double>
                {
                    ["row8len5"] = shared8Len5,
                    ["row8len6"] = shared8Len6,
                    ["row10len5"] = shared10Len5,
                    ["row10len6"] = shared10Len6,
                });
            }
        ),
        new(
            "Row-pair column motifs (length 5/6, edit distance 1)",
            "Column-ordered row-pair sequences in blocks 8/10 show no shared near-matches (edit distance 1) across messages.",
            HypothesisExpectation.Pass,
            context =>
            {
                var sequences8 = new Dictionary<int, int[]>();
                var sequences10 = new Dictionary<int, int[]>();

                foreach (var message in context.Messages)
                {
                    var seq8 = GetRowPairColumnSequence(message, rowPair: 8);
                    var seq10 = GetRowPairColumnSequence(message, rowPair: 10);
                    if (seq8.Length > 0)
                    {
                        sequences8[message.Id] = seq8;
                    }

                    if (seq10.Length > 0)
                    {
                        sequences10[message.Id] = seq10;
                    }
                }

                var near8Len5 = CountNearSharedMotifs(sequences8, 5, maxDistance: 1);
                var near8Len6 = CountNearSharedMotifs(sequences8, 6, maxDistance: 1);
                var near10Len5 = CountNearSharedMotifs(sequences10, 5, maxDistance: 1);
                var near10Len6 = CountNearSharedMotifs(sequences10, 6, maxDistance: 1);

                var failures = new List<string>();
                if (near8Len5 != 0) failures.Add($"row8len5:{near8Len5}");
                if (near8Len6 != 0) failures.Add($"row8len6:{near8Len6}");
                if (near10Len5 != 0) failures.Add($"row10len5:{near10Len5}");
                if (near10Len6 != 0) failures.Add($"row10len6:{near10Len6}");

                var passed = failures.Count == 0;
                var summary = failures.Count == 0
                    ? "No shared 5/6-gram motifs within edit distance 1 in row-pair 8/10 column sequences."
                    : $"Mismatches: {string.Join(", ", failures)}.";

                return new HypothesisResult(passed, summary, new Dictionary<string, double>
                {
                    ["row8len5"] = near8Len5,
                    ["row8len6"] = near8Len6,
                    ["row10len5"] = near10Len5,
                    ["row10len6"] = near10Len6,
                });
            }
        ),
        new(
            "Row-pair column motifs (coarse top/bottom digits)",
            "Coarse-grain row-pair column values to top/bottom digits and measure shared motifs (length 3-6).",
            HypothesisExpectation.Pass,
            context =>
            {
                var sequences8 = new Dictionary<int, int[]>();
                var sequences10 = new Dictionary<int, int[]>();

                foreach (var message in context.Messages)
                {
                    var seq8 = GetRowPairColumnSequence(message, rowPair: 8);
                    var seq10 = GetRowPairColumnSequence(message, rowPair: 10);
                    if (seq8.Length > 0)
                    {
                        sequences8[message.Id] = seq8;
                    }

                    if (seq10.Length > 0)
                    {
                        sequences10[message.Id] = seq10;
                    }
                }

                var top8 = sequences8.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Select(v => v / 5).ToArray());
                var bottom8 = sequences8.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Select(v => v % 5).ToArray());
                var top10 = sequences10.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Select(v => v / 5).ToArray());
                var bottom10 = sequences10.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Select(v => v % 5).ToArray());

                var top8Len3 = CountSharedMotifs(top8, 3);
                var top8Len4 = CountSharedMotifs(top8, 4);
                var top8Len5 = CountSharedMotifs(top8, 5);
                var top8Len6 = CountSharedMotifs(top8, 6);
                var bottom8Len3 = CountSharedMotifs(bottom8, 3);
                var bottom8Len4 = CountSharedMotifs(bottom8, 4);
                var bottom8Len5 = CountSharedMotifs(bottom8, 5);
                var bottom8Len6 = CountSharedMotifs(bottom8, 6);

                var top10Len3 = CountSharedMotifs(top10, 3);
                var top10Len4 = CountSharedMotifs(top10, 4);
                var top10Len5 = CountSharedMotifs(top10, 5);
                var top10Len6 = CountSharedMotifs(top10, 6);
                var bottom10Len3 = CountSharedMotifs(bottom10, 3);
                var bottom10Len4 = CountSharedMotifs(bottom10, 4);
                var bottom10Len5 = CountSharedMotifs(bottom10, 5);
                var bottom10Len6 = CountSharedMotifs(bottom10, 6);

                var passed = top8Len3 == 36
                             && top8Len4 == 11
                             && top8Len5 == 0
                             && top8Len6 == 0
                             && bottom8Len3 == 33
                             && bottom8Len4 == 11
                             && bottom8Len5 == 0
                             && bottom8Len6 == 0
                             && top10Len3 == 0
                             && top10Len4 == 0
                             && top10Len5 == 0
                             && top10Len6 == 0
                             && bottom10Len3 == 0
                             && bottom10Len4 == 0
                             && bottom10Len5 == 0
                             && bottom10Len6 == 0;

                var summary = $"top8(3/4/5/6)={top8Len3}/{top8Len4}/{top8Len5}/{top8Len6}, " +
                              $"bottom8(3/4/5/6)={bottom8Len3}/{bottom8Len4}/{bottom8Len5}/{bottom8Len6}, " +
                              $"top10(3/4/5/6)={top10Len3}/{top10Len4}/{top10Len5}/{top10Len6}, " +
                              $"bottom10(3/4/5/6)={bottom10Len3}/{bottom10Len4}/{bottom10Len5}/{bottom10Len6}.";

                return new HypothesisResult(passed, summary, new Dictionary<string, double>
                {
                    ["top8len3"] = top8Len3,
                    ["top8len4"] = top8Len4,
                    ["top8len5"] = top8Len5,
                    ["top8len6"] = top8Len6,
                    ["bottom8len3"] = bottom8Len3,
                    ["bottom8len4"] = bottom8Len4,
                    ["bottom8len5"] = bottom8Len5,
                    ["bottom8len6"] = bottom8Len6,
                    ["top10len3"] = top10Len3,
                    ["top10len4"] = top10Len4,
                    ["top10len5"] = top10Len5,
                    ["top10len6"] = top10Len6,
                    ["bottom10len3"] = bottom10Len3,
                    ["bottom10len4"] = bottom10Len4,
                    ["bottom10len5"] = bottom10Len5,
                    ["bottom10len6"] = bottom10Len6,
                });
            }
        ),
        new(
            "Row-pair column entropy profile",
            "Column entropy profile for row-pair 8 (and row-pair 10 singletons).",
            HypothesisExpectation.Pass,
            context =>
            {
                var sequences8 = new Dictionary<int, int[]>();
                var sequences10 = new Dictionary<int, int[]>();
                foreach (var message in context.Messages)
                {
                    var seq8 = GetRowPairColumnSequence(message, rowPair: 8);
                    var seq10 = GetRowPairColumnSequence(message, rowPair: 10);
                    if (seq8.Length > 0)
                    {
                        sequences8[message.Id] = seq8;
                    }

                    if (seq10.Length > 0)
                    {
                        sequences10[message.Id] = seq10;
                    }
                }

                var stats8 = ComputeColumnStats(sequences8);
                var stats10 = ComputeColumnStats(sequences10);

                var expected8 = new Dictionary<int, ColumnStat>
                {
                    [0] = new ColumnStat(6, 6, 2.585, 0.167),
                    [1] = new ColumnStat(6, 5, 2.252, 0.333),
                    [2] = new ColumnStat(6, 6, 2.585, 0.167),
                    [3] = new ColumnStat(6, 5, 2.252, 0.333),
                    [4] = new ColumnStat(6, 5, 2.252, 0.333),
                    [5] = new ColumnStat(6, 4, 1.792, 0.5),
                    [6] = new ColumnStat(6, 6, 2.585, 0.167),
                    [7] = new ColumnStat(6, 4, 1.792, 0.5),
                    [8] = new ColumnStat(6, 6, 2.585, 0.167),
                    [9] = new ColumnStat(6, 4, 1.918, 0.333),
                    [10] = new ColumnStat(6, 6, 2.585, 0.167),
                    [11] = new ColumnStat(6, 3, 1.459, 0.5),
                    [12] = new ColumnStat(6, 6, 2.585, 0.167),
                    [13] = new ColumnStat(6, 6, 2.585, 0.167),
                    [14] = new ColumnStat(6, 6, 2.585, 0.167),
                    [15] = new ColumnStat(5, 4, 1.922, 0.4),
                    [16] = new ColumnStat(5, 5, 2.322, 0.2),
                    [17] = new ColumnStat(5, 5, 2.322, 0.2),
                    [18] = new ColumnStat(5, 4, 1.922, 0.4),
                    [19] = new ColumnStat(5, 5, 2.322, 0.2),
                    [20] = new ColumnStat(5, 5, 2.322, 0.2),
                    [21] = new ColumnStat(4, 4, 2.0, 0.25),
                    [22] = new ColumnStat(3, 3, 1.585, 0.333),
                    [23] = new ColumnStat(3, 3, 1.585, 0.333),
                    [24] = new ColumnStat(2, 1, 0.0, 1.0),
                    [25] = new ColumnStat(2, 2, 1.0, 0.5),
                    [26] = new ColumnStat(2, 2, 1.0, 0.5),
                    [27] = new ColumnStat(2, 2, 1.0, 0.5),
                    [28] = new ColumnStat(2, 2, 1.0, 0.5),
                    [29] = new ColumnStat(2, 2, 1.0, 0.5),
                    [30] = new ColumnStat(1, 1, 0.0, 1.0),
                    [31] = new ColumnStat(1, 1, 0.0, 1.0),
                    [32] = new ColumnStat(1, 1, 0.0, 1.0),
                    [33] = new ColumnStat(1, 1, 0.0, 1.0),
                    [34] = new ColumnStat(1, 1, 0.0, 1.0),
                    [35] = new ColumnStat(1, 1, 0.0, 1.0),
                    [36] = new ColumnStat(1, 1, 0.0, 1.0),
                    [37] = new ColumnStat(1, 1, 0.0, 1.0),
                    [38] = new ColumnStat(1, 1, 0.0, 1.0),
                };

                var expected10 = new Dictionary<int, ColumnStat>
                {
                    [0] = new ColumnStat(1, 1, 0.0, 1.0),
                    [1] = new ColumnStat(1, 1, 0.0, 1.0),
                    [2] = new ColumnStat(1, 1, 0.0, 1.0),
                    [3] = new ColumnStat(1, 1, 0.0, 1.0),
                    [4] = new ColumnStat(1, 1, 0.0, 1.0),
                    [5] = new ColumnStat(1, 1, 0.0, 1.0),
                    [6] = new ColumnStat(1, 1, 0.0, 1.0),
                    [7] = new ColumnStat(1, 1, 0.0, 1.0),
                    [8] = new ColumnStat(1, 1, 0.0, 1.0),
                    [9] = new ColumnStat(1, 1, 0.0, 1.0),
                };

                var failures = new List<string>();
                ValidateColumnStats(stats8, expected8, "row8", failures);
                ValidateColumnStats(stats10, expected10, "row10", failures);

                var passed = failures.Count == 0;
                var summary = failures.Count == 0
                    ? "Column entropy profiles match."
                    : $"Mismatches: {string.Join(", ", failures)}.";

                return new HypothesisResult(passed, summary);
            }
        ),
        new(
            "Row-pair column restriction sets",
            "Column-level value sets for row-pair 8 (columns 0..14) and row-pair 10.",
            HypothesisExpectation.Pass,
            context =>
            {
                var sequences8 = new Dictionary<int, int[]>();
                var sequences10 = new Dictionary<int, int[]>();
                foreach (var message in context.Messages)
                {
                    var seq8 = GetRowPairColumnSequence(message, rowPair: 8);
                    var seq10 = GetRowPairColumnSequence(message, rowPair: 10);
                    if (seq8.Length > 0)
                    {
                        sequences8[message.Id] = seq8;
                    }

                    if (seq10.Length > 0)
                    {
                        sequences10[message.Id] = seq10;
                    }
                }

                var sets8 = ComputeColumnValueSets(sequences8, minCoverage: 6);
                var sets10 = ComputeColumnValueSets(sequences10, minCoverage: 1);

                var expected8 = new Dictionary<int, ColumnValueSet>
                {
                    [0] = new ColumnValueSet(6, new[] { 1, 4, 5, 7, 8, 10 }),
                    [1] = new ColumnValueSet(5, new[] { 1, 10, 12, 14, 18 }),
                    [2] = new ColumnValueSet(6, new[] { 2, 5, 7, 12, 20, 21 }),
                    [3] = new ColumnValueSet(5, new[] { 1, 2, 11, 14, 17 }),
                    [4] = new ColumnValueSet(5, new[] { 0, 8, 12, 14, 16 }),
                    [5] = new ColumnValueSet(4, new[] { 0, 7, 21, 22 }),
                    [6] = new ColumnValueSet(6, new[] { 0, 1, 5, 6, 10, 11 }),
                    [7] = new ColumnValueSet(4, new[] { 5, 10, 17, 22 }),
                    [8] = new ColumnValueSet(6, new[] { 1, 2, 10, 17, 20, 23 }),
                    [9] = new ColumnValueSet(4, new[] { 0, 5, 11, 13 }),
                    [10] = new ColumnValueSet(6, new[] { 1, 2, 10, 13, 19, 23 }),
                    [11] = new ColumnValueSet(3, new[] { 5, 11, 21 }),
                    [12] = new ColumnValueSet(6, new[] { 4, 5, 6, 11, 14, 15 }),
                    [13] = new ColumnValueSet(6, new[] { 0, 4, 8, 10, 13, 23 }),
                    [14] = new ColumnValueSet(6, new[] { 1, 5, 6, 7, 17, 22 }),
                };

                var expected10 = new Dictionary<int, ColumnValueSet>
                {
                    [0] = new ColumnValueSet(1, new[] { 0 }),
                    [1] = new ColumnValueSet(1, new[] { 24 }),
                    [2] = new ColumnValueSet(1, new[] { 7 }),
                    [3] = new ColumnValueSet(1, new[] { 13 }),
                    [4] = new ColumnValueSet(1, new[] { 20 }),
                    [5] = new ColumnValueSet(1, new[] { 1 }),
                    [6] = new ColumnValueSet(1, new[] { 5 }),
                    [7] = new ColumnValueSet(1, new[] { 11 }),
                    [8] = new ColumnValueSet(1, new[] { 15 }),
                    [9] = new ColumnValueSet(1, new[] { 4 }),
                };

                var failures = new List<string>();
                ValidateColumnValueSets(sets8, expected8, "row8", failures);
                ValidateColumnValueSets(sets10, expected10, "row10", failures);

                var intersection8 = expected8.Values
                    .Select(v => v.Values.ToHashSet())
                    .Aggregate(new HashSet<int>(Enumerable.Range(0, 25)), (acc, set) =>
                    {
                        acc.IntersectWith(set);
                        return acc;
                    });

                if (intersection8.Count != 0)
                {
                    failures.Add("row8:intersection");
                }

                var passed = failures.Count == 0;
                var summary = failures.Count == 0
                    ? "Column value sets match; row-pair 8 intersection is empty."
                    : $"Mismatches: {string.Join(", ", failures)}.";

                return new HypothesisResult(passed, summary);
            }
        ),
        new(
            "Row-pair 8 column sets by group",
            "Compare row-pair 8 column value sets by East/West and motif groups.",
            HypothesisExpectation.Pass,
            context =>
            {
                var eastIds = new HashSet<int> { 0, 2, 4, 6, 8 };
                var westIds = new HashSet<int> { 1, 3, 5, 7 };

                var eastSequences = new Dictionary<int, int[]>();
                var westSequences = new Dictionary<int, int[]>();
                var motifBSequences = new Dictionary<int, int[]>();
                var motifCSequences = new Dictionary<int, int[]>();

                foreach (var message in context.Messages)
                {
                    var seq = GetRowPairColumnSequence(message, rowPair: 8);
                    if (seq.Length == 0)
                    {
                        continue;
                    }

                    if (eastIds.Contains(message.Id))
                    {
                        eastSequences[message.Id] = seq;
                    }

                    if (westIds.Contains(message.Id))
                    {
                        westSequences[message.Id] = seq;
                    }

                    var motif = GetHeaderMotifType(message);
                    if (motif == 'B')
                    {
                        motifBSequences[message.Id] = seq;
                    }
                    else if (motif == 'C')
                    {
                        motifCSequences[message.Id] = seq;
                    }
                }

                var eastSets = ComputeColumnValueSets(eastSequences, minCoverage: 4);
                var westSets = ComputeColumnValueSets(westSequences, minCoverage: 2);
                var motifBSets = ComputeColumnValueSets(motifBSequences, minCoverage: 5);
                var motifCSets = ComputeColumnValueSets(motifCSequences, minCoverage: 1);

                var expectedEast = new Dictionary<int, ColumnValueSet>
                {
                    [0] = new ColumnValueSet(4, new[] { 1, 4, 7, 8 }),
                    [1] = new ColumnValueSet(3, new[] { 1, 14, 18 }),
                    [2] = new ColumnValueSet(4, new[] { 5, 7, 12, 21 }),
                    [3] = new ColumnValueSet(3, new[] { 1, 2, 14 }),
                    [4] = new ColumnValueSet(4, new[] { 0, 12, 14, 16 }),
                    [5] = new ColumnValueSet(4, new[] { 0, 7, 21, 22 }),
                    [6] = new ColumnValueSet(4, new[] { 0, 6, 10, 11 }),
                    [7] = new ColumnValueSet(3, new[] { 10, 17, 22 }),
                    [8] = new ColumnValueSet(4, new[] { 1, 2, 17, 23 }),
                    [9] = new ColumnValueSet(2, new[] { 5, 13 }),
                    [10] = new ColumnValueSet(4, new[] { 1, 2, 10, 13 }),
                    [11] = new ColumnValueSet(2, new[] { 5, 11 }),
                    [12] = new ColumnValueSet(4, new[] { 4, 6, 14, 15 }),
                    [13] = new ColumnValueSet(4, new[] { 4, 8, 13, 23 }),
                    [14] = new ColumnValueSet(4, new[] { 1, 6, 7, 22 }),
                };

                var expectedWest = new Dictionary<int, ColumnValueSet>
                {
                    [0] = new ColumnValueSet(2, new[] { 5, 10 }),
                    [1] = new ColumnValueSet(2, new[] { 10, 12 }),
                    [2] = new ColumnValueSet(2, new[] { 2, 20 }),
                    [3] = new ColumnValueSet(2, new[] { 11, 17 }),
                    [4] = new ColumnValueSet(2, new[] { 8, 12 }),
                    [5] = new ColumnValueSet(1, new[] { 21 }),
                    [6] = new ColumnValueSet(2, new[] { 1, 5 }),
                    [7] = new ColumnValueSet(2, new[] { 5, 10 }),
                    [8] = new ColumnValueSet(2, new[] { 10, 20 }),
                    [9] = new ColumnValueSet(2, new[] { 0, 11 }),
                    [10] = new ColumnValueSet(2, new[] { 19, 23 }),
                    [11] = new ColumnValueSet(2, new[] { 11, 21 }),
                    [12] = new ColumnValueSet(2, new[] { 5, 11 }),
                    [13] = new ColumnValueSet(2, new[] { 0, 10 }),
                    [14] = new ColumnValueSet(2, new[] { 5, 17 }),
                    [15] = new ColumnValueSet(2, new[] { 2, 6 }),
                    [16] = new ColumnValueSet(2, new[] { 5, 19 }),
                    [17] = new ColumnValueSet(2, new[] { 17, 22 }),
                    [18] = new ColumnValueSet(1, new[] { 6 }),
                    [19] = new ColumnValueSet(2, new[] { 10, 24 }),
                    [20] = new ColumnValueSet(2, new[] { 0, 3 }),
                    [21] = new ColumnValueSet(2, new[] { 5, 14 }),
                    [22] = new ColumnValueSet(2, new[] { 16, 20 }),
                    [23] = new ColumnValueSet(2, new[] { 1, 22 }),
                };

                var expectedMotifB = new Dictionary<int, ColumnValueSet>
                {
                    [0] = new ColumnValueSet(5, new[] { 4, 5, 7, 8, 10 }),
                    [1] = new ColumnValueSet(5, new[] { 1, 10, 12, 14, 18 }),
                    [2] = new ColumnValueSet(5, new[] { 2, 5, 7, 12, 20 }),
                    [3] = new ColumnValueSet(5, new[] { 1, 2, 11, 14, 17 }),
                    [4] = new ColumnValueSet(4, new[] { 8, 12, 14, 16 }),
                    [5] = new ColumnValueSet(3, new[] { 0, 21, 22 }),
                    [6] = new ColumnValueSet(5, new[] { 1, 5, 6, 10, 11 }),
                    [7] = new ColumnValueSet(3, new[] { 5, 10, 17 }),
                    [8] = new ColumnValueSet(5, new[] { 1, 2, 10, 20, 23 }),
                    [9] = new ColumnValueSet(4, new[] { 0, 5, 11, 13 }),
                    [10] = new ColumnValueSet(5, new[] { 1, 10, 13, 19, 23 }),
                    [11] = new ColumnValueSet(3, new[] { 5, 11, 21 }),
                    [12] = new ColumnValueSet(5, new[] { 4, 5, 11, 14, 15 }),
                    [13] = new ColumnValueSet(5, new[] { 0, 8, 10, 13, 23 }),
                    [14] = new ColumnValueSet(5, new[] { 5, 6, 7, 17, 22 }),
                };

                var expectedMotifC = new Dictionary<int, ColumnValueSet>
                {
                    [0] = new ColumnValueSet(1, new[] { 1 }),
                    [1] = new ColumnValueSet(1, new[] { 18 }),
                    [2] = new ColumnValueSet(1, new[] { 21 }),
                    [3] = new ColumnValueSet(1, new[] { 14 }),
                    [4] = new ColumnValueSet(1, new[] { 0 }),
                    [5] = new ColumnValueSet(1, new[] { 7 }),
                    [6] = new ColumnValueSet(1, new[] { 0 }),
                    [7] = new ColumnValueSet(1, new[] { 22 }),
                    [8] = new ColumnValueSet(1, new[] { 17 }),
                    [9] = new ColumnValueSet(1, new[] { 5 }),
                    [10] = new ColumnValueSet(1, new[] { 2 }),
                    [11] = new ColumnValueSet(1, new[] { 5 }),
                    [12] = new ColumnValueSet(1, new[] { 6 }),
                    [13] = new ColumnValueSet(1, new[] { 4 }),
                    [14] = new ColumnValueSet(1, new[] { 1 }),
                    [15] = new ColumnValueSet(1, new[] { 13 }),
                    [16] = new ColumnValueSet(1, new[] { 2 }),
                    [17] = new ColumnValueSet(1, new[] { 2 }),
                    [18] = new ColumnValueSet(1, new[] { 8 }),
                    [19] = new ColumnValueSet(1, new[] { 11 }),
                    [20] = new ColumnValueSet(1, new[] { 21 }),
                };

                var failures = new List<string>();
                ValidateColumnValueSets(eastSets, expectedEast, "east", failures);
                ValidateColumnValueSets(westSets, expectedWest, "west", failures);
                ValidateColumnValueSets(motifBSets, expectedMotifB, "motifB", failures);
                ValidateColumnValueSets(motifCSets, expectedMotifC, "motifC", failures);

                var passed = failures.Count == 0;
                var summary = failures.Count == 0
                    ? "Column sets match for East/West and motif groups."
                    : $"Mismatches: {string.Join(", ", failures)}.";

                return new HypothesisResult(passed, summary);
            }
        ),
        new(
            "Row-pair 8 East/West Jaccard overlap",
            "Jaccard overlap between East vs West column sets for row-pair 8 (columns 0..14).",
            HypothesisExpectation.Pass,
            context =>
            {
                var eastIds = new HashSet<int> { 0, 2, 4, 6, 8 };
                var westIds = new HashSet<int> { 1, 3, 5, 7 };

                var eastSequences = new Dictionary<int, int[]>();
                var westSequences = new Dictionary<int, int[]>();

                foreach (var message in context.Messages)
                {
                    var seq = GetRowPairColumnSequence(message, rowPair: 8);
                    if (seq.Length == 0)
                    {
                        continue;
                    }

                    if (eastIds.Contains(message.Id))
                    {
                        eastSequences[message.Id] = seq;
                    }
                    else if (westIds.Contains(message.Id))
                    {
                        westSequences[message.Id] = seq;
                    }
                }

                var eastSets = ComputeColumnValueSets(eastSequences, minCoverage: 4);
                var westSets = ComputeColumnValueSets(westSequences, minCoverage: 2);

                var expected = new Dictionary<int, double>
                {
                    [0] = 0.0,
                    [1] = 0.0,
                    [2] = 0.0,
                    [3] = 0.0,
                    [4] = 0.2,
                    [5] = 0.25,
                    [6] = 0.0,
                    [7] = 0.25,
                    [8] = 0.0,
                    [9] = 0.0,
                    [10] = 0.0,
                    [11] = 0.333,
                    [12] = 0.0,
                    [13] = 0.0,
                    [14] = 0.0,
                };

                var failures = new List<string>();
                foreach (var (col, expectedValue) in expected)
                {
                    if (!eastSets.TryGetValue(col, out var east) || !westSets.TryGetValue(col, out var west))
                    {
                        failures.Add($"{col}:missing");
                        continue;
                    }

                    var eastSet = east.Values.ToHashSet();
                    var westSet = west.Values.ToHashSet();
                    var inter = eastSet.Intersect(westSet).Count();
                    var union = eastSet.Union(westSet).Count();
                    var j = union == 0 ? 0 : Math.Round(inter / (double)union, 3);
                    if (Math.Abs(j - expectedValue) > 0.001)
                    {
                        failures.Add($"{col}:{j}");
                    }
                }

                var passed = failures.Count == 0;
                var summary = failures.Count == 0
                    ? "Jaccard overlaps match."
                    : $"Mismatches: {string.Join(", ", failures)}.";

                return new HypothesisResult(passed, summary);
            }
        ),
        new(
            "Row-pair 8 East/West coarse-binned overlap",
            "Compare East vs West column bin sets after coarse frequency binning (low/med/high).",
            HypothesisExpectation.Pass,
            context =>
            {
                var eastIds = new HashSet<int> { 0, 2, 4, 6, 8 };
                var westIds = new HashSet<int> { 1, 3, 5, 7 };

                var eastSequences = new Dictionary<int, int[]>();
                var westSequences = new Dictionary<int, int[]>();
                var allSequences = new List<int[]>();

                foreach (var message in context.Messages)
                {
                    var seq = GetRowPairColumnSequence(message, rowPair: 8);
                    if (seq.Length == 0)
                    {
                        continue;
                    }

                    allSequences.Add(seq);
                    if (eastIds.Contains(message.Id))
                    {
                        eastSequences[message.Id] = seq;
                    }
                    else if (westIds.Contains(message.Id))
                    {
                        westSequences[message.Id] = seq;
                    }
                }

                var binMap = BuildFrequencyBins(allSequences, highCount: 8, midCount: 8);
                var binnedEast = eastSequences.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.Select(v => binMap[v]).ToArray());
                var binnedWest = westSequences.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.Select(v => binMap[v]).ToArray());

                var eastSets = ComputeColumnValueSets(binnedEast, minCoverage: 4);
                var westSets = ComputeColumnValueSets(binnedWest, minCoverage: 2);

                var rawEastSets = ComputeColumnValueSets(eastSequences, minCoverage: 4);
                var rawWestSets = ComputeColumnValueSets(westSequences, minCoverage: 2);

                var jaccards = new SortedDictionary<int, double>();
                foreach (var col in eastSets.Keys.Intersect(westSets.Keys).OrderBy(c => c))
                {
                    var eastSet = eastSets[col].Values;
                    var westSet = westSets[col].Values;
                    var j = ComputeJaccard(eastSet, westSet).Jaccard;
                    jaccards[col] = Math.Round(j, 3);
                }

                var rawJaccards = new List<double>();
                foreach (var col in rawEastSets.Keys.Intersect(rawWestSets.Keys))
                {
                    var eastSet = rawEastSets[col].Values;
                    var westSet = rawWestSets[col].Values;
                    var j = ComputeJaccard(eastSet, westSet).Jaccard;
                    rawJaccards.Add(j);
                }

                var binAvg = jaccards.Count == 0 ? 0 : Math.Round(jaccards.Values.Average(), 3);
                var rawAvg = rawJaccards.Count == 0 ? 0 : Math.Round(rawJaccards.Average(), 3);

                var low = binMap.Where(kvp => kvp.Value == 0).Select(kvp => kvp.Key).OrderBy(v => v).ToArray();
                var mid = binMap.Where(kvp => kvp.Value == 1).Select(kvp => kvp.Key).OrderBy(v => v).ToArray();
                var high = binMap.Where(kvp => kvp.Value == 2).Select(kvp => kvp.Key).OrderBy(v => v).ToArray();

                var expectedJaccard = new Dictionary<int, double>
                {
                    [0] = 0.333,
                    [1] = 0.333,
                    [2] = 0.667,
                    [3] = 0.0,
                    [4] = 0.333,
                    [5] = 0.333,
                    [6] = 0.5,
                    [7] = 0.5,
                    [8] = 0.667,
                    [9] = 1.0,
                    [10] = 0.0,
                    [11] = 0.5,
                    [12] = 1.0,
                    [13] = 0.333,
                    [14] = 0.667,
                };

                var expectedLow = new[] { 3, 7, 9, 16, 18, 19, 20, 23, 24 };
                var expectedMid = new[] { 8, 11, 12, 13, 15, 17, 21, 22 };
                var expectedHigh = new[] { 0, 1, 2, 4, 5, 6, 10, 14 };

                var failures = new List<string>();
                foreach (var (col, expectedValue) in expectedJaccard)
                {
                    if (!jaccards.TryGetValue(col, out var observed) || Math.Abs(observed - expectedValue) > 0.001)
                    {
                        failures.Add($"{col}:{observed:0.###}");
                    }
                }

                if (Math.Abs(binAvg - 0.478) > 0.001) failures.Add($"binAvg:{binAvg:0.###}");
                if (Math.Abs(rawAvg - 0.069) > 0.001) failures.Add($"rawAvg:{rawAvg:0.###}");
                if (!low.SequenceEqual(expectedLow)) failures.Add("lowBins");
                if (!mid.SequenceEqual(expectedMid)) failures.Add("midBins");
                if (!high.SequenceEqual(expectedHigh)) failures.Add("highBins");

                var passed = failures.Count == 0;
                var summary = failures.Count == 0
                    ? "Binned overlaps match."
                    : $"Mismatches: {string.Join(", ", failures)}.";

                return new HypothesisResult(passed, summary, new Dictionary<string, double>
                {
                    ["binAvg"] = binAvg,
                    ["rawAvg"] = rawAvg,
                });
            }
        ),
        new(
            "Row-pair 8 East/West column-binned overlap",
            "Compare East vs West column bin sets after per-column frequency binning (low/med/high).",
            HypothesisExpectation.Pass,
            context =>
            {
                var eastIds = new HashSet<int> { 0, 2, 4, 6, 8 };
                var westIds = new HashSet<int> { 1, 3, 5, 7 };

                var eastSequences = new Dictionary<int, int[]>();
                var westSequences = new Dictionary<int, int[]>();
                var allSequences = new List<int[]>();

                foreach (var message in context.Messages)
                {
                    var seq = GetRowPairColumnSequence(message, rowPair: 8);
                    if (seq.Length == 0)
                    {
                        continue;
                    }

                    allSequences.Add(seq);
                    if (eastIds.Contains(message.Id))
                    {
                        eastSequences[message.Id] = seq;
                    }
                    else if (westIds.Contains(message.Id))
                    {
                        westSequences[message.Id] = seq;
                    }
                }

                var columnBins = BuildColumnFrequencyBins(allSequences, highCount: 2, midCount: 2);

                var binnedEast = eastSequences.ToDictionary(
                    kvp => kvp.Key,
                    kvp => ApplyColumnBins(kvp.Value, columnBins));
                var binnedWest = westSequences.ToDictionary(
                    kvp => kvp.Key,
                    kvp => ApplyColumnBins(kvp.Value, columnBins));

                var eastSets = ComputeColumnValueSets(binnedEast, minCoverage: 4);
                var westSets = ComputeColumnValueSets(binnedWest, minCoverage: 2);

                var rawEastSets = ComputeColumnValueSets(eastSequences, minCoverage: 4);
                var rawWestSets = ComputeColumnValueSets(westSequences, minCoverage: 2);

                var jaccards = new SortedDictionary<int, double>();
                foreach (var col in eastSets.Keys.Intersect(westSets.Keys).OrderBy(c => c))
                {
                    var eastSet = eastSets[col].Values;
                    var westSet = westSets[col].Values;
                    var j = ComputeJaccard(eastSet, westSet).Jaccard;
                    jaccards[col] = Math.Round(j, 3);
                }

                var rawJaccards = new List<double>();
                foreach (var col in rawEastSets.Keys.Intersect(rawWestSets.Keys))
                {
                    var eastSet = rawEastSets[col].Values;
                    var westSet = rawWestSets[col].Values;
                    var j = ComputeJaccard(eastSet, westSet).Jaccard;
                    rawJaccards.Add(j);
                }

                var binAvg = jaccards.Count == 0 ? 0 : Math.Round(jaccards.Values.Average(), 3);
                var rawAvg = rawJaccards.Count == 0 ? 0 : Math.Round(rawJaccards.Average(), 3);

                var full = jaccards.Where(kvp => Math.Abs(kvp.Value - 1.0) < 1e-9).Select(kvp => kvp.Key).ToArray();
                var disjoint = jaccards.Where(kvp => Math.Abs(kvp.Value) < 1e-9).Select(kvp => kvp.Key).ToArray();

                var expectedJaccard = new Dictionary<int, double>
                {
                    [0] = 0.667,
                    [1] = 0.0,
                    [2] = 0.667,
                    [3] = 0.333,
                    [4] = 0.667,
                    [5] = 0.5,
                    [6] = 0.667,
                    [7] = 0.5,
                    [8] = 0.667,
                    [9] = 0.0,
                    [10] = 0.0,
                    [11] = 0.5,
                    [12] = 0.667,
                    [13] = 0.667,
                    [14] = 0.667,
                };

                var expectedDisjoint = new[] { 1, 9, 10 };

                var failures = new List<string>();
                foreach (var (col, expectedValue) in expectedJaccard)
                {
                    if (!jaccards.TryGetValue(col, out var observed) || Math.Abs(observed - expectedValue) > 0.001)
                    {
                        failures.Add($"{col}:{observed:0.###}");
                    }
                }

                if (Math.Abs(binAvg - 0.478) > 0.001) failures.Add($"binAvg:{binAvg:0.###}");
                if (Math.Abs(rawAvg - 0.069) > 0.001) failures.Add($"rawAvg:{rawAvg:0.###}");
                if (full.Length != 0) failures.Add($"full:{string.Join(",", full)}");
                if (!disjoint.SequenceEqual(expectedDisjoint)) failures.Add("disjoint");

                var passed = failures.Count == 0;
                var summary = failures.Count == 0
                    ? "Column-binned overlaps match."
                    : $"Mismatches: {string.Join(", ", failures)}.";

                return new HypothesisResult(passed, summary, new Dictionary<string, double>
                {
                    ["binAvg"] = binAvg,
                    ["rawAvg"] = rawAvg,
                });
            }
        ),
        new(
            "Row-pair 8 East/West bin mode alignment",
            "Compare per-column dominant bin (mode) between East and West after per-column binning.",
            HypothesisExpectation.Pass,
            context =>
            {
                var eastIds = new HashSet<int> { 0, 2, 4, 6, 8 };
                var westIds = new HashSet<int> { 1, 3, 5, 7 };

                var eastSequences = new Dictionary<int, int[]>();
                var westSequences = new Dictionary<int, int[]>();
                var allSequences = new List<int[]>();

                foreach (var message in context.Messages)
                {
                    var seq = GetRowPairColumnSequence(message, rowPair: 8);
                    if (seq.Length == 0)
                    {
                        continue;
                    }

                    allSequences.Add(seq);
                    if (eastIds.Contains(message.Id))
                    {
                        eastSequences[message.Id] = seq;
                    }
                    else if (westIds.Contains(message.Id))
                    {
                        westSequences[message.Id] = seq;
                    }
                }

                var columnBins = BuildColumnFrequencyBins(allSequences, highCount: 2, midCount: 2);
                var binnedEast = eastSequences.ToDictionary(
                    kvp => kvp.Key,
                    kvp => ApplyColumnBins(kvp.Value, columnBins));
                var binnedWest = westSequences.ToDictionary(
                    kvp => kvp.Key,
                    kvp => ApplyColumnBins(kvp.Value, columnBins));

                var eastCounts = ComputeColumnValueCounts(binnedEast);
                var westCounts = ComputeColumnValueCounts(binnedWest);

                var matched = new List<int>();
                var mismatched = new List<int>();
                var details = new List<string>();
                foreach (var col in eastCounts.Keys.Intersect(westCounts.Keys).OrderBy(c => c))
                {
                    var eastTotal = eastCounts[col].Values.Sum();
                    var westTotal = westCounts[col].Values.Sum();
                    if (eastTotal < 4 || westTotal < 2)
                    {
                        continue;
                    }

                    var eastMode = GetModeBin(eastCounts[col]);
                    var westMode = GetModeBin(westCounts[col]);
                    details.Add($"{col}:{eastMode}-{westMode}");
                    if (eastMode == westMode)
                    {
                        matched.Add(col);
                    }
                    else
                    {
                        mismatched.Add(col);
                    }
                }

                var total = matched.Count + mismatched.Count;
                var matchRate = total == 0 ? 0 : Math.Round(matched.Count / (double)total, 3);

                var expectedMatched = new[] { 4, 5, 7, 11 };
                var expectedMismatched = new[] { 0, 1, 2, 3, 6, 8, 9, 10, 12, 13, 14 };
                var expectedModes = new Dictionary<int, (int East, int West)>
                {
                    [0] = (2, 1),
                    [1] = (2, 1),
                    [2] = (1, 2),
                    [3] = (2, 1),
                    [4] = (2, 2),
                    [5] = (2, 2),
                    [6] = (0, 2),
                    [7] = (2, 2),
                    [8] = (2, 1),
                    [9] = (2, 1),
                    [10] = (2, 0),
                    [11] = (2, 2),
                    [12] = (0, 2),
                    [13] = (0, 2),
                    [14] = (1, 2),
                };

                var failures = new List<string>();
                if (total != 15) failures.Add($"total:{total}");
                if (Math.Abs(matchRate - 0.267) > 0.001) failures.Add($"matchRate:{matchRate:0.###}");
                if (!matched.SequenceEqual(expectedMatched)) failures.Add("matched");
                if (!mismatched.SequenceEqual(expectedMismatched)) failures.Add("mismatched");

                foreach (var (col, expected) in expectedModes)
                {
                    var observed = details.FirstOrDefault(d => d.StartsWith($"{col}:"));
                    if (string.IsNullOrWhiteSpace(observed))
                    {
                        failures.Add($"{col}:missing");
                        continue;
                    }

                    var parts = observed.Split(':', '-');
                    if (parts.Length != 3
                        || !int.TryParse(parts[1], out var east)
                        || !int.TryParse(parts[2], out var west)
                        || east != expected.East
                        || west != expected.West)
                    {
                        failures.Add($"{col}:{observed}");
                    }
                }

                var passed = failures.Count == 0;
                var summary = failures.Count == 0
                    ? $"matches={matched.Count}/{total} ({matchRate:0.###}), " +
                      $"matched=[{string.Join(",", matched)}], mismatched=[{string.Join(",", mismatched)}]; " +
                      $"modes={string.Join(", ", details)}."
                    : $"Mismatches: {string.Join(", ", failures)}.";

                return new HypothesisResult(passed, summary, new Dictionary<string, double>
                {
                    ["matchRate"] = matchRate,
                    ["matchCount"] = matched.Count,
                    ["total"] = total,
                });
            }
        ),
        new(
            "Row-pair 8 East/West coarse bin sweep",
            "Sweep coarse global bin sizes and measure East/West overlap (row-pair 8 columns 0..14).",
            HypothesisExpectation.Pass,
            context =>
            {
                var eastIds = new HashSet<int> { 0, 2, 4, 6, 8 };
                var westIds = new HashSet<int> { 1, 3, 5, 7 };

                var eastSequences = new Dictionary<int, int[]>();
                var westSequences = new Dictionary<int, int[]>();
                var allSequences = new List<int[]>();

                foreach (var message in context.Messages)
                {
                    var seq = GetRowPairColumnSequence(message, rowPair: 8);
                    if (seq.Length == 0)
                    {
                        continue;
                    }

                    allSequences.Add(seq);
                    if (eastIds.Contains(message.Id))
                    {
                        eastSequences[message.Id] = seq;
                    }
                    else if (westIds.Contains(message.Id))
                    {
                        westSequences[message.Id] = seq;
                    }
                }

                var rawEastSets = ComputeColumnValueSets(eastSequences, minCoverage: 4);
                var rawWestSets = ComputeColumnValueSets(westSequences, minCoverage: 2);
                var rawJaccards = new List<double>();
                foreach (var col in rawEastSets.Keys.Intersect(rawWestSets.Keys))
                {
                    rawJaccards.Add(ComputeJaccard(rawEastSets[col].Values, rawWestSets[col].Values).Jaccard);
                }

                var rawAvg = rawJaccards.Count == 0 ? 0 : Math.Round(rawJaccards.Average(), 3);

                var configs = new List<(int High, int Mid)>
                {
                    (3, 3),
                    (4, 4),
                    (5, 5),
                };

                var results = new List<string>();
                var metrics = new Dictionary<string, double>
                {
                    ["rawAvg"] = rawAvg,
                };

                foreach (var (high, mid) in configs)
                {
                    var binMap = BuildFrequencyBins(allSequences, high, mid);
                    var binnedEast = eastSequences.ToDictionary(
                        kvp => kvp.Key,
                        kvp => kvp.Value.Select(v => binMap[v]).ToArray());
                    var binnedWest = westSequences.ToDictionary(
                        kvp => kvp.Key,
                        kvp => kvp.Value.Select(v => binMap[v]).ToArray());

                    var eastSets = ComputeColumnValueSets(binnedEast, minCoverage: 4);
                    var westSets = ComputeColumnValueSets(binnedWest, minCoverage: 2);

                    var jaccards = new List<double>();
                    foreach (var col in eastSets.Keys.Intersect(westSets.Keys))
                    {
                        jaccards.Add(ComputeJaccard(eastSets[col].Values, westSets[col].Values).Jaccard);
                    }

                    var avg = jaccards.Count == 0 ? 0 : Math.Round(jaccards.Average(), 3);
                    results.Add($"{high}/{mid}:{avg:0.###}");
                    metrics[$"h{high}m{mid}"] = avg;
                }

                var expected = new Dictionary<string, double>
                {
                    ["rawAvg"] = 0.069,
                    ["h3m3"] = 0.533,
                    ["h4m4"] = 0.456,
                    ["h5m5"] = 0.511,
                };

                var failures = new List<string>();
                foreach (var (key, expectedValue) in expected)
                {
                    if (!metrics.TryGetValue(key, out var observed) || Math.Abs(observed - expectedValue) > 0.001)
                    {
                        failures.Add($"{key}:{observed:0.###}");
                    }
                }

                var passed = failures.Count == 0;
                var summary = failures.Count == 0
                    ? $"rawAvg={rawAvg:0.###}; bins={string.Join(", ", results)}."
                    : $"Mismatches: {string.Join(", ", failures)}.";
                return new HypothesisResult(passed, summary, metrics);
            }
        ),
        new(
            "Row-pair 8 East/West bin top-2 alignment",
            "Compare East/West top-2 bin sets per column after per-column binning.",
            HypothesisExpectation.Pass,
            context =>
            {
                var eastIds = new HashSet<int> { 0, 2, 4, 6, 8 };
                var westIds = new HashSet<int> { 1, 3, 5, 7 };

                var eastSequences = new Dictionary<int, int[]>();
                var westSequences = new Dictionary<int, int[]>();
                var allSequences = new List<int[]>();

                foreach (var message in context.Messages)
                {
                    var seq = GetRowPairColumnSequence(message, rowPair: 8);
                    if (seq.Length == 0)
                    {
                        continue;
                    }

                    allSequences.Add(seq);
                    if (eastIds.Contains(message.Id))
                    {
                        eastSequences[message.Id] = seq;
                    }
                    else if (westIds.Contains(message.Id))
                    {
                        westSequences[message.Id] = seq;
                    }
                }

                var columnBins = BuildColumnFrequencyBins(allSequences, highCount: 2, midCount: 2);
                var binnedEast = eastSequences.ToDictionary(
                    kvp => kvp.Key,
                    kvp => ApplyColumnBins(kvp.Value, columnBins));
                var binnedWest = westSequences.ToDictionary(
                    kvp => kvp.Key,
                    kvp => ApplyColumnBins(kvp.Value, columnBins));

                var eastCounts = ComputeColumnValueCounts(binnedEast);
                var westCounts = ComputeColumnValueCounts(binnedWest);

                var matched = new List<int>();
                var disjoint = new List<int>();
                var details = new List<string>();
                var jaccards = new List<double>();

                foreach (var col in eastCounts.Keys.Intersect(westCounts.Keys).OrderBy(c => c))
                {
                    var eastTotal = eastCounts[col].Values.Sum();
                    var westTotal = westCounts[col].Values.Sum();
                    if (eastTotal < 4 || westTotal < 2)
                    {
                        continue;
                    }

                    var eastTop = GetTopBins(eastCounts[col], 2);
                    var westTop = GetTopBins(westCounts[col], 2);

                    var inter = eastTop.Intersect(westTop).Count();
                    var union = eastTop.Union(westTop).Count();
                    var j = union == 0 ? 0 : Math.Round(inter / (double)union, 3);
                    jaccards.Add(j);

                    if (Math.Abs(j - 1.0) < 1e-9)
                    {
                        matched.Add(col);
                    }
                    else if (Math.Abs(j) < 1e-9)
                    {
                        disjoint.Add(col);
                    }

                    details.Add($"{col}:{string.Join("/", eastTop)}-{string.Join("/", westTop)}");
                }

                var avg = jaccards.Count == 0 ? 0 : Math.Round(jaccards.Average(), 3);
                var expectedMatched = new[] { 4, 5, 7, 9, 11 };
                var expectedDisjoint = Array.Empty<int>();
                var expectedTop2 = new Dictionary<int, string>
                {
                    [0] = "2/1-1/0",
                    [1] = "2/0-1/2",
                    [2] = "1/2-2/0",
                    [3] = "2/1-1/0",
                    [4] = "2/1-2/1",
                    [5] = "2/1-2/1",
                    [6] = "0/2-2/1",
                    [7] = "2/1-2/1",
                    [8] = "2/1-1/0",
                    [9] = "2/1-1/2",
                    [10] = "2/1-0/2",
                    [11] = "2/1-2/1",
                    [12] = "0/2-2/1",
                    [13] = "0/2-2/1",
                    [14] = "1/2-2/0",
                };

                var failures = new List<string>();
                if (Math.Abs(avg - 0.555) > 0.001) failures.Add($"avg:{avg:0.###}");
                if (!matched.SequenceEqual(expectedMatched)) failures.Add("matched");
                if (!disjoint.SequenceEqual(expectedDisjoint)) failures.Add("disjoint");

                foreach (var (col, expected) in expectedTop2)
                {
                    var entry = details.FirstOrDefault(d => d.StartsWith($"{col}:"));
                    if (string.IsNullOrWhiteSpace(entry) || !entry.EndsWith(expected, StringComparison.Ordinal))
                    {
                        failures.Add($"{col}:{entry}");
                    }
                }

                var passed = failures.Count == 0;
                var summary = failures.Count == 0
                    ? $"avg={avg:0.###}, matched=[{string.Join(",", matched)}], " +
                      $"disjoint=[{string.Join(",", disjoint)}]; top2={string.Join(", ", details)}."
                    : $"Mismatches: {string.Join(", ", failures)}.";

                return new HypothesisResult(passed, summary, new Dictionary<string, double>
                {
                    ["avg"] = avg,
                    ["matched"] = matched.Count,
                    ["disjoint"] = disjoint.Count,
                });
            }
        ),
        new(
            "Row-pair 8 column entropy vs overlap",
            "Correlate row-pair 8 column entropy with East/West overlap (raw and binned).",
            HypothesisExpectation.Pass,
            context =>
            {
                var eastIds = new HashSet<int> { 0, 2, 4, 6, 8 };
                var westIds = new HashSet<int> { 1, 3, 5, 7 };

                var allSequences = new Dictionary<int, int[]>();
                var eastSequences = new Dictionary<int, int[]>();
                var westSequences = new Dictionary<int, int[]>();

                foreach (var message in context.Messages)
                {
                    var seq = GetRowPairColumnSequence(message, rowPair: 8);
                    if (seq.Length == 0)
                    {
                        continue;
                    }

                    allSequences[message.Id] = seq;
                    if (eastIds.Contains(message.Id))
                    {
                        eastSequences[message.Id] = seq;
                    }
                    else if (westIds.Contains(message.Id))
                    {
                        westSequences[message.Id] = seq;
                    }
                }

                var entropyStats = ComputeColumnStats(allSequences);
                var rawEastSets = ComputeColumnValueSets(eastSequences, minCoverage: 4);
                var rawWestSets = ComputeColumnValueSets(westSequences, minCoverage: 2);

                var binMap = BuildFrequencyBins(allSequences.Values, highCount: 8, midCount: 8);
                var binnedEast = eastSequences.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.Select(v => binMap[v]).ToArray());
                var binnedWest = westSequences.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.Select(v => binMap[v]).ToArray());

                var binnedEastSets = ComputeColumnValueSets(binnedEast, minCoverage: 4);
                var binnedWestSets = ComputeColumnValueSets(binnedWest, minCoverage: 2);

                var entropies = new List<double>();
                var rawOverlaps = new List<double>();
                var binnedOverlaps = new List<double>();

                foreach (var col in entropyStats.Keys.OrderBy(c => c))
                {
                    if (!rawEastSets.TryGetValue(col, out var rawEast) || !rawWestSets.TryGetValue(col, out var rawWest))
                    {
                        continue;
                    }

                    if (!binnedEastSets.TryGetValue(col, out var binnedEastSet) || !binnedWestSets.TryGetValue(col, out var binnedWestSet))
                    {
                        continue;
                    }

                    entropies.Add(entropyStats[col].Entropy);
                    rawOverlaps.Add(ComputeJaccard(rawEast.Values, rawWest.Values).Jaccard);
                    binnedOverlaps.Add(ComputeJaccard(binnedEastSet.Values, binnedWestSet.Values).Jaccard);
                }

                var corrRaw = entropies.Count == 0 ? 0 : Math.Round(Correlation(entropies, rawOverlaps), 3);
                var corrBinned = entropies.Count == 0 ? 0 : Math.Round(Correlation(entropies, binnedOverlaps), 3);

                var failures = new List<string>();
                if (Math.Abs(corrRaw - -0.833) > 0.001) failures.Add($"corrRaw:{corrRaw:0.###}");
                if (Math.Abs(corrBinned - -0.013) > 0.001) failures.Add($"corrBinned:{corrBinned:0.###}");
                if (entropies.Count != 15) failures.Add($"columns:{entropies.Count}");

                var passed = failures.Count == 0;
                var summary = failures.Count == 0
                    ? $"corrRaw={corrRaw:0.###}, corrBinned={corrBinned:0.###}, columns={entropies.Count}."
                    : $"Mismatches: {string.Join(", ", failures)}.";

                return new HypothesisResult(passed, summary, new Dictionary<string, double>
                {
                    ["corrRaw"] = corrRaw,
                    ["corrBinned"] = corrBinned,
                    ["columns"] = entropies.Count,
                });
            }
        ),
        new(
            "Row-pair 8 early vs late overlap",
            "Compare early (0-9) vs late (10-14) column overlap for raw and binned values.",
            HypothesisExpectation.Pass,
            context =>
            {
                var eastIds = new HashSet<int> { 0, 2, 4, 6, 8 };
                var westIds = new HashSet<int> { 1, 3, 5, 7 };

                var eastSequences = new Dictionary<int, int[]>();
                var westSequences = new Dictionary<int, int[]>();
                var allSequences = new List<int[]>();

                foreach (var message in context.Messages)
                {
                    var seq = GetRowPairColumnSequence(message, rowPair: 8);
                    if (seq.Length == 0)
                    {
                        continue;
                    }

                    allSequences.Add(seq);
                    if (eastIds.Contains(message.Id))
                    {
                        eastSequences[message.Id] = seq;
                    }
                    else if (westIds.Contains(message.Id))
                    {
                        westSequences[message.Id] = seq;
                    }
                }

                var rawEastSets = ComputeColumnValueSets(eastSequences, minCoverage: 4);
                var rawWestSets = ComputeColumnValueSets(westSequences, minCoverage: 2);

                var binMap = BuildFrequencyBins(allSequences, highCount: 8, midCount: 8);
                var binnedEast = eastSequences.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.Select(v => binMap[v]).ToArray());
                var binnedWest = westSequences.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.Select(v => binMap[v]).ToArray());
                var binnedEastSets = ComputeColumnValueSets(binnedEast, minCoverage: 4);
                var binnedWestSets = ComputeColumnValueSets(binnedWest, minCoverage: 2);

                var earlyRaw = new List<double>();
                var lateRaw = new List<double>();
                var earlyBinned = new List<double>();
                var lateBinned = new List<double>();

                for (var col = 0; col <= 14; col++)
                {
                    if (rawEastSets.TryGetValue(col, out var rawEast) && rawWestSets.TryGetValue(col, out var rawWest))
                    {
                        var j = ComputeJaccard(rawEast.Values, rawWest.Values).Jaccard;
                        if (col <= 9) earlyRaw.Add(j); else lateRaw.Add(j);
                    }

                    if (binnedEastSets.TryGetValue(col, out var binnedEastSet) && binnedWestSets.TryGetValue(col, out var binnedWestSet))
                    {
                        var j = ComputeJaccard(binnedEastSet.Values, binnedWestSet.Values).Jaccard;
                        if (col <= 9) earlyBinned.Add(j); else lateBinned.Add(j);
                    }
                }

                var earlyRawAvg = earlyRaw.Count == 0 ? 0 : Math.Round(earlyRaw.Average(), 3);
                var lateRawAvg = lateRaw.Count == 0 ? 0 : Math.Round(lateRaw.Average(), 3);
                var earlyBinnedAvg = earlyBinned.Count == 0 ? 0 : Math.Round(earlyBinned.Average(), 3);
                var lateBinnedAvg = lateBinned.Count == 0 ? 0 : Math.Round(lateBinned.Average(), 3);

                var failures = new List<string>();
                if (Math.Abs(earlyRawAvg - 0.07) > 0.001) failures.Add($"rawEarly:{earlyRawAvg:0.###}");
                if (Math.Abs(lateRawAvg - 0.067) > 0.001) failures.Add($"rawLate:{lateRawAvg:0.###}");
                if (Math.Abs(earlyBinnedAvg - 0.467) > 0.001) failures.Add($"binEarly:{earlyBinnedAvg:0.###}");
                if (Math.Abs(lateBinnedAvg - 0.5) > 0.001) failures.Add($"binLate:{lateBinnedAvg:0.###}");

                var passed = failures.Count == 0;
                var summary = failures.Count == 0
                    ? $"rawEarly={earlyRawAvg:0.###}, rawLate={lateRawAvg:0.###}; " +
                      $"binEarly={earlyBinnedAvg:0.###}, binLate={lateBinnedAvg:0.###}."
                    : $"Mismatches: {string.Join(", ", failures)}.";

                return new HypothesisResult(passed, summary, new Dictionary<string, double>
                {
                    ["rawEarly"] = earlyRawAvg,
                    ["rawLate"] = lateRawAvg,
                    ["binEarly"] = earlyBinnedAvg,
                    ["binLate"] = lateBinnedAvg,
                });
            }
        ),
        new(
            "Row-pair 8 stateful decode (prev2 run position buckets)",
            "Bucketed accuracy using prev2 state with run position key.",
            HypothesisExpectation.Pass,
            context =>
            {
                var sequences = new Dictionary<int, int[]>();
                foreach (var message in context.Messages)
                {
                    sequences[message.Id] = GetRowPairColumnSequence(message, rowPair: 8);
                }

                var eastIds = new[] { 0, 2, 4, 6, 8 };
                var westIds = new[] { 1, 3, 5, 7 };
                var pairs = new List<(int EastId, int WestId)>();
                foreach (var east in eastIds)
                {
                    foreach (var west in westIds)
                    {
                        pairs.Add((east, west));
                    }
                }

                var stats = EvaluateStatefulDecodeWithRunPositionBucketsPrev2(sequences, pairs, useAlignment: true);
                var failures = new List<string>();
                if (stats.PairCount != 8) failures.Add($"pairs:{stats.PairCount}");
                if (stats.AlignedPositions != 147) failures.Add($"aligned:{stats.AlignedPositions}");
                if (stats.CoveredPositions != 114) failures.Add($"covered:{stats.CoveredPositions}");
                if (stats.Correct != 24) failures.Add($"correct:{stats.Correct}");
                if (stats.Buckets[0].Aligned != 67 || stats.Buckets[0].Covered != 54 || stats.Buckets[0].Correct != 16) failures.Add("b0");
                if (stats.Buckets[1].Aligned != 64 || stats.Buckets[1].Covered != 58 || stats.Buckets[1].Correct != 8) failures.Add("b1");
                if (stats.Buckets[2].Aligned != 16 || stats.Buckets[2].Covered != 2 || stats.Buckets[2].Correct != 0) failures.Add("b2");

                var passed = failures.Count == 0;
                var summary = failures.Count == 0
                    ? $"pairs={stats.PairCount}, aligned={stats.AlignedPositions}, covered={stats.CoveredPositions}, " +
                      $"correct={stats.Correct}; " +
                      $"b0={stats.Buckets[0].Aligned}/{stats.Buckets[0].Covered}/{stats.Buckets[0].Correct}, " +
                      $"b1={stats.Buckets[1].Aligned}/{stats.Buckets[1].Covered}/{stats.Buckets[1].Correct}, " +
                      $"b2={stats.Buckets[2].Aligned}/{stats.Buckets[2].Covered}/{stats.Buckets[2].Correct}."
                    : $"Mismatches: {string.Join(", ", failures)}.";

                return new HypothesisResult(passed, summary, new Dictionary<string, double>
                {
                    ["pairs"] = stats.PairCount,
                    ["aligned"] = stats.AlignedPositions,
                    ["covered"] = stats.CoveredPositions,
                    ["correct"] = stats.Correct,
                });
            }
        ),
        new(
            "Row-pair 8 stateful decode (prev3 run position buckets)",
            "Bucketed accuracy using prev3 state with run position key.",
            HypothesisExpectation.Pass,
            context =>
            {
                var sequences = new Dictionary<int, int[]>();
                foreach (var message in context.Messages)
                {
                    sequences[message.Id] = GetRowPairColumnSequence(message, rowPair: 8);
                }

                var eastIds = new[] { 0, 2, 4, 6, 8 };
                var westIds = new[] { 1, 3, 5, 7 };
                var pairs = new List<(int EastId, int WestId)>();
                foreach (var east in eastIds)
                {
                    foreach (var west in westIds)
                    {
                        pairs.Add((east, west));
                    }
                }

                var stats = EvaluateStatefulDecodeWithRunPositionBucketsPrev3(sequences, pairs, useAlignment: true);
                var failures = new List<string>();
                if (stats.PairCount != 8) failures.Add($"pairs:{stats.PairCount}");
                if (stats.AlignedPositions != 147) failures.Add($"aligned:{stats.AlignedPositions}");
                if (stats.CoveredPositions != 114) failures.Add($"covered:{stats.CoveredPositions}");
                if (stats.Correct != 24) failures.Add($"correct:{stats.Correct}");
                if (stats.Buckets[0].Aligned != 67 || stats.Buckets[0].Covered != 54 || stats.Buckets[0].Correct != 16) failures.Add("b0");
                if (stats.Buckets[1].Aligned != 64 || stats.Buckets[1].Covered != 58 || stats.Buckets[1].Correct != 8) failures.Add("b1");
                if (stats.Buckets[2].Aligned != 16 || stats.Buckets[2].Covered != 2 || stats.Buckets[2].Correct != 0) failures.Add("b2");

                var passed = failures.Count == 0;
                var summary = failures.Count == 0
                    ? $"pairs={stats.PairCount}, aligned={stats.AlignedPositions}, covered={stats.CoveredPositions}, " +
                      $"correct={stats.Correct}; " +
                      $"b0={stats.Buckets[0].Aligned}/{stats.Buckets[0].Covered}/{stats.Buckets[0].Correct}, " +
                      $"b1={stats.Buckets[1].Aligned}/{stats.Buckets[1].Covered}/{stats.Buckets[1].Correct}, " +
                      $"b2={stats.Buckets[2].Aligned}/{stats.Buckets[2].Covered}/{stats.Buckets[2].Correct}."
                    : $"Mismatches: {string.Join(", ", failures)}.";

                return new HypothesisResult(passed, summary, new Dictionary<string, double>
                {
                    ["pairs"] = stats.PairCount,
                    ["aligned"] = stats.AlignedPositions,
                    ["covered"] = stats.CoveredPositions,
                    ["correct"] = stats.Correct,
                });
            }
        ),
        new(
            "Row-pair 8 stateful decode (bucket key)",
            "Use a bucketed index key (0-9, 10-19, 20+) with prevEast state.",
            HypothesisExpectation.Pass,
            context =>
            {
                var sequences = new Dictionary<int, int[]>();
                foreach (var message in context.Messages)
                {
                    sequences[message.Id] = GetRowPairColumnSequence(message, rowPair: 8);
                }

                var eastIds = new[] { 0, 2, 4, 6, 8 };
                var westIds = new[] { 1, 3, 5, 7 };
                var pairs = new List<(int EastId, int WestId)>();
                foreach (var east in eastIds)
                {
                    foreach (var west in westIds)
                    {
                        pairs.Add((east, west));
                    }
                }

                var stats = EvaluateStatefulDecodeWithBucketKey(sequences, pairs, useAlignment: true);
                var failures = new List<string>();
                if (stats.PairCount != 8) failures.Add($"pairs:{stats.PairCount}");
                if (stats.AlignedPositions != 147) failures.Add($"aligned:{stats.AlignedPositions}");
                if (stats.CoveredPositions != 114) failures.Add($"covered:{stats.CoveredPositions}");
                if (stats.Correct != 26) failures.Add($"correct:{stats.Correct}");
                if (stats.Buckets[0].Aligned != 67 || stats.Buckets[0].Covered != 54 || stats.Buckets[0].Correct != 16) failures.Add("b0");
                if (stats.Buckets[1].Aligned != 64 || stats.Buckets[1].Covered != 58 || stats.Buckets[1].Correct != 10) failures.Add("b1");
                if (stats.Buckets[2].Aligned != 16 || stats.Buckets[2].Covered != 2 || stats.Buckets[2].Correct != 0) failures.Add("b2");

                var passed = failures.Count == 0;
                var summary = failures.Count == 0
                    ? $"pairs={stats.PairCount}, aligned={stats.AlignedPositions}, covered={stats.CoveredPositions}, " +
                      $"correct={stats.Correct}; " +
                      $"b0={stats.Buckets[0].Aligned}/{stats.Buckets[0].Covered}/{stats.Buckets[0].Correct}, " +
                      $"b1={stats.Buckets[1].Aligned}/{stats.Buckets[1].Covered}/{stats.Buckets[1].Correct}, " +
                      $"b2={stats.Buckets[2].Aligned}/{stats.Buckets[2].Covered}/{stats.Buckets[2].Correct}."
                    : $"Mismatches: {string.Join(", ", failures)}.";

                return new HypothesisResult(passed, summary, new Dictionary<string, double>
                {
                    ["pairs"] = stats.PairCount,
                    ["aligned"] = stats.AlignedPositions,
                    ["covered"] = stats.CoveredPositions,
                    ["correct"] = stats.Correct,
                });
            }
        ),
        new(
            "Row-pair 8 stateful decode (run position constrained)",
            "Apply column value-set constraints to prevEast run-position decoding.",
            HypothesisExpectation.Pass,
            context =>
            {
                var sequences = new Dictionary<int, int[]>();
                foreach (var message in context.Messages)
                {
                    sequences[message.Id] = GetRowPairColumnSequence(message, rowPair: 8);
                }

                var eastIds = new[] { 0, 2, 4, 6, 8 };
                var westIds = new[] { 1, 3, 5, 7 };
                var pairs = new List<(int EastId, int WestId)>();
                foreach (var east in eastIds)
                {
                    foreach (var west in westIds)
                    {
                        pairs.Add((east, west));
                    }
                }

                var stats = EvaluateStatefulDecodeWithRunPositionConstrained(sequences, pairs, useAlignment: true);
                var failures = new List<string>();
                if (stats.PairCount != 8) failures.Add($"pairs:{stats.PairCount}");
                if (stats.AlignedPositions != 147) failures.Add($"aligned:{stats.AlignedPositions}");
                if (stats.CoveredPositions != 114) failures.Add($"covered:{stats.CoveredPositions}");
                if (stats.Correct != 24) failures.Add($"correct:{stats.Correct}");

                var passed = failures.Count == 0;
                var summary = failures.Count == 0
                    ? $"pairs={stats.PairCount}, aligned={stats.AlignedPositions}, " +
                      $"covered={stats.CoveredPositions}, correct={stats.Correct}."
                    : $"Mismatches: {string.Join(", ", failures)}.";

                return new HypothesisResult(passed, summary, new Dictionary<string, double>
                {
                    ["pairs"] = stats.PairCount,
                    ["aligned"] = stats.AlignedPositions,
                    ["covered"] = stats.CoveredPositions,
                    ["correct"] = stats.Correct,
                });
            }
        ),
        new(
            "Row-pair 8 stateful decode (offset alignment)",
            "Use best-offset alignment instead of Needleman–Wunsch for run-position decoding.",
            HypothesisExpectation.Pass,
            context =>
            {
                var sequences = new Dictionary<int, int[]>();
                foreach (var message in context.Messages)
                {
                    sequences[message.Id] = GetRowPairColumnSequence(message, rowPair: 8);
                }

                var eastIds = new[] { 0, 2, 4, 6, 8 };
                var westIds = new[] { 1, 3, 5, 7 };
                var pairs = new List<(int EastId, int WestId)>();
                foreach (var east in eastIds)
                {
                    foreach (var west in westIds)
                    {
                        pairs.Add((east, west));
                    }
                }

                var stats = EvaluateStatefulDecodeWithRunPositionOffset(sequences, pairs, maxOffset: 5);
                var failures = new List<string>();
                if (stats.PairCount != 8) failures.Add($"pairs:{stats.PairCount}");
                if (stats.AlignedPositions != 162) failures.Add($"aligned:{stats.AlignedPositions}");
                if (stats.CoveredPositions != 144) failures.Add($"covered:{stats.CoveredPositions}");
                if (stats.Correct != 14) failures.Add($"correct:{stats.Correct}");

                var passed = failures.Count == 0;
                var summary = failures.Count == 0
                    ? $"pairs={stats.PairCount}, aligned={stats.AlignedPositions}, " +
                      $"covered={stats.CoveredPositions}, correct={stats.Correct}."
                    : $"Mismatches: {string.Join(", ", failures)}.";

                return new HypothesisResult(passed, summary, new Dictionary<string, double>
                {
                    ["pairs"] = stats.PairCount,
                    ["aligned"] = stats.AlignedPositions,
                    ["covered"] = stats.CoveredPositions,
                    ["correct"] = stats.Correct,
                });
            }
        ),
        new(
            "Body trigram motifs (length 2/3)",
            "Shared trigram motifs in the body (post-header) across messages.",
            HypothesisExpectation.Pass,
            context =>
            {
                var bodySequences = new Dictionary<int, int[]>();
                foreach (var (id, trigrams) in context.WeaveTrigrams)
                {
                    var seq = trigrams.Select(t => t.Base10Value).ToArray();
                    if (!TryGetHeaderEndIndex(seq, out var headerEnd, out _))
                    {
                        continue;
                    }

                    var body = seq.Skip(headerEnd + 1).ToArray();
                    bodySequences[id] = body;
                }

                var sharedLen2 = CountSharedMotifs(bodySequences, 2);
                var sharedLen3 = CountSharedMotifs(bodySequences, 3);

                var topLen2 = GetTopMotifCoverage(bodySequences, 2);
                var topLen3 = GetTopMotifCoverage(bodySequences, 3);

                var failures = new List<string>();
                if (sharedLen2 != 89) failures.Add($"shared2:{sharedLen2}");
                if (sharedLen3 != 46) failures.Add($"shared3:{sharedLen3}");
                if (topLen2.Motif != "2-60" || topLen2.Coverage != 4) failures.Add($"top2:{topLen2.Motif}:{topLen2.Coverage}");
                if (topLen3.Motif != "2-60-29" || topLen3.Coverage != 4) failures.Add($"top3:{topLen3.Motif}:{topLen3.Coverage}");

                var passed = failures.Count == 0;
                var summary = failures.Count == 0
                    ? $"shared2={sharedLen2}, shared3={sharedLen3}; " +
                      $"top2={topLen2.Motif} ({topLen2.Coverage}), " +
                      $"top3={topLen3.Motif} ({topLen3.Coverage})."
                    : $"Mismatches: {string.Join(", ", failures)}.";

                return new HypothesisResult(passed, summary, new Dictionary<string, double>
                {
                    ["shared2"] = sharedLen2,
                    ["shared3"] = sharedLen3,
                    ["top2coverage"] = topLen2.Coverage,
                    ["top3coverage"] = topLen3.Coverage,
                });
            }
        ),
        new(
            "Body trigram motifs (length 2/3, edit distance 1)",
            "Near-shared trigram motifs in the body (post-header) across messages.",
            HypothesisExpectation.Pass,
            context =>
            {
                var bodySequences = new Dictionary<int, int[]>();
                foreach (var (id, trigrams) in context.WeaveTrigrams)
                {
                    var seq = trigrams.Select(t => t.Base10Value).ToArray();
                    if (!TryGetHeaderEndIndex(seq, out var headerEnd, out _))
                    {
                        continue;
                    }

                    var body = seq.Skip(headerEnd + 1).ToArray();
                    bodySequences[id] = body;
                }

                var nearLen2 = CountNearSharedMotifs(bodySequences, 2, maxDistance: 1);
                var nearLen3 = CountNearSharedMotifs(bodySequences, 3, maxDistance: 1);

                var failures = new List<string>();
                if (nearLen2 != 835) failures.Add($"near2:{nearLen2}");
                if (nearLen3 != 287) failures.Add($"near3:{nearLen3}");

                var passed = failures.Count == 0;
                var summary = failures.Count == 0
                    ? $"near2={nearLen2}, near3={nearLen3}."
                    : $"Mismatches: {string.Join(", ", failures)}.";

                return new HypothesisResult(passed, summary, new Dictionary<string, double>
                {
                    ["near2"] = nearLen2,
                    ["near3"] = nearLen3,
                });
            }
        ),
        new(
            "Body anchor segmentation (top motif)",
            "Split body by the most common short motif and summarize segment lengths.",
            HypothesisExpectation.Pass,
            context =>
            {
                var bodySequences = new Dictionary<int, int[]>();
                foreach (var (id, trigrams) in context.WeaveTrigrams)
                {
                    var seq = trigrams.Select(t => t.Base10Value).ToArray();
                    if (!TryGetHeaderEndIndex(seq, out var headerEnd, out _))
                    {
                        continue;
                    }

                    var body = seq.Skip(headerEnd + 1).ToArray();
                    bodySequences[id] = body;
                }

                var topLen2 = GetTopMotifCoverage(bodySequences, 2);
                var topLen3 = GetTopMotifCoverage(bodySequences, 3);
                var anchor = topLen3.Coverage > topLen2.Coverage ? topLen3 : topLen2;

                var anchorSeq = anchor.Motif.Split('-').Select(int.Parse).ToArray();
                var anchorsFound = 0;
                var anchorPositions = new List<int>();
                var preLengths = new List<int>();
                var postLengths = new List<int>();

                foreach (var body in bodySequences.Values)
                {
                    var idx = IndexOfSubsequence(body, anchorSeq);
                    if (idx < 0)
                    {
                        continue;
                    }

                    anchorsFound++;
                    anchorPositions.Add(idx);
                    preLengths.Add(idx);
                    postLengths.Add(body.Length - (idx + anchorSeq.Length));
                }

                var avgPos = anchorPositions.Count == 0 ? 0 : Math.Round(anchorPositions.Average(), 2);
                var avgPre = preLengths.Count == 0 ? 0 : Math.Round(preLengths.Average(), 2);
                var avgPost = postLengths.Count == 0 ? 0 : Math.Round(postLengths.Average(), 2);

                var failures = new List<string>();
                if (anchor.Motif != "2-60" || anchor.Length != 2 || anchor.Coverage != 4) failures.Add($"anchor:{anchor.Motif}:{anchor.Length}:{anchor.Coverage}");
                if (anchorsFound != 4 || bodySequences.Count != 9) failures.Add($"found:{anchorsFound}/{bodySequences.Count}");
                if (Math.Abs(avgPos - 0) > 0.01) failures.Add($"avgPos:{avgPos:0.##}");
                if (Math.Abs(avgPre - 0) > 0.01) failures.Add($"avgPre:{avgPre:0.##}");
                if (Math.Abs(avgPost - 114.5) > 0.01) failures.Add($"avgPost:{avgPost:0.##}");

                var passed = failures.Count == 0;
                var summary = failures.Count == 0
                    ? $"anchor={anchor.Motif} (len={anchor.Length}, coverage={anchor.Coverage}); " +
                      $"found={anchorsFound}/{bodySequences.Count}, avgPos={avgPos:0.##}, " +
                      $"avgPre={avgPre:0.##}, avgPost={avgPost:0.##}."
                    : $"Mismatches: {string.Join(", ", failures)}.";

                return new HypothesisResult(passed, summary, new Dictionary<string, double>
                {
                    ["anchorLen"] = anchor.Length,
                    ["anchorCoverage"] = anchor.Coverage,
                    ["found"] = anchorsFound,
                    ["avgPos"] = avgPos,
                    ["avgPre"] = avgPre,
                    ["avgPost"] = avgPost,
                });
            }
        ),
        new(
            "Row-pair 8 column band clustering",
            "Cluster row-pair 8 columns by Jaccard similarity of value sets.",
            HypothesisExpectation.Pass,
            context =>
            {
                var sequences = new Dictionary<int, int[]>();
                foreach (var message in context.Messages)
                {
                    var seq = GetRowPairColumnSequence(message, rowPair: 8);
                    if (seq.Length > 0)
                    {
                        sequences[message.Id] = seq;
                    }
                }

                var columnSets = ComputeColumnValueSets(sequences, minCoverage: 2);
                var columns = columnSets.Keys.OrderBy(c => c).ToArray();
                var threshold = 0.5;
                var adjacency = new Dictionary<int, HashSet<int>>();
                foreach (var col in columns)
                {
                    adjacency[col] = new HashSet<int> { col };
                }

                for (var i = 0; i < columns.Length; i++)
                {
                    for (var j = i + 1; j < columns.Length; j++)
                    {
                        var a = columns[i];
                        var b = columns[j];
                        var jaccard = ComputeJaccard(columnSets[a].Values, columnSets[b].Values).Jaccard;
                        if (jaccard >= threshold)
                        {
                            adjacency[a].Add(b);
                            adjacency[b].Add(a);
                        }
                    }
                }

                var clusters = new List<List<int>>();
                var visited = new HashSet<int>();
                foreach (var col in columns)
                {
                    if (visited.Contains(col))
                    {
                        continue;
                    }

                    var stack = new Stack<int>();
                    var cluster = new List<int>();
                    stack.Push(col);
                    visited.Add(col);

                    while (stack.Count > 0)
                    {
                        var current = stack.Pop();
                        cluster.Add(current);
                        foreach (var neighbor in adjacency[current])
                        {
                            if (visited.Add(neighbor))
                            {
                                stack.Push(neighbor);
                            }
                        }
                    }

                    cluster.Sort();
                    clusters.Add(cluster);
                }

                clusters = clusters.OrderByDescending(c => c.Count).ThenBy(c => c[0]).ToList();
                var largest = clusters.FirstOrDefault() ?? new List<int>();

                var failures = new List<string>();
                if (clusters.Count != 23) failures.Add($"clusters:{clusters.Count}");
                if (largest.Count != 3 || string.Join(",", largest) != "14,23,26") failures.Add($"largest:{string.Join(",", largest)}");

                var passed = failures.Count == 0;
                var summary = failures.Count == 0
                    ? $"clusters={clusters.Count}, largest={largest.Count} " +
                      $"[{string.Join(",", largest)}] (threshold={threshold:0.##})."
                    : $"Mismatches: {string.Join(", ", failures)}.";

                return new HypothesisResult(passed, summary, new Dictionary<string, double>
                {
                    ["clusters"] = clusters.Count,
                    ["largest"] = largest.Count,
                });
            }
        ),
        new(
            "HMM 2-state log-likelihood",
            "Train a 2-state HMM on trigram sequences and compare to a unigram baseline.",
            HypothesisExpectation.Pass,
            context =>
            {
                var sequences = context.WeaveTrigrams
                    .OrderBy(kvp => kvp.Key)
                    .Select(kvp => kvp.Value.Select(t => t.Base10Value).ToArray())
                    .ToList();

                var model = TrainHmm(sequences, states: 2, symbols: 83, iterations: 5);
                var hmmLogLik = ComputeHmmLogLikelihood(model, sequences);
                var tokenCount = sequences.Sum(s => s.Length);
                var hmmPerToken = Math.Round(hmmLogLik / tokenCount, 4);

                var unigramLogLik = ComputeUnigramLogLikelihood(sequences, symbols: 83);
                var unigramPerToken = Math.Round(unigramLogLik / tokenCount, 4);

                var failures = new List<string>();
                if (Math.Abs(hmmPerToken - -26.2913) > 0.001) failures.Add($"hmm:{hmmPerToken:0.####}");
                if (Math.Abs(unigramPerToken - -4.3478) > 0.001) failures.Add($"uni:{unigramPerToken:0.####}");

                var passed = failures.Count == 0;
                var summary = failures.Count == 0
                    ? $"hmmPerToken={hmmPerToken:0.####}, unigramPerToken={unigramPerToken:0.####}."
                    : $"Mismatches: {string.Join(", ", failures)}.";

                return new HypothesisResult(passed, summary, new Dictionary<string, double>
                {
                    ["hmmPerToken"] = hmmPerToken,
                    ["unigramPerToken"] = unigramPerToken,
                });
            }
        ),
        new(
            "Top trigram path templates (length 4)",
            "Extract top length-4 trigram paths and summarize coverage/position.",
            HypothesisExpectation.Pass,
            context =>
            {
                var sequences = new Dictionary<int, int[]>();
                foreach (var (id, trigrams) in context.WeaveTrigrams)
                {
                    sequences[id] = trigrams.Select(t => t.Base10Value).ToArray();
                }

                var motifStats = new Dictionary<string, (int Count, HashSet<int> Ids, List<int> Positions)>();
                foreach (var (id, seq) in sequences)
                {
                    if (seq.Length < 4)
                    {
                        continue;
                    }

                    for (var i = 0; i <= seq.Length - 4; i++)
                    {
                        var motif = string.Join("-", seq.Skip(i).Take(4));
                        if (!motifStats.TryGetValue(motif, out var entry))
                        {
                            entry = (0, new HashSet<int>(), new List<int>());
                        }

                        entry.Count += 1;
                        entry.Ids.Add(id);
                        entry.Positions.Add(i);
                        motifStats[motif] = entry;
                    }
                }

                var top = motifStats
                    .OrderByDescending(kvp => kvp.Value.Count)
                    .ThenBy(kvp => kvp.Key)
                    .Take(3)
                    .ToList();

                var summaries = new List<string>();
                foreach (var (motif, stats) in top)
                {
                    var avgPos = stats.Positions.Count == 0 ? 0 : Math.Round(stats.Positions.Average(), 2);
                    var coverage = stats.Ids.Count;
                    summaries.Add($"{motif} (count={stats.Count}, coverage={coverage}, avgPos={avgPos:0.##})");
                }

                var expected = new[]
                {
                    "5-49-75-54 (count=6, coverage=6, avgPos=2)",
                    "66-5-49-75 (count=6, coverage=6, avgPos=1)",
                    "2-60-29-40 (count=4, coverage=4, avgPos=6)",
                };

                var failures = new List<string>();
                for (var i = 0; i < expected.Length; i++)
                {
                    if (i >= summaries.Count || summaries[i] != expected[i])
                    {
                        failures.Add($"top{i}:{(i < summaries.Count ? summaries[i] : "missing")}");
                    }
                }

                var passed = failures.Count == 0;
                var summary = failures.Count == 0
                    ? $"top={string.Join("; ", summaries)}."
                    : $"Mismatches: {string.Join(", ", failures)}.";

                return new HypothesisResult(passed, summary, new Dictionary<string, double>
                {
                    ["topCount"] = top.Count,
                });
            }
        ),
        new(
            "Header lead trigram correlations",
            "Correlate the header lead trigram with message sums, unique counts, and East/West grouping.",
            HypothesisExpectation.Pass,
            context =>
            {
                var leads = new List<double>();
                var sums = new List<double>();
                var uniques = new List<double>();

                var eastLeads = new List<double>();
                var westLeads = new List<double>();

                var eastIds = new HashSet<int> { 0, 2, 4, 6, 8 };
                var westIds = new HashSet<int> { 1, 3, 5, 7 };

                foreach (var (id, trigrams) in context.WeaveTrigrams)
                {
                    if (trigrams.Count == 0)
                    {
                        continue;
                    }

                    var lead = trigrams[0].Base10Value;
                    var sum = trigrams.Sum(t => t.Base10Value);
                    var unique = trigrams.Select(t => t.Base10Value).Distinct().Count();

                    leads.Add(lead);
                    sums.Add(sum);
                    uniques.Add(unique);

                    if (eastIds.Contains(id)) eastLeads.Add(lead);
                    if (westIds.Contains(id)) westLeads.Add(lead);
                }

                var corrSum = Math.Round(Correlation(leads, sums), 3);
                var corrUnique = Math.Round(Correlation(leads, uniques), 3);
                var eastMean = eastLeads.Count == 0 ? 0 : Math.Round(eastLeads.Average(), 3);
                var westMean = westLeads.Count == 0 ? 0 : Math.Round(westLeads.Average(), 3);

                var failures = new List<string>();
                if (Math.Abs(corrSum - -0.258) > 0.001) failures.Add($"corrSum:{corrSum:0.###}");
                if (Math.Abs(corrUnique - -0.038) > 0.001) failures.Add($"corrUnique:{corrUnique:0.###}");
                if (Math.Abs(eastMean - 41.8) > 0.001) failures.Add($"eastMean:{eastMean:0.###}");
                if (Math.Abs(westMean - 66.75) > 0.001) failures.Add($"westMean:{westMean:0.###}");

                var passed = failures.Count == 0;
                var summary = failures.Count == 0
                    ? $"corrSum={corrSum:0.###}, corrUnique={corrUnique:0.###}, " +
                      $"eastMean={eastMean:0.###}, westMean={westMean:0.###}."
                    : $"Mismatches: {string.Join(", ", failures)}.";

                return new HypothesisResult(passed, summary, new Dictionary<string, double>
                {
                    ["corrSum"] = corrSum,
                    ["corrUnique"] = corrUnique,
                    ["eastMean"] = eastMean,
                    ["westMean"] = westMean,
                });
            }
        ),
        new(
            "Row-pair 8 length vs content",
            "Correlate row-pair 8 length with content metrics (unique count and bin composition).",
            HypothesisExpectation.Pass,
            context =>
            {
                var lengths = new List<double>();
                var uniques = new List<double>();
                var avgBins = new List<double>();
                var highFractions = new List<double>();

                var sequences = new List<int[]>();
                foreach (var message in context.Messages)
                {
                    var seq = GetRowPairColumnSequence(message, rowPair: 8);
                    sequences.Add(seq);
                }

                var binMap = BuildFrequencyBins(sequences, highCount: 8, midCount: 8);

                foreach (var seq in sequences)
                {
                    lengths.Add(seq.Length);
                    uniques.Add(seq.Distinct().Count());
                    if (seq.Length == 0)
                    {
                        avgBins.Add(0);
                        highFractions.Add(0);
                        continue;
                    }

                    var bins = seq.Select(v => binMap[v]).ToArray();
                    avgBins.Add(bins.Average());
                    highFractions.Add(bins.Count(b => b == 2) / (double)bins.Length);
                }

                var corrUnique = Math.Round(Correlation(lengths, uniques), 3);
                var corrAvgBin = Math.Round(Correlation(lengths, avgBins), 3);
                var corrHigh = Math.Round(Correlation(lengths, highFractions), 3);

                var failures = new List<string>();
                if (Math.Abs(corrUnique - 0.972) > 0.001) failures.Add($"corrUnique:{corrUnique:0.###}");
                if (Math.Abs(corrAvgBin - 0.909) > 0.001) failures.Add($"corrAvgBin:{corrAvgBin:0.###}");
                if (Math.Abs(corrHigh - 0.92) > 0.001) failures.Add($"corrHigh:{corrHigh:0.###}");

                var passed = failures.Count == 0;
                var summary = failures.Count == 0
                    ? $"corrUnique={corrUnique:0.###}, corrAvgBin={corrAvgBin:0.###}, corrHigh={corrHigh:0.###}."
                    : $"Mismatches: {string.Join(", ", failures)}.";

                return new HypothesisResult(passed, summary, new Dictionary<string, double>
                {
                    ["corrUnique"] = corrUnique,
                    ["corrAvgBin"] = corrAvgBin,
                    ["corrHigh"] = corrHigh,
                });
            }
        ),
        new(
            "Header lead vs motif group",
            "Compare header lead trigram distributions between motif B and motif C groups.",
            HypothesisExpectation.Pass,
            context =>
            {
                var motifB = new List<double>();
                var motifC = new List<double>();

                foreach (var (id, trigrams) in context.WeaveTrigrams)
                {
                    var seq = trigrams.Select(t => t.Base10Value).ToArray();
                    if (!TryGetHeaderEndIndex(seq, out _, out var motif))
                    {
                        continue;
                    }

                    if (seq.Length == 0)
                    {
                        continue;
                    }

                    var lead = seq[0];
                    if (motif == 'B') motifB.Add(lead);
                    if (motif == 'C') motifC.Add(lead);
                }

                var meanB = motifB.Count == 0 ? 0 : Math.Round(motifB.Average(), 3);
                var meanC = motifC.Count == 0 ? 0 : Math.Round(motifC.Average(), 3);

                var failures = new List<string>();
                if (Math.Abs(meanB - 51.667) > 0.001) failures.Add($"meanB:{meanB:0.###}");
                if (Math.Abs(meanC - 55.333) > 0.001) failures.Add($"meanC:{meanC:0.###}");
                if (motifB.Count != 6) failures.Add($"countB:{motifB.Count}");
                if (motifC.Count != 3) failures.Add($"countC:{motifC.Count}");

                var passed = failures.Count == 0;
                var summary = failures.Count == 0
                    ? $"meanB={meanB:0.###}, meanC={meanC:0.###}, " +
                      $"countB={motifB.Count}, countC={motifC.Count}."
                    : $"Mismatches: {string.Join(", ", failures)}.";

                return new HypothesisResult(passed, summary, new Dictionary<string, double>
                {
                    ["meanB"] = meanB,
                    ["meanC"] = meanC,
                    ["countB"] = motifB.Count,
                    ["countC"] = motifC.Count,
                });
            }
        ),
        new(
            "Header lead vs row-pair 8 length",
            "Correlate lead trigram with row-pair 8 length and body length.",
            HypothesisExpectation.Pass,
            context =>
            {
                var leads = new List<double>();
                var row8Lens = new List<double>();
                var bodyLens = new List<double>();

                foreach (var message in context.Messages)
                {
                    var trigrams = context.WeaveTrigrams[message.Id];
                    if (trigrams.Count == 0)
                    {
                        continue;
                    }

                    var lead = trigrams[0].Base10Value;
                    var row8 = GetRowPairColumnSequence(message, rowPair: 8);
                    var bodyLen = message.Lines.Sum(l => l.Length);

                    leads.Add(lead);
                    row8Lens.Add(row8.Length);
                    bodyLens.Add(bodyLen);
                }

                var corrRow8 = Math.Round(Correlation(leads, row8Lens), 3);
                var corrBody = Math.Round(Correlation(leads, bodyLens), 3);

                var failures = new List<string>();
                if (Math.Abs(corrRow8 - -0.335) > 0.001) failures.Add($"corrRow8:{corrRow8:0.###}");
                if (Math.Abs(corrBody - -0.259) > 0.001) failures.Add($"corrBody:{corrBody:0.###}");

                var passed = failures.Count == 0;
                var summary = failures.Count == 0
                    ? $"corrRow8={corrRow8:0.###}, corrBody={corrBody:0.###}."
                    : $"Mismatches: {string.Join(", ", failures)}.";

                return new HypothesisResult(passed, summary, new Dictionary<string, double>
                {
                    ["corrRow8"] = corrRow8,
                    ["corrBody"] = corrBody,
                });
            }
        ),
        new(
            "Header lead vs payload bin composition",
            "Compare row-pair 8 bin composition by lead trigram buckets.",
            HypothesisExpectation.Pass,
            context =>
            {
                var sequences = new List<int[]>();
                var leads = new List<int>();

                foreach (var message in context.Messages)
                {
                    var trigrams = context.WeaveTrigrams[message.Id];
                    if (trigrams.Count == 0)
                    {
                        continue;
                    }

                    var row8 = GetRowPairColumnSequence(message, rowPair: 8);
                    sequences.Add(row8);
                    leads.Add(trigrams[0].Base10Value);
                }

                var binMap = BuildFrequencyBins(sequences, highCount: 8, midCount: 8);
                var leadOrder = leads.Select((value, index) => (value, index)).OrderBy(t => t.value).ToList();
                var bucketSize = Math.Max(1, leadOrder.Count / 3);

                var buckets = new List<List<int>>();
                for (var i = 0; i < 3; i++)
                {
                    buckets.Add(new List<int>());
                }

                for (var i = 0; i < leadOrder.Count; i++)
                {
                    var bucket = Math.Min(2, i / bucketSize);
                    buckets[bucket].Add(leadOrder[i].index);
                }

                var bucketStats = new List<(double AvgBin, double HighFrac)>();
                for (var i = 0; i < 3; i++)
                {
                    var indices = buckets[i];
                    var bins = new List<int>();
                    foreach (var idx in indices)
                    {
                        var seq = sequences[idx];
                        bins.AddRange(seq.Select(v => binMap[v]));
                    }

                    if (bins.Count == 0)
                    {
                        bucketStats.Add((0, 0));
                    }
                    else
                    {
                        bucketStats.Add((Math.Round(bins.Average(), 3), Math.Round(bins.Count(b => b == 2) / (double)bins.Count, 3)));
                    }
                }

                var failures = new List<string>();
                if (Math.Abs(bucketStats[0].AvgBin - 1.328) > 0.001) failures.Add($"avg0:{bucketStats[0].AvgBin:0.###}");
                if (Math.Abs(bucketStats[1].AvgBin - 1.417) > 0.001) failures.Add($"avg1:{bucketStats[1].AvgBin:0.###}");
                if (Math.Abs(bucketStats[2].AvgBin - 1.333) > 0.001) failures.Add($"avg2:{bucketStats[2].AvgBin:0.###}");
                if (Math.Abs(bucketStats[0].HighFrac - 0.478) > 0.001) failures.Add($"high0:{bucketStats[0].HighFrac:0.###}");
                if (Math.Abs(bucketStats[1].HighFrac - 0.55) > 0.001) failures.Add($"high1:{bucketStats[1].HighFrac:0.###}");
                if (Math.Abs(bucketStats[2].HighFrac - 0.542) > 0.001) failures.Add($"high2:{bucketStats[2].HighFrac:0.###}");

                var passed = failures.Count == 0;
                var summary = failures.Count == 0
                    ? $"bucket0(avg={bucketStats[0].AvgBin:0.###}, high={bucketStats[0].HighFrac:0.###}), " +
                      $"bucket1(avg={bucketStats[1].AvgBin:0.###}, high={bucketStats[1].HighFrac:0.###}), " +
                      $"bucket2(avg={bucketStats[2].AvgBin:0.###}, high={bucketStats[2].HighFrac:0.###})."
                    : $"Mismatches: {string.Join(", ", failures)}.";

                return new HypothesisResult(passed, summary, new Dictionary<string, double>
                {
                    ["avg0"] = bucketStats[0].AvgBin,
                    ["avg1"] = bucketStats[1].AvgBin,
                    ["avg2"] = bucketStats[2].AvgBin,
                    ["high0"] = bucketStats[0].HighFrac,
                    ["high1"] = bucketStats[1].HighFrac,
                    ["high2"] = bucketStats[2].HighFrac,
                });
            }
        ),
        new(
            "Row-pair 8 length bucket overlap and entropy",
            "Recompute overlap, entropy, and bin alignment for short/medium/long row-pair 8 length buckets.",
            HypothesisExpectation.Pass,
            context =>
            {
                var sequences = new Dictionary<int, int[]>();
                foreach (var message in context.Messages)
                {
                    var seq = GetRowPairColumnSequence(message, rowPair: 8);
                    if (seq.Length > 0)
                    {
                        sequences[message.Id] = seq;
                    }
                }

                var bucketsById = BuildLengthBuckets(sequences, bucketCount: 3);
                var eastIds = new HashSet<int> { 0, 2, 4, 6, 8 };
                var westIds = new HashSet<int> { 1, 3, 5, 7 };

                var summaries = new List<string>();
                var metrics = new Dictionary<string, double>();

                for (var bucket = 0; bucket < 3; bucket++)
                {
                    var ids = bucketsById.Where(kvp => kvp.Value == bucket).Select(kvp => kvp.Key).OrderBy(id => id).ToArray();
                    var bucketSequences = sequences
                        .Where(kvp => ids.Contains(kvp.Key))
                        .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

                    var eastSequences = bucketSequences
                        .Where(kvp => eastIds.Contains(kvp.Key))
                        .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                    var westSequences = bucketSequences
                        .Where(kvp => westIds.Contains(kvp.Key))
                        .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

                    var meanLen = bucketSequences.Count == 0 ? 0 : Math.Round(bucketSequences.Values.Average(seq => seq.Length), 3);

                    var entropyStats = ComputeColumnStats(bucketSequences);
                    var entropyAvg = entropyStats.Count == 0 ? 0 : Math.Round(entropyStats.Values.Average(s => s.Entropy), 3);

                    var rawOverlap = 0.0;
                    var rawCols = 0;
                    if (eastSequences.Count > 0 && westSequences.Count > 0)
                    {
                        var eastSets = ComputeColumnValueSets(eastSequences, minCoverage: 1);
                        var westSets = ComputeColumnValueSets(westSequences, minCoverage: 1);
                        var rawJaccards = new List<double>();
                        foreach (var col in eastSets.Keys.Intersect(westSets.Keys))
                        {
                            rawJaccards.Add(ComputeJaccard(eastSets[col].Values, westSets[col].Values).Jaccard);
                        }

                        rawCols = rawJaccards.Count;
                        rawOverlap = rawJaccards.Count == 0 ? 0 : Math.Round(rawJaccards.Average(), 3);
                    }

                    var binOverlap = 0.0;
                    if (bucketSequences.Count > 0 && eastSequences.Count > 0 && westSequences.Count > 0)
                    {
                        var binMap = BuildFrequencyBins(bucketSequences.Values, highCount: 8, midCount: 8);
                        var binnedEast = eastSequences.ToDictionary(
                            kvp => kvp.Key,
                            kvp => kvp.Value.Select(v => binMap[v]).ToArray());
                        var binnedWest = westSequences.ToDictionary(
                            kvp => kvp.Key,
                            kvp => kvp.Value.Select(v => binMap[v]).ToArray());

                        var eastSets = ComputeColumnValueSets(binnedEast, minCoverage: 1);
                        var westSets = ComputeColumnValueSets(binnedWest, minCoverage: 1);

                        var jaccards = new List<double>();
                        foreach (var col in eastSets.Keys.Intersect(westSets.Keys))
                        {
                            jaccards.Add(ComputeJaccard(eastSets[col].Values, westSets[col].Values).Jaccard);
                        }

                        binOverlap = jaccards.Count == 0 ? 0 : Math.Round(jaccards.Average(), 3);
                    }

                    var modeMatch = 0.0;
                    var modeCols = 0;
                    if (bucketSequences.Count > 0 && eastSequences.Count > 0 && westSequences.Count > 0)
                    {
                        var columnBins = BuildColumnFrequencyBins(bucketSequences.Values, highCount: 2, midCount: 2);
                        var binnedEast = eastSequences.ToDictionary(
                            kvp => kvp.Key,
                            kvp => ApplyColumnBins(kvp.Value, columnBins));
                        var binnedWest = westSequences.ToDictionary(
                            kvp => kvp.Key,
                            kvp => ApplyColumnBins(kvp.Value, columnBins));

                        var eastCounts = ComputeColumnValueCounts(binnedEast);
                        var westCounts = ComputeColumnValueCounts(binnedWest);
                        var matched = 0;

                        foreach (var col in eastCounts.Keys.Intersect(westCounts.Keys))
                        {
                            var eastTotal = eastCounts[col].Values.Sum();
                            var westTotal = westCounts[col].Values.Sum();
                            if (eastTotal == 0 || westTotal == 0)
                            {
                                continue;
                            }

                            modeCols++;
                            var eastMode = GetModeBin(eastCounts[col]);
                            var westMode = GetModeBin(westCounts[col]);
                            if (eastMode == westMode)
                            {
                                matched++;
                            }
                        }

                        modeMatch = modeCols == 0 ? 0 : Math.Round(matched / (double)modeCols, 3);
                    }

                    metrics[$"b{bucket}MeanLen"] = meanLen;
                    metrics[$"b{bucket}Entropy"] = entropyAvg;
                    metrics[$"b{bucket}RawOverlap"] = rawOverlap;
                    metrics[$"b{bucket}BinOverlap"] = binOverlap;
                    metrics[$"b{bucket}ModeMatch"] = modeMatch;
                    metrics[$"b{bucket}RawCols"] = rawCols;
                    metrics[$"b{bucket}ModeCols"] = modeCols;
                    metrics[$"b{bucket}Count"] = ids.Length;
                    metrics[$"b{bucket}East"] = eastSequences.Count;
                    metrics[$"b{bucket}West"] = westSequences.Count;

                    summaries.Add(
                        $"b{bucket}(n={ids.Length},E={eastSequences.Count},W={westSequences.Count}," +
                        $"len={meanLen:0.###},ent={entropyAvg:0.###},raw={rawOverlap:0.###}," +
                        $"bin={binOverlap:0.###},mode={modeMatch:0.###},cols={rawCols})");
                }

                var expected = new Dictionary<string, double>
                {
                    ["b0MeanLen"] = 18,
                    ["b1MeanLen"] = 23,
                    ["b2MeanLen"] = 34.5,
                    ["b0Entropy"] = 0.619,
                    ["b1Entropy"] = 0.875,
                    ["b2Entropy"] = 0.718,
                    ["b0RawOverlap"] = 0,
                    ["b1RawOverlap"] = 0.045,
                    ["b2RawOverlap"] = 0.067,
                    ["b0BinOverlap"] = 0,
                    ["b1BinOverlap"] = 0.364,
                    ["b2BinOverlap"] = 0.533,
                    ["b0ModeMatch"] = 0,
                    ["b1ModeMatch"] = 1,
                    ["b2ModeMatch"] = 1,
                    ["b0RawCols"] = 0,
                    ["b1RawCols"] = 22,
                    ["b2RawCols"] = 30,
                    ["b0Count"] = 2,
                    ["b1Count"] = 2,
                    ["b2Count"] = 2,
                    ["b0East"] = 2,
                    ["b0West"] = 0,
                    ["b1East"] = 1,
                    ["b1West"] = 1,
                    ["b2East"] = 1,
                    ["b2West"] = 1,
                };

                var failures = new List<string>();
                foreach (var (key, expectedValue) in expected)
                {
                    if (!metrics.TryGetValue(key, out var observed) || Math.Abs(observed - expectedValue) > 0.001)
                    {
                        failures.Add($"{key}:{observed:0.###}");
                    }
                }

                var passed = failures.Count == 0;
                var summary = passed
                    ? string.Join("; ", summaries) + "."
                    : $"Mismatches: {string.Join(", ", failures)}.";

                return new HypothesisResult(passed, summary, metrics);
            }
        ),
        new(
            "Row-pair 8 length bucket decoding accuracy",
            "Evaluate run-position decoding accuracy by row-pair 8 length bucket (short/med/long).",
            HypothesisExpectation.Pass,
            context =>
            {
                var sequences = new Dictionary<int, int[]>();
                foreach (var message in context.Messages)
                {
                    var seq = GetRowPairColumnSequence(message, rowPair: 8);
                    if (seq.Length > 0)
                    {
                        sequences[message.Id] = seq;
                    }
                }

                var bucketsById = BuildLengthBuckets(sequences, bucketCount: 3);

                var eastIds = new[] { 0, 2, 4, 6, 8 };
                var westIds = new[] { 1, 3, 5, 7 };
                var pairs = new List<(int EastId, int WestId)>();
                foreach (var east in eastIds)
                {
                    foreach (var west in westIds)
                    {
                        pairs.Add((east, west));
                    }
                }

                var stats = EvaluateStatefulDecodeWithRunPositionLengthBuckets(sequences, pairs, bucketsById, useAlignment: true);

                var summaries = new List<string>();
                for (var bucket = 0; bucket < stats.Buckets.Count; bucket++)
                {
                    var b = stats.Buckets[bucket];
                    var acc = b.Covered == 0 ? 0 : Math.Round(b.Correct / (double)b.Covered, 3);
                    summaries.Add($"b{bucket}={b.Covered}/{b.Aligned}/{b.Correct} (acc={acc:0.###})");
                }

                var summary = $"pairs={stats.PairCount}, aligned={stats.AlignedPositions}, " +
                              $"covered={stats.CoveredPositions}, correct={stats.Correct}; " +
                              string.Join(", ", summaries) + ".";

                var metrics = new Dictionary<string, double>
                {
                    ["pairs"] = stats.PairCount,
                    ["aligned"] = stats.AlignedPositions,
                    ["covered"] = stats.CoveredPositions,
                    ["correct"] = stats.Correct,
                };

                for (var bucket = 0; bucket < stats.Buckets.Count; bucket++)
                {
                    var b = stats.Buckets[bucket];
                    metrics[$"b{bucket}Aligned"] = b.Aligned;
                    metrics[$"b{bucket}Covered"] = b.Covered;
                    metrics[$"b{bucket}Correct"] = b.Correct;
                    metrics[$"b{bucket}Acc"] = b.Covered == 0 ? 0 : Math.Round(b.Correct / (double)b.Covered, 3);
                }

                var expected = new Dictionary<string, double>
                {
                    ["pairs"] = 8,
                    ["aligned"] = 147,
                    ["covered"] = 114,
                    ["correct"] = 24,
                    ["b0Aligned"] = 0,
                    ["b0Covered"] = 0,
                    ["b0Correct"] = 0,
                    ["b0Acc"] = 0,
                    ["b1Aligned"] = 71,
                    ["b1Covered"] = 57,
                    ["b1Correct"] = 12,
                    ["b1Acc"] = 0.211,
                    ["b2Aligned"] = 76,
                    ["b2Covered"] = 57,
                    ["b2Correct"] = 12,
                    ["b2Acc"] = 0.211,
                };

                var failures = new List<string>();
                foreach (var (key, expectedValue) in expected)
                {
                    if (!metrics.TryGetValue(key, out var observed) || Math.Abs(observed - expectedValue) > 0.001)
                    {
                        failures.Add($"{key}:{observed:0.###}");
                    }
                }

                var passed = failures.Count == 0;
                var finalSummary = passed ? summary : $"Mismatches: {string.Join(", ", failures)}.";
                return new HypothesisResult(passed, finalSummary, metrics);
            }
        ),
        new(
            "Row-pair 8 length-normalized decoding",
            "Use normalized position buckets for decoding and report accuracy by length bucket.",
            HypothesisExpectation.Pass,
            context =>
            {
                var sequences = new Dictionary<int, int[]>();
                foreach (var message in context.Messages)
                {
                    var seq = GetRowPairColumnSequence(message, rowPair: 8);
                    if (seq.Length > 0)
                    {
                        sequences[message.Id] = seq;
                    }
                }

                var bucketsById = BuildLengthBuckets(sequences, bucketCount: 3);

                var eastIds = new[] { 0, 2, 4, 6, 8 };
                var westIds = new[] { 1, 3, 5, 7 };
                var pairs = new List<(int EastId, int WestId)>();
                foreach (var east in eastIds)
                {
                    foreach (var west in westIds)
                    {
                        pairs.Add((east, west));
                    }
                }

                var stats = EvaluateStatefulDecodeWithNormalizedBucketByLength(sequences, pairs, bucketsById, useAlignment: true);

                var summaries = new List<string>();
                for (var bucket = 0; bucket < stats.Buckets.Count; bucket++)
                {
                    var b = stats.Buckets[bucket];
                    var acc = b.Covered == 0 ? 0 : Math.Round(b.Correct / (double)b.Covered, 3);
                    summaries.Add($"b{bucket}={b.Covered}/{b.Aligned}/{b.Correct} (acc={acc:0.###})");
                }

                var summary = $"pairs={stats.PairCount}, aligned={stats.AlignedPositions}, " +
                              $"covered={stats.CoveredPositions}, correct={stats.Correct}; " +
                              string.Join(", ", summaries) + ".";

                var metrics = new Dictionary<string, double>
                {
                    ["pairs"] = stats.PairCount,
                    ["aligned"] = stats.AlignedPositions,
                    ["covered"] = stats.CoveredPositions,
                    ["correct"] = stats.Correct,
                };

                for (var bucket = 0; bucket < stats.Buckets.Count; bucket++)
                {
                    var b = stats.Buckets[bucket];
                    metrics[$"b{bucket}Aligned"] = b.Aligned;
                    metrics[$"b{bucket}Covered"] = b.Covered;
                    metrics[$"b{bucket}Correct"] = b.Correct;
                    metrics[$"b{bucket}Acc"] = b.Covered == 0 ? 0 : Math.Round(b.Correct / (double)b.Covered, 3);
                }

                var expected = new Dictionary<string, double>
                {
                    ["pairs"] = 8,
                    ["aligned"] = 147,
                    ["covered"] = 115,
                    ["correct"] = 26,
                    ["b0Aligned"] = 0,
                    ["b0Covered"] = 0,
                    ["b0Correct"] = 0,
                    ["b0Acc"] = 0,
                    ["b1Aligned"] = 71,
                    ["b1Covered"] = 57,
                    ["b1Correct"] = 14,
                    ["b1Acc"] = 0.246,
                    ["b2Aligned"] = 76,
                    ["b2Covered"] = 58,
                    ["b2Correct"] = 12,
                    ["b2Acc"] = 0.207,
                };

                var failures = new List<string>();
                foreach (var (key, expectedValue) in expected)
                {
                    if (!metrics.TryGetValue(key, out var observed) || Math.Abs(observed - expectedValue) > 0.001)
                    {
                        failures.Add($"{key}:{observed:0.###}");
                    }
                }

                var passed = failures.Count == 0;
                var finalSummary = passed ? summary : $"Mismatches: {string.Join(", ", failures)}.";
                return new HypothesisResult(passed, finalSummary, metrics);
            }
        ),
        new(
            "Row-pair 8 side-specific mapping consistency",
            "Measure mapping conflicts and leave-one-out decoding accuracy within East-only and West-only subsets.",
            HypothesisExpectation.Pass,
            context =>
            {
                var sequences = new Dictionary<int, int[]>();
                foreach (var message in context.Messages)
                {
                    var seq = GetRowPairColumnSequence(message, rowPair: 8);
                    if (seq.Length > 0)
                    {
                        sequences[message.Id] = seq;
                    }
                }

                var eastIds = new[] { 0, 2, 4, 6, 8 };
                var westIds = new[] { 1, 3, 5, 7 };

                var eastPairs = BuildWithinSidePairs(eastIds, sequences);
                var westPairs = BuildWithinSidePairs(westIds, sequences);

                var eastConflicts = ComputeStatefulMappingConflicts(sequences, eastPairs, useAlignment: true);
                var westConflicts = ComputeStatefulMappingConflicts(sequences, westPairs, useAlignment: true);

                var eastStats = EvaluateStatefulDecodeWithRunPosition(sequences, eastPairs, useAlignment: true);
                var westStats = EvaluateStatefulDecodeWithRunPosition(sequences, westPairs, useAlignment: true);

                var summary = $"eastPairs={eastPairs.Count}, eastConflicts={eastConflicts.Conflicts}/{eastConflicts.Mappings}, " +
                              $"eastCovered={eastStats.CoveredPositions}, eastCorrect={eastStats.Correct}; " +
                              $"westPairs={westPairs.Count}, westConflicts={westConflicts.Conflicts}/{westConflicts.Mappings}, " +
                              $"westCovered={westStats.CoveredPositions}, westCorrect={westStats.Correct}.";

                var metrics = new Dictionary<string, double>
                {
                    ["eastPairs"] = eastPairs.Count,
                    ["eastConflicts"] = eastConflicts.Conflicts,
                    ["eastMappings"] = eastConflicts.Mappings,
                    ["eastCovered"] = eastStats.CoveredPositions,
                    ["eastCorrect"] = eastStats.Correct,
                    ["westPairs"] = westPairs.Count,
                    ["westConflicts"] = westConflicts.Conflicts,
                    ["westMappings"] = westConflicts.Mappings,
                    ["westCovered"] = westStats.CoveredPositions,
                    ["westCorrect"] = westStats.Correct,
                };

                var expected = new Dictionary<string, double>
                {
                    ["eastPairs"] = 12,
                    ["eastConflicts"] = 54,
                    ["eastMappings"] = 93,
                    ["eastCovered"] = 158,
                    ["eastCorrect"] = 38,
                    ["westPairs"] = 2,
                    ["westConflicts"] = 0,
                    ["westMappings"] = 44,
                    ["westCovered"] = 0,
                    ["westCorrect"] = 0,
                };

                var failures = new List<string>();
                foreach (var (key, expectedValue) in expected)
                {
                    if (!metrics.TryGetValue(key, out var observed) || Math.Abs(observed - expectedValue) > 0.001)
                    {
                        failures.Add($"{key}:{observed:0.###}");
                    }
                }

                var passed = failures.Count == 0;
                var finalSummary = passed ? summary : $"Mismatches: {string.Join(", ", failures)}.";
                return new HypothesisResult(passed, finalSummary, metrics);
            }
        ),
        new(
            "Row-pair 8 cross-side transfer",
            "Train run-position mapping within one side and evaluate on the opposite side.",
            HypothesisExpectation.Pass,
            context =>
            {
                var sequences = new Dictionary<int, int[]>();
                foreach (var message in context.Messages)
                {
                    var seq = GetRowPairColumnSequence(message, rowPair: 8);
                    if (seq.Length > 0)
                    {
                        sequences[message.Id] = seq;
                    }
                }

                var eastIds = new[] { 0, 2, 4, 6, 8 };
                var westIds = new[] { 1, 3, 5, 7 };

                var eastPairs = BuildWithinSidePairs(eastIds, sequences);
                var westPairs = BuildWithinSidePairs(westIds, sequences);

                var eastMapping = BuildStatefulMappingWithRunPosition(sequences, eastPairs, useAlignment: true);
                var westMapping = BuildStatefulMappingWithRunPosition(sequences, westPairs, useAlignment: true);

                var eastToWest = EvaluateStatefulDecodeWithRunPositionUsingMapping(sequences, westPairs, eastMapping, useAlignment: true);
                var westToEast = EvaluateStatefulDecodeWithRunPositionUsingMapping(sequences, eastPairs, westMapping, useAlignment: true);

                var summary = $"east->west covered={eastToWest.CoveredPositions}, correct={eastToWest.Correct}; " +
                              $"west->east covered={westToEast.CoveredPositions}, correct={westToEast.Correct}.";

                var metrics = new Dictionary<string, double>
                {
                    ["eastToWestCovered"] = eastToWest.CoveredPositions,
                    ["eastToWestCorrect"] = eastToWest.Correct,
                    ["westToEastCovered"] = westToEast.CoveredPositions,
                    ["westToEastCorrect"] = westToEast.Correct,
                };

                var expected = new Dictionary<string, double>
                {
                    ["eastToWestCovered"] = 0,
                    ["eastToWestCorrect"] = 0,
                    ["westToEastCovered"] = 0,
                    ["westToEastCorrect"] = 0,
                };

                var failures = new List<string>();
                foreach (var (key, expectedValue) in expected)
                {
                    if (!metrics.TryGetValue(key, out var observed) || Math.Abs(observed - expectedValue) > 0.001)
                    {
                        failures.Add($"{key}:{observed:0.###}");
                    }
                }

                var passed = failures.Count == 0;
                var finalSummary = passed ? summary : $"Mismatches: {string.Join(", ", failures)}.";
                return new HypothesisResult(passed, finalSummary, metrics);
            }
        ),
        new(
            "Row-pair 8 side-conditional bins",
            "Compute overlap using side-specific bins and apply bin constraints during decoding.",
            HypothesisExpectation.Pass,
            context =>
            {
                var sequences = new Dictionary<int, int[]>();
                foreach (var message in context.Messages)
                {
                    var seq = GetRowPairColumnSequence(message, rowPair: 8);
                    if (seq.Length > 0)
                    {
                        sequences[message.Id] = seq;
                    }
                }

                var eastIds = new HashSet<int> { 0, 2, 4, 6, 8 };
                var westIds = new HashSet<int> { 1, 3, 5, 7 };

                var eastSequences = sequences.Where(kvp => eastIds.Contains(kvp.Key)).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                var westSequences = sequences.Where(kvp => westIds.Contains(kvp.Key)).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

                var eastBinMap = BuildFrequencyBins(eastSequences.Values, highCount: 8, midCount: 8);
                var westBinMap = BuildFrequencyBins(westSequences.Values, highCount: 8, midCount: 8);

                var eastBinned = eastSequences.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.Select(v => eastBinMap[v]).ToArray());
                var westBinned = westSequences.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.Select(v => westBinMap[v]).ToArray());

                var eastSets = ComputeColumnValueSets(eastBinned, minCoverage: 4);
                var westSets = ComputeColumnValueSets(westBinned, minCoverage: 2);

                var jaccards = new List<double>();
                foreach (var col in eastSets.Keys.Intersect(westSets.Keys))
                {
                    jaccards.Add(ComputeJaccard(eastSets[col].Values, westSets[col].Values).Jaccard);
                }

                var sideBinAvg = jaccards.Count == 0 ? 0 : Math.Round(jaccards.Average(), 3);

                var eastColumnBins = BuildColumnFrequencyBins(eastSequences.Values, highCount: 2, midCount: 2);
                var westColumnBins = BuildColumnFrequencyBins(westSequences.Values, highCount: 2, midCount: 2);

                var eastColumnBinned = eastSequences.ToDictionary(
                    kvp => kvp.Key,
                    kvp => ApplyColumnBins(kvp.Value, eastColumnBins));
                var westColumnBinned = westSequences.ToDictionary(
                    kvp => kvp.Key,
                    kvp => ApplyColumnBins(kvp.Value, westColumnBins));

                var eastColumnSets = ComputeColumnValueSets(eastColumnBinned, minCoverage: 4);
                var westColumnSets = ComputeColumnValueSets(westColumnBinned, minCoverage: 2);

                var columnJaccards = new List<double>();
                foreach (var col in eastColumnSets.Keys.Intersect(westColumnSets.Keys))
                {
                    columnJaccards.Add(ComputeJaccard(eastColumnSets[col].Values, westColumnSets[col].Values).Jaccard);
                }

                var sideColumnAvg = columnJaccards.Count == 0 ? 0 : Math.Round(columnJaccards.Average(), 3);

                var pairs = BuildCrossSidePairs(new[] { 0, 2, 4, 6, 8 }, new[] { 1, 3, 5, 7 });
                var binStats = EvaluateStatefulDecodeWithRunPositionBinConstraints(sequences, pairs);

                var summary = $"sideBinAvg={sideBinAvg:0.###}, sideColumnAvg={sideColumnAvg:0.###}, " +
                              $"binCovered={binStats.CoveredPositions}, binCorrect={binStats.Correct}.";

                var metrics = new Dictionary<string, double>
                {
                    ["sideBinAvg"] = sideBinAvg,
                    ["sideColumnAvg"] = sideColumnAvg,
                    ["binCovered"] = binStats.CoveredPositions,
                    ["binCorrect"] = binStats.Correct,
                };

                var expected = new Dictionary<string, double>
                {
                    ["sideBinAvg"] = 0.544,
                    ["sideColumnAvg"] = 0.567,
                    ["binCovered"] = 86,
                    ["binCorrect"] = 19,
                };

                var failures = new List<string>();
                foreach (var (key, expectedValue) in expected)
                {
                    if (!metrics.TryGetValue(key, out var observed) || Math.Abs(observed - expectedValue) > 0.001)
                    {
                        failures.Add($"{key}:{observed:0.###}");
                    }
                }

                var passed = failures.Count == 0;
                var finalSummary = passed ? summary : $"Mismatches: {string.Join(", ", failures)}.";
                return new HypothesisResult(passed, finalSummary, metrics);
            }
        ),
        new(
            "Row-pair 8 paired-length bucket overlap",
            "Recompute overlap and entropy using only paired messages that contain row-pair 8.",
            HypothesisExpectation.Pass,
            context =>
            {
                var pairedIds = new[] { 4, 5, 6, 7 };
                var sequences = new Dictionary<int, int[]>();
                foreach (var id in pairedIds)
                {
                    var message = context.Messages.First(m => m.Id == id);
                    var seq = GetRowPairColumnSequence(message, rowPair: 8);
                    if (seq.Length > 0)
                    {
                        sequences[id] = seq;
                    }
                }

                var bucketsById = BuildLengthBuckets(sequences, bucketCount: 3);
                var eastIds = new HashSet<int> { 4, 6 };
                var westIds = new HashSet<int> { 5, 7 };

                var summaries = new List<string>();
                var metrics = new Dictionary<string, double>();

                for (var bucket = 0; bucket < 3; bucket++)
                {
                    var ids = bucketsById.Where(kvp => kvp.Value == bucket).Select(kvp => kvp.Key).OrderBy(id => id).ToArray();
                    var bucketSequences = sequences
                        .Where(kvp => ids.Contains(kvp.Key))
                        .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

                    var eastSequences = bucketSequences
                        .Where(kvp => eastIds.Contains(kvp.Key))
                        .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                    var westSequences = bucketSequences
                        .Where(kvp => westIds.Contains(kvp.Key))
                        .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

                    var meanLen = bucketSequences.Count == 0 ? 0 : Math.Round(bucketSequences.Values.Average(seq => seq.Length), 3);
                    var entropyStats = ComputeColumnStats(bucketSequences);
                    var entropyAvg = entropyStats.Count == 0 ? 0 : Math.Round(entropyStats.Values.Average(s => s.Entropy), 3);

                    var rawOverlap = 0.0;
                    if (eastSequences.Count > 0 && westSequences.Count > 0)
                    {
                        var eastSets = ComputeColumnValueSets(eastSequences, minCoverage: 1);
                        var westSets = ComputeColumnValueSets(westSequences, minCoverage: 1);
                        var rawJaccards = new List<double>();
                        foreach (var col in eastSets.Keys.Intersect(westSets.Keys))
                        {
                            rawJaccards.Add(ComputeJaccard(eastSets[col].Values, westSets[col].Values).Jaccard);
                        }

                        rawOverlap = rawJaccards.Count == 0 ? 0 : Math.Round(rawJaccards.Average(), 3);
                    }

                    var binOverlap = 0.0;
                    if (bucketSequences.Count > 0 && eastSequences.Count > 0 && westSequences.Count > 0)
                    {
                        var binMap = BuildFrequencyBins(bucketSequences.Values, highCount: 8, midCount: 8);
                        var binnedEast = eastSequences.ToDictionary(
                            kvp => kvp.Key,
                            kvp => kvp.Value.Select(v => binMap[v]).ToArray());
                        var binnedWest = westSequences.ToDictionary(
                            kvp => kvp.Key,
                            kvp => kvp.Value.Select(v => binMap[v]).ToArray());

                        var eastSets = ComputeColumnValueSets(binnedEast, minCoverage: 1);
                        var westSets = ComputeColumnValueSets(binnedWest, minCoverage: 1);

                        var jaccards = new List<double>();
                        foreach (var col in eastSets.Keys.Intersect(westSets.Keys))
                        {
                            jaccards.Add(ComputeJaccard(eastSets[col].Values, westSets[col].Values).Jaccard);
                        }

                        binOverlap = jaccards.Count == 0 ? 0 : Math.Round(jaccards.Average(), 3);
                    }

                    metrics[$"b{bucket}MeanLen"] = meanLen;
                    metrics[$"b{bucket}Entropy"] = entropyAvg;
                    metrics[$"b{bucket}RawOverlap"] = rawOverlap;
                    metrics[$"b{bucket}BinOverlap"] = binOverlap;
                    metrics[$"b{bucket}Count"] = ids.Length;

                    summaries.Add(
                        $"b{bucket}(n={ids.Length},len={meanLen:0.###},ent={entropyAvg:0.###}," +
                        $"raw={rawOverlap:0.###},bin={binOverlap:0.###})");
                }

                var summary = string.Join("; ", summaries) + ".";
                var expected = new Dictionary<string, double>
                {
                    ["b0MeanLen"] = 22,
                    ["b0Entropy"] = 0,
                    ["b0RawOverlap"] = 0,
                    ["b0BinOverlap"] = 0,
                    ["b0Count"] = 1,
                    ["b1MeanLen"] = 24,
                    ["b1Entropy"] = 0,
                    ["b1RawOverlap"] = 0,
                    ["b1BinOverlap"] = 0,
                    ["b1Count"] = 1,
                    ["b2MeanLen"] = 34.5,
                    ["b2Entropy"] = 0.718,
                    ["b2RawOverlap"] = 0.067,
                    ["b2BinOverlap"] = 0.533,
                    ["b2Count"] = 2,
                };

                var failures = new List<string>();
                foreach (var (key, expectedValue) in expected)
                {
                    if (!metrics.TryGetValue(key, out var observed) || Math.Abs(observed - expectedValue) > 0.001)
                    {
                        failures.Add($"{key}:{observed:0.###}");
                    }
                }

                var passed = failures.Count == 0;
                var finalSummary = passed ? summary : $"Mismatches: {string.Join(", ", failures)}.";
                return new HypothesisResult(passed, finalSummary, metrics);
            }
        ),
        new(
            "Row-pair 8 paired-length decoding accuracy",
            "Evaluate run-position decoding accuracy using only paired messages with row-pair 8.",
            HypothesisExpectation.Pass,
            context =>
            {
                var sequences = new Dictionary<int, int[]>();
                foreach (var message in context.Messages)
                {
                    var seq = GetRowPairColumnSequence(message, rowPair: 8);
                    if (seq.Length > 0)
                    {
                        sequences[message.Id] = seq;
                    }
                }

                var pairs = new List<(int EastId, int WestId)>
                {
                    (4, 5),
                    (6, 7),
                };

                var pairedSequences = sequences
                    .Where(kvp => pairs.Select(p => p.EastId).Concat(pairs.Select(p => p.WestId)).Contains(kvp.Key))
                    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

                var bucketsById = BuildLengthBuckets(pairedSequences, bucketCount: 3);
                var stats = EvaluateStatefulDecodeWithRunPositionLengthBuckets(sequences, pairs, bucketsById, useAlignment: true);

                var summaries = new List<string>();
                for (var bucket = 0; bucket < stats.Buckets.Count; bucket++)
                {
                    var b = stats.Buckets[bucket];
                    var acc = b.Covered == 0 ? 0 : Math.Round(b.Correct / (double)b.Covered, 3);
                    summaries.Add($"b{bucket}={b.Covered}/{b.Aligned}/{b.Correct} (acc={acc:0.###})");
                }

                var summary = $"pairs={stats.PairCount}, aligned={stats.AlignedPositions}, " +
                              $"covered={stats.CoveredPositions}, correct={stats.Correct}; " +
                              string.Join(", ", summaries) + ".";

                var metrics = new Dictionary<string, double>
                {
                    ["pairs"] = stats.PairCount,
                    ["aligned"] = stats.AlignedPositions,
                    ["covered"] = stats.CoveredPositions,
                    ["correct"] = stats.Correct,
                };

                for (var bucket = 0; bucket < stats.Buckets.Count; bucket++)
                {
                    var b = stats.Buckets[bucket];
                    metrics[$"b{bucket}Aligned"] = b.Aligned;
                    metrics[$"b{bucket}Covered"] = b.Covered;
                    metrics[$"b{bucket}Correct"] = b.Correct;
                    metrics[$"b{bucket}Acc"] = b.Covered == 0 ? 0 : Math.Round(b.Correct / (double)b.Covered, 3);
                }

                var expected = new Dictionary<string, double>
                {
                    ["pairs"] = 2,
                    ["aligned"] = 47,
                    ["covered"] = 0,
                    ["correct"] = 0,
                    ["b0Aligned"] = 0,
                    ["b0Covered"] = 0,
                    ["b0Correct"] = 0,
                    ["b0Acc"] = 0,
                    ["b1Aligned"] = 20,
                    ["b1Covered"] = 0,
                    ["b1Correct"] = 0,
                    ["b1Acc"] = 0,
                    ["b2Aligned"] = 27,
                    ["b2Covered"] = 0,
                    ["b2Correct"] = 0,
                    ["b2Acc"] = 0,
                };

                var failures = new List<string>();
                foreach (var (key, expectedValue) in expected)
                {
                    if (!metrics.TryGetValue(key, out var observed) || Math.Abs(observed - expectedValue) > 0.001)
                    {
                        failures.Add($"{key}:{observed:0.###}");
                    }
                }

                var passed = failures.Count == 0;
                var finalSummary = passed ? summary : $"Mismatches: {string.Join(", ", failures)}.";
                return new HypothesisResult(passed, finalSummary, metrics);
            }
        ),
        new(
            "Row-pair 8 normalized bucket sweep",
            "Compare normalized bucket decoding for 3, 4, and 5 buckets.",
            HypothesisExpectation.Pass,
            context =>
            {
                var sequences = new Dictionary<int, int[]>();
                foreach (var message in context.Messages)
                {
                    var seq = GetRowPairColumnSequence(message, rowPair: 8);
                    if (seq.Length > 0)
                    {
                        sequences[message.Id] = seq;
                    }
                }

                var bucketsById = BuildLengthBuckets(sequences, bucketCount: 3);
                var pairs = BuildCrossSidePairs(new[] { 0, 2, 4, 6, 8 }, new[] { 1, 3, 5, 7 });

                var results = new List<string>();
                var metrics = new Dictionary<string, double>();

                foreach (var bucketCount in new[] { 3, 4, 5 })
                {
                    var stats = EvaluateStatefulDecodeWithNormalizedBucketByLength(sequences, pairs, bucketsById, useAlignment: true, bucketCount: bucketCount);
                    var acc = stats.CoveredPositions == 0 ? 0 : Math.Round(stats.Correct / (double)stats.CoveredPositions, 3);
                    results.Add($"{bucketCount}b:{stats.CoveredPositions}/{stats.AlignedPositions}/{stats.Correct} (acc={acc:0.###})");

                    metrics[$"b{bucketCount}Aligned"] = stats.AlignedPositions;
                    metrics[$"b{bucketCount}Covered"] = stats.CoveredPositions;
                    metrics[$"b{bucketCount}Correct"] = stats.Correct;
                    metrics[$"b{bucketCount}Acc"] = acc;
                }

                var summary = string.Join("; ", results) + ".";
                var expected = new Dictionary<string, double>
                {
                    ["b3Aligned"] = 147,
                    ["b3Covered"] = 115,
                    ["b3Correct"] = 26,
                    ["b3Acc"] = 0.226,
                    ["b4Aligned"] = 147,
                    ["b4Covered"] = 115,
                    ["b4Correct"] = 26,
                    ["b4Acc"] = 0.226,
                    ["b5Aligned"] = 147,
                    ["b5Covered"] = 116,
                    ["b5Correct"] = 26,
                    ["b5Acc"] = 0.224,
                };

                var failures = new List<string>();
                foreach (var (key, expectedValue) in expected)
                {
                    if (!metrics.TryGetValue(key, out var observed) || Math.Abs(observed - expectedValue) > 0.001)
                    {
                        failures.Add($"{key}:{observed:0.###}");
                    }
                }

                var passed = failures.Count == 0;
                var finalSummary = passed ? summary : $"Mismatches: {string.Join(", ", failures)}.";
                return new HypothesisResult(passed, finalSummary, metrics);
            }
        ),
        new(
            "Row-pair 8 direct alignment decoding",
            "Evaluate run-position decoding using direct index alignment (no gaps).",
            HypothesisExpectation.Pass,
            context =>
            {
                var sequences = new Dictionary<int, int[]>();
                foreach (var message in context.Messages)
                {
                    var seq = GetRowPairColumnSequence(message, rowPair: 8);
                    if (seq.Length > 0)
                    {
                        sequences[message.Id] = seq;
                    }
                }

                var pairs = BuildCrossSidePairs(new[] { 0, 2, 4, 6, 8 }, new[] { 1, 3, 5, 7 });
                var stats = EvaluateStatefulDecodeWithRunPosition(sequences, pairs, useAlignment: false);

                var summary = $"pairs={stats.PairCount}, aligned={stats.AlignedPositions}, " +
                              $"covered={stats.CoveredPositions}, correct={stats.Correct}.";

                var metrics = new Dictionary<string, double>
                {
                    ["pairs"] = stats.PairCount,
                    ["aligned"] = stats.AlignedPositions,
                    ["covered"] = stats.CoveredPositions,
                    ["correct"] = stats.Correct,
                };

                var expected = new Dictionary<string, double>
                {
                    ["pairs"] = 8,
                    ["aligned"] = 170,
                    ["covered"] = 164,
                    ["correct"] = 14,
                };

                var failures = new List<string>();
                foreach (var (key, expectedValue) in expected)
                {
                    if (!metrics.TryGetValue(key, out var observed) || Math.Abs(observed - expectedValue) > 0.001)
                    {
                        failures.Add($"{key}:{observed:0.###}");
                    }
                }

                var passed = failures.Count == 0;
                var finalSummary = passed ? summary : $"Mismatches: {string.Join(", ", failures)}.";
                return new HypothesisResult(passed, finalSummary, metrics);
            }
        ),
        new(
            "Row-pair 8 segmented alignment decoding",
            "Align early and late segments separately before decoding.",
            HypothesisExpectation.Pass,
            context =>
            {
                var sequences = new Dictionary<int, int[]>();
                foreach (var message in context.Messages)
                {
                    var seq = GetRowPairColumnSequence(message, rowPair: 8);
                    if (seq.Length > 0)
                    {
                        sequences[message.Id] = seq;
                    }
                }

                var pairs = BuildCrossSidePairs(new[] { 0, 2, 4, 6, 8 }, new[] { 1, 3, 5, 7 });
                var stats = EvaluateStatefulDecodeWithSegmentedAlignment(sequences, pairs, splitIndex: 10);

                var summary = $"pairs={stats.PairCount}, aligned={stats.AlignedPositions}, " +
                              $"covered={stats.CoveredPositions}, correct={stats.Correct}.";

                var metrics = new Dictionary<string, double>
                {
                    ["pairs"] = stats.PairCount,
                    ["aligned"] = stats.AlignedPositions,
                    ["covered"] = stats.CoveredPositions,
                    ["correct"] = stats.Correct,
                };

                var expected = new Dictionary<string, double>
                {
                    ["pairs"] = 8,
                    ["aligned"] = 148,
                    ["covered"] = 118,
                    ["correct"] = 14,
                };

                var failures = new List<string>();
                foreach (var (key, expectedValue) in expected)
                {
                    if (!metrics.TryGetValue(key, out var observed) || Math.Abs(observed - expectedValue) > 0.001)
                    {
                        failures.Add($"{key}:{observed:0.###}");
                    }
                }

                var passed = failures.Count == 0;
                var finalSummary = passed ? summary : $"Mismatches: {string.Join(", ", failures)}.";
                return new HypothesisResult(passed, finalSummary, metrics);
            }
        ),
        new(
            "Row-pair 8 bin-aware alignment decoding",
            "Align sequences using coarse bins and evaluate decoding.",
            HypothesisExpectation.Pass,
            context =>
            {
                var sequences = new Dictionary<int, int[]>();
                foreach (var message in context.Messages)
                {
                    var seq = GetRowPairColumnSequence(message, rowPair: 8);
                    if (seq.Length > 0)
                    {
                        sequences[message.Id] = seq;
                    }
                }

                var pairs = BuildCrossSidePairs(new[] { 0, 2, 4, 6, 8 }, new[] { 1, 3, 5, 7 });
                var binMap = BuildFrequencyBins(sequences.Values, highCount: 8, midCount: 8);
                var stats = EvaluateStatefulDecodeWithBinAlignment(sequences, pairs, binMap);

                var summary = $"pairs={stats.PairCount}, aligned={stats.AlignedPositions}, " +
                              $"covered={stats.CoveredPositions}, correct={stats.Correct}.";

                var metrics = new Dictionary<string, double>
                {
                    ["pairs"] = stats.PairCount,
                    ["aligned"] = stats.AlignedPositions,
                    ["covered"] = stats.CoveredPositions,
                    ["correct"] = stats.Correct,
                };

                var expected = new Dictionary<string, double>
                {
                    ["pairs"] = 8,
                    ["aligned"] = 153,
                    ["covered"] = 126,
                    ["correct"] = 12,
                };

                var failures = new List<string>();
                foreach (var (key, expectedValue) in expected)
                {
                    if (!metrics.TryGetValue(key, out var observed) || Math.Abs(observed - expectedValue) > 0.001)
                    {
                        failures.Add($"{key}:{observed:0.###}");
                    }
                }

                var passed = failures.Count == 0;
                var finalSummary = passed ? summary : $"Mismatches: {string.Join(", ", failures)}.";
                return new HypothesisResult(passed, finalSummary, metrics);
            }
        ),
        new(
            "Row-pair 8 digit overlap",
            "Compare East/West column overlap for top vs bottom digits.",
            HypothesisExpectation.Pass,
            context =>
            {
                var sequences = new Dictionary<int, int[]>();
                foreach (var message in context.Messages)
                {
                    var seq = GetRowPairColumnSequence(message, rowPair: 8);
                    if (seq.Length > 0)
                    {
                        sequences[message.Id] = seq;
                    }
                }

                var eastIds = new HashSet<int> { 0, 2, 4, 6, 8 };
                var westIds = new HashSet<int> { 1, 3, 5, 7 };

                var eastSeq = sequences.Where(kvp => eastIds.Contains(kvp.Key)).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                var westSeq = sequences.Where(kvp => westIds.Contains(kvp.Key)).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

                var eastTop = eastSeq.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Select(v => v / 5).ToArray());
                var eastBottom = eastSeq.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Select(v => v % 5).ToArray());
                var westTop = westSeq.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Select(v => v / 5).ToArray());
                var westBottom = westSeq.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Select(v => v % 5).ToArray());

                var topSets = ComputeColumnValueSets(eastTop, minCoverage: 4);
                var topWest = ComputeColumnValueSets(westTop, minCoverage: 2);
                var bottomSets = ComputeColumnValueSets(eastBottom, minCoverage: 4);
                var bottomWest = ComputeColumnValueSets(westBottom, minCoverage: 2);

                var topJ = new List<double>();
                foreach (var col in topSets.Keys.Intersect(topWest.Keys))
                {
                    topJ.Add(ComputeJaccard(topSets[col].Values, topWest[col].Values).Jaccard);
                }

                var bottomJ = new List<double>();
                foreach (var col in bottomSets.Keys.Intersect(bottomWest.Keys))
                {
                    bottomJ.Add(ComputeJaccard(bottomSets[col].Values, bottomWest[col].Values).Jaccard);
                }

                var topAvg = topJ.Count == 0 ? 0 : Math.Round(topJ.Average(), 3);
                var bottomAvg = bottomJ.Count == 0 ? 0 : Math.Round(bottomJ.Average(), 3);

                var summary = $"topAvg={topAvg:0.###}, bottomAvg={bottomAvg:0.###}.";

                var metrics = new Dictionary<string, double>
                {
                    ["topAvg"] = topAvg,
                    ["bottomAvg"] = bottomAvg,
                };

                var expected = new Dictionary<string, double>
                {
                    ["topAvg"] = 0.328,
                    ["bottomAvg"] = 0.36,
                };

                var failures = new List<string>();
                foreach (var (key, expectedValue) in expected)
                {
                    if (!metrics.TryGetValue(key, out var observed) || Math.Abs(observed - expectedValue) > 0.001)
                    {
                        failures.Add($"{key}:{observed:0.###}");
                    }
                }

                var passed = failures.Count == 0;
                var finalSummary = passed ? summary : $"Mismatches: {string.Join(", ", failures)}.";
                return new HypothesisResult(passed, finalSummary, metrics);
            }
        ),
        new(
            "Row-pair 8 digit-level decoding",
            "Evaluate run-position decoding separately for top and bottom digits.",
            HypothesisExpectation.Pass,
            context =>
            {
                var sequences = new Dictionary<int, int[]>();
                foreach (var message in context.Messages)
                {
                    var seq = GetRowPairColumnSequence(message, rowPair: 8);
                    if (seq.Length > 0)
                    {
                        sequences[message.Id] = seq;
                    }
                }

                var topSequences = sequences.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Select(v => v / 5).ToArray());
                var bottomSequences = sequences.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Select(v => v % 5).ToArray());

                var pairs = BuildCrossSidePairs(new[] { 0, 2, 4, 6, 8 }, new[] { 1, 3, 5, 7 });

                var topStats = EvaluateStatefulDecodeWithRunPosition(topSequences, pairs, useAlignment: true);
                var bottomStats = EvaluateStatefulDecodeWithRunPosition(bottomSequences, pairs, useAlignment: true);

                var summary = $"top covered={topStats.CoveredPositions}, correct={topStats.Correct}; " +
                              $"bottom covered={bottomStats.CoveredPositions}, correct={bottomStats.Correct}.";

                var metrics = new Dictionary<string, double>
                {
                    ["topCovered"] = topStats.CoveredPositions,
                    ["topCorrect"] = topStats.Correct,
                    ["bottomCovered"] = bottomStats.CoveredPositions,
                    ["bottomCorrect"] = bottomStats.Correct,
                };

                var expected = new Dictionary<string, double>
                {
                    ["topCovered"] = 130,
                    ["topCorrect"] = 53,
                    ["bottomCovered"] = 127,
                    ["bottomCorrect"] = 69,
                };

                var failures = new List<string>();
                foreach (var (key, expectedValue) in expected)
                {
                    if (!metrics.TryGetValue(key, out var observed) || Math.Abs(observed - expectedValue) > 0.001)
                    {
                        failures.Add($"{key}:{observed:0.###}");
                    }
                }

                var passed = failures.Count == 0;
                var finalSummary = passed ? summary : $"Mismatches: {string.Join(", ", failures)}.";
                return new HypothesisResult(passed, finalSummary, metrics);
            }
        ),
        new(
            "Row-pair 8 hybrid constraints decoding",
            "Apply digit + bin constraints to run-position decoding.",
            HypothesisExpectation.Pass,
            context =>
            {
                var sequences = new Dictionary<int, int[]>();
                foreach (var message in context.Messages)
                {
                    var seq = GetRowPairColumnSequence(message, rowPair: 8);
                    if (seq.Length > 0)
                    {
                        sequences[message.Id] = seq;
                    }
                }

                var pairs = BuildCrossSidePairs(new[] { 0, 2, 4, 6, 8 }, new[] { 1, 3, 5, 7 });
                var stats = EvaluateStatefulDecodeWithHybridConstraints(sequences, pairs);

                var summary = $"pairs={stats.PairCount}, aligned={stats.AlignedPositions}, " +
                              $"covered={stats.CoveredPositions}, correct={stats.Correct}.";

                var metrics = new Dictionary<string, double>
                {
                    ["pairs"] = stats.PairCount,
                    ["aligned"] = stats.AlignedPositions,
                    ["covered"] = stats.CoveredPositions,
                    ["correct"] = stats.Correct,
                };

                var expected = new Dictionary<string, double>
                {
                    ["pairs"] = 8,
                    ["aligned"] = 147,
                    ["covered"] = 23,
                    ["correct"] = 9,
                };

                var failures = new List<string>();
                foreach (var (key, expectedValue) in expected)
                {
                    if (!metrics.TryGetValue(key, out var observed) || Math.Abs(observed - expectedValue) > 0.001)
                    {
                        failures.Add($"{key}:{observed:0.###}");
                    }
                }

                var passed = failures.Count == 0;
                var finalSummary = passed ? summary : $"Mismatches: {string.Join(", ", failures)}.";
                return new HypothesisResult(passed, finalSummary, metrics);
            }
        ),
        new(
            "Row-pair 8 conditional digit mapping",
            "Measure bottom|top and top|bottom digit predictability with run-position keys.",
            HypothesisExpectation.Pass,
            context =>
            {
                var sequences = new Dictionary<int, int[]>();
                foreach (var message in context.Messages)
                {
                    var seq = GetRowPairColumnSequence(message, rowPair: 8);
                    if (seq.Length > 0)
                    {
                        sequences[message.Id] = seq;
                    }
                }

                var pairs = BuildCrossSidePairs(new[] { 0, 2, 4, 6, 8 }, new[] { 1, 3, 5, 7 });

                var bottomGivenTop = EvaluateConditionalDigitMapping(
                    sequences,
                    pairs,
                    conditionSelector: value => value / 5,
                    targetSelector: value => value % 5,
                    useAlignment: true);

                var topGivenBottom = EvaluateConditionalDigitMapping(
                    sequences,
                    pairs,
                    conditionSelector: value => value % 5,
                    targetSelector: value => value / 5,
                    useAlignment: true);

                var summary = $"bottom|top covered={bottomGivenTop.CoveredPositions}, correct={bottomGivenTop.Correct}; " +
                              $"top|bottom covered={topGivenBottom.CoveredPositions}, correct={topGivenBottom.Correct}.";

                var metrics = new Dictionary<string, double>
                {
                    ["bottomGivenTopCovered"] = bottomGivenTop.CoveredPositions,
                    ["bottomGivenTopCorrect"] = bottomGivenTop.Correct,
                    ["topGivenBottomCovered"] = topGivenBottom.CoveredPositions,
                    ["topGivenBottomCorrect"] = topGivenBottom.Correct,
                };

                var expected = new Dictionary<string, double>
                {
                    ["bottomGivenTopCovered"] = 98,
                    ["bottomGivenTopCorrect"] = 53,
                    ["topGivenBottomCovered"] = 110,
                    ["topGivenBottomCorrect"] = 51,
                };

                var failures = new List<string>();
                foreach (var (key, expectedValue) in expected)
                {
                    if (!metrics.TryGetValue(key, out var observed) || Math.Abs(observed - expectedValue) > 0.001)
                    {
                        failures.Add($"{key}:{observed:0.###}");
                    }
                }

                var passed = failures.Count == 0;
                var finalSummary = passed ? summary : $"{summary} Mismatches: {string.Join(", ", failures)}.";
                return new HypothesisResult(passed, finalSummary, metrics);
            }
        ),
        new(
            "Row-pair 8 two-stage digit decoding",
            "Combine digit predictions into full values (independent and conditional variants).",
            HypothesisExpectation.Pass,
            context =>
            {
                var sequences = new Dictionary<int, int[]>();
                foreach (var message in context.Messages)
                {
                    var seq = GetRowPairColumnSequence(message, rowPair: 8);
                    if (seq.Length > 0)
                    {
                        sequences[message.Id] = seq;
                    }
                }

                var pairs = BuildCrossSidePairs(new[] { 0, 2, 4, 6, 8 }, new[] { 1, 3, 5, 7 });

                var independentStats = EvaluateTwoStageDigitDecode(
                    sequences,
                    pairs,
                    useAlignment: true,
                    mode: TwoStageDigitMode.Independent);

                var topConditionalStats = EvaluateTwoStageDigitDecode(
                    sequences,
                    pairs,
                    useAlignment: true,
                    mode: TwoStageDigitMode.TopThenConditionalBottom);

                var bottomConditionalStats = EvaluateTwoStageDigitDecode(
                    sequences,
                    pairs,
                    useAlignment: true,
                    mode: TwoStageDigitMode.BottomThenConditionalTop);

                var summary = $"ind covered={independentStats.CoveredPositions}, correct={independentStats.Correct}; " +
                              $"top->bottom covered={topConditionalStats.CoveredPositions}, correct={topConditionalStats.Correct}; " +
                              $"bottom->top covered={bottomConditionalStats.CoveredPositions}, correct={bottomConditionalStats.Correct}.";

                var metrics = new Dictionary<string, double>
                {
                    ["indCovered"] = independentStats.CoveredPositions,
                    ["indCorrect"] = independentStats.Correct,
                    ["topCondCovered"] = topConditionalStats.CoveredPositions,
                    ["topCondCorrect"] = topConditionalStats.Correct,
                    ["bottomCondCovered"] = bottomConditionalStats.CoveredPositions,
                    ["bottomCondCorrect"] = bottomConditionalStats.Correct,
                };

                var expected = new Dictionary<string, double>
                {
                    ["indCovered"] = 114,
                    ["indCorrect"] = 20,
                    ["topCondCovered"] = 116,
                    ["topCondCorrect"] = 21,
                    ["bottomCondCovered"] = 117,
                    ["bottomCondCorrect"] = 27,
                };

                var failures = new List<string>();
                foreach (var (key, expectedValue) in expected)
                {
                    if (!metrics.TryGetValue(key, out var observed) || Math.Abs(observed - expectedValue) > 0.001)
                    {
                        failures.Add($"{key}:{observed:0.###}");
                    }
                }

                var passed = failures.Count == 0;
                var finalSummary = passed ? summary : $"{summary} Mismatches: {string.Join(", ", failures)}.";
                return new HypothesisResult(passed, finalSummary, metrics);
            }
        ),
        new(
            "Row-pair 8 adaptive constraint decoding",
            "Score candidates with bin bonuses instead of hard rejection.",
            HypothesisExpectation.Pass,
            context =>
            {
                var sequences = new Dictionary<int, int[]>();
                foreach (var message in context.Messages)
                {
                    var seq = GetRowPairColumnSequence(message, rowPair: 8);
                    if (seq.Length > 0)
                    {
                        sequences[message.Id] = seq;
                    }
                }

                var pairs = BuildCrossSidePairs(new[] { 0, 2, 4, 6, 8 }, new[] { 1, 3, 5, 7 });
                var stats = EvaluateStatefulDecodeWithAdaptiveConstraints(
                    sequences,
                    pairs,
                    useAlignment: true,
                    binBonus: 1.0);

                var summary = $"pairs={stats.PairCount}, aligned={stats.AlignedPositions}, " +
                              $"covered={stats.CoveredPositions}, correct={stats.Correct}.";

                var metrics = new Dictionary<string, double>
                {
                    ["pairs"] = stats.PairCount,
                    ["aligned"] = stats.AlignedPositions,
                    ["covered"] = stats.CoveredPositions,
                    ["correct"] = stats.Correct,
                };

                var expected = new Dictionary<string, double>
                {
                    ["pairs"] = 8,
                    ["aligned"] = 147,
                    ["covered"] = 114,
                    ["correct"] = 24,
                };

                var failures = new List<string>();
                foreach (var (key, expectedValue) in expected)
                {
                    if (!metrics.TryGetValue(key, out var observed) || Math.Abs(observed - expectedValue) > 0.001)
                    {
                        failures.Add($"{key}:{observed:0.###}");
                    }
                }

                var passed = failures.Count == 0;
                var finalSummary = passed ? summary : $"{summary} Mismatches: {string.Join(", ", failures)}.";
                return new HypothesisResult(passed, finalSummary, metrics);
            }
        ),
        new(
            "Row-pair 8 entropy-gated bin constraints",
            "Apply bin constraints only for low-entropy, high-overlap columns.",
            HypothesisExpectation.Pass,
            context =>
            {
                var sequences = new Dictionary<int, int[]>();
                foreach (var message in context.Messages)
                {
                    var seq = GetRowPairColumnSequence(message, rowPair: 8);
                    if (seq.Length > 0)
                    {
                        sequences[message.Id] = seq;
                    }
                }

                var pairs = BuildCrossSidePairs(new[] { 0, 2, 4, 6, 8 }, new[] { 1, 3, 5, 7 });
                var stats = EvaluateStatefulDecodeWithEntropyGatedBinConstraints(
                    sequences,
                    pairs,
                    useAlignment: true,
                    entropyThreshold: 1.5,
                    overlapThreshold: 0.2);

                var summary = $"pairs={stats.PairCount}, aligned={stats.AlignedPositions}, " +
                              $"covered={stats.CoveredPositions}, correct={stats.Correct}.";

                var metrics = new Dictionary<string, double>
                {
                    ["pairs"] = stats.PairCount,
                    ["aligned"] = stats.AlignedPositions,
                    ["covered"] = stats.CoveredPositions,
                    ["correct"] = stats.Correct,
                };

                var expected = new Dictionary<string, double>
                {
                    ["pairs"] = 8,
                    ["aligned"] = 147,
                    ["covered"] = 112,
                    ["correct"] = 24,
                };

                var failures = new List<string>();
                foreach (var (key, expectedValue) in expected)
                {
                    if (!metrics.TryGetValue(key, out var observed) || Math.Abs(observed - expectedValue) > 0.001)
                    {
                        failures.Add($"{key}:{observed:0.###}");
                    }
                }

                var passed = failures.Count == 0;
                var finalSummary = passed ? summary : $"{summary} Mismatches: {string.Join(", ", failures)}.";
                return new HypothesisResult(passed, finalSummary, metrics);
            }
        ),
        new(
            "Row-pair 8 early vs late decoding",
            "Compare run-position decoding accuracy on early vs late columns.",
            HypothesisExpectation.Pass,
            context =>
            {
                var sequences = new Dictionary<int, int[]>();
                foreach (var message in context.Messages)
                {
                    var seq = GetRowPairColumnSequence(message, rowPair: 8);
                    if (seq.Length > 0)
                    {
                        sequences[message.Id] = seq;
                    }
                }

                var pairs = BuildCrossSidePairs(new[] { 0, 2, 4, 6, 8 }, new[] { 1, 3, 5, 7 });
                var earlyStats = EvaluateStatefulDecodeWithRunPositionWindow(
                    sequences,
                    pairs,
                    useAlignment: true,
                    minIndexInclusive: 0,
                    maxIndexExclusive: 10);
                var lateStats = EvaluateStatefulDecodeWithRunPositionWindow(
                    sequences,
                    pairs,
                    useAlignment: true,
                    minIndexInclusive: 10,
                    maxIndexExclusive: null);

                var summary = $"early covered={earlyStats.CoveredPositions}, correct={earlyStats.Correct}; " +
                              $"late covered={lateStats.CoveredPositions}, correct={lateStats.Correct}.";

                var metrics = new Dictionary<string, double>
                {
                    ["earlyCovered"] = earlyStats.CoveredPositions,
                    ["earlyCorrect"] = earlyStats.Correct,
                    ["lateCovered"] = lateStats.CoveredPositions,
                    ["lateCorrect"] = lateStats.Correct,
                };

                var expected = new Dictionary<string, double>
                {
                    ["earlyCovered"] = 54,
                    ["earlyCorrect"] = 16,
                    ["lateCovered"] = 60,
                    ["lateCorrect"] = 8,
                };

                var failures = new List<string>();
                foreach (var (key, expectedValue) in expected)
                {
                    if (!metrics.TryGetValue(key, out var observed) || Math.Abs(observed - expectedValue) > 0.001)
                    {
                        failures.Add($"{key}:{observed:0.###}");
                    }
                }

                var passed = failures.Count == 0;
                var finalSummary = passed ? summary : $"{summary} Mismatches: {string.Join(", ", failures)}.";
                return new HypothesisResult(passed, finalSummary, metrics);
            }
        ),
        new(
            "Row-pair 8 rhythm-anchored alignment",
            "Align row-pair 8 sequences using body rhythm anchor offsets.",
            HypothesisExpectation.Pass,
            context =>
            {
                var sequences = new Dictionary<int, int[]>();
                foreach (var message in context.Messages)
                {
                    var seq = GetRowPairColumnSequence(message, rowPair: 8);
                    if (seq.Length > 0)
                    {
                        sequences[message.Id] = seq;
                    }
                }

                var anchorStarts = new Dictionary<int, int>();
                foreach (var message in context.Messages)
                {
                    if (TryGetRowPairRunStartIndex(message, rowPair: 8, out var startIndex))
                    {
                        anchorStarts[message.Id] = startIndex;
                    }
                }

                var pairs = BuildCrossSidePairs(new[] { 0, 2, 4, 6, 8 }, new[] { 1, 3, 5, 7 });
                var stats = EvaluateStatefulDecodeWithAnchorOffsets(sequences, pairs, anchorStarts);

                var summary = $"pairs={stats.PairCount}, aligned={stats.AlignedPositions}, " +
                              $"covered={stats.CoveredPositions}, correct={stats.Correct}.";

                var metrics = new Dictionary<string, double>
                {
                    ["pairs"] = stats.PairCount,
                    ["aligned"] = stats.AlignedPositions,
                    ["covered"] = stats.CoveredPositions,
                    ["correct"] = stats.Correct,
                };

                var expected = new Dictionary<string, double>
                {
                    ["pairs"] = 8,
                    ["aligned"] = 170,
                    ["covered"] = 164,
                    ["correct"] = 14,
                };

                var failures = new List<string>();
                foreach (var (key, expectedValue) in expected)
                {
                    if (!metrics.TryGetValue(key, out var observed) || Math.Abs(observed - expectedValue) > 0.001)
                    {
                        failures.Add($"{key}:{observed:0.###}");
                    }
                }

                var passed = failures.Count == 0;
                var finalSummary = passed ? summary : $"{summary} Mismatches: {string.Join(", ", failures)}.";
                return new HypothesisResult(passed, finalSummary, metrics);
            }
        ),
        new(
            "HMM 3-state log-likelihood (header/body init)",
            "Train a 3-state HMM with header/body initialization.",
            HypothesisExpectation.Pass,
            context =>
            {
                var sequences = context.WeaveTrigrams
                    .OrderBy(kvp => kvp.Key)
                    .Select(kvp => kvp.Value.Select(t => t.Base10Value).ToArray())
                    .ToList();

                var model = TrainHmmWithHeaderBodyInit(sequences, states: 3, symbols: 83, iterations: 5);
                var hmmLogLik = ComputeHmmLogLikelihood(model, sequences);
                var tokenCount = sequences.Sum(s => s.Length);
                var hmmPerToken = Math.Round(hmmLogLik / tokenCount, 4);

                var unigramLogLik = ComputeUnigramLogLikelihood(sequences, symbols: 83);
                var unigramPerToken = Math.Round(unigramLogLik / tokenCount, 4);

                var summary = $"hmmPerToken={hmmPerToken:0.####}, unigramPerToken={unigramPerToken:0.####}.";

                var metrics = new Dictionary<string, double>
                {
                    ["hmmPerToken"] = hmmPerToken,
                    ["unigramPerToken"] = unigramPerToken,
                };

                var expected = new Dictionary<string, double>
                {
                    ["hmmPerToken"] = -25.9105,
                    ["unigramPerToken"] = -4.3478,
                };

                var failures = new List<string>();
                foreach (var (key, expectedValue) in expected)
                {
                    if (!metrics.TryGetValue(key, out var observed) || Math.Abs(observed - expectedValue) > 0.001)
                    {
                        failures.Add($"{key}:{observed:0.####}");
                    }
                }

                var passed = failures.Count == 0;
                var finalSummary = passed ? summary : $"Mismatches: {string.Join(", ", failures)}.";
                return new HypothesisResult(passed, finalSummary, metrics);
            }
        ),
        new(
            "HMM 4-state log-likelihood (header/body init)",
            "Train a 4-state HMM with header/body initialization.",
            HypothesisExpectation.Pass,
            context =>
            {
                var sequences = context.WeaveTrigrams
                    .OrderBy(kvp => kvp.Key)
                    .Select(kvp => kvp.Value.Select(t => t.Base10Value).ToArray())
                    .ToList();

                var model = TrainHmmWithHeaderBodyInit(sequences, states: 4, symbols: 83, iterations: 5);
                var hmmLogLik = ComputeHmmLogLikelihood(model, sequences);
                var tokenCount = sequences.Sum(s => s.Length);
                var hmmPerToken = Math.Round(hmmLogLik / tokenCount, 4);

                var unigramLogLik = ComputeUnigramLogLikelihood(sequences, symbols: 83);
                var unigramPerToken = Math.Round(unigramLogLik / tokenCount, 4);

                var summary = $"hmmPerToken={hmmPerToken:0.####}, unigramPerToken={unigramPerToken:0.####}.";

                var metrics = new Dictionary<string, double>
                {
                    ["hmmPerToken"] = hmmPerToken,
                    ["unigramPerToken"] = unigramPerToken,
                };

                var expected = new Dictionary<string, double>
                {
                    ["hmmPerToken"] = -25.9335,
                    ["unigramPerToken"] = -4.3478,
                };

                var failures = new List<string>();
                foreach (var (key, expectedValue) in expected)
                {
                    if (!metrics.TryGetValue(key, out var observed) || Math.Abs(observed - expectedValue) > 0.001)
                    {
                        failures.Add($"{key}:{observed:0.####}");
                    }
                }

                var passed = failures.Count == 0;
                var finalSummary = passed ? summary : $"Mismatches: {string.Join(", ", failures)}.";
                return new HypothesisResult(passed, finalSummary, metrics);
            }
        ),
        new(
            "Top body trigram path templates (length 4)",
            "Extract top length-4 trigram paths restricted to body indices.",
            HypothesisExpectation.Pass,
            context =>
            {
                var sequences = new Dictionary<int, int[]>();
                foreach (var (id, trigrams) in context.WeaveTrigrams)
                {
                    var seq = trigrams.Select(t => t.Base10Value).ToArray();
                    if (!TryGetHeaderEndIndex(seq, out var headerEndIndex, out _))
                    {
                        continue;
                    }

                    var body = seq.Skip(headerEndIndex + 1).ToArray();
                    sequences[id] = body;
                }

                var motifStats = new Dictionary<string, (int Count, HashSet<int> Ids, List<int> Positions)>();
                foreach (var (id, seq) in sequences)
                {
                    if (seq.Length < 4)
                    {
                        continue;
                    }

                    for (var i = 0; i <= seq.Length - 4; i++)
                    {
                        var motif = string.Join("-", seq.Skip(i).Take(4));
                        if (!motifStats.TryGetValue(motif, out var entry))
                        {
                            entry = (0, new HashSet<int>(), new List<int>());
                        }

                        entry.Count += 1;
                        entry.Ids.Add(id);
                        entry.Positions.Add(i);
                        motifStats[motif] = entry;
                    }
                }

                var top = motifStats
                    .OrderByDescending(kvp => kvp.Value.Count)
                    .ThenBy(kvp => kvp.Key)
                    .Take(3)
                    .ToList();

                var summaries = new List<string>();
                foreach (var (motif, stats) in top)
                {
                    var avgPos = stats.Positions.Count == 0 ? 0 : Math.Round(stats.Positions.Average(), 2);
                    summaries.Add($"{motif} (count={stats.Count}, coverage={stats.Ids.Count}, avgPos={avgPos:0.##})");
                }

                var summary = $"top={string.Join("; ", summaries)}.";
                var metrics = new Dictionary<string, double>
                {
                    ["topCount"] = top.Count,
                };

                var expected = new[]
                {
                    "2-60-29-40 (count=4, coverage=4, avgPos=0)",
                    "14-81-8-15 (count=3, coverage=3, avgPos=9)",
                    "15-59-18-68 (count=3, coverage=3, avgPos=7)",
                };

                var failures = new List<string>();
                for (var i = 0; i < expected.Length; i++)
                {
                    if (i >= summaries.Count || summaries[i] != expected[i])
                    {
                        failures.Add($"top{i}:{(i < summaries.Count ? summaries[i] : "missing")}");
                    }
                }

                var passed = failures.Count == 0;
                var finalSummary = passed ? summary : $"Mismatches: {string.Join(", ", failures)}.";
                return new HypothesisResult(passed, finalSummary, metrics);
            }
        ),
        new(
            "Shared order-2 grammar contexts",
            "Identify order-2 contexts shared across messages and evaluate their predictive power.",
            HypothesisExpectation.Pass,
            context =>
            {
                var sequences = context.WeaveTrigrams
                    .OrderBy(kvp => kvp.Key)
                    .ToDictionary(
                        kvp => kvp.Key,
                        kvp => kvp.Value.Select(t => t.Base10Value).ToArray());

                var sharedContexts = BuildSharedOrder2Contexts(sequences, minMessageCoverage: 3);
                var coverageStats = ComputeSharedContextCoverage(sequences, sharedContexts);
                var predictionStats = EvaluateSharedContextPrediction(sequences, sharedContexts);
                var enrichment = ComputeSharedContextEnrichment(sequences, sharedContexts);

                var summary = $"contexts={sharedContexts.Count}, coverage={coverageStats.CoverageRate:0.###}, " +
                              $"pred={predictionStats.Correct}/{predictionStats.Covered}, " +
                              $"headerShare={enrichment.SharedHeaderShare:0.###}, baseline={enrichment.BaselineHeaderShare:0.###}, " +
                              $"ratio={enrichment.EnrichmentRatio:0.###}.";

                var metrics = new Dictionary<string, double>
                {
                    ["contexts"] = sharedContexts.Count,
                    ["coverage"] = coverageStats.CoverageRate,
                    ["predCovered"] = predictionStats.Covered,
                    ["predCorrect"] = predictionStats.Correct,
                    ["sharedHeaderShare"] = enrichment.SharedHeaderShare,
                    ["baselineHeaderShare"] = enrichment.BaselineHeaderShare,
                    ["enrichmentRatio"] = enrichment.EnrichmentRatio,
                };

                var expected = new Dictionary<string, double>
                {
                    ["contexts"] = 47,
                    ["coverage"] = 0.162,
                    ["predCovered"] = 165,
                    ["predCorrect"] = 132,
                    ["sharedHeaderShare"] = 0.164,
                    ["baselineHeaderShare"] = 0.035,
                    ["enrichmentRatio"] = 4.686,
                };

                var failures = new List<string>();
                foreach (var (key, expectedValue) in expected)
                {
                    if (!metrics.TryGetValue(key, out var observed) || Math.Abs(observed - expectedValue) > 0.001)
                    {
                        failures.Add($"{key}:{observed:0.###}");
                    }
                }

                var passed = failures.Count == 0;
                var finalSummary = passed ? summary : $"{summary} Mismatches: {string.Join(", ", failures)}.";
                return new HypothesisResult(passed, finalSummary, metrics);
            }
        ),
        new(
            "No adjacent trigram repeats",
            "No trigram value appears twice in a row within a message.",
            HypothesisExpectation.Pass,
            context =>
            {
                var repeats = new List<int>();
                foreach (var (id, trigrams) in context.WeaveTrigrams)
                {
                    if (TrigramAnalysis.HasAdjacentRepeats(trigrams))
                    {
                        repeats.Add(id);
                    }
                }

                var passed = repeats.Count == 0;
                var summary = repeats.Count == 0
                    ? "No adjacent repeats found."
                    : $"Adjacent repeats in messages: {string.Join(", ", repeats)}.";
                return new HypothesisResult(passed, summary);
            }
        ),
        new(
            "Starting trigrams are > 26",
            "First trigram value in each message is greater than 26.",
            HypothesisExpectation.Pass,
            context =>
            {
                var failures = new List<int>();
                foreach (var (id, trigrams) in context.WeaveTrigrams)
                {
                    if (trigrams.Count == 0 || trigrams[0].Base10Value <= 26)
                    {
                        failures.Add(id);
                    }
                }

                var passed = failures.Count == 0;
                var summary = failures.Count == 0
                    ? "All starting trigrams > 26."
                    : $"Failures: {string.Join(", ", failures)}.";
                return new HypothesisResult(passed, summary);
            }
        ),
        new(
            "Source glyph messages match engine data",
            "Validate that the source glyph message file matches the engine messages.",
            HypothesisExpectation.Pass,
            context =>
            {
                var source = LoadSourceGlyphMessages();
                var mismatches = new List<string>();

                foreach (var message in context.Messages)
                {
                    if (!source.TryGetValue(message.Id, out var lines))
                    {
                        mismatches.Add($"{message.Id}:missing");
                        continue;
                    }

                    var engineLines = message.Lines.Select(l => new string(l.Where(char.IsDigit).ToArray())).ToArray();
                    if (engineLines.Length != lines.Length)
                    {
                        mismatches.Add($"{message.Id}:len {engineLines.Length}!={lines.Length}");
                        continue;
                    }

                    for (var i = 0; i < engineLines.Length; i++)
                    {
                        if (engineLines[i] != lines[i])
                        {
                            mismatches.Add($"{message.Id}:line{i}");
                            break;
                        }
                    }
                }

                foreach (var id in source.Keys.Except(context.Messages.Select(m => m.Id)))
                {
                    mismatches.Add($"{id}:extra");
                }

                var summary = mismatches.Count == 0
                    ? $"sourceCount={source.Count}, engineCount={context.Messages.Count}, mismatches=0."
                    : $"sourceCount={source.Count}, engineCount={context.Messages.Count}, mismatches={mismatches.Count}.";

                var metrics = new Dictionary<string, double>
                {
                    ["sourceCount"] = source.Count,
                    ["engineCount"] = context.Messages.Count,
                    ["mismatchCount"] = mismatches.Count,
                };

                var expected = new Dictionary<string, double>
                {
                    ["sourceCount"] = 9,
                    ["engineCount"] = 9,
                    ["mismatchCount"] = 0,
                };

                var failures = new List<string>();
                foreach (var (key, expectedValue) in expected)
                {
                    if (!metrics.TryGetValue(key, out var observed) || Math.Abs(observed - expectedValue) > 0.001)
                    {
                        failures.Add($"{key}:{observed:0.###}");
                    }
                }

                var passed = failures.Count == 0 && mismatches.Count == 0;
                var finalSummary = passed ? summary : $"{summary} Mismatches: {string.Join(", ", mismatches)}.";
                return new HypothesisResult(passed, finalSummary, metrics);
            }
        ),
        new(
            "All glyph digits are within 0-4",
            "Validate that glyph digits are within the expected range across all messages.",
            HypothesisExpectation.Pass,
            context =>
            {
                var invalidCount = 0;
                foreach (var message in context.Messages)
                {
                    foreach (var line in message.Lines)
                    {
                        foreach (var ch in line)
                        {
                            if (ch < '0' || ch > '4')
                            {
                                invalidCount++;
                            }
                        }
                    }
                }

                var passed = invalidCount == 0;
                var summary = $"Invalid glyphs found: {invalidCount}.";
                return new HypothesisResult(passed, summary, new Dictionary<string, double>
                {
                    ["invalid"] = invalidCount,
                });
            }
        )
    };

    private static int Gcd(int a, int b)
    {
        a = Math.Abs(a);
        b = Math.Abs(b);
        while (b != 0)
        {
            var temp = a % b;
            a = b;
            b = temp;
        }

        return a;
    }

    private static Dictionary<int, int> GetRowPairCounts(EyeMessage message, HashSet<int> core)
    {
        var occurrences = TrigramWeaver.BuildWeaveOccurrences(message, TrigramScheme.Canonical);
        var counts = new Dictionary<int, int>();

        foreach (var occurrence in occurrences)
        {
            if (!core.Contains(occurrence.Base10Value))
            {
                continue;
            }

            counts[occurrence.RowPair] = counts.TryGetValue(occurrence.RowPair, out var existing)
                ? existing + 1
                : 1;
        }

        return counts;
    }

    private static List<(int Index, int RowPair, int From, int To)> GetTransitions(EyeMessage message)
    {
        var occurrences = TrigramWeaver.BuildWeaveOccurrences(message, TrigramScheme.Canonical);
        var transitions = new List<(int Index, int RowPair, int From, int To)>();

        for (var i = 0; i < occurrences.Count - 1; i++)
        {
            transitions.Add((i, occurrences[i].RowPair, occurrences[i].Base10Value, occurrences[i + 1].Base10Value));
        }

        return transitions;
    }

    private static int L1Distance(IReadOnlyDictionary<int, int> a, IReadOnlyDictionary<int, int> b)
    {
        var sum = 0;
        var keys = a.Keys.Concat(b.Keys).Distinct();
        foreach (var key in keys)
        {
            var av = a.TryGetValue(key, out var aValue) ? aValue : 0;
            var bv = b.TryGetValue(key, out var bValue) ? bValue : 0;
            sum += Math.Abs(av - bv);
        }

        return sum;
    }

    private static Dictionary<int, int> GetBodyRowPairCounts(
        EyeMessage message,
        out int headerEndIndex,
        out char motif)
    {
        var occurrences = TrigramWeaver.BuildWeaveOccurrences(message, TrigramScheme.Canonical);
        var seq = occurrences.Select(o => o.Base10Value).ToArray();
        if (!TryGetHeaderEndIndex(seq, out headerEndIndex, out motif))
        {
            return new Dictionary<int, int>();
        }

        var counts = new Dictionary<int, int>();
        foreach (var occ in occurrences.Skip(headerEndIndex + 1))
        {
            counts[occ.RowPair] = counts.TryGetValue(occ.RowPair, out var existing)
                ? existing + 1
                : 1;
        }

        return counts;
    }

    private static Dictionary<(int From, int To), int> GetBodyRowPairTransitions(EyeMessage message)
    {
        var sequence = GetBodyRowPairSequence(message);
        var transitions = new Dictionary<(int From, int To), int>();
        for (var i = 0; i < sequence.Length - 1; i++)
        {
            var key = (sequence[i], sequence[i + 1]);
            transitions[key] = transitions.TryGetValue(key, out var existing) ? existing + 1 : 1;
        }

        return transitions;
    }

    private static List<(int Value, int Length)> GetBodyRowPairRuns(EyeMessage message)
    {
        var sequence = GetBodyRowPairSequence(message);
        var runs = new List<(int Value, int Length)>();
        if (sequence.Length == 0)
        {
            return runs;
        }

        var current = sequence[0];
        var length = 1;
        for (var i = 1; i < sequence.Length; i++)
        {
            if (sequence[i] == current)
            {
                length++;
            }
            else
            {
                runs.Add((current, length));
                current = sequence[i];
                length = 1;
            }
        }

        runs.Add((current, length));
        return runs;
    }

    private sealed record BodyMetrics(int BodyLength, int Sum, int Unique, int Run6, int Run8, int Run10);

    private sealed record BodyGroupStats(int TotalCount, int Run8Total, int Run10Total, int Run8NonZero, int Run10NonZero);

    private sealed record BodyBlockRange(
        int RowPair,
        int TopMin,
        int TopMax,
        int TopCount,
        bool TopContiguous,
        int BottomMin,
        int BottomMax,
        int BottomCount,
        bool BottomContiguous);

    private sealed record ColumnStat(int Count, int Unique, double Entropy, double MaxProb);

    private sealed record ColumnValueSet(int Count, int[] Values);

    private sealed record MotifCoverage(string Motif, int Coverage, int Length);

    private sealed record HmmModel(double[] Pi, double[,] A, double[,] B);

    private sealed record CellEntropyStat(
        int Row,
        int Col,
        int Coverage,
        double Entropy,
        double MaxProb,
        int[] MaxValues);

    private sealed record CellValueSet(
        int Coverage,
        int[] Values);

    private sealed record ColumnSubstitutionConflict(int Index, int EastValue, int[] WestValues);

    private sealed record ColumnSubstitutionStats(
        int Conflicts,
        int Mappings,
        int PairCount,
        int AlignedPositions,
        IReadOnlyList<ColumnSubstitutionConflict> ConflictDetails);

    private sealed record ColumnSubstitutionConflictWithPrev(int Index, int EastValue, int? PrevEastValue, int[] WestValues);

    private sealed record ColumnSubstitutionStatsWithPrev(
        int Conflicts,
        int Mappings,
        int PairCount,
        int AlignedPositions,
        IReadOnlyList<ColumnSubstitutionConflictWithPrev> ConflictDetails);

    private sealed record ColumnSubstitutionConflictWithPrevWest(int Index, int EastValue, int? PrevWestValue, int[] WestValues);

    private sealed record ColumnSubstitutionStatsWithPrevWest(
        int Conflicts,
        int Mappings,
        int PairCount,
        int AlignedPositions,
        IReadOnlyList<ColumnSubstitutionConflictWithPrevWest> ConflictDetails);

    private sealed record ColumnSubstitutionConflictWithPrevBoth(
        int Index,
        int EastValue,
        int? PrevEastValue,
        int? PrevWestValue,
        int[] WestValues);

    private sealed record ColumnSubstitutionStatsWithPrevBoth(
        int Conflicts,
        int Mappings,
        int PairCount,
        int AlignedPositions,
        IReadOnlyList<ColumnSubstitutionConflictWithPrevBoth> ConflictDetails);

    private sealed record StatefulDecodeStats(
        int PairCount,
        int AlignedPositions,
        int CoveredPositions,
        int Correct);

    private sealed record BucketDecodeStats(int Aligned, int Covered, int Correct);

    private sealed record BucketedDecodeStats(
        int PairCount,
        int AlignedPositions,
        int CoveredPositions,
        int Correct,
        IReadOnlyList<BucketDecodeStats> Buckets);

    private sealed record EarlyDecodeStats(int PairCount, int Aligned, int Covered, int Correct);

    private sealed record EarlyDecodeBaselineStats(
        int PairCount,
        int Aligned,
        int Covered,
        int Correct,
        double BaselineMeanCorrect);

    private sealed record SharedContextCoverageStats(double CoverageRate, int SharedPositions, int TotalPositions);

    private sealed record SharedContextPredictionStats(int Covered, int Correct);

    private sealed record SharedContextEnrichmentStats(double SharedHeaderShare, double BaselineHeaderShare, double EnrichmentRatio);

    private enum TwoStageDigitMode
    {
        Independent,
        TopThenConditionalBottom,
        BottomThenConditionalTop
    }

    private static BodyMetrics GetBodyMetrics(EyeMessage message)
    {
        var trigrams = TrigramWeaver.BuildWeaveTrigrams(message, TrigramScheme.Canonical);
        var sum = trigrams.Sum(t => t.Base10Value);
        var unique = trigrams.Select(t => t.Base10Value).Distinct().Count();
        var sequence = GetBodyRowPairSequence(message);
        var bodyLength = sequence.Length;
        var run6 = 0;
        var run8 = 0;
        var run10 = 0;
        foreach (var (value, length) in GetBodyRowPairRuns(message))
        {
            if (value == 6) run6 = length;
            if (value == 8) run8 = length;
            if (value == 10) run10 = length;
        }

        return new BodyMetrics(bodyLength, sum, unique, run6, run8, run10);
    }

    private static BodyGroupStats AggregateGroup(IEnumerable<BodyMetrics> metrics)
    {
        var total = 0;
        var run8 = 0;
        var run10 = 0;
        var run8NonZero = 0;
        var run10NonZero = 0;

        foreach (var metric in metrics)
        {
            total++;
            run8 += metric.Run8;
            run10 += metric.Run10;
            if (metric.Run8 > 0) run8NonZero++;
            if (metric.Run10 > 0) run10NonZero++;
        }

        return new BodyGroupStats(total, run8, run10, run8NonZero, run10NonZero);
    }

    private static List<BodyBlockRange> GetBodyBlockRanges(EyeMessage message)
    {
        var placements = TrigramWeaver.BuildWeavePlacements(message, TrigramScheme.Canonical);
        var values = placements.Select(p => p.Base10Value).ToArray();
        if (!TryGetHeaderEndIndex(values, out var headerEndIndex, out _))
        {
            return new List<BodyBlockRange>();
        }

        var body = placements.Skip(headerEndIndex + 1).ToArray();
        var blocks = new List<List<TrigramPlacement>>();
        var current = new List<TrigramPlacement>();
        var currentRowPair = -1;
        foreach (var placement in body)
        {
            if (currentRowPair == -1 || placement.RowPair == currentRowPair)
            {
                currentRowPair = placement.RowPair;
                current.Add(placement);
            }
            else
            {
                blocks.Add(current);
                current = new List<TrigramPlacement> { placement };
                currentRowPair = placement.RowPair;
            }
        }

        if (current.Count > 0)
        {
            blocks.Add(current);
        }

        var ranges = new List<BodyBlockRange>();
        foreach (var block in blocks)
        {
            var rowPair = block[0].RowPair;
            var topRow = rowPair;
            var bottomRow = rowPair + 1;
            var topCols = block.SelectMany(p => new[] { p.VertexA, p.VertexB, p.VertexC })
                .Where(v => v.Row == topRow)
                .Select(v => v.Column)
                .Distinct()
                .OrderBy(v => v)
                .ToArray();
            var bottomCols = block.SelectMany(p => new[] { p.VertexA, p.VertexB, p.VertexC })
                .Where(v => v.Row == bottomRow)
                .Select(v => v.Column)
                .Distinct()
                .OrderBy(v => v)
                .ToArray();

            ranges.Add(new BodyBlockRange(
                rowPair,
                topCols.First(),
                topCols.Last(),
                topCols.Length,
                IsContiguous(topCols),
                bottomCols.First(),
                bottomCols.Last(),
                bottomCols.Length,
                IsContiguous(bottomCols)));
        }

        return ranges;
    }

    private static bool IsContiguous(IReadOnlyList<int> values)
    {
        if (values.Count == 0)
        {
            return true;
        }

        for (var i = 1; i < values.Count; i++)
        {
            if (values[i] != values[i - 1] + 1)
            {
                return false;
            }
        }

        return true;
    }

    private static int[] GetRowPairColumnSequence(EyeMessage message, int rowPair)
    {
        var placements = TrigramWeaver.BuildWeavePlacements(message, TrigramScheme.Canonical);
        var values = placements.Select(p => p.Base10Value).ToArray();
        if (!TryGetHeaderEndIndex(values, out var headerEndIndex, out _))
        {
            return Array.Empty<int>();
        }

        var body = placements.Skip(headerEndIndex + 1).ToArray();
        var blocks = new List<List<TrigramPlacement>>();
        var current = new List<TrigramPlacement>();
        var currentRowPair = -1;
        foreach (var placement in body)
        {
            if (currentRowPair == -1 || placement.RowPair == currentRowPair)
            {
                currentRowPair = placement.RowPair;
                current.Add(placement);
            }
            else
            {
                blocks.Add(current);
                current = new List<TrigramPlacement> { placement };
                currentRowPair = placement.RowPair;
            }
        }

        if (current.Count > 0)
        {
            blocks.Add(current);
        }

        foreach (var block in blocks)
        {
            if (block[0].RowPair != rowPair)
            {
                continue;
            }

            var topRow = rowPair;
            var bottomRow = rowPair + 1;
            var topCols = block.SelectMany(p => new[] { p.VertexA, p.VertexB, p.VertexC })
                .Where(v => v.Row == topRow)
                .Select(v => v.Column)
                .Distinct()
                .OrderBy(v => v)
                .ToArray();
            var bottomCols = block.SelectMany(p => new[] { p.VertexA, p.VertexB, p.VertexC })
                .Where(v => v.Row == bottomRow)
                .Select(v => v.Column)
                .Distinct()
                .OrderBy(v => v)
                .ToArray();

            var commonCols = topCols.Intersect(bottomCols).OrderBy(v => v).ToArray();
            if (commonCols.Length == 0)
            {
                return Array.Empty<int>();
            }

            var topLine = message.Lines[topRow];
            var bottomLine = message.Lines[bottomRow];
            var sequence = new List<int>();
            foreach (var col in commonCols)
            {
                if (col >= topLine.Length || col >= bottomLine.Length)
                {
                    continue;
                }

                var top = topLine[col] - '0';
                var bottom = bottomLine[col] - '0';
                sequence.Add((top * 5) + bottom);
            }

            return sequence.ToArray();
        }

        return Array.Empty<int>();
    }

    private static int CountSharedMotifs(Dictionary<int, int[]> sequences, int length)
    {
        var motifToIds = new Dictionary<string, HashSet<int>>();
        foreach (var (id, sequence) in sequences)
        {
            if (sequence.Length < length)
            {
                continue;
            }

            for (var i = 0; i <= sequence.Length - length; i++)
            {
                var motif = string.Join("-", sequence.Skip(i).Take(length));
                if (!motifToIds.TryGetValue(motif, out var ids))
                {
                    ids = new HashSet<int>();
                    motifToIds[motif] = ids;
                }

                ids.Add(id);
            }
        }

        return motifToIds.Count(kvp => kvp.Value.Count >= 2);
    }

    private static Dictionary<int, int> BuildLengthBuckets(Dictionary<int, int[]> sequences, int bucketCount)
    {
        var ordered = sequences
            .Select(kvp => (Id: kvp.Key, Length: kvp.Value.Length))
            .OrderBy(item => item.Length)
            .ThenBy(item => item.Id)
            .ToList();

        var bucketSize = Math.Max(1, ordered.Count / bucketCount);
        var buckets = new Dictionary<int, int>();
        for (var i = 0; i < ordered.Count; i++)
        {
            var bucket = Math.Min(bucketCount - 1, i / bucketSize);
            buckets[ordered[i].Id] = bucket;
        }

        return buckets;
    }

    private static int GetNormalizedBucket(int index, int length, int bucketCount)
    {
        if (bucketCount <= 1 || length <= 1)
        {
            return 0;
        }

        var ratio = index / (double)(length - 1);
        var bucket = (int)Math.Floor(ratio * bucketCount);
        if (bucket < 0)
        {
            return 0;
        }

        return bucket >= bucketCount ? bucketCount - 1 : bucket;
    }

    private static Dictionary<int, int> BuildFrequencyBins(IEnumerable<int[]> sequences, int highCount, int midCount)
    {
        var counts = Enumerable.Range(0, 25).ToDictionary(v => v, _ => 0);
        foreach (var sequence in sequences)
        {
            foreach (var value in sequence)
            {
                counts[value]++;
            }
        }

        var ordered = counts
            .OrderByDescending(kvp => kvp.Value)
            .ThenBy(kvp => kvp.Key)
            .Select(kvp => kvp.Key)
            .ToList();

        var bins = new Dictionary<int, int>();
        for (var i = 0; i < ordered.Count; i++)
        {
            var value = ordered[i];
            bins[value] = i < highCount ? 2
                : i < highCount + midCount ? 1
                : 0;
        }

        return bins;
    }

    private static Dictionary<int, Dictionary<int, int>> BuildColumnFrequencyBins(
        IEnumerable<int[]> sequences,
        int highCount,
        int midCount)
    {
        var countsByColumn = new Dictionary<int, Dictionary<int, int>>();
        foreach (var sequence in sequences)
        {
            for (var i = 0; i < sequence.Length; i++)
            {
                if (!countsByColumn.TryGetValue(i, out var counts))
                {
                    counts = new Dictionary<int, int>();
                    countsByColumn[i] = counts;
                }

                var value = sequence[i];
                counts[value] = counts.TryGetValue(value, out var count) ? count + 1 : 1;
            }
        }

        var binsByColumn = new Dictionary<int, Dictionary<int, int>>();
        foreach (var (col, counts) in countsByColumn)
        {
            var ordered = counts
                .OrderByDescending(kvp => kvp.Value)
                .ThenBy(kvp => kvp.Key)
                .Select(kvp => kvp.Key)
                .ToList();

            var bins = new Dictionary<int, int>();
            for (var i = 0; i < ordered.Count; i++)
            {
                var value = ordered[i];
                bins[value] = i < highCount ? 2
                    : i < highCount + midCount ? 1
                    : 0;
            }

            binsByColumn[col] = bins;
        }

        return binsByColumn;
    }

    private static int[] ApplyColumnBins(int[] sequence, IReadOnlyDictionary<int, Dictionary<int, int>> columnBins)
    {
        var binned = new int[sequence.Length];
        for (var i = 0; i < sequence.Length; i++)
        {
            if (columnBins.TryGetValue(i, out var bins) && bins.TryGetValue(sequence[i], out var bin))
            {
                binned[i] = bin;
            }
            else
            {
                binned[i] = 0;
            }
        }

        return binned;
    }

    private static Dictionary<int, Dictionary<int, int>> ComputeColumnValueCounts(Dictionary<int, int[]> sequences)
    {
        var columns = new Dictionary<int, Dictionary<int, int>>();
        foreach (var sequence in sequences.Values)
        {
            for (var i = 0; i < sequence.Length; i++)
            {
                if (!columns.TryGetValue(i, out var counts))
                {
                    counts = new Dictionary<int, int>();
                    columns[i] = counts;
                }

                var value = sequence[i];
                counts[value] = counts.TryGetValue(value, out var count) ? count + 1 : 1;
            }
        }

        return columns;
    }

    private static int GetModeBin(Dictionary<int, int> counts)
    {
        var bins = new[] { 2, 1, 0 };
        var bestBin = bins[0];
        var bestCount = -1;
        foreach (var bin in bins)
        {
            var count = counts.TryGetValue(bin, out var value) ? value : 0;
            if (count > bestCount)
            {
                bestCount = count;
                bestBin = bin;
            }
        }

        return bestBin;
    }

    private static int[] GetTopBins(Dictionary<int, int> counts, int topN)
    {
        var ordered = counts
            .Where(kvp => kvp.Value > 0)
            .OrderByDescending(kvp => kvp.Value)
            .ThenByDescending(kvp => kvp.Key)
            .Select(kvp => kvp.Key)
            .ToList();

        if (ordered.Count < topN)
        {
            var fallback = new[] { 2, 1, 0 };
            foreach (var bin in fallback)
            {
                if (ordered.Count >= topN)
                {
                    break;
                }

                if (!ordered.Contains(bin))
                {
                    ordered.Add(bin);
                }
            }
        }

        return ordered.Take(topN).ToArray();
    }

    private static MotifCoverage GetTopMotifCoverage(Dictionary<int, int[]> sequences, int length)
    {
        var motifToIds = new Dictionary<string, HashSet<int>>();
        foreach (var (id, sequence) in sequences)
        {
            if (sequence.Length < length)
            {
                continue;
            }

            for (var i = 0; i <= sequence.Length - length; i++)
            {
                var motif = string.Join("-", sequence.Skip(i).Take(length));
                if (!motifToIds.TryGetValue(motif, out var ids))
                {
                    ids = new HashSet<int>();
                    motifToIds[motif] = ids;
                }

                ids.Add(id);
            }
        }

        if (motifToIds.Count == 0)
        {
            return new MotifCoverage(string.Empty, 0, length);
        }

        var best = motifToIds
            .OrderByDescending(kvp => kvp.Value.Count)
            .ThenBy(kvp => kvp.Key)
            .First();

        return new MotifCoverage(best.Key, best.Value.Count, length);
    }

    private static int CountNearSharedMotifs(Dictionary<int, int[]> sequences, int length, int maxDistance)
    {
        var motifs = new List<(int Id, int[] Values, string Key)>();
        foreach (var (id, sequence) in sequences)
        {
            if (sequence.Length < length)
            {
                continue;
            }

            for (var i = 0; i <= sequence.Length - length; i++)
            {
                var values = sequence.Skip(i).Take(length).ToArray();
                var key = string.Join("-", values);
                motifs.Add((id, values, key));
            }
        }

        var matched = new HashSet<string>();
        for (var i = 0; i < motifs.Count; i++)
        {
            for (var j = i + 1; j < motifs.Count; j++)
            {
                if (motifs[i].Id == motifs[j].Id)
                {
                    continue;
                }

                if (HammingDistance(motifs[i].Values, motifs[j].Values, maxDistance) <= maxDistance)
                {
                    matched.Add(motifs[i].Key);
                    matched.Add(motifs[j].Key);
                }
            }
        }

        return matched.Count;
    }

    private static int HammingDistance(int[] left, int[] right, int maxDistance)
    {
        var distance = 0;
        for (var i = 0; i < left.Length; i++)
        {
            if (left[i] != right[i])
            {
                distance++;
                if (distance > maxDistance)
                {
                    return distance;
                }
            }
        }

        return distance;
    }

    private static Dictionary<int, ColumnStat> ComputeColumnStats(Dictionary<int, int[]> sequences)
    {
        var columns = new Dictionary<int, Dictionary<int, int>>();
        foreach (var sequence in sequences.Values)
        {
            for (var i = 0; i < sequence.Length; i++)
            {
                if (!columns.TryGetValue(i, out var counts))
                {
                    counts = new Dictionary<int, int>();
                    columns[i] = counts;
                }

                var value = sequence[i];
                counts[value] = counts.TryGetValue(value, out var count) ? count + 1 : 1;
            }
        }

        var stats = new Dictionary<int, ColumnStat>();
        foreach (var (index, counts) in columns)
        {
            var total = counts.Values.Sum();
            var unique = counts.Count;
            var entropy = Math.Round(ComputeEntropy(counts.Values, total), 3);
            var maxProb = Math.Round(counts.Values.Max() / (double)total, 3);
            stats[index] = new ColumnStat(total, unique, entropy, maxProb);
        }

        return stats;
    }

    private static double ComputeEntropy(IEnumerable<int> counts, int total)
    {
        var entropy = 0.0;
        foreach (var count in counts)
        {
            var p = count / (double)total;
            entropy -= p * Math.Log(p, 2);
        }

        return entropy;
    }

    private static void ValidateColumnStats(
        Dictionary<int, ColumnStat> actual,
        Dictionary<int, ColumnStat> expected,
        string label,
        List<string> failures)
    {
        foreach (var (index, exp) in expected)
        {
            if (!actual.TryGetValue(index, out var act))
            {
                failures.Add($"{label}:{index}:missing");
                continue;
            }

            if (act.Count != exp.Count
                || act.Unique != exp.Unique
                || Math.Abs(act.Entropy - exp.Entropy) > 0.001
                || Math.Abs(act.MaxProb - exp.MaxProb) > 0.001)
            {
                failures.Add($"{label}:{index}");
            }
        }

        foreach (var index in actual.Keys.Except(expected.Keys))
        {
            failures.Add($"{label}:{index}:extra");
        }
    }

    private static Dictionary<int, ColumnValueSet> ComputeColumnValueSets(
        Dictionary<int, int[]> sequences,
        int minCoverage)
    {
        var sets = new Dictionary<int, HashSet<int>>();
        var coverage = new Dictionary<int, int>();
        foreach (var sequence in sequences.Values)
        {
            for (var i = 0; i < sequence.Length; i++)
            {
                if (!sets.TryGetValue(i, out var set))
                {
                    set = new HashSet<int>();
                    sets[i] = set;
                    coverage[i] = 0;
                }

                set.Add(sequence[i]);
                coverage[i] = coverage[i] + 1;
            }
        }

        return sets
            .Where(kvp => coverage.TryGetValue(kvp.Key, out var count) && count >= minCoverage)
            .ToDictionary(
                kvp => kvp.Key,
                kvp => new ColumnValueSet(kvp.Value.Count, kvp.Value.OrderBy(v => v).ToArray()));
    }

    private static void ValidateColumnValueSets(
        Dictionary<int, ColumnValueSet> actual,
        Dictionary<int, ColumnValueSet> expected,
        string label,
        List<string> failures)
    {
        foreach (var (index, exp) in expected)
        {
            if (!actual.TryGetValue(index, out var act))
            {
                failures.Add($"{label}:{index}:missing");
                continue;
            }

            if (act.Count != exp.Count || !act.Values.SequenceEqual(exp.Values))
            {
                failures.Add($"{label}:{index}");
            }
        }

        foreach (var index in actual.Keys.Except(expected.Keys))
        {
            failures.Add($"{label}:{index}:extra");
        }
    }

    private static IReadOnlyList<CellEntropyStat> ComputeCellEntropyStats(IReadOnlyList<EyeMessage> messages)
    {
        var counts = new Dictionary<(int Row, int Col), Dictionary<int, int>>();
        var coverage = new Dictionary<(int Row, int Col), int>();

        foreach (var message in messages)
        {
            var seen = new HashSet<(int Row, int Col)>();
            for (var row = 0; row < message.Lines.Count; row++)
            {
                var line = message.Lines[row];
                for (var col = 0; col < line.Length; col++)
                {
                    var value = line[col] - '0';
                    var key = (row, col);
                    if (!counts.TryGetValue(key, out var map))
                    {
                        map = new Dictionary<int, int>();
                        counts[key] = map;
                    }

                    map[value] = map.TryGetValue(value, out var existing) ? existing + 1 : 1;
                    seen.Add(key);
                }
            }

            foreach (var key in seen)
            {
                coverage[key] = coverage.TryGetValue(key, out var existing) ? existing + 1 : 1;
            }
        }

        var stats = new List<CellEntropyStat>();
        foreach (var (key, map) in counts)
        {
            var total = map.Values.Sum();
            var max = map.Values.Max();
            var maxValues = map.Where(kvp => kvp.Value == max)
                .Select(kvp => kvp.Key)
                .OrderBy(v => v)
                .ToArray();
            var entropy = ComputeEntropy(map.Values, total);
            var maxProb = max / (double)total;
            var cov = coverage.TryGetValue(key, out var covCount) ? covCount : 0;

            stats.Add(new CellEntropyStat(
                key.Row,
                key.Col,
                cov,
                entropy,
                maxProb,
                maxValues));
        }

        return stats;
    }

    private static Dictionary<(int Row, int Col), CellValueSet> ComputeCellValueSets(
        IReadOnlyList<EyeMessage> messages,
        IReadOnlySet<int> messageIds)
    {
        var sets = new Dictionary<(int Row, int Col), HashSet<int>>();
        var coverage = new Dictionary<(int Row, int Col), int>();

        foreach (var message in messages.Where(m => messageIds.Contains(m.Id)))
        {
            var seen = new HashSet<(int Row, int Col)>();
            for (var row = 0; row < message.Lines.Count; row++)
            {
                var line = message.Lines[row];
                for (var col = 0; col < line.Length; col++)
                {
                    var value = line[col] - '0';
                    var key = (row, col);
                    if (!sets.TryGetValue(key, out var set))
                    {
                        set = new HashSet<int>();
                        sets[key] = set;
                    }

                    set.Add(value);
                    seen.Add(key);
                }
            }

            foreach (var key in seen)
            {
                coverage[key] = coverage.TryGetValue(key, out var existing) ? existing + 1 : 1;
            }
        }

        return sets.ToDictionary(
            kvp => kvp.Key,
            kvp => new CellValueSet(
                coverage.TryGetValue(kvp.Key, out var cov) ? cov : 0,
                kvp.Value.OrderBy(v => v).ToArray()));
    }

    private static (int Intersection, int Union, double Jaccard) ComputeJaccard(int[] left, int[] right)
    {
        if (left.Length == 0 && right.Length == 0)
        {
            return (0, 0, 0.0);
        }

        var leftSet = new HashSet<int>(left);
        var rightSet = new HashSet<int>(right);
        var intersection = leftSet.Intersect(rightSet).Count();
        var union = leftSet.Union(rightSet).Count();
        var jaccard = union == 0 ? 0.0 : intersection / (double)union;
        return (intersection, union, jaccard);
    }

    private static ColumnSubstitutionStats ComputeColumnSubstitutionStats(
        Dictionary<int, int[]> sequences,
        IReadOnlyList<(int EastId, int WestId)> pairs,
        bool useAlignment)
    {
        var mapping = new Dictionary<(int Index, int Value), HashSet<int>>();
        var pairCount = 0;
        var aligned = 0;

        foreach (var (eastId, westId) in pairs)
        {
            if (!sequences.TryGetValue(eastId, out var east) || !sequences.TryGetValue(westId, out var west))
            {
                continue;
            }

            if (east.Length == 0 || west.Length == 0)
            {
                continue;
            }

            pairCount++;

            if (useAlignment)
            {
                var alignment = SequenceAlignment.Align(east, west);
                var stepIndex = 0;
                foreach (var step in alignment.Steps)
                {
                    if (step.ValueA.HasValue && step.ValueB.HasValue)
                    {
                        aligned++;
                        var key = (stepIndex, step.ValueA.Value);
                        if (!mapping.TryGetValue(key, out var set))
                        {
                            set = new HashSet<int>();
                            mapping[key] = set;
                        }

                        set.Add(step.ValueB.Value);
                    }

                    stepIndex++;
                }
            }
            else
            {
                var len = Math.Min(east.Length, west.Length);
                aligned += len;
                for (var i = 0; i < len; i++)
                {
                    var key = (i, east[i]);
                    if (!mapping.TryGetValue(key, out var set))
                    {
                        set = new HashSet<int>();
                        mapping[key] = set;
                    }

                    set.Add(west[i]);
                }
            }
        }

        var conflicts = mapping
            .Where(kvp => kvp.Value.Count > 1)
            .Select(kvp => new ColumnSubstitutionConflict(
                kvp.Key.Index,
                kvp.Key.Value,
                kvp.Value.OrderBy(v => v).ToArray()))
            .OrderBy(c => c.Index)
            .ThenBy(c => c.EastValue)
            .ToArray();

        return new ColumnSubstitutionStats(
            conflicts.Length,
            mapping.Count,
            pairCount,
            aligned,
            conflicts);
    }

    private static ColumnSubstitutionStatsWithPrev ComputeColumnSubstitutionStatsWithPrev(
        Dictionary<int, int[]> sequences,
        IReadOnlyList<(int EastId, int WestId)> pairs,
        bool useAlignment)
    {
        var mapping = new Dictionary<(int Index, int Value, int? Prev), HashSet<int>>();
        var pairCount = 0;
        var aligned = 0;

        foreach (var (eastId, westId) in pairs)
        {
            if (!sequences.TryGetValue(eastId, out var east) || !sequences.TryGetValue(westId, out var west))
            {
                continue;
            }

            if (east.Length == 0 || west.Length == 0)
            {
                continue;
            }

            pairCount++;

            if (useAlignment)
            {
                var alignment = SequenceAlignment.Align(east, west);
                var stepIndex = 0;
                foreach (var step in alignment.Steps)
                {
                    if (step.ValueA.HasValue && step.ValueB.HasValue)
                    {
                        aligned++;
                        var prev = step.IndexA.HasValue && step.IndexA.Value > 0 ? east[step.IndexA.Value - 1] : (int?)null;
                        var key = (stepIndex, step.ValueA.Value, prev);
                        if (!mapping.TryGetValue(key, out var set))
                        {
                            set = new HashSet<int>();
                            mapping[key] = set;
                        }

                        set.Add(step.ValueB.Value);
                    }

                    stepIndex++;
                }
            }
            else
            {
                var len = Math.Min(east.Length, west.Length);
                aligned += len;
                for (var i = 0; i < len; i++)
                {
                    var prev = i > 0 ? east[i - 1] : (int?)null;
                    var key = (i, east[i], prev);
                    if (!mapping.TryGetValue(key, out var set))
                    {
                        set = new HashSet<int>();
                        mapping[key] = set;
                    }

                    set.Add(west[i]);
                }
            }
        }

        var conflicts = mapping
            .Where(kvp => kvp.Value.Count > 1)
            .Select(kvp => new ColumnSubstitutionConflictWithPrev(
                kvp.Key.Index,
                kvp.Key.Value,
                kvp.Key.Prev,
                kvp.Value.OrderBy(v => v).ToArray()))
            .OrderBy(c => c.Index)
            .ThenBy(c => c.EastValue)
            .ThenBy(c => c.PrevEastValue ?? -1)
            .ToArray();

        return new ColumnSubstitutionStatsWithPrev(
            conflicts.Length,
            mapping.Count,
            pairCount,
            aligned,
            conflicts);
    }

    private static ColumnSubstitutionStatsWithPrevWest ComputeColumnSubstitutionStatsWithPrevWest(
        Dictionary<int, int[]> sequences,
        IReadOnlyList<(int EastId, int WestId)> pairs,
        bool useAlignment)
    {
        var mapping = new Dictionary<(int Index, int Value, int? PrevWest), HashSet<int>>();
        var pairCount = 0;
        var aligned = 0;

        foreach (var (eastId, westId) in pairs)
        {
            if (!sequences.TryGetValue(eastId, out var east) || !sequences.TryGetValue(westId, out var west))
            {
                continue;
            }

            if (east.Length == 0 || west.Length == 0)
            {
                continue;
            }

            pairCount++;

            if (useAlignment)
            {
                var alignment = SequenceAlignment.Align(east, west);
                var stepIndex = 0;
                foreach (var step in alignment.Steps)
                {
                    if (step.ValueA.HasValue && step.ValueB.HasValue)
                    {
                        aligned++;
                        var prevWest = step.IndexB.HasValue && step.IndexB.Value > 0 ? west[step.IndexB.Value - 1] : (int?)null;
                        var key = (stepIndex, step.ValueA.Value, prevWest);
                        if (!mapping.TryGetValue(key, out var set))
                        {
                            set = new HashSet<int>();
                            mapping[key] = set;
                        }

                        set.Add(step.ValueB.Value);
                    }

                    stepIndex++;
                }
            }
            else
            {
                var len = Math.Min(east.Length, west.Length);
                aligned += len;
                for (var i = 0; i < len; i++)
                {
                    var prevWest = i > 0 ? west[i - 1] : (int?)null;
                    var key = (i, east[i], prevWest);
                    if (!mapping.TryGetValue(key, out var set))
                    {
                        set = new HashSet<int>();
                        mapping[key] = set;
                    }

                    set.Add(west[i]);
                }
            }
        }

        var conflicts = mapping
            .Where(kvp => kvp.Value.Count > 1)
            .Select(kvp => new ColumnSubstitutionConflictWithPrevWest(
                kvp.Key.Index,
                kvp.Key.Value,
                kvp.Key.PrevWest,
                kvp.Value.OrderBy(v => v).ToArray()))
            .OrderBy(c => c.Index)
            .ThenBy(c => c.EastValue)
            .ThenBy(c => c.PrevWestValue ?? -1)
            .ToArray();

        return new ColumnSubstitutionStatsWithPrevWest(
            conflicts.Length,
            mapping.Count,
            pairCount,
            aligned,
            conflicts);
    }

    private static ColumnSubstitutionStatsWithPrevBoth ComputeColumnSubstitutionStatsWithPrevBoth(
        Dictionary<int, int[]> sequences,
        IReadOnlyList<(int EastId, int WestId)> pairs,
        bool useAlignment)
    {
        var mapping = new Dictionary<(int Index, int Value, int? PrevEast, int? PrevWest), HashSet<int>>();
        var pairCount = 0;
        var aligned = 0;

        foreach (var (eastId, westId) in pairs)
        {
            if (!sequences.TryGetValue(eastId, out var east) || !sequences.TryGetValue(westId, out var west))
            {
                continue;
            }

            if (east.Length == 0 || west.Length == 0)
            {
                continue;
            }

            pairCount++;

            if (useAlignment)
            {
                var alignment = SequenceAlignment.Align(east, west);
                var stepIndex = 0;
                foreach (var step in alignment.Steps)
                {
                    if (step.ValueA.HasValue && step.ValueB.HasValue)
                    {
                        aligned++;
                        var prevEast = step.IndexA.HasValue && step.IndexA.Value > 0 ? east[step.IndexA.Value - 1] : (int?)null;
                        var prevWest = step.IndexB.HasValue && step.IndexB.Value > 0 ? west[step.IndexB.Value - 1] : (int?)null;
                        var key = (stepIndex, step.ValueA.Value, prevEast, prevWest);
                        if (!mapping.TryGetValue(key, out var set))
                        {
                            set = new HashSet<int>();
                            mapping[key] = set;
                        }

                        set.Add(step.ValueB.Value);
                    }

                    stepIndex++;
                }
            }
            else
            {
                var len = Math.Min(east.Length, west.Length);
                aligned += len;
                for (var i = 0; i < len; i++)
                {
                    var prevEast = i > 0 ? east[i - 1] : (int?)null;
                    var prevWest = i > 0 ? west[i - 1] : (int?)null;
                    var key = (i, east[i], prevEast, prevWest);
                    if (!mapping.TryGetValue(key, out var set))
                    {
                        set = new HashSet<int>();
                        mapping[key] = set;
                    }

                    set.Add(west[i]);
                }
            }
        }

        var conflicts = mapping
            .Where(kvp => kvp.Value.Count > 1)
            .Select(kvp => new ColumnSubstitutionConflictWithPrevBoth(
                kvp.Key.Index,
                kvp.Key.Value,
                kvp.Key.PrevEast,
                kvp.Key.PrevWest,
                kvp.Value.OrderBy(v => v).ToArray()))
            .OrderBy(c => c.Index)
            .ThenBy(c => c.EastValue)
            .ThenBy(c => c.PrevEastValue ?? -1)
            .ThenBy(c => c.PrevWestValue ?? -1)
            .ToArray();

        return new ColumnSubstitutionStatsWithPrevBoth(
            conflicts.Length,
            mapping.Count,
            pairCount,
            aligned,
            conflicts);
    }

    private static Dictionary<(int Index, int EastValue, int? PrevEast), int> BuildStatefulMapping(
        Dictionary<int, int[]> sequences,
        IReadOnlyList<(int EastId, int WestId)> pairs,
        bool useAlignment)
    {
        var counts = new Dictionary<(int Index, int EastValue, int? PrevEast), Dictionary<int, int>>();

        foreach (var (eastId, westId) in pairs)
        {
            if (!sequences.TryGetValue(eastId, out var east) || !sequences.TryGetValue(westId, out var west))
            {
                continue;
            }

            if (east.Length == 0 || west.Length == 0)
            {
                continue;
            }

            if (useAlignment)
            {
                var alignment = SequenceAlignment.Align(east, west);
                var alignedIndex = 0;
                foreach (var step in alignment.Steps)
                {
                    if (step.ValueA.HasValue && step.ValueB.HasValue)
                    {
                        var prevEast = step.IndexA.HasValue && step.IndexA.Value > 0 ? east[step.IndexA.Value - 1] : (int?)null;
                        var key = (alignedIndex, step.ValueA.Value, prevEast);
                        if (!counts.TryGetValue(key, out var map))
                        {
                            map = new Dictionary<int, int>();
                            counts[key] = map;
                        }

                        var valueB = step.ValueB.Value;
                        map[valueB] = map.TryGetValue(valueB, out var existing) ? existing + 1 : 1;
                        alignedIndex++;
                    }
                }
            }
            else
            {
                var len = Math.Min(east.Length, west.Length);
                for (var i = 0; i < len; i++)
                {
                    var prevEast = i > 0 ? east[i - 1] : (int?)null;
                    var key = (i, east[i], prevEast);
                    if (!counts.TryGetValue(key, out var map))
                    {
                        map = new Dictionary<int, int>();
                        counts[key] = map;
                    }

                    var valueB = west[i];
                    map[valueB] = map.TryGetValue(valueB, out var existing) ? existing + 1 : 1;
                }
            }
        }

        var mapping = new Dictionary<(int Index, int EastValue, int? PrevEast), int>();
        foreach (var (key, map) in counts)
        {
            var bestValue = int.MaxValue;
            var bestCount = -1;
            foreach (var (value, count) in map)
            {
                if (count > bestCount || (count == bestCount && value < bestValue))
                {
                    bestCount = count;
                    bestValue = value;
                }
            }

            mapping[key] = bestValue;
        }

        return mapping;
    }

    private static StatefulDecodeStats EvaluateStatefulDecode(
        Dictionary<int, int[]> sequences,
        IReadOnlyList<(int EastId, int WestId)> pairs,
        bool useAlignment)
    {
        var totalAligned = 0;
        var totalCovered = 0;
        var totalCorrect = 0;
        var usedPairs = 0;

        foreach (var (eastId, westId) in pairs)
        {
            var training = pairs.Where(p => p != (eastId, westId)).ToList();
            var mapping = BuildStatefulMapping(sequences, training, useAlignment);

            if (!sequences.TryGetValue(eastId, out var east) || !sequences.TryGetValue(westId, out var west))
            {
                continue;
            }

            if (east.Length == 0 || west.Length == 0)
            {
                continue;
            }

            usedPairs++;

            if (useAlignment)
            {
                var alignment = SequenceAlignment.Align(east, west);
                var alignedIndex = 0;
                foreach (var step in alignment.Steps)
                {
                    if (step.ValueA.HasValue && step.ValueB.HasValue)
                    {
                        totalAligned++;
                        var prevEast = step.IndexA.HasValue && step.IndexA.Value > 0 ? east[step.IndexA.Value - 1] : (int?)null;
                        var key = (alignedIndex, step.ValueA.Value, prevEast);
                        if (mapping.TryGetValue(key, out var predicted))
                        {
                            totalCovered++;
                            if (predicted == step.ValueB.Value)
                            {
                                totalCorrect++;
                            }
                        }

                        alignedIndex++;
                    }
                }
            }
            else
            {
                var len = Math.Min(east.Length, west.Length);
                totalAligned += len;
                for (var i = 0; i < len; i++)
                {
                    var prevEast = i > 0 ? east[i - 1] : (int?)null;
                    var key = (i, east[i], prevEast);
                    if (mapping.TryGetValue(key, out var predicted))
                    {
                        totalCovered++;
                        if (predicted == west[i])
                        {
                            totalCorrect++;
                        }
                    }
                }
            }
        }

        return new StatefulDecodeStats(usedPairs, totalAligned, totalCovered, totalCorrect);
    }

    private static StatefulDecodeStats EvaluateStatefulDecodeByMotif(
        Dictionary<int, int[]> sequences,
        IReadOnlyList<(int EastId, int WestId)> pairs,
        IReadOnlyDictionary<int, char> motifs,
        bool useAlignment)
    {
        var totalAligned = 0;
        var totalCovered = 0;
        var totalCorrect = 0;
        var usedPairs = 0;

        foreach (var (eastId, westId) in pairs)
        {
            if (!motifs.TryGetValue(eastId, out var motif) || motif == '?')
            {
                continue;
            }

            var training = pairs
                .Where(p => p != (eastId, westId)
                            && motifs.TryGetValue(p.EastId, out var m)
                            && m == motif)
                .ToList();

            var mapping = BuildStatefulMapping(sequences, training, useAlignment);

            if (!sequences.TryGetValue(eastId, out var east) || !sequences.TryGetValue(westId, out var west))
            {
                continue;
            }

            if (east.Length == 0 || west.Length == 0)
            {
                continue;
            }

            usedPairs++;

            if (useAlignment)
            {
                var alignment = SequenceAlignment.Align(east, west);
                var alignedIndex = 0;
                foreach (var step in alignment.Steps)
                {
                    if (step.ValueA.HasValue && step.ValueB.HasValue)
                    {
                        totalAligned++;
                        var prevEast = step.IndexA.HasValue && step.IndexA.Value > 0 ? east[step.IndexA.Value - 1] : (int?)null;
                        var key = (alignedIndex, step.ValueA.Value, prevEast);
                        if (mapping.TryGetValue(key, out var predicted))
                        {
                            totalCovered++;
                            if (predicted == step.ValueB.Value)
                            {
                                totalCorrect++;
                            }
                        }

                        alignedIndex++;
                    }
                }
            }
            else
            {
                var len = Math.Min(east.Length, west.Length);
                totalAligned += len;
                for (var i = 0; i < len; i++)
                {
                    var prevEast = i > 0 ? east[i - 1] : (int?)null;
                    var key = (i, east[i], prevEast);
                    if (mapping.TryGetValue(key, out var predicted))
                    {
                        totalCovered++;
                        if (predicted == west[i])
                        {
                            totalCorrect++;
                        }
                    }
                }
            }
        }

        return new StatefulDecodeStats(usedPairs, totalAligned, totalCovered, totalCorrect);
    }

    private static Dictionary<(int Index, int EastValue, int? PrevEast), int> BuildStatefulMappingWithRunPosition(
        Dictionary<int, int[]> sequences,
        IReadOnlyList<(int EastId, int WestId)> pairs,
        bool useAlignment)
    {
        var counts = new Dictionary<(int Index, int EastValue, int? PrevEast), Dictionary<int, int>>();

        foreach (var (eastId, westId) in pairs)
        {
            if (!sequences.TryGetValue(eastId, out var east) || !sequences.TryGetValue(westId, out var west))
            {
                continue;
            }

            if (east.Length == 0 || west.Length == 0)
            {
                continue;
            }

            if (useAlignment)
            {
                var alignment = SequenceAlignment.Align(east, west);
                foreach (var step in alignment.Steps)
                {
                    if (step.ValueA.HasValue && step.ValueB.HasValue && step.IndexA.HasValue)
                    {
                        var index = step.IndexA.Value;
                        var prevEast = index > 0 ? east[index - 1] : (int?)null;
                        var key = (index, step.ValueA.Value, prevEast);
                        if (!counts.TryGetValue(key, out var map))
                        {
                            map = new Dictionary<int, int>();
                            counts[key] = map;
                        }

                        var valueB = step.ValueB.Value;
                        map[valueB] = map.TryGetValue(valueB, out var existing) ? existing + 1 : 1;
                    }
                }
            }
            else
            {
                var len = Math.Min(east.Length, west.Length);
                for (var i = 0; i < len; i++)
                {
                    var prevEast = i > 0 ? east[i - 1] : (int?)null;
                    var key = (i, east[i], prevEast);
                    if (!counts.TryGetValue(key, out var map))
                    {
                        map = new Dictionary<int, int>();
                        counts[key] = map;
                    }

                    var valueB = west[i];
                    map[valueB] = map.TryGetValue(valueB, out var existing) ? existing + 1 : 1;
                }
            }
        }

        var mapping = new Dictionary<(int Index, int EastValue, int? PrevEast), int>();
        foreach (var (key, map) in counts)
        {
            var bestValue = int.MaxValue;
            var bestCount = -1;
            foreach (var (value, count) in map)
            {
                if (count > bestCount || (count == bestCount && value < bestValue))
                {
                    bestCount = count;
                    bestValue = value;
                }
            }

            mapping[key] = bestValue;
        }

        return mapping;
    }

    private static StatefulDecodeStats EvaluateStatefulDecodeWithRunPosition(
        Dictionary<int, int[]> sequences,
        IReadOnlyList<(int EastId, int WestId)> pairs,
        bool useAlignment)
    {
        var totalAligned = 0;
        var totalCovered = 0;
        var totalCorrect = 0;
        var usedPairs = 0;

        foreach (var (eastId, westId) in pairs)
        {
            var training = pairs.Where(p => p != (eastId, westId)).ToList();
            var mapping = BuildStatefulMappingWithRunPosition(sequences, training, useAlignment);

            if (!sequences.TryGetValue(eastId, out var east) || !sequences.TryGetValue(westId, out var west))
            {
                continue;
            }

            if (east.Length == 0 || west.Length == 0)
            {
                continue;
            }

            usedPairs++;

            if (useAlignment)
            {
                var alignment = SequenceAlignment.Align(east, west);
                foreach (var step in alignment.Steps)
                {
                    if (step.ValueA.HasValue && step.ValueB.HasValue && step.IndexA.HasValue)
                    {
                        totalAligned++;
                        var index = step.IndexA.Value;
                        var prevEast = index > 0 ? east[index - 1] : (int?)null;
                        var key = (index, step.ValueA.Value, prevEast);
                        if (mapping.TryGetValue(key, out var predicted))
                        {
                            totalCovered++;
                            if (predicted == step.ValueB.Value)
                            {
                                totalCorrect++;
                            }
                        }
                    }
                }
            }
            else
            {
                var len = Math.Min(east.Length, west.Length);
                totalAligned += len;
                for (var i = 0; i < len; i++)
                {
                    var prevEast = i > 0 ? east[i - 1] : (int?)null;
                    var key = (i, east[i], prevEast);
                    if (mapping.TryGetValue(key, out var predicted))
                    {
                        totalCovered++;
                        if (predicted == west[i])
                        {
                            totalCorrect++;
                        }
                    }
                }
            }
        }

        return new StatefulDecodeStats(usedPairs, totalAligned, totalCovered, totalCorrect);
    }

    private static BucketedDecodeStats EvaluateStatefulDecodeWithRunPositionLengthBuckets(
        Dictionary<int, int[]> sequences,
        IReadOnlyList<(int EastId, int WestId)> pairs,
        IReadOnlyDictionary<int, int> lengthBuckets,
        bool useAlignment)
    {
        var bucketCount = lengthBuckets.Count == 0 ? 0 : lengthBuckets.Values.Max() + 1;
        var buckets = Enumerable.Range(0, bucketCount).Select(_ => new BucketDecodeStats(0, 0, 0)).ToArray();

        var totalAligned = 0;
        var totalCovered = 0;
        var totalCorrect = 0;
        var usedPairs = 0;

        foreach (var (eastId, westId) in pairs)
        {
            if (!lengthBuckets.TryGetValue(westId, out var bucket))
            {
                continue;
            }

            var training = pairs.Where(p => p != (eastId, westId)).ToList();
            var mapping = BuildStatefulMappingWithRunPosition(sequences, training, useAlignment);

            if (!sequences.TryGetValue(eastId, out var east) || !sequences.TryGetValue(westId, out var west))
            {
                continue;
            }

            if (east.Length == 0 || west.Length == 0)
            {
                continue;
            }

            usedPairs++;

            if (useAlignment)
            {
                var alignment = SequenceAlignment.Align(east, west);
                foreach (var step in alignment.Steps)
                {
                    if (step.ValueA.HasValue && step.ValueB.HasValue && step.IndexA.HasValue)
                    {
                        totalAligned++;
                        buckets[bucket] = buckets[bucket] with { Aligned = buckets[bucket].Aligned + 1 };

                        var index = step.IndexA.Value;
                        var prevEast = index > 0 ? east[index - 1] : (int?)null;
                        var key = (index, step.ValueA.Value, prevEast);

                        if (mapping.TryGetValue(key, out var predicted))
                        {
                            totalCovered++;
                            buckets[bucket] = buckets[bucket] with { Covered = buckets[bucket].Covered + 1 };
                            if (predicted == step.ValueB.Value)
                            {
                                totalCorrect++;
                                buckets[bucket] = buckets[bucket] with { Correct = buckets[bucket].Correct + 1 };
                            }
                        }
                    }
                }
            }
            else
            {
                var len = Math.Min(east.Length, west.Length);
                totalAligned += len;
                for (var i = 0; i < len; i++)
                {
                    buckets[bucket] = buckets[bucket] with { Aligned = buckets[bucket].Aligned + 1 };

                    var prevEast = i > 0 ? east[i - 1] : (int?)null;
                    var key = (i, east[i], prevEast);
                    if (mapping.TryGetValue(key, out var predicted))
                    {
                        totalCovered++;
                        buckets[bucket] = buckets[bucket] with { Covered = buckets[bucket].Covered + 1 };
                        if (predicted == west[i])
                        {
                            totalCorrect++;
                            buckets[bucket] = buckets[bucket] with { Correct = buckets[bucket].Correct + 1 };
                        }
                    }
                }
            }
        }

        return new BucketedDecodeStats(usedPairs, totalAligned, totalCovered, totalCorrect, buckets);
    }

    private static BucketedDecodeStats EvaluateStatefulDecodeWithRunPositionBuckets(
        Dictionary<int, int[]> sequences,
        IReadOnlyList<(int EastId, int WestId)> pairs,
        bool useAlignment)
    {
        var totalAligned = 0;
        var totalCovered = 0;
        var totalCorrect = 0;
        var usedPairs = 0;
        var buckets = new[]
        {
            new BucketDecodeStats(0, 0, 0),
            new BucketDecodeStats(0, 0, 0),
            new BucketDecodeStats(0, 0, 0),
        };

        int GetBucket(int index)
        {
            if (index < 10) return 0;
            if (index < 20) return 1;
            return 2;
        }

        foreach (var (eastId, westId) in pairs)
        {
            var training = pairs.Where(p => p != (eastId, westId)).ToList();
            var mapping = BuildStatefulMappingWithRunPosition(sequences, training, useAlignment);

            if (!sequences.TryGetValue(eastId, out var east) || !sequences.TryGetValue(westId, out var west))
            {
                continue;
            }

            if (east.Length == 0 || west.Length == 0)
            {
                continue;
            }

            usedPairs++;

            if (useAlignment)
            {
                var alignment = SequenceAlignment.Align(east, west);
                foreach (var step in alignment.Steps)
                {
                    if (step.ValueA.HasValue && step.ValueB.HasValue && step.IndexA.HasValue)
                    {
                        totalAligned++;
                        var index = step.IndexA.Value;
                        var bucket = GetBucket(index);
                        var prevEast = index > 0 ? east[index - 1] : (int?)null;
                        var key = (index, step.ValueA.Value, prevEast);

                        buckets[bucket] = buckets[bucket] with { Aligned = buckets[bucket].Aligned + 1 };

                        if (mapping.TryGetValue(key, out var predicted))
                        {
                            totalCovered++;
                            buckets[bucket] = buckets[bucket] with { Covered = buckets[bucket].Covered + 1 };
                            if (predicted == step.ValueB.Value)
                            {
                                totalCorrect++;
                                buckets[bucket] = buckets[bucket] with { Correct = buckets[bucket].Correct + 1 };
                            }
                        }
                    }
                }
            }
            else
            {
                var len = Math.Min(east.Length, west.Length);
                totalAligned += len;
                for (var i = 0; i < len; i++)
                {
                    var bucket = GetBucket(i);
                    var prevEast = i > 0 ? east[i - 1] : (int?)null;
                    var key = (i, east[i], prevEast);

                    buckets[bucket] = buckets[bucket] with { Aligned = buckets[bucket].Aligned + 1 };

                    if (mapping.TryGetValue(key, out var predicted))
                    {
                        totalCovered++;
                        buckets[bucket] = buckets[bucket] with { Covered = buckets[bucket].Covered + 1 };
                        if (predicted == west[i])
                        {
                            totalCorrect++;
                            buckets[bucket] = buckets[bucket] with { Correct = buckets[bucket].Correct + 1 };
                        }
                    }
                }
            }
        }

        return new BucketedDecodeStats(usedPairs, totalAligned, totalCovered, totalCorrect, buckets);
    }

    private static Dictionary<(int Index, int EastValue, int? Prev1, int? Prev2), int> BuildStatefulMappingWithRunPositionPrev2(
        Dictionary<int, int[]> sequences,
        IReadOnlyList<(int EastId, int WestId)> pairs,
        bool useAlignment)
    {
        var counts = new Dictionary<(int Index, int EastValue, int? Prev1, int? Prev2), Dictionary<int, int>>();

        foreach (var (eastId, westId) in pairs)
        {
            if (!sequences.TryGetValue(eastId, out var east) || !sequences.TryGetValue(westId, out var west))
            {
                continue;
            }

            if (east.Length == 0 || west.Length == 0)
            {
                continue;
            }

            if (useAlignment)
            {
                var alignment = SequenceAlignment.Align(east, west);
                foreach (var step in alignment.Steps)
                {
                    if (step.ValueA.HasValue && step.ValueB.HasValue && step.IndexA.HasValue)
                    {
                        var index = step.IndexA.Value;
                        var prev1 = index > 0 ? east[index - 1] : (int?)null;
                        var prev2 = index > 1 ? east[index - 2] : (int?)null;
                        var key = (index, step.ValueA.Value, prev1, prev2);
                        if (!counts.TryGetValue(key, out var map))
                        {
                            map = new Dictionary<int, int>();
                            counts[key] = map;
                        }

                        var valueB = step.ValueB.Value;
                        map[valueB] = map.TryGetValue(valueB, out var existing) ? existing + 1 : 1;
                    }
                }
            }
            else
            {
                var len = Math.Min(east.Length, west.Length);
                for (var i = 0; i < len; i++)
                {
                    var prev1 = i > 0 ? east[i - 1] : (int?)null;
                    var prev2 = i > 1 ? east[i - 2] : (int?)null;
                    var key = (i, east[i], prev1, prev2);
                    if (!counts.TryGetValue(key, out var map))
                    {
                        map = new Dictionary<int, int>();
                        counts[key] = map;
                    }

                    var valueB = west[i];
                    map[valueB] = map.TryGetValue(valueB, out var existing) ? existing + 1 : 1;
                }
            }
        }

        var mapping = new Dictionary<(int Index, int EastValue, int? Prev1, int? Prev2), int>();
        foreach (var (key, map) in counts)
        {
            var bestValue = int.MaxValue;
            var bestCount = -1;
            foreach (var (value, count) in map)
            {
                if (count > bestCount || (count == bestCount && value < bestValue))
                {
                    bestCount = count;
                    bestValue = value;
                }
            }

            mapping[key] = bestValue;
        }

        return mapping;
    }

    private static Dictionary<(int Index, int EastValue, int? Prev1, int? Prev2, int? Prev3), int> BuildStatefulMappingWithRunPositionPrev3(
        Dictionary<int, int[]> sequences,
        IReadOnlyList<(int EastId, int WestId)> pairs,
        bool useAlignment)
    {
        var counts = new Dictionary<(int Index, int EastValue, int? Prev1, int? Prev2, int? Prev3), Dictionary<int, int>>();

        foreach (var (eastId, westId) in pairs)
        {
            if (!sequences.TryGetValue(eastId, out var east) || !sequences.TryGetValue(westId, out var west))
            {
                continue;
            }

            if (east.Length == 0 || west.Length == 0)
            {
                continue;
            }

            if (useAlignment)
            {
                var alignment = SequenceAlignment.Align(east, west);
                foreach (var step in alignment.Steps)
                {
                    if (step.ValueA.HasValue && step.ValueB.HasValue && step.IndexA.HasValue)
                    {
                        var index = step.IndexA.Value;
                        var prev1 = index > 0 ? east[index - 1] : (int?)null;
                        var prev2 = index > 1 ? east[index - 2] : (int?)null;
                        var prev3 = index > 2 ? east[index - 3] : (int?)null;
                        var key = (index, step.ValueA.Value, prev1, prev2, prev3);
                        if (!counts.TryGetValue(key, out var map))
                        {
                            map = new Dictionary<int, int>();
                            counts[key] = map;
                        }

                        var valueB = step.ValueB.Value;
                        map[valueB] = map.TryGetValue(valueB, out var existing) ? existing + 1 : 1;
                    }
                }
            }
            else
            {
                var len = Math.Min(east.Length, west.Length);
                for (var i = 0; i < len; i++)
                {
                    var prev1 = i > 0 ? east[i - 1] : (int?)null;
                    var prev2 = i > 1 ? east[i - 2] : (int?)null;
                    var prev3 = i > 2 ? east[i - 3] : (int?)null;
                    var key = (i, east[i], prev1, prev2, prev3);
                    if (!counts.TryGetValue(key, out var map))
                    {
                        map = new Dictionary<int, int>();
                        counts[key] = map;
                    }

                    var valueB = west[i];
                    map[valueB] = map.TryGetValue(valueB, out var existing) ? existing + 1 : 1;
                }
            }
        }

        var mapping = new Dictionary<(int Index, int EastValue, int? Prev1, int? Prev2, int? Prev3), int>();
        foreach (var (key, map) in counts)
        {
            var bestValue = int.MaxValue;
            var bestCount = -1;
            foreach (var (value, count) in map)
            {
                if (count > bestCount || (count == bestCount && value < bestValue))
                {
                    bestCount = count;
                    bestValue = value;
                }
            }

            mapping[key] = bestValue;
        }

        return mapping;
    }

    private static BucketedDecodeStats EvaluateStatefulDecodeWithRunPositionBucketsPrev2(
        Dictionary<int, int[]> sequences,
        IReadOnlyList<(int EastId, int WestId)> pairs,
        bool useAlignment)
    {
        var totalAligned = 0;
        var totalCovered = 0;
        var totalCorrect = 0;
        var usedPairs = 0;
        var buckets = new[]
        {
            new BucketDecodeStats(0, 0, 0),
            new BucketDecodeStats(0, 0, 0),
            new BucketDecodeStats(0, 0, 0),
        };

        int GetBucket(int index)
        {
            if (index < 10) return 0;
            if (index < 20) return 1;
            return 2;
        }

        foreach (var (eastId, westId) in pairs)
        {
            var training = pairs.Where(p => p != (eastId, westId)).ToList();
            var mapping = BuildStatefulMappingWithRunPositionPrev2(sequences, training, useAlignment);

            if (!sequences.TryGetValue(eastId, out var east) || !sequences.TryGetValue(westId, out var west))
            {
                continue;
            }

            if (east.Length == 0 || west.Length == 0)
            {
                continue;
            }

            usedPairs++;

            if (useAlignment)
            {
                var alignment = SequenceAlignment.Align(east, west);
                foreach (var step in alignment.Steps)
                {
                    if (step.ValueA.HasValue && step.ValueB.HasValue && step.IndexA.HasValue)
                    {
                        totalAligned++;
                        var index = step.IndexA.Value;
                        var bucket = GetBucket(index);
                        var prev1 = index > 0 ? east[index - 1] : (int?)null;
                        var prev2 = index > 1 ? east[index - 2] : (int?)null;
                        var key = (index, step.ValueA.Value, prev1, prev2);

                        buckets[bucket] = buckets[bucket] with { Aligned = buckets[bucket].Aligned + 1 };

                        if (mapping.TryGetValue(key, out var predicted))
                        {
                            totalCovered++;
                            buckets[bucket] = buckets[bucket] with { Covered = buckets[bucket].Covered + 1 };
                            if (predicted == step.ValueB.Value)
                            {
                                totalCorrect++;
                                buckets[bucket] = buckets[bucket] with { Correct = buckets[bucket].Correct + 1 };
                            }
                        }
                    }
                }
            }
            else
            {
                var len = Math.Min(east.Length, west.Length);
                totalAligned += len;
                for (var i = 0; i < len; i++)
                {
                    var bucket = GetBucket(i);
                    var prev1 = i > 0 ? east[i - 1] : (int?)null;
                    var prev2 = i > 1 ? east[i - 2] : (int?)null;
                    var key = (i, east[i], prev1, prev2);

                    buckets[bucket] = buckets[bucket] with { Aligned = buckets[bucket].Aligned + 1 };

                    if (mapping.TryGetValue(key, out var predicted))
                    {
                        totalCovered++;
                        buckets[bucket] = buckets[bucket] with { Covered = buckets[bucket].Covered + 1 };
                        if (predicted == west[i])
                        {
                            totalCorrect++;
                            buckets[bucket] = buckets[bucket] with { Correct = buckets[bucket].Correct + 1 };
                        }
                    }
                }
            }
        }

        return new BucketedDecodeStats(usedPairs, totalAligned, totalCovered, totalCorrect, buckets);
    }

    private static BucketedDecodeStats EvaluateStatefulDecodeWithRunPositionBucketsPrev3(
        Dictionary<int, int[]> sequences,
        IReadOnlyList<(int EastId, int WestId)> pairs,
        bool useAlignment)
    {
        var totalAligned = 0;
        var totalCovered = 0;
        var totalCorrect = 0;
        var usedPairs = 0;
        var buckets = new[]
        {
            new BucketDecodeStats(0, 0, 0),
            new BucketDecodeStats(0, 0, 0),
            new BucketDecodeStats(0, 0, 0),
        };

        int GetBucket(int index)
        {
            if (index < 10) return 0;
            if (index < 20) return 1;
            return 2;
        }

        foreach (var (eastId, westId) in pairs)
        {
            var training = pairs.Where(p => p != (eastId, westId)).ToList();
            var mapping = BuildStatefulMappingWithRunPositionPrev3(sequences, training, useAlignment);

            if (!sequences.TryGetValue(eastId, out var east) || !sequences.TryGetValue(westId, out var west))
            {
                continue;
            }

            if (east.Length == 0 || west.Length == 0)
            {
                continue;
            }

            usedPairs++;

            if (useAlignment)
            {
                var alignment = SequenceAlignment.Align(east, west);
                foreach (var step in alignment.Steps)
                {
                    if (step.ValueA.HasValue && step.ValueB.HasValue && step.IndexA.HasValue)
                    {
                        totalAligned++;
                        var index = step.IndexA.Value;
                        var bucket = GetBucket(index);
                        var prev1 = index > 0 ? east[index - 1] : (int?)null;
                        var prev2 = index > 1 ? east[index - 2] : (int?)null;
                        var prev3 = index > 2 ? east[index - 3] : (int?)null;
                        var key = (index, step.ValueA.Value, prev1, prev2, prev3);

                        buckets[bucket] = buckets[bucket] with { Aligned = buckets[bucket].Aligned + 1 };

                        if (mapping.TryGetValue(key, out var predicted))
                        {
                            totalCovered++;
                            buckets[bucket] = buckets[bucket] with { Covered = buckets[bucket].Covered + 1 };
                            if (predicted == step.ValueB.Value)
                            {
                                totalCorrect++;
                                buckets[bucket] = buckets[bucket] with { Correct = buckets[bucket].Correct + 1 };
                            }
                        }
                    }
                }
            }
            else
            {
                var len = Math.Min(east.Length, west.Length);
                totalAligned += len;
                for (var i = 0; i < len; i++)
                {
                    var bucket = GetBucket(i);
                    var prev1 = i > 0 ? east[i - 1] : (int?)null;
                    var prev2 = i > 1 ? east[i - 2] : (int?)null;
                    var prev3 = i > 2 ? east[i - 3] : (int?)null;
                    var key = (i, east[i], prev1, prev2, prev3);

                    buckets[bucket] = buckets[bucket] with { Aligned = buckets[bucket].Aligned + 1 };

                    if (mapping.TryGetValue(key, out var predicted))
                    {
                        totalCovered++;
                        buckets[bucket] = buckets[bucket] with { Covered = buckets[bucket].Covered + 1 };
                        if (predicted == west[i])
                        {
                            totalCorrect++;
                            buckets[bucket] = buckets[bucket] with { Correct = buckets[bucket].Correct + 1 };
                        }
                    }
                }
            }
        }

        return new BucketedDecodeStats(usedPairs, totalAligned, totalCovered, totalCorrect, buckets);
    }

    private static Dictionary<(int Bucket, int EastValue, int? PrevEast), int> BuildStatefulMappingWithBucketKey(
        Dictionary<int, int[]> sequences,
        IReadOnlyList<(int EastId, int WestId)> pairs,
        bool useAlignment)
    {
        var counts = new Dictionary<(int Bucket, int EastValue, int? PrevEast), Dictionary<int, int>>();

        int GetBucket(int index)
        {
            if (index < 10) return 0;
            if (index < 20) return 1;
            return 2;
        }

        foreach (var (eastId, westId) in pairs)
        {
            if (!sequences.TryGetValue(eastId, out var east) || !sequences.TryGetValue(westId, out var west))
            {
                continue;
            }

            if (east.Length == 0 || west.Length == 0)
            {
                continue;
            }

            if (useAlignment)
            {
                var alignment = SequenceAlignment.Align(east, west);
                foreach (var step in alignment.Steps)
                {
                    if (step.ValueA.HasValue && step.ValueB.HasValue && step.IndexA.HasValue)
                    {
                        var index = step.IndexA.Value;
                        var bucket = GetBucket(index);
                        var prevEast = index > 0 ? east[index - 1] : (int?)null;
                        var key = (bucket, step.ValueA.Value, prevEast);
                        if (!counts.TryGetValue(key, out var map))
                        {
                            map = new Dictionary<int, int>();
                            counts[key] = map;
                        }

                        var valueB = step.ValueB.Value;
                        map[valueB] = map.TryGetValue(valueB, out var existing) ? existing + 1 : 1;
                    }
                }
            }
            else
            {
                var len = Math.Min(east.Length, west.Length);
                for (var i = 0; i < len; i++)
                {
                    var bucket = GetBucket(i);
                    var prevEast = i > 0 ? east[i - 1] : (int?)null;
                    var key = (bucket, east[i], prevEast);
                    if (!counts.TryGetValue(key, out var map))
                    {
                        map = new Dictionary<int, int>();
                        counts[key] = map;
                    }

                    var valueB = west[i];
                    map[valueB] = map.TryGetValue(valueB, out var existing) ? existing + 1 : 1;
                }
            }
        }

        var mapping = new Dictionary<(int Bucket, int EastValue, int? PrevEast), int>();
        foreach (var (key, map) in counts)
        {
            var bestValue = int.MaxValue;
            var bestCount = -1;
            foreach (var (value, count) in map)
            {
                if (count > bestCount || (count == bestCount && value < bestValue))
                {
                    bestCount = count;
                    bestValue = value;
                }
            }

            mapping[key] = bestValue;
        }

        return mapping;
    }

    private static Dictionary<(int Bucket, int EastValue, int? PrevEast), int> BuildStatefulMappingWithNormalizedBucket(
        Dictionary<int, int[]> sequences,
        IReadOnlyList<(int EastId, int WestId)> pairs,
        bool useAlignment,
        int bucketCount)
    {
        var counts = new Dictionary<(int Bucket, int EastValue, int? PrevEast), Dictionary<int, int>>();

        foreach (var (eastId, westId) in pairs)
        {
            if (!sequences.TryGetValue(eastId, out var east) || !sequences.TryGetValue(westId, out var west))
            {
                continue;
            }

            if (east.Length == 0 || west.Length == 0)
            {
                continue;
            }

            if (useAlignment)
            {
                var alignment = SequenceAlignment.Align(east, west);
                foreach (var step in alignment.Steps)
                {
                    if (step.ValueA.HasValue && step.ValueB.HasValue && step.IndexA.HasValue)
                    {
                        var index = step.IndexA.Value;
                        var bucket = GetNormalizedBucket(index, east.Length, bucketCount);
                        var prevEast = index > 0 ? east[index - 1] : (int?)null;
                        var key = (bucket, step.ValueA.Value, prevEast);
                        if (!counts.TryGetValue(key, out var map))
                        {
                            map = new Dictionary<int, int>();
                            counts[key] = map;
                        }

                        var valueB = step.ValueB.Value;
                        map[valueB] = map.TryGetValue(valueB, out var existing) ? existing + 1 : 1;
                    }
                }
            }
            else
            {
                var len = Math.Min(east.Length, west.Length);
                for (var i = 0; i < len; i++)
                {
                    var bucket = GetNormalizedBucket(i, east.Length, bucketCount);
                    var prevEast = i > 0 ? east[i - 1] : (int?)null;
                    var key = (bucket, east[i], prevEast);
                    if (!counts.TryGetValue(key, out var map))
                    {
                        map = new Dictionary<int, int>();
                        counts[key] = map;
                    }

                    var valueB = west[i];
                    map[valueB] = map.TryGetValue(valueB, out var existing) ? existing + 1 : 1;
                }
            }
        }

        var mapping = new Dictionary<(int Bucket, int EastValue, int? PrevEast), int>();
        foreach (var (key, map) in counts)
        {
            var bestValue = int.MaxValue;
            var bestCount = -1;
            foreach (var (value, count) in map)
            {
                if (count > bestCount || (count == bestCount && value < bestValue))
                {
                    bestCount = count;
                    bestValue = value;
                }
            }

            mapping[key] = bestValue;
        }

        return mapping;
    }

    private static BucketedDecodeStats EvaluateStatefulDecodeWithBucketKey(
        Dictionary<int, int[]> sequences,
        IReadOnlyList<(int EastId, int WestId)> pairs,
        bool useAlignment)
    {
        var totalAligned = 0;
        var totalCovered = 0;
        var totalCorrect = 0;
        var usedPairs = 0;
        var buckets = new[]
        {
            new BucketDecodeStats(0, 0, 0),
            new BucketDecodeStats(0, 0, 0),
            new BucketDecodeStats(0, 0, 0),
        };

        int GetBucket(int index)
        {
            if (index < 10) return 0;
            if (index < 20) return 1;
            return 2;
        }

        foreach (var (eastId, westId) in pairs)
        {
            var training = pairs.Where(p => p != (eastId, westId)).ToList();
            var mapping = BuildStatefulMappingWithBucketKey(sequences, training, useAlignment);

            if (!sequences.TryGetValue(eastId, out var east) || !sequences.TryGetValue(westId, out var west))
            {
                continue;
            }

            if (east.Length == 0 || west.Length == 0)
            {
                continue;
            }

            usedPairs++;

            if (useAlignment)
            {
                var alignment = SequenceAlignment.Align(east, west);
                foreach (var step in alignment.Steps)
                {
                    if (step.ValueA.HasValue && step.ValueB.HasValue && step.IndexA.HasValue)
                    {
                        totalAligned++;
                        var index = step.IndexA.Value;
                        var bucket = GetBucket(index);
                        var prevEast = index > 0 ? east[index - 1] : (int?)null;
                        var key = (bucket, step.ValueA.Value, prevEast);

                        buckets[bucket] = buckets[bucket] with { Aligned = buckets[bucket].Aligned + 1 };

                        if (mapping.TryGetValue(key, out var predicted))
                        {
                            totalCovered++;
                            buckets[bucket] = buckets[bucket] with { Covered = buckets[bucket].Covered + 1 };
                            if (predicted == step.ValueB.Value)
                            {
                                totalCorrect++;
                                buckets[bucket] = buckets[bucket] with { Correct = buckets[bucket].Correct + 1 };
                            }
                        }
                    }
                }
            }
            else
            {
                var len = Math.Min(east.Length, west.Length);
                totalAligned += len;
                for (var i = 0; i < len; i++)
                {
                    var bucket = GetBucket(i);
                    var prevEast = i > 0 ? east[i - 1] : (int?)null;
                    var key = (bucket, east[i], prevEast);

                    buckets[bucket] = buckets[bucket] with { Aligned = buckets[bucket].Aligned + 1 };

                    if (mapping.TryGetValue(key, out var predicted))
                    {
                        totalCovered++;
                        buckets[bucket] = buckets[bucket] with { Covered = buckets[bucket].Covered + 1 };
                        if (predicted == west[i])
                        {
                            totalCorrect++;
                            buckets[bucket] = buckets[bucket] with { Correct = buckets[bucket].Correct + 1 };
                        }
                    }
                }
            }
        }

        return new BucketedDecodeStats(usedPairs, totalAligned, totalCovered, totalCorrect, buckets);
    }

    private static BucketedDecodeStats EvaluateStatefulDecodeWithNormalizedBucketByLength(
        Dictionary<int, int[]> sequences,
        IReadOnlyList<(int EastId, int WestId)> pairs,
        IReadOnlyDictionary<int, int> lengthBuckets,
        bool useAlignment,
        int bucketCount = 3)
    {
        var lengthBucketCount = lengthBuckets.Count == 0 ? 0 : lengthBuckets.Values.Max() + 1;
        var buckets = Enumerable.Range(0, lengthBucketCount).Select(_ => new BucketDecodeStats(0, 0, 0)).ToArray();

        var totalAligned = 0;
        var totalCovered = 0;
        var totalCorrect = 0;
        var usedPairs = 0;

        foreach (var (eastId, westId) in pairs)
        {
            if (!lengthBuckets.TryGetValue(westId, out var lengthBucket))
            {
                continue;
            }

            var training = pairs.Where(p => p != (eastId, westId)).ToList();
            var mapping = BuildStatefulMappingWithNormalizedBucket(sequences, training, useAlignment, bucketCount);

            if (!sequences.TryGetValue(eastId, out var east) || !sequences.TryGetValue(westId, out var west))
            {
                continue;
            }

            if (east.Length == 0 || west.Length == 0)
            {
                continue;
            }

            usedPairs++;

            if (useAlignment)
            {
                var alignment = SequenceAlignment.Align(east, west);
                foreach (var step in alignment.Steps)
                {
                    if (step.ValueA.HasValue && step.ValueB.HasValue && step.IndexA.HasValue)
                    {
                        totalAligned++;
                        buckets[lengthBucket] = buckets[lengthBucket] with { Aligned = buckets[lengthBucket].Aligned + 1 };

                        var index = step.IndexA.Value;
                        var bucket = GetNormalizedBucket(index, east.Length, bucketCount);
                        var prevEast = index > 0 ? east[index - 1] : (int?)null;
                        var key = (bucket, step.ValueA.Value, prevEast);

                        if (mapping.TryGetValue(key, out var predicted))
                        {
                            totalCovered++;
                            buckets[lengthBucket] = buckets[lengthBucket] with { Covered = buckets[lengthBucket].Covered + 1 };
                            if (predicted == step.ValueB.Value)
                            {
                                totalCorrect++;
                                buckets[lengthBucket] = buckets[lengthBucket] with { Correct = buckets[lengthBucket].Correct + 1 };
                            }
                        }
                    }
                }
            }
            else
            {
                var len = Math.Min(east.Length, west.Length);
                totalAligned += len;
                for (var i = 0; i < len; i++)
                {
                    buckets[lengthBucket] = buckets[lengthBucket] with { Aligned = buckets[lengthBucket].Aligned + 1 };

                    var bucket = GetNormalizedBucket(i, east.Length, bucketCount);
                    var prevEast = i > 0 ? east[i - 1] : (int?)null;
                    var key = (bucket, east[i], prevEast);
                    if (mapping.TryGetValue(key, out var predicted))
                    {
                        totalCovered++;
                        buckets[lengthBucket] = buckets[lengthBucket] with { Covered = buckets[lengthBucket].Covered + 1 };
                        if (predicted == west[i])
                        {
                            totalCorrect++;
                            buckets[lengthBucket] = buckets[lengthBucket] with { Correct = buckets[lengthBucket].Correct + 1 };
                        }
                    }
                }
            }
        }

        return new BucketedDecodeStats(usedPairs, totalAligned, totalCovered, totalCorrect, buckets);
    }

    private static Dictionary<int, HashSet<int>> BuildAllowedWestSets(
        Dictionary<int, int[]> sequences,
        IReadOnlyList<(int EastId, int WestId)> pairs,
        bool useAlignment)
    {
        var allowed = new Dictionary<int, HashSet<int>>();

        foreach (var (eastId, westId) in pairs)
        {
            if (!sequences.TryGetValue(eastId, out var east) || !sequences.TryGetValue(westId, out var west))
            {
                continue;
            }

            if (east.Length == 0 || west.Length == 0)
            {
                continue;
            }

            if (useAlignment)
            {
                var alignment = SequenceAlignment.Align(east, west);
                foreach (var step in alignment.Steps)
                {
                    if (step.ValueA.HasValue && step.ValueB.HasValue && step.IndexA.HasValue)
                    {
                        var index = step.IndexA.Value;
                        if (!allowed.TryGetValue(index, out var set))
                        {
                            set = new HashSet<int>();
                            allowed[index] = set;
                        }

                        set.Add(step.ValueB.Value);
                    }
                }
            }
            else
            {
                var len = Math.Min(east.Length, west.Length);
                for (var i = 0; i < len; i++)
                {
                    if (!allowed.TryGetValue(i, out var set))
                    {
                        set = new HashSet<int>();
                        allowed[i] = set;
                    }

                    set.Add(west[i]);
                }
            }
        }

        return allowed;
    }

    private static StatefulDecodeStats EvaluateStatefulDecodeWithRunPositionConstrained(
        Dictionary<int, int[]> sequences,
        IReadOnlyList<(int EastId, int WestId)> pairs,
        bool useAlignment)
    {
        var totalAligned = 0;
        var totalCovered = 0;
        var totalCorrect = 0;
        var usedPairs = 0;

        foreach (var (eastId, westId) in pairs)
        {
            var training = pairs.Where(p => p != (eastId, westId)).ToList();
            var mapping = BuildStatefulMappingWithRunPosition(sequences, training, useAlignment);
            var allowed = BuildAllowedWestSets(sequences, training, useAlignment);

            if (!sequences.TryGetValue(eastId, out var east) || !sequences.TryGetValue(westId, out var west))
            {
                continue;
            }

            if (east.Length == 0 || west.Length == 0)
            {
                continue;
            }

            usedPairs++;

            if (useAlignment)
            {
                var alignment = SequenceAlignment.Align(east, west);
                foreach (var step in alignment.Steps)
                {
                    if (step.ValueA.HasValue && step.ValueB.HasValue && step.IndexA.HasValue)
                    {
                        totalAligned++;
                        var index = step.IndexA.Value;
                        var prevEast = index > 0 ? east[index - 1] : (int?)null;
                        var key = (index, step.ValueA.Value, prevEast);
                        if (mapping.TryGetValue(key, out var predicted)
                            && allowed.TryGetValue(index, out var set)
                            && set.Contains(predicted))
                        {
                            totalCovered++;
                            if (predicted == step.ValueB.Value)
                            {
                                totalCorrect++;
                            }
                        }
                    }
                }
            }
            else
            {
                var len = Math.Min(east.Length, west.Length);
                totalAligned += len;
                for (var i = 0; i < len; i++)
                {
                    var prevEast = i > 0 ? east[i - 1] : (int?)null;
                    var key = (i, east[i], prevEast);
                    if (mapping.TryGetValue(key, out var predicted)
                        && allowed.TryGetValue(i, out var set)
                        && set.Contains(predicted))
                    {
                        totalCovered++;
                        if (predicted == west[i])
                        {
                            totalCorrect++;
                        }
                    }
                }
            }
        }

        return new StatefulDecodeStats(usedPairs, totalAligned, totalCovered, totalCorrect);
    }

    private static Dictionary<(int Index, int EastValue, int? PrevEast), int> BuildStatefulMappingWithRunPositionOffset(
        Dictionary<int, int[]> sequences,
        IReadOnlyList<(int EastId, int WestId)> pairs,
        int maxOffset)
    {
        var counts = new Dictionary<(int Index, int EastValue, int? PrevEast), Dictionary<int, int>>();

        foreach (var (eastId, westId) in pairs)
        {
            if (!sequences.TryGetValue(eastId, out var east) || !sequences.TryGetValue(westId, out var west))
            {
                continue;
            }

            if (east.Length == 0 || west.Length == 0)
            {
                continue;
            }

            var offset = FindBestOffset(east, west, maxOffset);
            var startEast = Math.Max(0, -offset);
            var startWest = Math.Max(0, offset);
            var len = Math.Min(east.Length - startEast, west.Length - startWest);

            for (var i = 0; i < len; i++)
            {
                var indexA = startEast + i;
                var indexB = startWest + i;
                var prevEast = indexA > 0 ? east[indexA - 1] : (int?)null;
                var key = (indexA, east[indexA], prevEast);
                if (!counts.TryGetValue(key, out var map))
                {
                    map = new Dictionary<int, int>();
                    counts[key] = map;
                }

                var valueB = west[indexB];
                map[valueB] = map.TryGetValue(valueB, out var existing) ? existing + 1 : 1;
            }
        }

        var mapping = new Dictionary<(int Index, int EastValue, int? PrevEast), int>();
        foreach (var (key, map) in counts)
        {
            var bestValue = int.MaxValue;
            var bestCount = -1;
            foreach (var (value, count) in map)
            {
                if (count > bestCount || (count == bestCount && value < bestValue))
                {
                    bestCount = count;
                    bestValue = value;
                }
            }

            mapping[key] = bestValue;
        }

        return mapping;
    }

    private static StatefulDecodeStats EvaluateStatefulDecodeWithRunPositionOffset(
        Dictionary<int, int[]> sequences,
        IReadOnlyList<(int EastId, int WestId)> pairs,
        int maxOffset)
    {
        var totalAligned = 0;
        var totalCovered = 0;
        var totalCorrect = 0;
        var usedPairs = 0;

        foreach (var (eastId, westId) in pairs)
        {
            var training = pairs.Where(p => p != (eastId, westId)).ToList();
            var mapping = BuildStatefulMappingWithRunPositionOffset(sequences, training, maxOffset);

            if (!sequences.TryGetValue(eastId, out var east) || !sequences.TryGetValue(westId, out var west))
            {
                continue;
            }

            if (east.Length == 0 || west.Length == 0)
            {
                continue;
            }

            usedPairs++;

            var offset = FindBestOffset(east, west, maxOffset);
            var startEast = Math.Max(0, -offset);
            var startWest = Math.Max(0, offset);
            var len = Math.Min(east.Length - startEast, west.Length - startWest);

            totalAligned += len;
            for (var i = 0; i < len; i++)
            {
                var indexA = startEast + i;
                var indexB = startWest + i;
                var prevEast = indexA > 0 ? east[indexA - 1] : (int?)null;
                var key = (indexA, east[indexA], prevEast);
                if (mapping.TryGetValue(key, out var predicted))
                {
                    totalCovered++;
                    if (predicted == west[indexB])
                    {
                        totalCorrect++;
                    }
                }
            }
        }

        return new StatefulDecodeStats(usedPairs, totalAligned, totalCovered, totalCorrect);
    }

    private static int FindBestOffset(int[] east, int[] west, int maxOffset)
    {
        var bestOffset = 0;
        var bestMatches = -1;
        var bestAbs = int.MaxValue;

        for (var offset = -maxOffset; offset <= maxOffset; offset++)
        {
            var startEast = Math.Max(0, -offset);
            var startWest = Math.Max(0, offset);
            var len = Math.Min(east.Length - startEast, west.Length - startWest);
            if (len <= 0)
            {
                continue;
            }

            var matches = 0;
            for (var i = 0; i < len; i++)
            {
                if (east[startEast + i] == west[startWest + i])
                {
                    matches++;
                }
            }

            var abs = Math.Abs(offset);
            if (matches > bestMatches || (matches == bestMatches && (abs < bestAbs || (abs == bestAbs && offset < bestOffset))))
            {
                bestMatches = matches;
                bestAbs = abs;
                bestOffset = offset;
            }
        }

        return bestOffset;
    }

    private static List<(int EastId, int WestId)> BuildWithinSidePairs(
        IEnumerable<int> ids,
        IReadOnlyDictionary<int, int[]> sequences)
    {
        var available = ids.Where(id => sequences.ContainsKey(id)).ToArray();
        var pairs = new List<(int EastId, int WestId)>();
        for (var i = 0; i < available.Length; i++)
        {
            for (var j = 0; j < available.Length; j++)
            {
                if (i == j)
                {
                    continue;
                }

                pairs.Add((available[i], available[j]));
            }
        }

        return pairs;
    }

    private static List<(int EastId, int WestId)> BuildCrossSidePairs(
        IEnumerable<int> eastIds,
        IEnumerable<int> westIds)
    {
        var pairs = new List<(int EastId, int WestId)>();
        foreach (var east in eastIds)
        {
            foreach (var west in westIds)
            {
                pairs.Add((east, west));
            }
        }

        return pairs;
    }

    private static (int Conflicts, int Mappings) ComputeStatefulMappingConflicts(
        Dictionary<int, int[]> sequences,
        IReadOnlyList<(int EastId, int WestId)> pairs,
        bool useAlignment)
    {
        var mapping = new Dictionary<(int Index, int EastValue, int? PrevEast), HashSet<int>>();

        foreach (var (eastId, westId) in pairs)
        {
            if (!sequences.TryGetValue(eastId, out var east) || !sequences.TryGetValue(westId, out var west))
            {
                continue;
            }

            if (east.Length == 0 || west.Length == 0)
            {
                continue;
            }

            if (useAlignment)
            {
                var alignment = SequenceAlignment.Align(east, west);
                foreach (var step in alignment.Steps)
                {
                    if (step.ValueA.HasValue && step.ValueB.HasValue && step.IndexA.HasValue)
                    {
                        var index = step.IndexA.Value;
                        var prevEast = index > 0 ? east[index - 1] : (int?)null;
                        var key = (index, step.ValueA.Value, prevEast);
                        if (!mapping.TryGetValue(key, out var set))
                        {
                            set = new HashSet<int>();
                            mapping[key] = set;
                        }

                        set.Add(step.ValueB.Value);
                    }
                }
            }
            else
            {
                var len = Math.Min(east.Length, west.Length);
                for (var i = 0; i < len; i++)
                {
                    var prevEast = i > 0 ? east[i - 1] : (int?)null;
                    var key = (i, east[i], prevEast);
                    if (!mapping.TryGetValue(key, out var set))
                    {
                        set = new HashSet<int>();
                        mapping[key] = set;
                    }

                    set.Add(west[i]);
                }
            }
        }

        var conflicts = mapping.Count(kvp => kvp.Value.Count > 1);
        return (conflicts, mapping.Count);
    }

    private static StatefulDecodeStats EvaluateStatefulDecodeWithRunPositionUsingMapping(
        Dictionary<int, int[]> sequences,
        IReadOnlyList<(int EastId, int WestId)> pairs,
        Dictionary<(int Index, int EastValue, int? PrevEast), int> mapping,
        bool useAlignment)
    {
        var totalAligned = 0;
        var totalCovered = 0;
        var totalCorrect = 0;
        var usedPairs = 0;

        foreach (var (eastId, westId) in pairs)
        {
            if (!sequences.TryGetValue(eastId, out var east) || !sequences.TryGetValue(westId, out var west))
            {
                continue;
            }

            if (east.Length == 0 || west.Length == 0)
            {
                continue;
            }

            usedPairs++;

            if (useAlignment)
            {
                var alignment = SequenceAlignment.Align(east, west);
                foreach (var step in alignment.Steps)
                {
                    if (step.ValueA.HasValue && step.ValueB.HasValue && step.IndexA.HasValue)
                    {
                        totalAligned++;
                        var index = step.IndexA.Value;
                        var prevEast = index > 0 ? east[index - 1] : (int?)null;
                        var key = (index, step.ValueA.Value, prevEast);
                        if (mapping.TryGetValue(key, out var predicted))
                        {
                            totalCovered++;
                            if (predicted == step.ValueB.Value)
                            {
                                totalCorrect++;
                            }
                        }
                    }
                }
            }
            else
            {
                var len = Math.Min(east.Length, west.Length);
                totalAligned += len;
                for (var i = 0; i < len; i++)
                {
                    var prevEast = i > 0 ? east[i - 1] : (int?)null;
                    var key = (i, east[i], prevEast);
                    if (mapping.TryGetValue(key, out var predicted))
                    {
                        totalCovered++;
                        if (predicted == west[i])
                        {
                            totalCorrect++;
                        }
                    }
                }
            }
        }

        return new StatefulDecodeStats(usedPairs, totalAligned, totalCovered, totalCorrect);
    }

    private static (Dictionary<int, int> BinMap, Dictionary<int, HashSet<int>> AllowedBins) BuildAllowedBinSets(
        Dictionary<int, int[]> sequences,
        IReadOnlyList<(int EastId, int WestId)> pairs)
    {
        var westSequences = new List<int[]>();
        foreach (var (_, westId) in pairs)
        {
            if (sequences.TryGetValue(westId, out var west) && west.Length > 0)
            {
                westSequences.Add(west);
            }
        }

        var sourceSequences = westSequences.Count > 0 ? westSequences : sequences.Values.ToList();
        var binMap = BuildFrequencyBins(sourceSequences, highCount: 8, midCount: 8);

        var allowedBins = new Dictionary<int, HashSet<int>>();
        foreach (var west in westSequences)
        {
            for (var i = 0; i < west.Length; i++)
            {
                var bin = binMap[west[i]];
                if (!allowedBins.TryGetValue(i, out var set))
                {
                    set = new HashSet<int>();
                    allowedBins[i] = set;
                }

                set.Add(bin);
            }
        }

        return (binMap, allowedBins);
    }

    private static StatefulDecodeStats EvaluateStatefulDecodeWithRunPositionBinConstraints(
        Dictionary<int, int[]> sequences,
        IReadOnlyList<(int EastId, int WestId)> pairs)
    {
        var totalAligned = 0;
        var totalCovered = 0;
        var totalCorrect = 0;
        var usedPairs = 0;

        foreach (var (eastId, westId) in pairs)
        {
            var training = pairs.Where(p => p != (eastId, westId)).ToList();
            var mapping = BuildStatefulMappingWithRunPosition(sequences, training, useAlignment: true);
            var (binMap, allowedBins) = BuildAllowedBinSets(sequences, training);

            if (!sequences.TryGetValue(eastId, out var east) || !sequences.TryGetValue(westId, out var west))
            {
                continue;
            }

            if (east.Length == 0 || west.Length == 0)
            {
                continue;
            }

            usedPairs++;

            var alignment = SequenceAlignment.Align(east, west);
            foreach (var step in alignment.Steps)
            {
                if (step.ValueA.HasValue && step.ValueB.HasValue && step.IndexA.HasValue)
                {
                    totalAligned++;
                    var index = step.IndexA.Value;
                    var prevEast = index > 0 ? east[index - 1] : (int?)null;
                    var key = (index, step.ValueA.Value, prevEast);
                    if (mapping.TryGetValue(key, out var predicted))
                    {
                        var bin = binMap[predicted];
                        if (allowedBins.TryGetValue(index, out var allowed) && allowed.Contains(bin))
                        {
                            totalCovered++;
                            if (predicted == step.ValueB.Value)
                            {
                                totalCorrect++;
                            }
                        }
                    }
                }
            }
        }

        return new StatefulDecodeStats(usedPairs, totalAligned, totalCovered, totalCorrect);
    }

    private static Dictionary<(int Index, int EastValue, int? PrevEast), Dictionary<int, int>> BuildStatefulMappingCountsWithRunPosition(
        Dictionary<int, int[]> sequences,
        IReadOnlyList<(int EastId, int WestId)> pairs,
        bool useAlignment)
    {
        var counts = new Dictionary<(int Index, int EastValue, int? PrevEast), Dictionary<int, int>>();

        foreach (var (eastId, westId) in pairs)
        {
            if (!sequences.TryGetValue(eastId, out var east) || !sequences.TryGetValue(westId, out var west))
            {
                continue;
            }

            if (east.Length == 0 || west.Length == 0)
            {
                continue;
            }

            if (useAlignment)
            {
                foreach (var (index, eastValue, westValue) in EnumerateAlignedValues(east, west, useAlignment: true))
                {
                    var prevEast = index > 0 ? east[index - 1] : (int?)null;
                    var key = (index, eastValue, prevEast);
                    if (!counts.TryGetValue(key, out var map))
                    {
                        map = new Dictionary<int, int>();
                        counts[key] = map;
                    }

                    map[westValue] = map.TryGetValue(westValue, out var existing) ? existing + 1 : 1;
                }
            }
            else
            {
                var len = Math.Min(east.Length, west.Length);
                for (var i = 0; i < len; i++)
                {
                    var prevEast = i > 0 ? east[i - 1] : (int?)null;
                    var key = (i, east[i], prevEast);
                    if (!counts.TryGetValue(key, out var map))
                    {
                        map = new Dictionary<int, int>();
                        counts[key] = map;
                    }

                    var valueB = west[i];
                    map[valueB] = map.TryGetValue(valueB, out var existing) ? existing + 1 : 1;
                }
            }
        }

        return counts;
    }

    private static int? SelectCandidateWithBinBonus(
        Dictionary<int, int> counts,
        IReadOnlyDictionary<int, int> binMap,
        HashSet<int>? allowedBins,
        double binBonus)
    {
        var bestValue = (int?)null;
        var bestScore = double.NegativeInfinity;

        foreach (var (value, count) in counts)
        {
            var score = (double)count;
            if (allowedBins != null && allowedBins.Contains(binMap[value]))
            {
                score += binBonus;
            }

            if (score > bestScore || (Math.Abs(score - bestScore) < 1e-9 && (bestValue == null || value < bestValue)))
            {
                bestScore = score;
                bestValue = value;
            }
        }

        return bestValue;
    }

    private static StatefulDecodeStats EvaluateStatefulDecodeWithAdaptiveConstraints(
        Dictionary<int, int[]> sequences,
        IReadOnlyList<(int EastId, int WestId)> pairs,
        bool useAlignment,
        double binBonus)
    {
        var totalAligned = 0;
        var totalCovered = 0;
        var totalCorrect = 0;
        var usedPairs = 0;

        foreach (var (eastId, westId) in pairs)
        {
            var training = pairs.Where(p => p != (eastId, westId)).ToList();
            var mappingCounts = BuildStatefulMappingCountsWithRunPosition(sequences, training, useAlignment);
            var (binMap, allowedBins) = BuildAllowedBinSets(sequences, training);

            if (!sequences.TryGetValue(eastId, out var east) || !sequences.TryGetValue(westId, out var west))
            {
                continue;
            }

            if (east.Length == 0 || west.Length == 0)
            {
                continue;
            }

            usedPairs++;

            foreach (var (index, eastValue, westValue) in EnumerateAlignedValues(east, west, useAlignment))
            {
                totalAligned++;
                var prevEast = index > 0 ? east[index - 1] : (int?)null;
                var key = (index, eastValue, prevEast);
                if (mappingCounts.TryGetValue(key, out var counts))
                {
                    allowedBins.TryGetValue(index, out var allowed);
                    var predicted = SelectCandidateWithBinBonus(counts, binMap, allowed, binBonus);
                    if (predicted.HasValue)
                    {
                        totalCovered++;
                        if (predicted.Value == westValue)
                        {
                            totalCorrect++;
                        }
                    }
                }
            }
        }

        return new StatefulDecodeStats(usedPairs, totalAligned, totalCovered, totalCorrect);
    }

    private static StatefulDecodeStats EvaluateStatefulDecodeWithEntropyGatedBinConstraints(
        Dictionary<int, int[]> sequences,
        IReadOnlyList<(int EastId, int WestId)> pairs,
        bool useAlignment,
        double entropyThreshold,
        double overlapThreshold)
    {
        var eastIds = new HashSet<int> { 0, 2, 4, 6, 8 };
        var westIds = new HashSet<int> { 1, 3, 5, 7 };

        var eastSequences = sequences.Where(kvp => eastIds.Contains(kvp.Key)).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        var westSequences = sequences.Where(kvp => westIds.Contains(kvp.Key)).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

        var entropyStats = ComputeColumnStats(sequences);
        var eastSets = ComputeColumnValueSets(eastSequences, minCoverage: 4);
        var westSets = ComputeColumnValueSets(westSequences, minCoverage: 2);

        var overlapByColumn = new Dictionary<int, double>();
        foreach (var col in eastSets.Keys.Intersect(westSets.Keys))
        {
            overlapByColumn[col] = ComputeJaccard(eastSets[col].Values, westSets[col].Values).Jaccard;
        }

        var totalAligned = 0;
        var totalCovered = 0;
        var totalCorrect = 0;
        var usedPairs = 0;

        foreach (var (eastId, westId) in pairs)
        {
            var training = pairs.Where(p => p != (eastId, westId)).ToList();
            var mapping = BuildStatefulMappingWithRunPosition(sequences, training, useAlignment);
            var (binMap, allowedBins) = BuildAllowedBinSets(sequences, training);

            if (!sequences.TryGetValue(eastId, out var east) || !sequences.TryGetValue(westId, out var west))
            {
                continue;
            }

            if (east.Length == 0 || west.Length == 0)
            {
                continue;
            }

            usedPairs++;

            foreach (var (index, eastValue, westValue) in EnumerateAlignedValues(east, west, useAlignment))
            {
                totalAligned++;
                var prevEast = index > 0 ? east[index - 1] : (int?)null;
                var key = (index, eastValue, prevEast);
                if (!mapping.TryGetValue(key, out var predicted))
                {
                    continue;
                }

                var applyConstraint = entropyStats.TryGetValue(index, out var stat)
                                      && stat.Entropy <= entropyThreshold
                                      && overlapByColumn.TryGetValue(index, out var overlap)
                                      && overlap >= overlapThreshold;

                if (applyConstraint)
                {
                    if (!allowedBins.TryGetValue(index, out var allowed))
                    {
                        continue;
                    }

                    var bin = binMap[predicted];
                    if (!allowed.Contains(bin))
                    {
                        continue;
                    }
                }

                totalCovered++;
                if (predicted == westValue)
                {
                    totalCorrect++;
                }
            }
        }

        return new StatefulDecodeStats(usedPairs, totalAligned, totalCovered, totalCorrect);
    }

    private static StatefulDecodeStats EvaluateStatefulDecodeWithRunPositionWindow(
        Dictionary<int, int[]> sequences,
        IReadOnlyList<(int EastId, int WestId)> pairs,
        bool useAlignment,
        int minIndexInclusive,
        int? maxIndexExclusive)
    {
        var totalAligned = 0;
        var totalCovered = 0;
        var totalCorrect = 0;
        var usedPairs = 0;

        foreach (var (eastId, westId) in pairs)
        {
            var training = pairs.Where(p => p != (eastId, westId)).ToList();
            var mapping = BuildStatefulMappingWithRunPosition(sequences, training, useAlignment);

            if (!sequences.TryGetValue(eastId, out var east) || !sequences.TryGetValue(westId, out var west))
            {
                continue;
            }

            if (east.Length == 0 || west.Length == 0)
            {
                continue;
            }

            usedPairs++;

            foreach (var (index, eastValue, westValue) in EnumerateAlignedValues(east, west, useAlignment))
            {
                if (index < minIndexInclusive || (maxIndexExclusive.HasValue && index >= maxIndexExclusive.Value))
                {
                    continue;
                }

                totalAligned++;
                var prevEast = index > 0 ? east[index - 1] : (int?)null;
                var key = (index, eastValue, prevEast);
                if (mapping.TryGetValue(key, out var predicted))
                {
                    totalCovered++;
                    if (predicted == westValue)
                    {
                        totalCorrect++;
                    }
                }
            }
        }

        return new StatefulDecodeStats(usedPairs, totalAligned, totalCovered, totalCorrect);
    }

    private static bool TryGetRowPairRunStartIndex(EyeMessage message, int rowPair, out int startIndex)
    {
        var sequence = GetBodyRowPairSequence(message);
        for (var i = 0; i < sequence.Length; i++)
        {
            if (sequence[i] == rowPair)
            {
                startIndex = i;
                return true;
            }
        }

        startIndex = -1;
        return false;
    }

    private static StatefulDecodeStats EvaluateStatefulDecodeWithAnchorOffsets(
        Dictionary<int, int[]> sequences,
        IReadOnlyList<(int EastId, int WestId)> pairs,
        IReadOnlyDictionary<int, int> anchorStarts)
    {
        var totalAligned = 0;
        var totalCovered = 0;
        var totalCorrect = 0;
        var usedPairs = 0;

        foreach (var (eastId, westId) in pairs)
        {
            if (!anchorStarts.TryGetValue(eastId, out var eastStart) || !anchorStarts.TryGetValue(westId, out var westStart))
            {
                continue;
            }

            var training = pairs.Where(p => p != (eastId, westId)).ToList();
            var counts = new Dictionary<(int Index, int EastValue, int? PrevEast), Dictionary<int, int>>();
            foreach (var (trainEastId, trainWestId) in training)
            {
                if (!anchorStarts.TryGetValue(trainEastId, out var trainEastStart)
                    || !anchorStarts.TryGetValue(trainWestId, out var trainWestStart))
                {
                    continue;
                }

                if (!sequences.TryGetValue(trainEastId, out var trainEast)
                    || !sequences.TryGetValue(trainWestId, out var trainWest)
                    || trainEast.Length == 0
                    || trainWest.Length == 0)
                {
                    continue;
                }

                var offset = trainEastStart - trainWestStart;
                for (var i = 0; i < trainEast.Length; i++)
                {
                    var j = i + offset;
                    if (j < 0 || j >= trainWest.Length)
                    {
                        continue;
                    }

                    var prevEast = i > 0 ? trainEast[i - 1] : (int?)null;
                    var key = (i, trainEast[i], prevEast);
                    if (!counts.TryGetValue(key, out var map))
                    {
                        map = new Dictionary<int, int>();
                        counts[key] = map;
                    }

                    var valueB = trainWest[j];
                    map[valueB] = map.TryGetValue(valueB, out var existing) ? existing + 1 : 1;
                }
            }

            var mapping = new Dictionary<(int Index, int EastValue, int? PrevEast), int>();
            foreach (var (key, map) in counts)
            {
                var bestValue = int.MaxValue;
                var bestCount = -1;
                foreach (var (value, count) in map)
                {
                    if (count > bestCount || (count == bestCount && value < bestValue))
                    {
                        bestCount = count;
                        bestValue = value;
                    }
                }

                mapping[key] = bestValue;
            }

            if (!sequences.TryGetValue(eastId, out var east) || !sequences.TryGetValue(westId, out var west))
            {
                continue;
            }

            if (east.Length == 0 || west.Length == 0)
            {
                continue;
            }

            usedPairs++;

            var evalOffset = eastStart - westStart;
            for (var i = 0; i < east.Length; i++)
            {
                var j = i + evalOffset;
                if (j < 0 || j >= west.Length)
                {
                    continue;
                }

                totalAligned++;
                var prevEast = i > 0 ? east[i - 1] : (int?)null;
                var key = (i, east[i], prevEast);
                if (mapping.TryGetValue(key, out var predicted))
                {
                    totalCovered++;
                    if (predicted == west[j])
                    {
                        totalCorrect++;
                    }
                }
            }
        }

        return new StatefulDecodeStats(usedPairs, totalAligned, totalCovered, totalCorrect);
    }

    private static StatefulDecodeStats EvaluateConditionalDigitMapping(
        Dictionary<int, int[]> sequences,
        IReadOnlyList<(int EastId, int WestId)> pairs,
        Func<int, int> conditionSelector,
        Func<int, int> targetSelector,
        bool useAlignment)
    {
        var totalAligned = 0;
        var totalCovered = 0;
        var totalCorrect = 0;
        var usedPairs = 0;

        foreach (var (eastId, westId) in pairs)
        {
            var training = pairs.Where(p => p != (eastId, westId)).ToList();
            var mapping = BuildConditionalDigitMappingWithRunPosition(sequences, training, conditionSelector, targetSelector, useAlignment);

            if (!sequences.TryGetValue(eastId, out var east) || !sequences.TryGetValue(westId, out var west))
            {
                continue;
            }

            if (east.Length == 0 || west.Length == 0)
            {
                continue;
            }

            usedPairs++;

            foreach (var (index, _, westValue) in EnumerateAlignedValues(east, west, useAlignment))
            {
                totalAligned++;
                var condition = conditionSelector(westValue);
                var key = (index, condition);
                if (mapping.TryGetValue(key, out var predicted))
                {
                    totalCovered++;
                    if (predicted == targetSelector(westValue))
                    {
                        totalCorrect++;
                    }
                }
            }
        }

        return new StatefulDecodeStats(usedPairs, totalAligned, totalCovered, totalCorrect);
    }

    private static StatefulDecodeStats EvaluateTwoStageDigitDecode(
        Dictionary<int, int[]> sequences,
        IReadOnlyList<(int EastId, int WestId)> pairs,
        bool useAlignment,
        TwoStageDigitMode mode)
    {
        var totalAligned = 0;
        var totalCovered = 0;
        var totalCorrect = 0;
        var usedPairs = 0;

        foreach (var (eastId, westId) in pairs)
        {
            var training = pairs.Where(p => p != (eastId, westId)).ToList();
            var topMapping = BuildDigitMappingWithRunPosition(sequences, training, value => value / 5, useAlignment);
            var bottomMapping = BuildDigitMappingWithRunPosition(sequences, training, value => value % 5, useAlignment);
            var bottomGivenTop = BuildConditionalDigitMappingWithRunPosition(sequences, training, value => value / 5, value => value % 5, useAlignment);
            var topGivenBottom = BuildConditionalDigitMappingWithRunPosition(sequences, training, value => value % 5, value => value / 5, useAlignment);

            if (!sequences.TryGetValue(eastId, out var east) || !sequences.TryGetValue(westId, out var west))
            {
                continue;
            }

            if (east.Length == 0 || west.Length == 0)
            {
                continue;
            }

            usedPairs++;

            foreach (var (index, eastValue, westValue) in EnumerateAlignedValues(east, west, useAlignment))
            {
                totalAligned++;
                var eastTop = eastValue / 5;
                var eastBottom = eastValue % 5;
                var prevEastTop = index > 0 ? east[index - 1] / 5 : (int?)null;
                var prevEastBottom = index > 0 ? east[index - 1] % 5 : (int?)null;

                int? predictedTop = null;
                int? predictedBottom = null;

                if (mode == TwoStageDigitMode.Independent || mode == TwoStageDigitMode.TopThenConditionalBottom)
                {
                    var topKey = (index, eastTop, prevEastTop);
                    if (topMapping.TryGetValue(topKey, out var topPred))
                    {
                        predictedTop = topPred;
                    }
                }

                if (mode == TwoStageDigitMode.Independent || mode == TwoStageDigitMode.BottomThenConditionalTop)
                {
                    var bottomKey = (index, eastBottom, prevEastBottom);
                    if (bottomMapping.TryGetValue(bottomKey, out var bottomPred))
                    {
                        predictedBottom = bottomPred;
                    }
                }

                if (mode == TwoStageDigitMode.TopThenConditionalBottom)
                {
                    if (predictedTop.HasValue && bottomGivenTop.TryGetValue((index, predictedTop.Value), out var bottomPred))
                    {
                        predictedBottom = bottomPred;
                    }
                    else
                    {
                        predictedBottom = null;
                    }
                }
                else if (mode == TwoStageDigitMode.BottomThenConditionalTop)
                {
                    if (predictedBottom.HasValue && topGivenBottom.TryGetValue((index, predictedBottom.Value), out var topPred))
                    {
                        predictedTop = topPred;
                    }
                    else
                    {
                        predictedTop = null;
                    }
                }

                if (predictedTop.HasValue && predictedBottom.HasValue)
                {
                    totalCovered++;
                    var predictedValue = predictedTop.Value * 5 + predictedBottom.Value;
                    if (predictedValue == westValue)
                    {
                        totalCorrect++;
                    }
                }
            }
        }

        return new StatefulDecodeStats(usedPairs, totalAligned, totalCovered, totalCorrect);
    }

    private static Dictionary<(int Index, int EastDigit, int? PrevEastDigit), int> BuildDigitMappingWithRunPosition(
        Dictionary<int, int[]> sequences,
        IReadOnlyList<(int EastId, int WestId)> pairs,
        Func<int, int> digitSelector,
        bool useAlignment)
    {
        var counts = new Dictionary<(int Index, int EastDigit, int? PrevEastDigit), Dictionary<int, int>>();

        foreach (var (eastId, westId) in pairs)
        {
            if (!sequences.TryGetValue(eastId, out var east) || !sequences.TryGetValue(westId, out var west))
            {
                continue;
            }

            if (east.Length == 0 || west.Length == 0)
            {
                continue;
            }

            foreach (var (index, eastValue, westValue) in EnumerateAlignedValues(east, west, useAlignment))
            {
                var eastDigit = digitSelector(eastValue);
                var prevEastDigit = index > 0 ? digitSelector(east[index - 1]) : (int?)null;
                var key = (index, eastDigit, prevEastDigit);
                if (!counts.TryGetValue(key, out var map))
                {
                    map = new Dictionary<int, int>();
                    counts[key] = map;
                }

                var westDigit = digitSelector(westValue);
                map[westDigit] = map.TryGetValue(westDigit, out var existing) ? existing + 1 : 1;
            }
        }

        var mapping = new Dictionary<(int Index, int EastDigit, int? PrevEastDigit), int>();
        foreach (var (key, map) in counts)
        {
            var bestValue = int.MaxValue;
            var bestCount = -1;
            foreach (var (value, count) in map)
            {
                if (count > bestCount || (count == bestCount && value < bestValue))
                {
                    bestCount = count;
                    bestValue = value;
                }
            }

            mapping[key] = bestValue;
        }

        return mapping;
    }

    private static Dictionary<(int Index, int ConditionDigit), int> BuildConditionalDigitMappingWithRunPosition(
        Dictionary<int, int[]> sequences,
        IReadOnlyList<(int EastId, int WestId)> pairs,
        Func<int, int> conditionSelector,
        Func<int, int> targetSelector,
        bool useAlignment)
    {
        var counts = new Dictionary<(int Index, int ConditionDigit), Dictionary<int, int>>();

        foreach (var (eastId, westId) in pairs)
        {
            if (!sequences.TryGetValue(eastId, out var east) || !sequences.TryGetValue(westId, out var west))
            {
                continue;
            }

            if (east.Length == 0 || west.Length == 0)
            {
                continue;
            }

            foreach (var (index, _, westValue) in EnumerateAlignedValues(east, west, useAlignment))
            {
                var condition = conditionSelector(westValue);
                var key = (index, condition);
                if (!counts.TryGetValue(key, out var map))
                {
                    map = new Dictionary<int, int>();
                    counts[key] = map;
                }

                var target = targetSelector(westValue);
                map[target] = map.TryGetValue(target, out var existing) ? existing + 1 : 1;
            }
        }

        var mapping = new Dictionary<(int Index, int ConditionDigit), int>();
        foreach (var (key, map) in counts)
        {
            var bestValue = int.MaxValue;
            var bestCount = -1;
            foreach (var (value, count) in map)
            {
                if (count > bestCount || (count == bestCount && value < bestValue))
                {
                    bestCount = count;
                    bestValue = value;
                }
            }

            mapping[key] = bestValue;
        }

        return mapping;
    }

    private static IEnumerable<(int Index, int EastValue, int WestValue)> EnumerateAlignedValues(
        int[] east,
        int[] west,
        bool useAlignment)
    {
        if (useAlignment)
        {
            var alignment = SequenceAlignment.Align(east, west);
            foreach (var step in alignment.Steps)
            {
                if (step.ValueA.HasValue && step.ValueB.HasValue && step.IndexA.HasValue)
                {
                    yield return (step.IndexA.Value, step.ValueA.Value, step.ValueB.Value);
                }
            }

            yield break;
        }

        var len = Math.Min(east.Length, west.Length);
        for (var i = 0; i < len; i++)
        {
            yield return (i, east[i], west[i]);
        }
    }

    private static IEnumerable<AlignmentStep> OffsetAlignmentSteps(
        IReadOnlyList<AlignmentStep> steps,
        int offsetA,
        int offsetB)
    {
        foreach (var step in steps)
        {
            var indexA = step.IndexA.HasValue ? step.IndexA + offsetA : null;
            var indexB = step.IndexB.HasValue ? step.IndexB + offsetB : null;
            yield return new AlignmentStep(indexA, indexB, step.ValueA, step.ValueB);
        }
    }

    private static IReadOnlyList<AlignmentStep> AlignSegmented(int[] east, int[] west, int splitIndex)
    {
        var steps = new List<AlignmentStep>();

        var startA = Math.Min(splitIndex, east.Length);
        var startB = Math.Min(splitIndex, west.Length);

        var earlyA = east.Take(startA).ToArray();
        var earlyB = west.Take(startB).ToArray();

        if (earlyA.Length > 0 || earlyB.Length > 0)
        {
            var early = SequenceAlignment.Align(earlyA, earlyB);
            steps.AddRange(OffsetAlignmentSteps(early.Steps, 0, 0));
        }

        var lateA = east.Skip(startA).ToArray();
        var lateB = west.Skip(startB).ToArray();
        if (lateA.Length > 0 || lateB.Length > 0)
        {
            var late = SequenceAlignment.Align(lateA, lateB);
            steps.AddRange(OffsetAlignmentSteps(late.Steps, startA, startB));
        }

        return steps;
    }

    private static Dictionary<(int Index, int EastValue, int? PrevEast), int> BuildStatefulMappingWithSegmentedAlignment(
        Dictionary<int, int[]> sequences,
        IReadOnlyList<(int EastId, int WestId)> pairs,
        int splitIndex)
    {
        var counts = new Dictionary<(int Index, int EastValue, int? PrevEast), Dictionary<int, int>>();

        foreach (var (eastId, westId) in pairs)
        {
            if (!sequences.TryGetValue(eastId, out var east) || !sequences.TryGetValue(westId, out var west))
            {
                continue;
            }

            if (east.Length == 0 || west.Length == 0)
            {
                continue;
            }

            var steps = AlignSegmented(east, west, splitIndex);
            foreach (var step in steps)
            {
                if (step.ValueA.HasValue && step.ValueB.HasValue && step.IndexA.HasValue)
                {
                    var index = step.IndexA.Value;
                    var prevEast = index > 0 ? east[index - 1] : (int?)null;
                    var key = (index, step.ValueA.Value, prevEast);
                    if (!counts.TryGetValue(key, out var map))
                    {
                        map = new Dictionary<int, int>();
                        counts[key] = map;
                    }

                    var valueB = step.ValueB.Value;
                    map[valueB] = map.TryGetValue(valueB, out var existing) ? existing + 1 : 1;
                }
            }
        }

        var mapping = new Dictionary<(int Index, int EastValue, int? PrevEast), int>();
        foreach (var (key, map) in counts)
        {
            var bestValue = int.MaxValue;
            var bestCount = -1;
            foreach (var (value, count) in map)
            {
                if (count > bestCount || (count == bestCount && value < bestValue))
                {
                    bestCount = count;
                    bestValue = value;
                }
            }

            mapping[key] = bestValue;
        }

        return mapping;
    }

    private static StatefulDecodeStats EvaluateStatefulDecodeWithSegmentedAlignment(
        Dictionary<int, int[]> sequences,
        IReadOnlyList<(int EastId, int WestId)> pairs,
        int splitIndex)
    {
        var totalAligned = 0;
        var totalCovered = 0;
        var totalCorrect = 0;
        var usedPairs = 0;

        foreach (var (eastId, westId) in pairs)
        {
            var training = pairs.Where(p => p != (eastId, westId)).ToList();
            var mapping = BuildStatefulMappingWithSegmentedAlignment(sequences, training, splitIndex);

            if (!sequences.TryGetValue(eastId, out var east) || !sequences.TryGetValue(westId, out var west))
            {
                continue;
            }

            if (east.Length == 0 || west.Length == 0)
            {
                continue;
            }

            usedPairs++;

            var steps = AlignSegmented(east, west, splitIndex);
            foreach (var step in steps)
            {
                if (step.ValueA.HasValue && step.ValueB.HasValue && step.IndexA.HasValue)
                {
                    totalAligned++;
                    var index = step.IndexA.Value;
                    var prevEast = index > 0 ? east[index - 1] : (int?)null;
                    var key = (index, step.ValueA.Value, prevEast);
                    if (mapping.TryGetValue(key, out var predicted))
                    {
                        totalCovered++;
                        if (predicted == step.ValueB.Value)
                        {
                            totalCorrect++;
                        }
                    }
                }
            }
        }

        return new StatefulDecodeStats(usedPairs, totalAligned, totalCovered, totalCorrect);
    }

    private static IReadOnlyList<AlignmentStep> AlignByBins(
        int[] east,
        int[] west,
        IReadOnlyDictionary<int, int> binMap)
    {
        var eastBins = east.Select(v => binMap[v]).ToArray();
        var westBins = west.Select(v => binMap[v]).ToArray();
        return SequenceAlignment.Align(eastBins, westBins).Steps;
    }

    private static Dictionary<(int Index, int EastValue, int? PrevEast), int> BuildStatefulMappingWithBinAlignment(
        Dictionary<int, int[]> sequences,
        IReadOnlyList<(int EastId, int WestId)> pairs,
        IReadOnlyDictionary<int, int> binMap)
    {
        var counts = new Dictionary<(int Index, int EastValue, int? PrevEast), Dictionary<int, int>>();

        foreach (var (eastId, westId) in pairs)
        {
            if (!sequences.TryGetValue(eastId, out var east) || !sequences.TryGetValue(westId, out var west))
            {
                continue;
            }

            if (east.Length == 0 || west.Length == 0)
            {
                continue;
            }

            var steps = AlignByBins(east, west, binMap);
            foreach (var step in steps)
            {
                if (step.IndexA.HasValue && step.IndexB.HasValue)
                {
                    var indexA = step.IndexA.Value;
                    var indexB = step.IndexB.Value;
                    var valueA = east[indexA];
                    var valueB = west[indexB];
                    var prevEast = indexA > 0 ? east[indexA - 1] : (int?)null;
                    var key = (indexA, valueA, prevEast);
                    if (!counts.TryGetValue(key, out var map))
                    {
                        map = new Dictionary<int, int>();
                        counts[key] = map;
                    }

                    map[valueB] = map.TryGetValue(valueB, out var existing) ? existing + 1 : 1;
                }
            }
        }

        var mapping = new Dictionary<(int Index, int EastValue, int? PrevEast), int>();
        foreach (var (key, map) in counts)
        {
            var bestValue = int.MaxValue;
            var bestCount = -1;
            foreach (var (value, count) in map)
            {
                if (count > bestCount || (count == bestCount && value < bestValue))
                {
                    bestCount = count;
                    bestValue = value;
                }
            }

            mapping[key] = bestValue;
        }

        return mapping;
    }

    private static StatefulDecodeStats EvaluateStatefulDecodeWithBinAlignment(
        Dictionary<int, int[]> sequences,
        IReadOnlyList<(int EastId, int WestId)> pairs,
        IReadOnlyDictionary<int, int> binMap)
    {
        var totalAligned = 0;
        var totalCovered = 0;
        var totalCorrect = 0;
        var usedPairs = 0;

        foreach (var (eastId, westId) in pairs)
        {
            var training = pairs.Where(p => p != (eastId, westId)).ToList();
            var mapping = BuildStatefulMappingWithBinAlignment(sequences, training, binMap);

            if (!sequences.TryGetValue(eastId, out var east) || !sequences.TryGetValue(westId, out var west))
            {
                continue;
            }

            if (east.Length == 0 || west.Length == 0)
            {
                continue;
            }

            usedPairs++;

            var steps = AlignByBins(east, west, binMap);
            foreach (var step in steps)
            {
                if (step.IndexA.HasValue && step.IndexB.HasValue)
                {
                    totalAligned++;
                    var indexA = step.IndexA.Value;
                    var indexB = step.IndexB.Value;
                    var prevEast = indexA > 0 ? east[indexA - 1] : (int?)null;
                    var key = (indexA, east[indexA], prevEast);
                    if (mapping.TryGetValue(key, out var predicted))
                    {
                        totalCovered++;
                        if (predicted == west[indexB])
                        {
                            totalCorrect++;
                        }
                    }
                }
            }
        }

        return new StatefulDecodeStats(usedPairs, totalAligned, totalCovered, totalCorrect);
    }

    private static Dictionary<int, HashSet<int>> BuildHybridAllowedSetsFromWest(
        Dictionary<int, int[]> sequences,
        IReadOnlyList<(int EastId, int WestId)> pairs)
    {
        var westSequences = new List<int[]>();
        foreach (var (_, westId) in pairs)
        {
            if (sequences.TryGetValue(westId, out var west) && west.Length > 0)
            {
                westSequences.Add(west);
            }
        }

        if (westSequences.Count == 0)
        {
            return new Dictionary<int, HashSet<int>>();
        }

        var binMap = BuildFrequencyBins(westSequences, highCount: 8, midCount: 8);
        var topSets = new Dictionary<int, HashSet<int>>();
        var bottomSets = new Dictionary<int, HashSet<int>>();
        var binSets = new Dictionary<int, HashSet<int>>();

        foreach (var west in westSequences)
        {
            for (var i = 0; i < west.Length; i++)
            {
                var value = west[i];
                var top = value / 5;
                var bottom = value % 5;
                var bin = binMap[value];

                if (!topSets.TryGetValue(i, out var topSet))
                {
                    topSet = new HashSet<int>();
                    topSets[i] = topSet;
                }

                if (!bottomSets.TryGetValue(i, out var bottomSet))
                {
                    bottomSet = new HashSet<int>();
                    bottomSets[i] = bottomSet;
                }

                if (!binSets.TryGetValue(i, out var binSet))
                {
                    binSet = new HashSet<int>();
                    binSets[i] = binSet;
                }

                topSet.Add(top);
                bottomSet.Add(bottom);
                binSet.Add(bin);
            }
        }

        var allowed = new Dictionary<int, HashSet<int>>();
        foreach (var index in topSets.Keys.Intersect(bottomSets.Keys).Intersect(binSets.Keys))
        {
            var set = new HashSet<int>();
            for (var value = 0; value < 25; value++)
            {
                if (topSets[index].Contains(value / 5)
                    && bottomSets[index].Contains(value % 5)
                    && binSets[index].Contains(binMap[value]))
                {
                    set.Add(value);
                }
            }

            allowed[index] = set;
        }

        return allowed;
    }

    private static StatefulDecodeStats EvaluateStatefulDecodeWithHybridConstraints(
        Dictionary<int, int[]> sequences,
        IReadOnlyList<(int EastId, int WestId)> pairs)
    {
        var totalAligned = 0;
        var totalCovered = 0;
        var totalCorrect = 0;
        var usedPairs = 0;

        foreach (var (eastId, westId) in pairs)
        {
            var training = pairs.Where(p => p != (eastId, westId)).ToList();
            var mapping = BuildStatefulMappingWithRunPosition(sequences, training, useAlignment: true);
            var allowed = BuildHybridAllowedSetsFromWest(sequences, training);

            if (!sequences.TryGetValue(eastId, out var east) || !sequences.TryGetValue(westId, out var west))
            {
                continue;
            }

            if (east.Length == 0 || west.Length == 0)
            {
                continue;
            }

            usedPairs++;

            var alignment = SequenceAlignment.Align(east, west);
            foreach (var step in alignment.Steps)
            {
                if (step.ValueA.HasValue && step.ValueB.HasValue && step.IndexA.HasValue)
                {
                    totalAligned++;
                    var index = step.IndexA.Value;
                    var prevEast = index > 0 ? east[index - 1] : (int?)null;
                    var key = (index, step.ValueA.Value, prevEast);
                    if (mapping.TryGetValue(key, out var predicted)
                        && allowed.TryGetValue(index, out var allowedSet)
                        && allowedSet.Contains(predicted))
                    {
                        totalCovered++;
                        if (predicted == step.ValueB.Value)
                        {
                            totalCorrect++;
                        }
                    }
                }
            }
        }

        return new StatefulDecodeStats(usedPairs, totalAligned, totalCovered, totalCorrect);
    }

    private static HmmModel TrainHmmWithHeaderBodyInit(IReadOnlyList<int[]> sequences, int states, int symbols, int iterations)
    {
        var pi = new double[states];
        var a = new double[states, states];
        var b = new double[states, symbols];

        var pseudo = 1e-3;
        for (var i = 0; i < states; i++)
        {
            pi[i] = pseudo;
            for (var j = 0; j < states; j++)
            {
                a[i, j] = pseudo;
            }

            for (var k = 0; k < symbols; k++)
            {
                b[i, k] = pseudo;
            }
        }

        int GetState(int index, int length)
        {
            if (states <= 1)
            {
                return 0;
            }

            if (index <= 5)
            {
                return 0;
            }

            var bodyLen = Math.Max(1, length - 6);
            var bodyIndex = index - 6;
            var bodyStates = Math.Max(1, states - 1);
            var ratio = bodyIndex / (double)bodyLen;
            var bucket = (int)Math.Floor(ratio * bodyStates);
            if (bucket >= bodyStates)
            {
                bucket = bodyStates - 1;
            }

            return 1 + bucket;
        }

        foreach (var seq in sequences)
        {
            if (seq.Length == 0)
            {
                continue;
            }

            var s0 = GetState(0, seq.Length);
            pi[s0] += 1;
            b[s0, seq[0]] += 1;

            for (var t = 1; t < seq.Length; t++)
            {
                var sPrev = GetState(t - 1, seq.Length);
                var s = GetState(t, seq.Length);
                a[sPrev, s] += 1;
                b[s, seq[t]] += 1;
            }
        }

        Normalize(pi);
        NormalizeRows(a);
        NormalizeRows(b);

        for (var iter = 0; iter < iterations; iter++)
        {
            var piAcc = new double[states];
            var aAcc = new double[states, states];
            var aDen = new double[states];
            var bAcc = new double[states, symbols];

            foreach (var seq in sequences)
            {
                if (seq.Length == 0)
                {
                    continue;
                }

                var tLen = seq.Length;
                var alpha = new double[tLen, states];
                var beta = new double[tLen, states];
                var scale = new double[tLen];

                for (var i = 0; i < states; i++)
                {
                    alpha[0, i] = pi[i] * b[i, seq[0]];
                }

                scale[0] = 0;
                for (var i = 0; i < states; i++)
                {
                    scale[0] += alpha[0, i];
                }

                if (scale[0] == 0)
                {
                    scale[0] = 1;
                }

                for (var i = 0; i < states; i++)
                {
                    alpha[0, i] /= scale[0];
                }

                for (var t = 1; t < tLen; t++)
                {
                    for (var i = 0; i < states; i++)
                    {
                        var sum = 0.0;
                        for (var j = 0; j < states; j++)
                        {
                            sum += alpha[t - 1, j] * a[j, i];
                        }

                        alpha[t, i] = sum * b[i, seq[t]];
                    }

                    scale[t] = 0;
                    for (var i = 0; i < states; i++)
                    {
                        scale[t] += alpha[t, i];
                    }

                    if (scale[t] == 0)
                    {
                        scale[t] = 1;
                    }

                    for (var i = 0; i < states; i++)
                    {
                        alpha[t, i] /= scale[t];
                    }
                }

                for (var i = 0; i < states; i++)
                {
                    beta[tLen - 1, i] = 1.0 / scale[tLen - 1];
                }

                for (var t = tLen - 2; t >= 0; t--)
                {
                    for (var i = 0; i < states; i++)
                    {
                        var sum = 0.0;
                        for (var j = 0; j < states; j++)
                        {
                            sum += a[i, j] * b[j, seq[t + 1]] * beta[t + 1, j];
                        }

                        beta[t, i] = sum / scale[t];
                    }
                }

                for (var i = 0; i < states; i++)
                {
                    var gamma = alpha[0, i] * beta[0, i];
                    piAcc[i] += gamma;
                }

                for (var t = 0; t < tLen - 1; t++)
                {
                    var denom = 0.0;
                    for (var i = 0; i < states; i++)
                    {
                        for (var j = 0; j < states; j++)
                        {
                            denom += alpha[t, i] * a[i, j] * b[j, seq[t + 1]] * beta[t + 1, j];
                        }
                    }

                    if (denom == 0)
                    {
                        continue;
                    }

                    for (var i = 0; i < states; i++)
                    {
                        var gamma = alpha[t, i] * beta[t, i];
                        aDen[i] += gamma;
                        for (var j = 0; j < states; j++)
                        {
                            var xi = alpha[t, i] * a[i, j] * b[j, seq[t + 1]] * beta[t + 1, j] / denom;
                            aAcc[i, j] += xi;
                        }
                    }
                }

                for (var t = 0; t < tLen; t++)
                {
                    for (var i = 0; i < states; i++)
                    {
                        var gamma = alpha[t, i] * beta[t, i];
                        bAcc[i, seq[t]] += gamma;
                    }
                }
            }

            for (var i = 0; i < states; i++)
            {
                pi[i] = piAcc[i] + pseudo;
            }

            Normalize(pi);

            for (var i = 0; i < states; i++)
            {
                var denom = aDen[i] + pseudo * states;
                for (var j = 0; j < states; j++)
                {
                    a[i, j] = (aAcc[i, j] + pseudo) / denom;
                }
            }

            for (var i = 0; i < states; i++)
            {
                var denom = 0.0;
                for (var k = 0; k < symbols; k++)
                {
                    denom += bAcc[i, k] + pseudo;
                }

                if (denom == 0)
                {
                    denom = 1;
                }

                for (var k = 0; k < symbols; k++)
                {
                    b[i, k] = (bAcc[i, k] + pseudo) / denom;
                }
            }
        }

        return new HmmModel(pi, a, b);
    }

    private static HmmModel TrainHmm(IReadOnlyList<int[]> sequences, int states, int symbols, int iterations)
    {
        var pi = new double[states];
        var a = new double[states, states];
        var b = new double[states, symbols];

        var pseudo = 1e-3;
        for (var i = 0; i < states; i++)
        {
            pi[i] = pseudo;
            for (var j = 0; j < states; j++)
            {
                a[i, j] = pseudo;
            }

            for (var k = 0; k < symbols; k++)
            {
                b[i, k] = pseudo;
            }
        }

        foreach (var seq in sequences)
        {
            if (seq.Length == 0)
            {
                continue;
            }

            var s0 = seq[0] % states;
            pi[s0] += 1;
            b[s0, seq[0]] += 1;

            for (var t = 1; t < seq.Length; t++)
            {
                var sPrev = seq[t - 1] % states;
                var s = seq[t] % states;
                a[sPrev, s] += 1;
                b[s, seq[t]] += 1;
            }
        }

        Normalize(pi);
        NormalizeRows(a);
        NormalizeRows(b);

        for (var iter = 0; iter < iterations; iter++)
        {
            var piAcc = new double[states];
            var aAcc = new double[states, states];
            var aDen = new double[states];
            var bAcc = new double[states, symbols];

            foreach (var seq in sequences)
            {
                if (seq.Length == 0)
                {
                    continue;
                }

                var tLen = seq.Length;
                var alpha = new double[tLen, states];
                var beta = new double[tLen, states];
                var scale = new double[tLen];

                for (var i = 0; i < states; i++)
                {
                    alpha[0, i] = pi[i] * b[i, seq[0]];
                }

                scale[0] = 0;
                for (var i = 0; i < states; i++)
                {
                    scale[0] += alpha[0, i];
                }

                if (scale[0] == 0)
                {
                    scale[0] = 1;
                }

                for (var i = 0; i < states; i++)
                {
                    alpha[0, i] /= scale[0];
                }

                for (var t = 1; t < tLen; t++)
                {
                    for (var i = 0; i < states; i++)
                    {
                        var sum = 0.0;
                        for (var j = 0; j < states; j++)
                        {
                            sum += alpha[t - 1, j] * a[j, i];
                        }

                        alpha[t, i] = sum * b[i, seq[t]];
                    }

                    scale[t] = 0;
                    for (var i = 0; i < states; i++)
                    {
                        scale[t] += alpha[t, i];
                    }

                    if (scale[t] == 0)
                    {
                        scale[t] = 1;
                    }

                    for (var i = 0; i < states; i++)
                    {
                        alpha[t, i] /= scale[t];
                    }
                }

                for (var i = 0; i < states; i++)
                {
                    beta[tLen - 1, i] = 1.0 / scale[tLen - 1];
                }

                for (var t = tLen - 2; t >= 0; t--)
                {
                    for (var i = 0; i < states; i++)
                    {
                        var sum = 0.0;
                        for (var j = 0; j < states; j++)
                        {
                            sum += a[i, j] * b[j, seq[t + 1]] * beta[t + 1, j];
                        }

                        beta[t, i] = sum / scale[t];
                    }
                }

                for (var i = 0; i < states; i++)
                {
                    var gamma = alpha[0, i] * beta[0, i];
                    piAcc[i] += gamma;
                }

                for (var t = 0; t < tLen - 1; t++)
                {
                    var denom = 0.0;
                    for (var i = 0; i < states; i++)
                    {
                        for (var j = 0; j < states; j++)
                        {
                            denom += alpha[t, i] * a[i, j] * b[j, seq[t + 1]] * beta[t + 1, j];
                        }
                    }

                    if (denom == 0)
                    {
                        continue;
                    }

                    for (var i = 0; i < states; i++)
                    {
                        var gamma = alpha[t, i] * beta[t, i];
                        aDen[i] += gamma;
                        for (var j = 0; j < states; j++)
                        {
                            var xi = alpha[t, i] * a[i, j] * b[j, seq[t + 1]] * beta[t + 1, j] / denom;
                            aAcc[i, j] += xi;
                        }
                    }
                }

                for (var t = 0; t < tLen; t++)
                {
                    for (var i = 0; i < states; i++)
                    {
                        var gamma = alpha[t, i] * beta[t, i];
                        bAcc[i, seq[t]] += gamma;
                    }
                }
            }

            for (var i = 0; i < states; i++)
            {
                pi[i] = piAcc[i] + pseudo;
            }

            Normalize(pi);

            for (var i = 0; i < states; i++)
            {
                var denom = aDen[i] + pseudo * states;
                for (var j = 0; j < states; j++)
                {
                    a[i, j] = (aAcc[i, j] + pseudo) / denom;
                }
            }

            for (var i = 0; i < states; i++)
            {
                var denom = 0.0;
                for (var k = 0; k < symbols; k++)
                {
                    denom += bAcc[i, k] + pseudo;
                }

                if (denom == 0)
                {
                    denom = 1;
                }

                for (var k = 0; k < symbols; k++)
                {
                    b[i, k] = (bAcc[i, k] + pseudo) / denom;
                }
            }
        }

        return new HmmModel(pi, a, b);
    }

    private static double ComputeHmmLogLikelihood(HmmModel model, IReadOnlyList<int[]> sequences)
    {
        var logLik = 0.0;
        var states = model.Pi.Length;

        foreach (var seq in sequences)
        {
            if (seq.Length == 0)
            {
                continue;
            }

            var alpha = new double[states];
            var scale = new double[seq.Length];

            for (var i = 0; i < states; i++)
            {
                alpha[i] = model.Pi[i] * model.B[i, seq[0]];
            }

            scale[0] = alpha.Sum();
            if (scale[0] == 0)
            {
                scale[0] = 1;
            }

            for (var i = 0; i < states; i++)
            {
                alpha[i] /= scale[0];
            }

            for (var t = 1; t < seq.Length; t++)
            {
                var next = new double[states];
                for (var i = 0; i < states; i++)
                {
                    var sum = 0.0;
                    for (var j = 0; j < states; j++)
                    {
                        sum += alpha[j] * model.A[j, i];
                    }

                    next[i] = sum * model.B[i, seq[t]];
                }

                var scaleT = next.Sum();
                if (scaleT == 0)
                {
                    scaleT = 1;
                }

                scale[t] = scaleT;
                for (var i = 0; i < states; i++)
                {
                    next[i] /= scaleT;
                }

                alpha = next;
            }

            logLik += scale.Sum(s => Math.Log(s));
        }

        return logLik;
    }

    private static double ComputeUnigramLogLikelihood(IReadOnlyList<int[]> sequences, int symbols)
    {
        var counts = new double[symbols];
        var total = 0.0;
        foreach (var seq in sequences)
        {
            foreach (var value in seq)
            {
                counts[value] += 1;
                total += 1;
            }
        }

        var logLik = 0.0;
        for (var i = 0; i < counts.Length; i++)
        {
            counts[i] = counts[i] / total;
        }

        foreach (var seq in sequences)
        {
            foreach (var value in seq)
            {
                var p = counts[value];
                logLik += Math.Log(p);
            }
        }

        return logLik;
    }

    private static void Normalize(double[] values)
    {
        var sum = values.Sum();
        if (sum == 0)
        {
            return;
        }

        for (var i = 0; i < values.Length; i++)
        {
            values[i] /= sum;
        }
    }

    private static void NormalizeRows(double[,] matrix)
    {
        var rows = matrix.GetLength(0);
        var cols = matrix.GetLength(1);
        for (var i = 0; i < rows; i++)
        {
            var sum = 0.0;
            for (var j = 0; j < cols; j++)
            {
                sum += matrix[i, j];
            }

            if (sum == 0)
            {
                continue;
            }

            for (var j = 0; j < cols; j++)
            {
                matrix[i, j] /= sum;
            }
        }
    }

    private static Dictionary<(int Index, int EastValue, int? Prev1, int? Prev2), int> BuildStatefulMappingWithPrev2Early(
        Dictionary<int, int[]> sequences,
        IReadOnlyList<(int EastId, int WestId)> pairs,
        bool useAlignment,
        int maxIndexExclusive)
    {
        var counts = new Dictionary<(int Index, int EastValue, int? Prev1, int? Prev2), Dictionary<int, int>>();

        foreach (var (eastId, westId) in pairs)
        {
            if (!sequences.TryGetValue(eastId, out var east) || !sequences.TryGetValue(westId, out var west))
            {
                continue;
            }

            if (east.Length == 0 || west.Length == 0)
            {
                continue;
            }

            if (useAlignment)
            {
                var alignment = SequenceAlignment.Align(east, west);
                foreach (var step in alignment.Steps)
                {
                    if (step.ValueA.HasValue && step.ValueB.HasValue && step.IndexA.HasValue)
                    {
                        var index = step.IndexA.Value;
                        if (index >= maxIndexExclusive)
                        {
                            continue;
                        }

                        var prev1 = index > 0 ? east[index - 1] : (int?)null;
                        var prev2 = index > 1 ? east[index - 2] : (int?)null;
                        var key = (index, step.ValueA.Value, prev1, prev2);
                        if (!counts.TryGetValue(key, out var map))
                        {
                            map = new Dictionary<int, int>();
                            counts[key] = map;
                        }

                        var valueB = step.ValueB.Value;
                        map[valueB] = map.TryGetValue(valueB, out var existing) ? existing + 1 : 1;
                    }
                }
            }
            else
            {
                var len = Math.Min(east.Length, west.Length);
                for (var i = 0; i < len && i < maxIndexExclusive; i++)
                {
                    var prev1 = i > 0 ? east[i - 1] : (int?)null;
                    var prev2 = i > 1 ? east[i - 2] : (int?)null;
                    var key = (i, east[i], prev1, prev2);
                    if (!counts.TryGetValue(key, out var map))
                    {
                        map = new Dictionary<int, int>();
                        counts[key] = map;
                    }

                    var valueB = west[i];
                    map[valueB] = map.TryGetValue(valueB, out var existing) ? existing + 1 : 1;
                }
            }
        }

        var mapping = new Dictionary<(int Index, int EastValue, int? Prev1, int? Prev2), int>();
        foreach (var (key, map) in counts)
        {
            var bestValue = int.MaxValue;
            var bestCount = -1;
            foreach (var (value, count) in map)
            {
                if (count > bestCount || (count == bestCount && value < bestValue))
                {
                    bestCount = count;
                    bestValue = value;
                }
            }

            mapping[key] = bestValue;
        }

        return mapping;
    }

    private static EarlyDecodeStats EvaluateStatefulDecodePrev2Early(
        Dictionary<int, int[]> sequences,
        IReadOnlyList<(int EastId, int WestId)> pairs,
        bool useAlignment,
        int maxIndexExclusive)
    {
        var totalAligned = 0;
        var totalCovered = 0;
        var totalCorrect = 0;
        var usedPairs = 0;

        foreach (var (eastId, westId) in pairs)
        {
            var training = pairs.Where(p => p != (eastId, westId)).ToList();
            var mapping = BuildStatefulMappingWithPrev2Early(sequences, training, useAlignment, maxIndexExclusive);

            if (!sequences.TryGetValue(eastId, out var east) || !sequences.TryGetValue(westId, out var west))
            {
                continue;
            }

            if (east.Length == 0 || west.Length == 0)
            {
                continue;
            }

            usedPairs++;

            if (useAlignment)
            {
                var alignment = SequenceAlignment.Align(east, west);
                foreach (var step in alignment.Steps)
                {
                    if (step.ValueA.HasValue && step.ValueB.HasValue && step.IndexA.HasValue)
                    {
                        var index = step.IndexA.Value;
                        if (index >= maxIndexExclusive)
                        {
                            continue;
                        }

                        totalAligned++;
                        var prev1 = index > 0 ? east[index - 1] : (int?)null;
                        var prev2 = index > 1 ? east[index - 2] : (int?)null;
                        var key = (index, step.ValueA.Value, prev1, prev2);
                        if (mapping.TryGetValue(key, out var predicted))
                        {
                            totalCovered++;
                            if (predicted == step.ValueB.Value)
                            {
                                totalCorrect++;
                            }
                        }
                    }
                }
            }
            else
            {
                var len = Math.Min(east.Length, west.Length);
                var limit = Math.Min(len, maxIndexExclusive);
                totalAligned += limit;
                for (var i = 0; i < limit; i++)
                {
                    var prev1 = i > 0 ? east[i - 1] : (int?)null;
                    var prev2 = i > 1 ? east[i - 2] : (int?)null;
                    var key = (i, east[i], prev1, prev2);
                    if (mapping.TryGetValue(key, out var predicted))
                    {
                        totalCovered++;
                        if (predicted == west[i])
                        {
                            totalCorrect++;
                        }
                    }
                }
            }
        }

        return new EarlyDecodeStats(usedPairs, totalAligned, totalCovered, totalCorrect);
    }

    private static EarlyDecodeBaselineStats EvaluateStatefulDecodePrev1EarlyWithBaseline(
        Dictionary<int, int[]> sequences,
        IReadOnlyList<(int EastId, int WestId)> pairs,
        bool useAlignment,
        int maxIndexExclusive,
        int samples,
        int seed)
    {
        var totalAligned = 0;
        var totalCovered = 0;
        var totalCorrect = 0;
        var usedPairs = 0;
        var baselineCorrectTotal = 0.0;
        var rng = new Random(seed);

        foreach (var (eastId, westId) in pairs)
        {
            var training = pairs.Where(p => p != (eastId, westId)).ToList();
            var mapping = BuildStatefulMappingWithRunPosition(sequences, training, useAlignment);

            if (!sequences.TryGetValue(eastId, out var east) || !sequences.TryGetValue(westId, out var west))
            {
                continue;
            }

            if (east.Length == 0 || west.Length == 0)
            {
                continue;
            }

            usedPairs++;

            var alignedSteps = new List<(int Index, int EastValue, int WestValue, int? PrevEast)>();

            if (useAlignment)
            {
                var alignment = SequenceAlignment.Align(east, west);
                foreach (var step in alignment.Steps)
                {
                    if (step.ValueA.HasValue && step.ValueB.HasValue && step.IndexA.HasValue)
                    {
                        var index = step.IndexA.Value;
                        if (index >= maxIndexExclusive)
                        {
                            continue;
                        }

                        totalAligned++;
                        var prevEast = index > 0 ? east[index - 1] : (int?)null;
                        alignedSteps.Add((index, step.ValueA.Value, step.ValueB.Value, prevEast));
                    }
                }
            }
            else
            {
                var len = Math.Min(east.Length, west.Length);
                var limit = Math.Min(len, maxIndexExclusive);
                totalAligned += limit;
                for (var i = 0; i < limit; i++)
                {
                    var prevEast = i > 0 ? east[i - 1] : (int?)null;
                    alignedSteps.Add((i, east[i], west[i], prevEast));
                }
            }

            foreach (var step in alignedSteps)
            {
                var key = (step.Index, step.EastValue, step.PrevEast);
                if (mapping.TryGetValue(key, out var predicted))
                {
                    totalCovered++;
                    if (predicted == step.WestValue)
                    {
                        totalCorrect++;
                    }
                }
            }

            if (alignedSteps.Count == 0 || samples <= 0)
            {
                continue;
            }

            var westValues = alignedSteps.Select(s => s.WestValue).ToArray();
            var baselineCorrect = 0.0;
            for (var s = 0; s < samples; s++)
            {
                var shuffled = Shuffle(westValues, rng);
                var correct = 0;
                for (var i = 0; i < alignedSteps.Count; i++)
                {
                    var step = alignedSteps[i];
                    var key = (step.Index, step.EastValue, step.PrevEast);
                    if (mapping.TryGetValue(key, out var predicted) && predicted == shuffled[i])
                    {
                        correct++;
                    }
                }

                baselineCorrect += correct;
            }

            baselineCorrectTotal += baselineCorrect / samples;
        }

        var baselineMean = usedPairs == 0 ? 0.0 : baselineCorrectTotal;
        return new EarlyDecodeBaselineStats(usedPairs, totalAligned, totalCovered, totalCorrect, baselineMean);
    }

    private static char GetHeaderMotifType(EyeMessage message)
    {
        var values = TrigramWeaver.BuildWeaveTrigrams(message, TrigramScheme.Canonical)
            .Select(t => t.Base10Value)
            .ToArray();
        if (TryGetHeaderEndIndex(values, out _, out var motif))
        {
            return motif;
        }

        return '?';
    }

    private static List<int> GetRowPairBlockValues(EyeMessage message, int rowPair)
    {
        var occurrences = TrigramWeaver.BuildWeaveOccurrences(message, TrigramScheme.Canonical);
        var seq = occurrences.Select(o => o.Base10Value).ToArray();
        if (!TryGetHeaderEndIndex(seq, out var headerEndIndex, out _))
        {
            return new List<int>();
        }

        var body = occurrences.Skip(headerEndIndex + 1).ToArray();
        var runs = GetRuns(body.Select(o => o.RowPair).ToArray());
        var cursor = 0;
        var values = new List<int>();
        foreach (var run in runs)
        {
            if (run.Value == rowPair)
            {
                values.AddRange(body.Skip(cursor).Take(run.Length).Select(o => o.Base10Value));
            }

            cursor += run.Length;
        }

        return values;
    }

    private static Dictionary<(int From, int To), int> GetRowPairBlockTransitions(EyeMessage message, int rowPair)
    {
        var values = GetRowPairBlockValues(message, rowPair);
        var transitions = new Dictionary<(int From, int To), int>();
        for (var i = 0; i < values.Count - 1; i++)
        {
            var key = (values[i], values[i + 1]);
            transitions[key] = transitions.TryGetValue(key, out var existing) ? existing + 1 : 1;
        }

        return transitions;
    }

    private static void MergeTransitions(
        Dictionary<(int From, int To), int> target,
        Dictionary<(int From, int To), int> source)
    {
        foreach (var (key, count) in source)
        {
            target[key] = target.TryGetValue(key, out var existing) ? existing + count : count;
        }
    }

    private static List<(int Value, int Length)> GetRuns(IReadOnlyList<int> sequence)
    {
        var runs = new List<(int Value, int Length)>();
        if (sequence.Count == 0)
        {
            return runs;
        }

        var current = sequence[0];
        var length = 1;
        for (var i = 1; i < sequence.Count; i++)
        {
            if (sequence[i] == current)
            {
                length++;
            }
            else
            {
                runs.Add((current, length));
                current = sequence[i];
                length = 1;
            }
        }

        runs.Add((current, length));
        return runs;
    }

    private static int[] GetBodyRowPairSequence(EyeMessage message)
    {
        var occurrences = TrigramWeaver.BuildWeaveOccurrences(message, TrigramScheme.Canonical);
        var seq = occurrences.Select(o => o.Base10Value).ToArray();
        if (!TryGetHeaderEndIndex(seq, out var headerEndIndex, out _))
        {
            return Array.Empty<int>();
        }

        return occurrences.Skip(headerEndIndex + 1).Select(o => o.RowPair).ToArray();
    }

    private static bool TryGetHeaderEndIndex(IReadOnlyList<int> sequence, out int endIndex, out char motif)
    {
        var motifB = new[] { 5, 49, 75, 54 };
        var motifC = new[] { 62, 13 };

        var b = IndexOfSubsequence(sequence, motifB);
        var c = IndexOfSubsequence(sequence, motifC);

        if (b >= 0 && (c < 0 || b <= c))
        {
            endIndex = b + motifB.Length - 1;
            motif = 'B';
            return true;
        }

        if (c >= 0)
        {
            endIndex = c + motifC.Length - 1;
            motif = 'C';
            return true;
        }

        endIndex = -1;
        motif = '?';
        return false;
    }

    private static double Correlation(IReadOnlyList<double> a, IReadOnlyList<double> b)
    {
        var meanA = a.Average();
        var meanB = b.Average();
        var sumAB = 0.0;
        var sumAA = 0.0;
        var sumBB = 0.0;
        for (var i = 0; i < a.Count; i++)
        {
            var da = a[i] - meanA;
            var db = b[i] - meanB;
            sumAB += da * db;
            sumAA += da * da;
            sumBB += db * db;
        }

        return sumAA == 0 || sumBB == 0 ? 0 : sumAB / Math.Sqrt(sumAA * sumBB);
    }

    private static int IndexOfSubsequence(IReadOnlyList<int> sequence, IReadOnlyList<int> needle)
    {
        if (needle.Count == 0)
        {
            return 0;
        }

        for (var i = 0; i <= sequence.Count - needle.Count; i++)
        {
            var matches = true;
            for (var j = 0; j < needle.Count; j++)
            {
                if (sequence[i + j] != needle[j])
                {
                    matches = false;
                    break;
                }
            }

            if (matches)
            {
                return i;
            }
        }

        return -1;
    }

    private static (int Conflicts, int Mappings) ComputePeriodicConflicts(
        SequenceAlignmentResult alignment,
        int modulus)
    {
        var mapping = new Dictionary<(int Residue, int ValueA), HashSet<int>>();

        foreach (var step in alignment.Steps)
        {
            if (!step.IndexA.HasValue || !step.ValueA.HasValue || !step.ValueB.HasValue)
            {
                continue;
            }

            var residue = step.IndexA.Value % modulus;
            var key = (residue, step.ValueA.Value);

            if (!mapping.TryGetValue(key, out var set))
            {
                set = new HashSet<int>();
                mapping[key] = set;
            }

            set.Add(step.ValueB.Value);
        }

        var conflicts = mapping.Count(kvp => kvp.Value.Count > 1);
        return (conflicts, mapping.Count);
    }

    private sealed record AlignmentPairs(int[] IndexA, int[] ValueA, int[] ValueB);

    private static IReadOnlyList<AlignmentPairs> BuildAlignmentPairs(
        HypothesisContext context,
        IReadOnlyList<(int EastId, int WestId)> pairs,
        IReadOnlyList<int> anchor)
    {
        var results = new List<AlignmentPairs>();

        foreach (var (eastId, westId) in pairs)
        {
            var east = context.WeaveTrigrams[eastId].Select(t => t.Base10Value).ToArray();
            var west = context.WeaveTrigrams[westId].Select(t => t.Base10Value).ToArray();
            var anchorA = IndexOfSubsequence(east, anchor);
            var anchorB = IndexOfSubsequence(west, anchor);

            if (anchorA < 0 || anchorB < 0)
            {
                continue;
            }

            var alignment = SequenceAlignment.AlignAnchored(east, west, anchorA, anchorB, anchor.Count);
            var indices = new List<int>();
            var valuesA = new List<int>();
            var valuesB = new List<int>();

            foreach (var step in alignment.Steps)
            {
                if (!step.IndexA.HasValue || !step.ValueA.HasValue || !step.ValueB.HasValue)
                {
                    continue;
                }

                indices.Add(step.IndexA.Value);
                valuesA.Add(step.ValueA.Value);
                valuesB.Add(step.ValueB.Value);
            }

            results.Add(new AlignmentPairs(indices.ToArray(), valuesA.ToArray(), valuesB.ToArray()));
        }

        return results;
    }

    private static (int Conflicts, int Mappings) ComputeConflicts(
        IReadOnlyList<AlignmentPairs> alignments,
        int modulus)
    {
        var mapping = new Dictionary<(int Residue, int ValueA), int>();
        var conflicts = new HashSet<(int Residue, int ValueA)>();

        foreach (var alignment in alignments)
        {
            for (var i = 0; i < alignment.IndexA.Length; i++)
            {
                var residue = alignment.IndexA[i] % modulus;
                var key = (residue, alignment.ValueA[i]);
                var valueB = alignment.ValueB[i];

                if (!mapping.TryGetValue(key, out var existing))
                {
                    mapping[key] = valueB;
                }
                else if (existing != valueB)
                {
                    conflicts.Add(key);
                }
            }
        }

        return (conflicts.Count, mapping.Count);
    }

    private static int ComputeConflictsShuffled(
        IReadOnlyList<AlignmentPairs> alignments,
        int modulus,
        Random rng)
    {
        var mapping = new Dictionary<(int Residue, int ValueA), int>();
        var conflicts = new HashSet<(int Residue, int ValueA)>();

        foreach (var alignment in alignments)
        {
            var shuffled = Shuffle(alignment.ValueB, rng);
            for (var i = 0; i < alignment.IndexA.Length; i++)
            {
                var residue = alignment.IndexA[i] % modulus;
                var key = (residue, alignment.ValueA[i]);
                var valueB = shuffled[i];

                if (!mapping.TryGetValue(key, out var existing))
                {
                    mapping[key] = valueB;
                }
                else if (existing != valueB)
                {
                    conflicts.Add(key);
                }
            }
        }

        return conflicts.Count;
    }

    private static int[] Shuffle(int[] values, Random rng)
    {
        var copy = (int[])values.Clone();
        for (var i = copy.Length - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (copy[i], copy[j]) = (copy[j], copy[i]);
        }

        return copy;
    }

    private sealed record MarkovStats(
        int BaselineCorrect,
        int BaselineTotal,
        int Order1Correct,
        int Order1Total,
        int Order2Correct,
        int Order2Total,
        int Order1Contexts,
        int Order2Contexts,
        int Order2Deterministic
    );

    private sealed record LeaveOneOutStats(
        int Order1Correct,
        int Order1Seen,
        int Order1Total,
        int Order2Correct,
        int Order2Seen,
        int Order2Total
    );

    private sealed record SmoothedLeaveOneOutStats(
        int Order1Correct,
        int Order1Total,
        int Order2Correct,
        int Order2Total,
        double K,
        double Lambda2,
        double Lambda1,
        double Lambda0
    );

    private static MarkovStats ComputeMarkovStats(IReadOnlyList<EyeMessage> messages)
    {
        var sequences = messages.ToDictionary(
            message => message.Id,
            message => TrigramWeaver.BuildWeaveTrigrams(message, TrigramScheme.Canonical)
                .Select(t => t.Base10Value)
                .ToArray());

        var globalCounts = new Dictionary<int, int>();
        foreach (var sequence in sequences.Values)
        {
            foreach (var value in sequence)
            {
                globalCounts[value] = globalCounts.TryGetValue(value, out var count) ? count + 1 : 1;
            }
        }

        var globalMost = globalCounts
            .OrderByDescending(kvp => kvp.Value)
            .ThenBy(kvp => kvp.Key)
            .First();

        var counts1 = new Dictionary<int, Dictionary<int, int>>();
        var counts2 = new Dictionary<(int Prev2, int Prev1), Dictionary<int, int>>();

        foreach (var sequence in sequences.Values)
        {
            for (var i = 1; i < sequence.Length; i++)
            {
                var prev = sequence[i - 1];
                var next = sequence[i];
                if (!counts1.TryGetValue(prev, out var map))
                {
                    map = new Dictionary<int, int>();
                    counts1[prev] = map;
                }

                map[next] = map.TryGetValue(next, out var count) ? count + 1 : 1;
            }

            for (var i = 2; i < sequence.Length; i++)
            {
                var key = (sequence[i - 2], sequence[i - 1]);
                var next = sequence[i];
                if (!counts2.TryGetValue(key, out var map))
                {
                    map = new Dictionary<int, int>();
                    counts2[key] = map;
                }

                map[next] = map.TryGetValue(next, out var count) ? count + 1 : 1;
            }
        }

        var baselineCorrect = 0;
        var baselineTotal = 0;
        var order1Correct = 0;
        var order1Total = 0;
        var order2Correct = 0;
        var order2Total = 0;

        foreach (var sequence in sequences.Values)
        {
            for (var i = 1; i < sequence.Length; i++)
            {
                var actual = sequence[i];
                baselineTotal++;
                if (actual == globalMost.Key)
                {
                    baselineCorrect++;
                }

                var prev = sequence[i - 1];
                var prediction = ArgMax(counts1[prev]);
                order1Total++;
                if (prediction == actual)
                {
                    order1Correct++;
                }
            }

            for (var i = 2; i < sequence.Length; i++)
            {
                var actual = sequence[i];
                var key = (sequence[i - 2], sequence[i - 1]);
                var prediction = ArgMax(counts2[key]);
                order2Total++;
                if (prediction == actual)
                {
                    order2Correct++;
                }
            }
        }

        var order1Contexts = counts1.Count;
        var order2Contexts = counts2.Count;
        var order2Deterministic = counts2.Values.Count(map => map.Count == 1);

        return new MarkovStats(
            baselineCorrect,
            baselineTotal,
            order1Correct,
            order1Total,
            order2Correct,
            order2Total,
            order1Contexts,
            order2Contexts,
            order2Deterministic);
    }

    private static LeaveOneOutStats ComputeLeaveOneOutMarkovStats(IReadOnlyList<EyeMessage> messages)
    {
        var sequences = messages.ToDictionary(
            message => message.Id,
            message => TrigramWeaver.BuildWeaveTrigrams(message, TrigramScheme.Canonical)
                .Select(t => t.Base10Value)
                .ToArray());

        var order1Correct = 0;
        var order1Seen = 0;
        var order1Total = 0;
        var order2Correct = 0;
        var order2Seen = 0;
        var order2Total = 0;

        foreach (var message in messages)
        {
            var counts1 = new Dictionary<int, Dictionary<int, int>>();
            var counts2 = new Dictionary<(int Prev2, int Prev1), Dictionary<int, int>>();

            foreach (var other in messages)
            {
                if (other.Id == message.Id)
                {
                    continue;
                }

                var sequence = sequences[other.Id];
                for (var i = 1; i < sequence.Length; i++)
                {
                    var prev = sequence[i - 1];
                    var next = sequence[i];
                    if (!counts1.TryGetValue(prev, out var map))
                    {
                        map = new Dictionary<int, int>();
                        counts1[prev] = map;
                    }

                    map[next] = map.TryGetValue(next, out var count) ? count + 1 : 1;
                }

                for (var i = 2; i < sequence.Length; i++)
                {
                    var key = (sequence[i - 2], sequence[i - 1]);
                    var next = sequence[i];
                    if (!counts2.TryGetValue(key, out var map))
                    {
                        map = new Dictionary<int, int>();
                        counts2[key] = map;
                    }

                    map[next] = map.TryGetValue(next, out var count) ? count + 1 : 1;
                }
            }

            var target = sequences[message.Id];
            for (var i = 1; i < target.Length; i++)
            {
                order1Total++;
                var prev = target[i - 1];
                var actual = target[i];
                if (counts1.TryGetValue(prev, out var map))
                {
                    order1Seen++;
                    if (ArgMax(map) == actual)
                    {
                        order1Correct++;
                    }
                }
            }

            for (var i = 2; i < target.Length; i++)
            {
                order2Total++;
                var key = (target[i - 2], target[i - 1]);
                var actual = target[i];
                if (counts2.TryGetValue(key, out var map))
                {
                    order2Seen++;
                    if (ArgMax(map) == actual)
                    {
                        order2Correct++;
                    }
                }
            }
        }

        return new LeaveOneOutStats(order1Correct, order1Seen, order1Total, order2Correct, order2Seen, order2Total);
    }

    private static SmoothedLeaveOneOutStats ComputeLeaveOneOutSmoothedStats(
        IReadOnlyList<EyeMessage> messages,
        double k,
        double lambda2,
        double lambda1,
        double lambda0)
    {
        var sequences = messages.ToDictionary(
            message => message.Id,
            message => TrigramWeaver.BuildWeaveTrigrams(message, TrigramScheme.Canonical)
                .Select(t => t.Base10Value)
                .ToArray());

        var vocabulary = sequences.Values
            .SelectMany(sequence => sequence)
            .Distinct()
            .OrderBy(value => value)
            .ToArray();

        var order1Correct = 0;
        var order1Total = 0;
        var order2Correct = 0;
        var order2Total = 0;

        foreach (var message in messages)
        {
            var unigram = new Dictionary<int, int>();
            var totalUnigram = 0;
            var bigram = new Dictionary<int, Dictionary<int, int>>();
            var bigramTotals = new Dictionary<int, int>();
            var trigram = new Dictionary<(int Prev2, int Prev1), Dictionary<int, int>>();
            var trigramTotals = new Dictionary<(int Prev2, int Prev1), int>();

            foreach (var other in messages)
            {
                if (other.Id == message.Id)
                {
                    continue;
                }

                var sequence = sequences[other.Id];
                foreach (var value in sequence)
                {
                    unigram[value] = unigram.TryGetValue(value, out var count) ? count + 1 : 1;
                    totalUnigram++;
                }

                for (var i = 1; i < sequence.Length; i++)
                {
                    var prev = sequence[i - 1];
                    var next = sequence[i];
                    if (!bigram.TryGetValue(prev, out var map))
                    {
                        map = new Dictionary<int, int>();
                        bigram[prev] = map;
                        bigramTotals[prev] = 0;
                    }

                    map[next] = map.TryGetValue(next, out var count) ? count + 1 : 1;
                    bigramTotals[prev] = bigramTotals[prev] + 1;
                }

                for (var i = 2; i < sequence.Length; i++)
                {
                    var key = (sequence[i - 2], sequence[i - 1]);
                    var next = sequence[i];
                    if (!trigram.TryGetValue(key, out var map))
                    {
                        map = new Dictionary<int, int>();
                        trigram[key] = map;
                        trigramTotals[key] = 0;
                    }

                    map[next] = map.TryGetValue(next, out var count) ? count + 1 : 1;
                    trigramTotals[key] = trigramTotals[key] + 1;
                }
            }

            var target = sequences[message.Id];
            for (var i = 1; i < target.Length; i++)
            {
                order1Total++;
                var prev = target[i - 1];
                var actual = target[i];
                var prediction = PredictInterpolated(
                    vocabulary,
                    k,
                    lambda2: 0,
                    lambda1,
                    lambda0,
                    unigram,
                    totalUnigram,
                    bigram,
                    bigramTotals,
                    trigram,
                    trigramTotals,
                    prev2: null,
                    prev1: prev);

                if (prediction == actual)
                {
                    order1Correct++;
                }
            }

            for (var i = 2; i < target.Length; i++)
            {
                order2Total++;
                var prev2 = target[i - 2];
                var prev1 = target[i - 1];
                var actual = target[i];
                var prediction = PredictInterpolated(
                    vocabulary,
                    k,
                    lambda2,
                    lambda1,
                    lambda0,
                    unigram,
                    totalUnigram,
                    bigram,
                    bigramTotals,
                    trigram,
                    trigramTotals,
                    prev2,
                    prev1);

                if (prediction == actual)
                {
                    order2Correct++;
                }
            }
        }

        return new SmoothedLeaveOneOutStats(order1Correct, order1Total, order2Correct, order2Total, k, lambda2, lambda1, lambda0);
    }

    private static HashSet<(int Prev2, int Prev1)> BuildSharedOrder2Contexts(
        IReadOnlyDictionary<int, int[]> sequences,
        int minMessageCoverage)
    {
        var coverage = new Dictionary<(int Prev2, int Prev1), HashSet<int>>();
        foreach (var (id, sequence) in sequences)
        {
            for (var i = 2; i < sequence.Length; i++)
            {
                var key = (sequence[i - 2], sequence[i - 1]);
                if (!coverage.TryGetValue(key, out var ids))
                {
                    ids = new HashSet<int>();
                    coverage[key] = ids;
                }

                ids.Add(id);
            }
        }

        return coverage
            .Where(kvp => kvp.Value.Count >= minMessageCoverage)
            .Select(kvp => kvp.Key)
            .ToHashSet();
    }

    private static SharedContextCoverageStats ComputeSharedContextCoverage(
        IReadOnlyDictionary<int, int[]> sequences,
        IReadOnlySet<(int Prev2, int Prev1)> sharedContexts)
    {
        var totalPositions = 0;
        var sharedPositions = 0;

        foreach (var sequence in sequences.Values)
        {
            for (var i = 2; i < sequence.Length; i++)
            {
                totalPositions++;
                var key = (sequence[i - 2], sequence[i - 1]);
                if (sharedContexts.Contains(key))
                {
                    sharedPositions++;
                }
            }
        }

        var coverageRate = totalPositions == 0 ? 0 : Math.Round(sharedPositions / (double)totalPositions, 3);
        return new SharedContextCoverageStats(coverageRate, sharedPositions, totalPositions);
    }

    private static SharedContextPredictionStats EvaluateSharedContextPrediction(
        IReadOnlyDictionary<int, int[]> sequences,
        IReadOnlySet<(int Prev2, int Prev1)> sharedContexts)
    {
        var totalCovered = 0;
        var totalCorrect = 0;

        foreach (var (id, sequence) in sequences)
        {
            var training = sequences.Where(kvp => kvp.Key != id).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            var counts = new Dictionary<(int Prev2, int Prev1), Dictionary<int, int>>();

            foreach (var trainSeq in training.Values)
            {
                for (var i = 2; i < trainSeq.Length; i++)
                {
                    var key = (trainSeq[i - 2], trainSeq[i - 1]);
                    if (!sharedContexts.Contains(key))
                    {
                        continue;
                    }

                    if (!counts.TryGetValue(key, out var map))
                    {
                        map = new Dictionary<int, int>();
                        counts[key] = map;
                    }

                    var next = trainSeq[i];
                    map[next] = map.TryGetValue(next, out var existing) ? existing + 1 : 1;
                }
            }

            var mapping = new Dictionary<(int Prev2, int Prev1), int>();
            foreach (var (key, map) in counts)
            {
                var bestValue = int.MaxValue;
                var bestCount = -1;
                foreach (var (value, count) in map)
                {
                    if (count > bestCount || (count == bestCount && value < bestValue))
                    {
                        bestCount = count;
                        bestValue = value;
                    }
                }

                mapping[key] = bestValue;
            }

            for (var i = 2; i < sequence.Length; i++)
            {
                var key = (sequence[i - 2], sequence[i - 1]);
                if (!sharedContexts.Contains(key))
                {
                    continue;
                }

                if (mapping.TryGetValue(key, out var predicted))
                {
                    totalCovered++;
                    if (predicted == sequence[i])
                    {
                        totalCorrect++;
                    }
                }
            }
        }

        return new SharedContextPredictionStats(totalCovered, totalCorrect);
    }

    private static SharedContextEnrichmentStats ComputeSharedContextEnrichment(
        IReadOnlyDictionary<int, int[]> sequences,
        IReadOnlySet<(int Prev2, int Prev1)> sharedContexts)
    {
        var sharedHeader = 0;
        var sharedBody = 0;
        var totalHeader = 0;
        var totalBody = 0;

        foreach (var (_, sequence) in sequences)
        {
            if (!TryGetHeaderEndIndex(sequence, out var headerEnd, out _))
            {
                continue;
            }

            for (var i = 2; i < sequence.Length; i++)
            {
                var key = (sequence[i - 2], sequence[i - 1]);
                var isHeader = i <= headerEnd;
                if (isHeader)
                {
                    totalHeader++;
                }
                else
                {
                    totalBody++;
                }

                if (sharedContexts.Contains(key))
                {
                    if (isHeader)
                    {
                        sharedHeader++;
                    }
                    else
                    {
                        sharedBody++;
                    }
                }
            }
        }

        var sharedTotal = sharedHeader + sharedBody;
        var baselineTotal = totalHeader + totalBody;
        var sharedHeaderShare = sharedTotal == 0 ? 0 : Math.Round(sharedHeader / (double)sharedTotal, 3);
        var baselineHeaderShare = baselineTotal == 0 ? 0 : Math.Round(totalHeader / (double)baselineTotal, 3);
        var enrichmentRatio = baselineHeaderShare == 0 ? 0 : Math.Round(sharedHeaderShare / baselineHeaderShare, 3);

        return new SharedContextEnrichmentStats(sharedHeaderShare, baselineHeaderShare, enrichmentRatio);
    }

    private static Dictionary<int, string[]> LoadSourceGlyphMessages()
    {
        var assembly = typeof(HypothesisTests).Assembly;
        using var stream = assembly.GetManifestResourceStream("sources.NoitaEyeGlyphMessages.md");
        string[] lines;
        if (stream != null)
        {
            using var reader = new StreamReader(stream);
            var content = reader.ReadToEnd();
            lines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        }
        else
        {
            var candidates = new List<string>
            {
                Path.Combine(AppContext.BaseDirectory, "sources", "Noita Eye Glyph Messages.md"),
            };

            var root = FindRepoRootContaining("sources");
            if (root != null)
            {
                candidates.Add(Path.Combine(root, "sources", "Noita Eye Glyph Messages.md"));
            }

            var path = candidates.FirstOrDefault(File.Exists);
            if (path == null)
            {
                return new Dictionary<int, string[]>();
            }

            lines = File.ReadAllLines(path);
        }
        var messageHeader = new Regex(@"^##\s*Message\s+(\d+)", RegexOptions.Compiled);
        var messages = new Dictionary<int, List<string>>();
        int? current = null;

        foreach (var raw in lines)
        {
            var line = raw.Trim();
            var match = messageHeader.Match(line);
            if (match.Success)
            {
                current = int.Parse(match.Groups[1].Value);
                if (!messages.ContainsKey(current.Value))
                {
                    messages[current.Value] = new List<string>();
                }

                continue;
            }

            if (!current.HasValue)
            {
                continue;
            }

            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                current = null;
                continue;
            }

            var compact = new string(line.Where(ch => !char.IsWhiteSpace(ch)).ToArray());
            if (compact.Length == 0 || !compact.All(char.IsDigit))
            {
                continue;
            }

            var normalized = compact.Replace("5", string.Empty);
            if (normalized.Length > 0)
            {
                messages[current.Value].Add(normalized);
            }
        }

        return messages.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToArray());
    }

    private static string? FindRepoRootContaining(string folderName)
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var directory = new DirectoryInfo(start);
            while (directory != null)
            {
                if (Directory.Exists(Path.Combine(directory.FullName, folderName)))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }
        }

        return null;
    }

    private static int ArgMax(Dictionary<int, int> map)
    {
        var best = int.MaxValue;
        var bestCount = -1;
        foreach (var (value, count) in map)
        {
            if (count > bestCount || (count == bestCount && value < best))
            {
                bestCount = count;
                best = value;
            }
        }

        return best;
    }

    private static int PredictInterpolated(
        IReadOnlyList<int> vocabulary,
        double k,
        double lambda2,
        double lambda1,
        double lambda0,
        Dictionary<int, int> unigram,
        int totalUnigram,
        Dictionary<int, Dictionary<int, int>> bigram,
        Dictionary<int, int> bigramTotals,
        Dictionary<(int Prev2, int Prev1), Dictionary<int, int>> trigram,
        Dictionary<(int Prev2, int Prev1), int> trigramTotals,
        int? prev2,
        int prev1)
    {
        var map1 = bigram.TryGetValue(prev1, out var bigramMap) ? bigramMap : null;
        var total1 = map1 != null && bigramTotals.TryGetValue(prev1, out var totalCount1) ? totalCount1 : 0;

        Dictionary<int, int>? map2 = null;
        var total2 = 0;
        if (prev2.HasValue)
        {
            var key = (prev2.Value, prev1);
            if (trigram.TryGetValue(key, out var trigramMap))
            {
                map2 = trigramMap;
                total2 = trigramTotals.TryGetValue(key, out var totalCount2) ? totalCount2 : 0;
            }
        }

        var bestValue = int.MaxValue;
        var bestScore = double.NegativeInfinity;
        foreach (var value in vocabulary)
        {
            var p0 = SmoothedProbability(unigram, totalUnigram, value, k, vocabulary.Count);
            var p1 = SmoothedProbability(map1, total1, value, k, vocabulary.Count);
            var p2 = SmoothedProbability(map2, total2, value, k, vocabulary.Count);
            var score = lambda2 * p2 + lambda1 * p1 + lambda0 * p0;

            if (score > bestScore || (Math.Abs(score - bestScore) < 1e-12 && value < bestValue))
            {
                bestScore = score;
                bestValue = value;
            }
        }

        return bestValue;
    }

    private static double SmoothedProbability(
        Dictionary<int, int>? map,
        int total,
        int value,
        double k,
        int vocabularySize)
    {
        var count = 0;
        if (map != null)
        {
            map.TryGetValue(value, out count);
        }

        return (count + k) / (total + k * vocabularySize);
    }
}
