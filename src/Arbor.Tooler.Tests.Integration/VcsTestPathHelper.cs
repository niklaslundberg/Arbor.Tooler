using System.IO;
using Arbor.Aesculus.Core;

namespace Arbor.Tooler.ConsoleClient
{
    public static class VcsTestPathHelper
    {
        public static string FindVcsRootPath()
        {
            if (NCrunch.Framework.NCrunchEnvironment.NCrunchIsResident())
            {
                string originalSolutionPath = NCrunch.Framework.NCrunchEnvironment.GetOriginalSolutionPath();

                var fileInfo = new FileInfo(originalSolutionPath);

                return VcsPathHelper.FindVcsRootPath(fileInfo.Directory?.FullName);
            }

            return VcsPathHelper.FindVcsRootPath();
        }
    }
}
