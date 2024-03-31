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

### Download package and extract its content

    dotnet-arbor-tooler download -package-id=Arbor.Tooler -version=0.26.0 -output-directory=C:\temp -config=C:\\nuget.config -source=nuget.org --extract