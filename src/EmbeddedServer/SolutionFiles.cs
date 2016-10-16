using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace DotNetTestkit
{
    public class SolutionFiles
    {
        private readonly string solutionDir;

        public SolutionFiles(string solutionDir)
        {
            this.solutionDir = solutionDir;
        }

        public static SolutionFiles FromPathFile(string filepath)
        {
            var fullFilepath = GetDirectory(filepath).FullName;

            return new SolutionFiles(File.ReadAllText(fullFilepath).Trim());
        }

        public static SolutionFiles FromSolutionRoot()
        {
            return FindSolutionRootFromAbsolute(GetDirectory("."));
        }

        public static SolutionFiles FromSolutionRootFor(string path)
        {
            return FindSolutionRootFromAbsolute(GetDirectory(path));
        }

        public string ResolvePath(string relativePath)
        {
            return Path.Combine(solutionDir, relativePath);
        }

        private static SolutionFiles FindSolutionRootFromAbsolute(DirectoryInfo dir)
        {
            if (dir.EnumerateFiles("*.sln").Count() > 0)
            {
                return new SolutionFiles(dir.FullName);
            }
            else if (dir.Parent != null)
            {
                return FindSolutionRootFromAbsolute(dir.Parent);
            }
            else
            {
                throw new Exception("Failed to find a solution directory");
            }
        }

        private static DirectoryInfo GetDirectory(string filepath)
        {
            return new DirectoryInfo(
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, filepath));
        }
    }
}
