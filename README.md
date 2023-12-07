# Arbor.Tooler - Downloads NuGet.exe and NuGet packages

[NuGet - https://www.nuget.org/packages/Arbor.Tooler](https://www.nuget.org/packages/Arbor.Tooler)

[GitHub - https://github.com/niklaslundberg/arbor.tooler](https://github.com/niklaslundberg/arbor.tooler)

[License - MIT https://github.com/niklaslundberg/Arbor.Tooler/blob/master/LICENSE.txt](https://github.com/niklaslundberg/Arbor.Tooler/blob/master/LICENSE.txt)

## NuGetDownloadClient

* Downloads nuget.exe from a specified URL or by default from nuget.org
* Downloads latest stable nuget.exe available at nuget.org by default

## NuGetPackageInstaller

* Downloads a NuGet package with nuget.exe to a specified directory or by default to the current user's %LocalApplicationData%\Arbor.Tooler\Packages
* Downloads latest stable version by default

## NugetPackageSettings

* AllowPreRelease, default false
* NugetSource, default null, using user's default settings
* NugetConfigFile, default null, using user's default settings

# Minimum usage

        NuGetPackageInstallResult result = await new NuGetPackageInstaller().InstallPackageAsync("Arbor.Tooler");
        Console.WriteLine(result.NuGetPackageId);
        Console.WriteLine(result.SemanticVersion);
        Console.WriteLine(result.PackageDirectory);

# Command line global tool

## Examples

### Show versions

    dotnet-arbor-tooler list -package-id=Arbor.Tooler -take=5 -config=C:\\nuget.config -source=nuget.org

Outputs

    0.19.0
    0.18.0
    0.17.0
    0.16.0
    0.15.0

### Download package

    dotnet-arbor-tooler download -package-id=Arbor.Tooler -version=0.26.0 -output-directory=C:\temp -config=C:\\nuget.config -source=nuget.org --extract