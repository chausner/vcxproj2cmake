using Microsoft.Extensions.Logging;

namespace vcxproj2cmake;

class DirectoryAwareProjectOrdering
{
    public static CMakeProject[] OrderProjectsByDependencies(IEnumerable<CMakeProject> projects, ILogger logger)
    {
        DirectedGraph<CMakeProject> dependencyGraph = new();

        foreach (var project in projects)
            dependencyGraph.Nodes.Add(new GraphNode<CMakeProject> { Value = project });

        foreach (var project in projects)
        {
            var node = dependencyGraph.GetNodeByValue(project)!;

            foreach (var projectReference in project.ProjectReferences)
            {
                var referencedNode = dependencyGraph.GetNodeByValue(projectReference.Project!)!;
                node.AddEdgeTo(referencedNode);
            }
        }

        BuildDirectoryGraph(projects, out var directoryGraph, out var projectToDirectoryMapping);

        FlattenDirectoryGraph(directoryGraph, projectToDirectoryMapping);

        var sortedNodes = dependencyGraph.TopologicalSort(directoryGraph, project => projectToDirectoryMapping[project]);

        return sortedNodes.Select(node => node.Value).ToArray();
    }

    private static void BuildDirectoryGraph(
        IEnumerable<CMakeProject> projects, 
        out DirectedGraph<Directory> directoryGraph, 
        out Dictionary<CMakeProject, GraphNode<Directory>> projectToDirectoryMapping)
    {
        directoryGraph = new();
        projectToDirectoryMapping = new();

        foreach (var project in projects)
        {
            var pathItems = Path.GetDirectoryName(project.AbsoluteProjectPath)!.Split(Path.DirectorySeparatorChar);

            GraphNode<Directory>? lastNode = null;
            foreach (var item in pathItems)
            {
                if (lastNode == null)
                {
                    lastNode = directoryGraph.Nodes.FirstOrDefault(node => node.IncomingEdges.Count == 0 && node.Value.Name == item);
                    if (lastNode == null)
                    {
                        var currentDirectory = new Directory { Name = item };
                        lastNode = new GraphNode<Directory> { Value = currentDirectory };
                        directoryGraph.Nodes.Add(lastNode);
                    }
                }
                else
                {
                    var newNode = lastNode.OutgoingEdges.FirstOrDefault(edge => edge.Destination.Value.Name == item)?.Destination;
                    if (newNode == null)
                    {
                        var newDirectory = new Directory { Name = item };
                        newNode = new GraphNode<Directory> { Value = newDirectory };
                        directoryGraph.Nodes.Add(newNode);
                        lastNode.AddEdgeTo(newNode);
                    }
                    lastNode = newNode;
                }
            }

            projectToDirectoryMapping[project] = lastNode!;
        }
    }

    private static void FlattenDirectoryGraph(DirectedGraph<Directory> directoryGraph, Dictionary<CMakeProject, GraphNode<Directory>> projectToDirectoryMapping)
    {
        bool change;
        do
        {
            change = false;
            foreach (var node in directoryGraph.Nodes)
            {
                if (node.OutgoingEdges.Count == 1 && !projectToDirectoryMapping.Values.Contains(node))
                {
                    var edge = node.OutgoingEdges[0];
                    var node2 = edge.Destination;

                    node.Value.Name = Path.Combine(node.Value.Name, node2.Value.Name);
                    node.RemoveEdgeTo(node2);

                    foreach (var project in projectToDirectoryMapping.Keys)
                        if (projectToDirectoryMapping[project] == node2)
                            projectToDirectoryMapping[project] = node;

                    foreach (var edge2 in node2.OutgoingEdges.ToArray())
                    {
                        node2.RemoveEdgeTo(edge2.Destination);
                        node.AddEdgeTo(edge2.Destination);
                    }
                    directoryGraph.Nodes.Remove(node2);
                    change = true;
                    break;
                }
            }
        } while (change);
    }
}

class Directory
{
    public required string Name { get; set; }
}

class GraphEdge<TNodeValue>
{
    public required GraphNode<TNodeValue> Source { get; init; }
    public required GraphNode<TNodeValue> Destination { get; init; }
}

class GraphNode<TValue>
{
    public required TValue Value { get; set; }

    List<GraphEdge<TValue>> incomingEdges = [];
    List<GraphEdge<TValue>> outgoingEdges = [];

    public IReadOnlyList<GraphEdge<TValue>> IncomingEdges => incomingEdges;

    public IReadOnlyList<GraphEdge<TValue>> OutgoingEdges => outgoingEdges;

    public void AddEdgeTo(GraphNode<TValue> destination)
    {
        var edge = new GraphEdge<TValue> { Source = this, Destination = destination };
        outgoingEdges.Add(edge);
        destination.incomingEdges.Add(edge);
    }

    public void RemoveEdgeTo(GraphNode<TValue> destination)
    {
        var edge = outgoingEdges.FirstOrDefault(e => e.Destination == destination);

        if (edge != null)
        {
            outgoingEdges.Remove(edge);
            destination.incomingEdges.Remove(edge);
        }
        else
            throw new InvalidOperationException("Edge not found.");        
    }
}

class DirectedGraph<TNodeValue>
{
    public List<GraphNode<TNodeValue>> Nodes { get; set; } = [];

    public GraphNode<TNodeValue>? GetNodeByValue(TNodeValue value) =>
        Nodes.FirstOrDefault(node => EqualityComparer<TNodeValue>.Default.Equals(node.Value, value));

    public IList<GraphNode<TNodeValue>> TopologicalSort<TStructureNodeValue>(DirectedGraph<TStructureNodeValue> structureGraph, Func<TNodeValue, GraphNode<TStructureNodeValue>> mapping)
    {
        return Nodes;
    }
}

