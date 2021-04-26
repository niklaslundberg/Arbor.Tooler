using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace Arbor.Tooler.Tests.Integration
{
    public class GetAllPackageVersionsTests
    {
        [Fact]
        public async Task GetAllPackageVersions()
        {
            var nuGetPackageInstaller = new NuGetPackageInstaller();

            Directory.SetCurrentDirectory(VcsTestPathHelper.FindVcsRootPath());

            var packageVersions = await nuGetPackageInstaller.GetAllVersionsAsync(new NuGetPackageId("Newtonsoft.Json"));

            packageVersions.Should().NotBeEmpty();
        }
    }
}
