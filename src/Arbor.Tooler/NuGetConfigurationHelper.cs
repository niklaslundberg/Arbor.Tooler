using System.Collections.Generic;
using System.IO;
using System.Linq;
using NuGet.Configuration;

namespace Arbor.Tooler
{
    public class NuGetConfigurationHelper
    {
        public static IReadOnlyCollection<NuGetConfigTreeNode> GetUsedConfigurationFiles(string? rootPath)
        {
            var currentDirectory = string.IsNullOrWhiteSpace(rootPath)
                ? new DirectoryInfo(Directory.GetCurrentDirectory())
                : new DirectoryInfo(rootPath);

            var settings = Settings.LoadDefaultSettings(
                currentDirectory.FullName,
                configFileName: null,
                new XPlatMachineWideSetting());

            var rootNodes = settings.GetConfigRoots().OrderBy(s => s.Length).Select(s=> new NuGetConfigTreeNode(s)).ToArray();


            var nodes = new List<NuGetConfigTreeNode>();

            foreach (var nuGetConfigTreeNode in rootNodes)
            {
                var parent = nodes.FirstOrDefault(s => nuGetConfigTreeNode.Path.StartsWith(s.Path));

                if (parent is { })
                {
                    parent.AddNode(nuGetConfigTreeNode);
                }
                else
                {
                    nodes.Add(nuGetConfigTreeNode);
                }
            }


            foreach (string configFilePath in settings.GetConfigFilePaths().OrderBy(s => s.Count(a => a.Equals(Path.DirectorySeparatorChar))))
            {
                var node = new NuGetConfigTreeNode(configFilePath);

                var fileInfo = new FileInfo(configFilePath);

                var parent = rootNodes.FirstOrDefault(node => fileInfo.FullName.StartsWith(node.Path));

                parent.AddNode(node);

                int hops = 0;
                DirectoryInfo? parentDirectory = fileInfo.Directory;
                while (true)
                {
                    if (parentDirectory is null)
                    {
                        break;
                    }

                    if (parentDirectory.FullName == currentDirectory.FullName)
                    {
                        break;
                    }

                    hops++;
                    parentDirectory = parentDirectory.Parent;
                }

                node.Hops = hops;
            }

            return nodes;
        }
    }
}
