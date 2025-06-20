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

    public override string ToString() => Name;
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

    public IList<GraphNode<TNodeValue>> TopologicalSort<TStructureNodeValue>(
        DirectedGraph<TStructureNodeValue> structureGraph,
        Func<TNodeValue, GraphNode<TStructureNodeValue>> mapping)
    {
        //--------------------------------------------------------------------
        // Hilfsfunktionen – lokal, weil sie nur hier gebraucht werden
        //--------------------------------------------------------------------
        static GraphNode<TStructureNodeValue> GetParent(GraphNode<TStructureNodeValue> n)
            => n.IncomingEdges.Count == 0 ? null! : n.IncomingEdges[0].Source;

        // liefert das oberste Verzeichnis-Segment eines Knotens relativ zu "topDirs"
        static GraphNode<TStructureNodeValue> AscendToTopDir(
            GraphNode<TStructureNodeValue> dir,
            HashSet<GraphNode<TStructureNodeValue>> topDirs)
        {
            var cur = dir;
            while (!topDirs.Contains(cur))
                cur = GetParent(cur);
            return cur;
        }

        // einfacher, rein lexikografischer Kahn (Variante 1) – wird an den Blättern benutzt
        List<GraphNode<TNodeValue>> LexicographicTopo(List<GraphNode<TNodeValue>> subProjects)
        {
            var inDeg = new Dictionary<GraphNode<TNodeValue>, int>();
            foreach (var n in subProjects)
                inDeg[n] = n.IncomingEdges.Count(e => subProjects.Contains(e.Source));

            var ready = new SortedSet<GraphNode<TNodeValue>>(
                Comparer<GraphNode<TNodeValue>>.Create(
                    (a, b) => StringComparer.OrdinalIgnoreCase.Compare(
                                  a.Value?.ToString(), b.Value?.ToString())));

            foreach (var n in subProjects.Where(n => inDeg[n] == 0))
                ready.Add(n);

            var res = new List<GraphNode<TNodeValue>>(subProjects.Count);

            while (ready.Count > 0)
            {
                var n = ready.Min!;
                ready.Remove(n);
                res.Add(n);

                foreach (var e in n.OutgoingEdges.Where(e => inDeg.ContainsKey(e.Destination)))
                {
                    if (--inDeg[e.Destination] == 0)
                        ready.Add(e.Destination);
                }
            }

            if (res.Count != subProjects.Count)
                throw new InvalidOperationException("Zyklen innerhalb eines Verzeichnisses erkannt.");

            return res;
        }

        //--------------------------------------------------------------------
        // Rekursiver Kern: sortiert alle Projekte einer gegebenen Teilmenge
        //--------------------------------------------------------------------
        List<GraphNode<TNodeValue>> SortRecursive(List<GraphNode<TNodeValue>> projectSubset)
        {
            // 1) alle Directory-Knoten bestimmen, in denen die Projekte liegen
            var dirsInSubset = new HashSet<GraphNode<TStructureNodeValue>>(
                projectSubset.Select(p => mapping(p.Value)));

            // 2) Top-Level-Directories innerhalb dieses Subsets herausfiltern
            var topDirs = new HashSet<GraphNode<TStructureNodeValue>>(
                dirsInSubset.Where(d =>
                {
                    var parent = GetParent(d);
                    return parent == null || !dirsInSubset.Contains(parent);
                }));

            // Basisfall: nur noch ein einziges Verzeichnis – lexikografische Topo genügt
            if (topDirs.Count == 1)
                return LexicographicTopo(projectSubset);

            //----------------------------------------------------------------
            // 3) Projekte nach ihrem Top-Level-Directory buckeln
            //----------------------------------------------------------------
            var bucket = new Dictionary<GraphNode<TStructureNodeValue>, List<GraphNode<TNodeValue>>>();
            foreach (var p in projectSubset)
            {
                var dir = AscendToTopDir(mapping(p.Value), topDirs);
                if (!bucket.TryGetValue(dir, out var list))
                {
                    list = new List<GraphNode<TNodeValue>>();
                    bucket[dir] = list;
                }
                list.Add(p);
            }

            //----------------------------------------------------------------
            // 4) Verzeichnis-Abhängigkeitsgraph aufbauen (einmal pro Dir-Paar)
            //----------------------------------------------------------------
            var dirInDeg = topDirs.ToDictionary(d => d, _ => 0);
            var dirOutSet = topDirs.ToDictionary(d => d,
                              _ => new HashSet<GraphNode<TStructureNodeValue>>());

            foreach (var p in projectSubset)
            {
                var srcDir = AscendToTopDir(mapping(p.Value), topDirs);

                foreach (var e in p.OutgoingEdges.Where(e => projectSubset.Contains(e.Destination)))
                {
                    var dstDir = AscendToTopDir(mapping(e.Destination.Value), topDirs);
                    if (srcDir != dstDir && dirOutSet[srcDir].Add(dstDir))
                        dirInDeg[dstDir] += 1;
                }
            }

            //----------------------------------------------------------------
            // 5) Kahn auf Verzeichnis-Ebene
            //----------------------------------------------------------------
            var dirReady = new SortedSet<GraphNode<TStructureNodeValue>>(
                Comparer<GraphNode<TStructureNodeValue>>.Create(
                    (a, b) => StringComparer.OrdinalIgnoreCase.Compare(
                                  a.Value?.ToString(), b.Value?.ToString())));

            foreach (var d in topDirs.Where(d => dirInDeg[d] == 0))
                dirReady.Add(d);

            var ordered = new List<GraphNode<TNodeValue>>(projectSubset.Count);

            while (dirReady.Count > 0)
            {
                var dir = dirReady.Min!;
                dirReady.Remove(dir);

                // 5a) Rekursiv alle Projekte *innerhalb* dieses Verzeichnisses sortieren
                ordered.AddRange(SortRecursive(bucket[dir]));

                // 5b) Verzeichnis aus dem Graph „entfernen“
                foreach (var succ in dirOutSet[dir])
                    if (--dirInDeg[succ] == 0)
                        dirReady.Add(succ);
            }

            if (ordered.Count != projectSubset.Count)
                throw new InvalidOperationException("Zyklen zwischen Verzeichnissen erkannt.");

            return ordered;
        }

        // -------------------------------------------------------------------
        // Aufruf auf der Gesamtmenge aller Projekt-Knoten
        // -------------------------------------------------------------------
        return SortRecursive(Nodes.ToList());
    }

}

