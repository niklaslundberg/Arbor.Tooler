using System.IO;
using Arbor.Aesculus.Core;
using NCrunch.Framework;

namespace Arbor.Tooler.Tests.Integration
{
    public static class VcsTestPathHelper
    {
        public static string FindVcsRootPath()
        {
            if (NCrunchEnvironment.NCrunchIsResident())
            {
                string originalSolutionPath = NCrunchEnvironment.GetOriginalSolutionPath();

                var fileInfo = new FileInfo(originalSolutionPath);

                return VcsPathHelper.FindVcsRootPath(fileInfo.Directory?.FullName);
            }

            return VcsPathHelper.FindVcsRootPath();
        }
    }
}
