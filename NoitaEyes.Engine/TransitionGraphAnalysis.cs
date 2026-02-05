using System.Collections.Generic;
using System.Linq;

namespace NoitaEyes.Engine;

public sealed record TransitionGraphAnalysis(
    IReadOnlyDictionary<int, IReadOnlyDictionary<int, int>> Graph,
    IReadOnlyList<int> Nodes,
    IReadOnlyList<int> Sources,
    IReadOnlyList<int> Sinks,
    IReadOnlyList<int> Hubs,
    IReadOnlyList<IReadOnlyList<int>> StronglyConnectedComponents,
    IReadOnlyDictionary<int, int> InDegrees,
    IReadOnlyDictionary<int, int> OutDegrees
)
{
    public int NodeCount => Nodes.Count;
    public int EdgeCount => Graph.Sum(kvp => kvp.Value.Count);
    public int TotalTransitions => Graph.Sum(kvp => kvp.Value.Values.Sum());
    public int LargestComponentSize => StronglyConnectedComponents.Count == 0
        ? 0
        : StronglyConnectedComponents.Max(c => c.Count);
}
