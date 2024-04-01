using System.Collections.Generic;

namespace Arbor.Tooler;

public class NuGetConfigTreeNode
{
    public string Path { get; }

    public List<NuGetConfigTreeNode> Nodes { get; } = new();

    public NuGetConfigTreeNode(string path)
    {
        Path = path;
    }

    public void AddNode(NuGetConfigTreeNode node)
    {
        Nodes.Add(node);
        node.Parent = this;
    }

    public NuGetConfigTreeNode? Parent { get; set; }
    public int Hops { get; set; }
}