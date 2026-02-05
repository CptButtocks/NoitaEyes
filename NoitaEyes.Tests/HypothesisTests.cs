using System.Collections.Generic;
using System.Linq;
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
        Assert.Equal(expectedPass, result.Passed);
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
