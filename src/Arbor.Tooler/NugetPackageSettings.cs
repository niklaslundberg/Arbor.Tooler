namespace Arbor.Tooler
{
    public class NugetPackageSettings
    {
        public NugetPackageSettings(bool allowPreRelease)
        {
            AllowPreRelease = allowPreRelease;
        }

        public bool AllowPreRelease { get; }
    }
}