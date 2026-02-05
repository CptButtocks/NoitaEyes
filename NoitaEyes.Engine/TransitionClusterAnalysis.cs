using System.Collections.Generic;
using System.Linq;

namespace NoitaEyes.Engine;

public sealed record TransitionClusterAnalysis(
    int MinEdgeWeight,
    IReadOnlyDictionary<int, IReadOnlyList<int>> Graph,
    IReadOnlyList<IReadOnlyList<int>> Clusters,
    IReadOnlyList<int> ClusterSizes
)
{
    public int ClusterCount => ClusterSizes.Count;
    public int LargestClusterSize => ClusterSizes.Count == 0 ? 0 : ClusterSizes[0];
    public int SingletonCount => ClusterSizes.Count(size => size == 1);
}
