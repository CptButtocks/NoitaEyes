using System.Collections.Generic;

namespace NoitaEyes.Engine;

public static class TrigramAnalysis
{
    public static IReadOnlyCollection<int> UniqueValues(IEnumerable<EyeTrigramToken> trigrams)
    {
        var set = new HashSet<int>();
        foreach (var trigram in trigrams)
        {
            set.Add(trigram.Base10Value);
        }

        return set;
    }

    public static bool HasAdjacentRepeats(IReadOnlyList<EyeTrigramToken> trigrams)
    {
        for (var i = 1; i < trigrams.Count; i++)
        {
            if (trigrams[i].Base10Value == trigrams[i - 1].Base10Value)
            {
                return true;
            }
        }

        return false;
    }

    public static IReadOnlyDictionary<int, IReadOnlyDictionary<int, int>> BuildTransitionGraph(IReadOnlyList<EyeTrigramToken> trigrams)
    {
        var graph = new Dictionary<int, Dictionary<int, int>>();
        for (var i = 1; i < trigrams.Count; i++)
        {
            var from = trigrams[i - 1].Base10Value;
            var to = trigrams[i].Base10Value;
            if (!graph.TryGetValue(from, out var edges))
            {
                edges = new Dictionary<int, int>();
                graph[from] = edges;
            }

            edges[to] = edges.TryGetValue(to, out var count) ? count + 1 : 1;
        }

        var readOnly = new Dictionary<int, IReadOnlyDictionary<int, int>>();
        foreach (var (key, value) in graph)
        {
            readOnly[key] = value;
        }

        return readOnly;
    }

    public static IReadOnlyDictionary<int, int> ComputeRepeatGapCounts(
        IReadOnlyList<EyeTrigramToken> trigrams,
        bool gapIsBetweenCount = true)
    {
        var positions = new Dictionary<int, List<int>>();
        for (var i = 0; i < trigrams.Count; i++)
        {
            var value = trigrams[i].Base10Value;
            if (!positions.TryGetValue(value, out var list))
            {
                list = new List<int>();
                positions[value] = list;
            }

            list.Add(i);
        }

        var counts = new Dictionary<int, int>();
        foreach (var list in positions.Values)
        {
            if (list.Count < 2)
            {
                continue;
            }

            for (var i = 1; i < list.Count; i++)
            {
                var distance = list[i] - list[i - 1];
                var gap = gapIsBetweenCount ? distance - 1 : distance;
                counts[gap] = counts.TryGetValue(gap, out var existing) ? existing + 1 : 1;
            }
        }

        return counts;
    }

    public static TransitionGraphAnalysis AnalyzeTransitionGraph(
        IReadOnlyList<EyeTrigramToken> trigrams,
        int hubThreshold = 10)
    {
        var graph = BuildTransitionGraph(trigrams);
        var nodes = new HashSet<int>();

        foreach (var trigram in trigrams)
        {
            nodes.Add(trigram.Base10Value);
        }

        var inDegrees = nodes.ToDictionary(v => v, _ => 0);
        var outDegrees = nodes.ToDictionary(v => v, _ => 0);

        foreach (var (from, edges) in graph)
        {
            outDegrees[from] = edges.Count;
            foreach (var to in edges.Keys)
            {
                if (!inDegrees.ContainsKey(to))
                {
                    inDegrees[to] = 0;
                }

                inDegrees[to] += 1;
            }
        }

        var sources = inDegrees.Where(kvp => kvp.Value == 0).Select(kvp => kvp.Key).OrderBy(v => v).ToArray();
        var sinks = outDegrees.Where(kvp => kvp.Value == 0).Select(kvp => kvp.Key).OrderBy(v => v).ToArray();
        var hubs = outDegrees.Where(kvp => kvp.Value >= hubThreshold).Select(kvp => kvp.Key).OrderByDescending(v => outDegrees[v]).ToArray();

        var components = ComputeStronglyConnectedComponents(nodes, graph);

        return new TransitionGraphAnalysis(
            graph,
            nodes.OrderBy(v => v).ToArray(),
            sources,
            sinks,
            hubs,
            components,
            inDegrees,
            outDegrees
        );
    }

    public static TransitionClusterAnalysis AnalyzeTransitionClusters(
        IReadOnlyList<EyeTrigramToken> trigrams,
        int minEdgeWeight = 2)
    {
        if (minEdgeWeight < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(minEdgeWeight), "Edge weight must be at least 1.");
        }

        var graph = BuildTransitionGraph(trigrams);
        var nodes = trigrams.Select(trigram => trigram.Base10Value).Distinct().ToArray();
        var adjacency = nodes.ToDictionary(node => node, _ => new HashSet<int>());

        foreach (var (from, edges) in graph)
        {
            foreach (var (to, count) in edges)
            {
                if (count < minEdgeWeight)
                {
                    continue;
                }

                adjacency[from].Add(to);
                adjacency[to].Add(from);
            }
        }

        var orderedNodes = nodes.OrderBy(node => node).ToArray();
        var visited = new HashSet<int>();
        var clusters = new List<IReadOnlyList<int>>();

        foreach (var node in orderedNodes)
        {
            if (visited.Contains(node))
            {
                continue;
            }

            var component = new List<int>();
            var stack = new Stack<int>();
            stack.Push(node);
            visited.Add(node);

            while (stack.Count > 0)
            {
                var current = stack.Pop();
                component.Add(current);

                foreach (var neighbor in adjacency[current].OrderBy(value => value))
                {
                    if (visited.Add(neighbor))
                    {
                        stack.Push(neighbor);
                    }
                }
            }

            component.Sort();
            clusters.Add(component);
        }

        var orderedClusters = clusters
            .OrderByDescending(cluster => cluster.Count)
            .ThenBy(cluster => cluster[0])
            .ToArray();

        var clusterSizes = orderedClusters.Select(cluster => cluster.Count).ToArray();

        var readOnlyAdjacency = adjacency.ToDictionary(
            kvp => kvp.Key,
            kvp => (IReadOnlyList<int>)kvp.Value.OrderBy(value => value).ToArray());

        return new TransitionClusterAnalysis(
            minEdgeWeight,
            readOnlyAdjacency,
            orderedClusters,
            clusterSizes
        );
    }

    private static IReadOnlyList<IReadOnlyList<int>> ComputeStronglyConnectedComponents(
        IReadOnlyCollection<int> nodes,
        IReadOnlyDictionary<int, IReadOnlyDictionary<int, int>> graph)
    {
        var visited = new HashSet<int>();
        var order = new List<int>();

        foreach (var node in nodes)
        {
            if (!visited.Contains(node))
            {
                Dfs(node, graph, visited, order);
            }
        }

        var reversed = ReverseGraph(nodes, graph);
        visited.Clear();
        var components = new List<IReadOnlyList<int>>();

        for (var i = order.Count - 1; i >= 0; i--)
        {
            var node = order[i];
            if (visited.Contains(node))
            {
                continue;
            }

            var component = new List<int>();
            DfsCollect(node, reversed, visited, component);
            components.Add(component);
        }

        return components.OrderByDescending(c => c.Count).ToArray();
    }

    private static void Dfs(
        int node,
        IReadOnlyDictionary<int, IReadOnlyDictionary<int, int>> graph,
        HashSet<int> visited,
        List<int> order)
    {
        visited.Add(node);
        if (graph.TryGetValue(node, out var edges))
        {
            foreach (var next in edges.Keys)
            {
                if (!visited.Contains(next))
                {
                    Dfs(next, graph, visited, order);
                }
            }
        }

        order.Add(node);
    }

    private static void DfsCollect(
        int node,
        IReadOnlyDictionary<int, IReadOnlyDictionary<int, int>> graph,
        HashSet<int> visited,
        List<int> component)
    {
        visited.Add(node);
        component.Add(node);
        if (graph.TryGetValue(node, out var edges))
        {
            foreach (var next in edges.Keys)
            {
                if (!visited.Contains(next))
                {
                    DfsCollect(next, graph, visited, component);
                }
            }
        }
    }

    private static Dictionary<int, IReadOnlyDictionary<int, int>> ReverseGraph(
        IReadOnlyCollection<int> nodes,
        IReadOnlyDictionary<int, IReadOnlyDictionary<int, int>> graph)
    {
        var reversed = nodes.ToDictionary(v => v, _ => (IReadOnlyDictionary<int, int>)new Dictionary<int, int>());
        var mutable = nodes.ToDictionary(v => v, _ => new Dictionary<int, int>());

        foreach (var (from, edges) in graph)
        {
            foreach (var (to, count) in edges)
            {
                if (!mutable.TryGetValue(to, out var list))
                {
                    list = new Dictionary<int, int>();
                    mutable[to] = list;
                }

                list[from] = count;
            }
        }

        foreach (var (node, edges) in mutable)
        {
            reversed[node] = edges;
        }

        return reversed;
    }
}
