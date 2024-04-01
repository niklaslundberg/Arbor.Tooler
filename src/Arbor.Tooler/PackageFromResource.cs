using NuGet.Protocol.Core.Types;

namespace Arbor.Tooler;

public record PackageFromResource(NuGetPackage Package, FindPackageByIdResource Resource, SourceCacheContext SourceCacheContext);