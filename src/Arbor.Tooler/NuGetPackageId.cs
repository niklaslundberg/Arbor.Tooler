namespace Arbor.Tooler
{
    public class NuGetPackageId
    {
        public NuGetPackageId(string packageId)
        {
            PackageId = packageId;
        }

        public string PackageId { get; }
    }
}