using System.Collections.Generic;

namespace Arbor.Tooler;

public class NuGetConfigTreeNode(string path)
{
    public string Path { get; } = path;

    public List<NuGetConfigTreeNode> Nodes { get; } = new();

    public void AddNode(NuGetConfigTreeNode node)
    {
        Nodes.Add(node);
        node.Parent = this;
    }

    public NuGetConfigTreeNode? Parent { get; set; }
    public int Hops { get; set; }
}