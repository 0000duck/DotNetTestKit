// -----------------------------------------------------------------------
// <copyright file="VSProvider.cs" company="">
// TODO: Update copyright text.
// </copyright>
// -----------------------------------------------------------------------

namespace CassiniDev.Autodeploy
{
    using System;
    using System.Reflection;
    using System.Collections.Generic;
    using System.Linq;
    using System.Diagnostics;
    using System.IO;
    using Microsoft.Build.Evaluation;
    
    public class Solution
    {
        //internal class SolutionParser 
        //Name: Microsoft.Build.Construction.SolutionParser 
        //Assembly: Microsoft.Build, Version=4.0.0.0 

        static readonly Type s_SolutionParser;
        static readonly PropertyInfo s_SolutionParser_solutionFile;
        static readonly MethodInfo s_SolutionParser_parseSolutionFile;
        static readonly PropertyInfo s_SolutionParser_projects;
        private string solutionFileName;
        private ProjectCollection projects;
        private static MethodInfo s_SolutionParser_addProjectToSolution;

        public string Name
        {
            get
            {
                return Path.GetFileNameWithoutExtension(solutionFileName);
            }
        }

        public ProjectCollection Projects
        {
            get
            {
                return projects;
            }
        }

        static Solution()
        {
            s_SolutionParser = Type.GetType("Microsoft.Build.Construction.SolutionParser, Microsoft.Build, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a", false, false);
            s_SolutionParser_solutionFile = s_SolutionParser.GetProperty("SolutionFile", BindingFlags.NonPublic | BindingFlags.Instance);
            s_SolutionParser_projects = s_SolutionParser.GetProperty("Projects", BindingFlags.NonPublic | BindingFlags.Instance);
            s_SolutionParser_parseSolutionFile = s_SolutionParser.GetMethod("ParseSolutionFileForConversion", BindingFlags.NonPublic | BindingFlags.Instance);
            s_SolutionParser_addProjectToSolution = s_SolutionParser.GetMethod("AddProjectToSolution", BindingFlags.NonPublic | BindingFlags.Instance);
        }

        public string SolutionPath
        {
            get
            {
                var file = new FileInfo(solutionFileName);

                return file.DirectoryName;
            }
        }

        public Solution(string solutionFileName)
        {
            if (s_SolutionParser == null)
            {
                throw new InvalidOperationException("Can not find type 'Microsoft.Build.Construction.SolutionParser' are you missing a assembly reference to 'Microsoft.Build.dll'?");
            }

            var solutionParser = s_SolutionParser.GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic).First().Invoke(null);
            var project = new SolutionProject(solutionParser);

            project.ProjectName = "Test";
            project.ProjectGuid = "{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}";
            project.RelativePath = "Test.csproj";

            //using (var streamReader = new StreamReader(solutionFileName))
            //{
                s_SolutionParser_solutionFile.SetValue(solutionParser, solutionFileName, null);
                s_SolutionParser_parseSolutionFile.Invoke(solutionParser, null);
            //}

            this.solutionFileName = solutionFileName;

            var types = solutionParser.GetType().Assembly.GetTypes();

            var slnTypes = types.Where(x => x.FullName.ToLower().IndexOf("solution") > 0).ToList();

            var methods = project.Object.GetType().GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);


            projects = new ProjectCollection();
            var array = (Array)s_SolutionParser_projects.GetValue(solutionParser, null);

            for (int i = 0; i < array.Length; i++)
            {
                var projectInSolution = array.GetValue(i);
                var relativePathProp = projectInSolution.GetType().GetProperty("RelativePath", BindingFlags.NonPublic | BindingFlags.Instance);
                var relativePath = (string)relativePathProp.GetValue(projectInSolution, null);

                projects.LoadProject(Path.Combine(Path.GetDirectoryName(solutionFileName), relativePath));
            }

            s_SolutionParser_addProjectToSolution.Invoke(solutionParser, new object[] { project.Object });
        }

        public void AddProject(SolutionProject project)
        {
            
        }

        public void Dispose()
        {
        }
    }
}
