// -----------------------------------------------------------------------
// <copyright file="ProjectMonitor.cs" company="">
// TODO: Update copyright text.
// </copyright>
// -----------------------------------------------------------------------

namespace CassiniDev.Autodeploy
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
using Microsoft.Build.Evaluation;
    using System.IO;
    using System.Timers;
    using Microsoft.Build.Logging;
    using Microsoft.Build.Framework;
    using System.Collections;

    /// <summary>
    /// TODO: Update summary.
    /// </summary>
    public class HostWatchManager
    {
        private Dictionary<string, ProjectCollection> projectCollections = new Dictionary<string, ProjectCollection>();
        private Dictionary<string, Project> loadedProjects = new Dictionary<string, Project>();
        private Dictionary<string, List<Project>> fileDependencies = new Dictionary<string, List<Project>>();
        private Dictionary<string, List<Project>> projDependencies = new Dictionary<string, List<Project>>();
        private Dictionary<string, List<Project>> dllDependencies = new Dictionary<string, List<Project>>();
        private Dictionary<string, FileSystemWatcher> projWatchers = new Dictionary<string, FileSystemWatcher>();
        private Dictionary<string, Timer> m_buildTimers = new Dictionary<string, Timer>();
        private Dictionary<string, Timer> m_changeTimers = new Dictionary<string, Timer>();
        private Dictionary<string, Action<object, BuildContextEventArgs>> m_buildHooks = new Dictionary<string, Action<object, BuildContextEventArgs>>();
        private Dictionary<string, Action<object, FileSystemEventArgs>> m_changedHooks = new Dictionary<string, Action<object, FileSystemEventArgs>>();

        private static ILogger logger = new ConsoleLogger();

        public HostWatchManager()
        {
        }
        
        public void AddSolution(string slnFile)
        {
            Solution sln = new Solution(slnFile);

            var projects = sln.Projects;

            var projCollection = MakeProjectCollection();

            projectCollections.Add(slnFile, projCollection);

            projCollection.GlobalProperties.Add("SolutionDir", string.Format("{0}{1}", Path.GetDirectoryName(slnFile), Path.DirectorySeparatorChar));
            projCollection.GlobalProperties.Add("SolutionPath", slnFile);
            projCollection.GlobalProperties.Add("SolutionName", Path.GetFileNameWithoutExtension(slnFile));
            projCollection.GlobalProperties.Add("SolutionFileName", Path.GetFileName(slnFile));

            foreach (var proj in projects.LoadedProjects)
            {
                AddProject(projCollection, proj.FullPath);
            }
        }

        public void OnBuild(string watchPath, Action<object, BuildContextEventArgs> callback)
        {
            m_buildHooks.Add(GetPathKey(watchPath), callback);
        }

        public void OnChanged(string watchPath, Action<object, FileSystemEventArgs> callback)
        {
            m_changedHooks.Add(GetPathKey(watchPath), callback);
        }

        private ProjectCollection MakeProjectCollection()
        {
            var projects = new ProjectCollection();
            projects.RegisterLogger(logger);

            return projects;
        }
        
        public void AddProject(string projectFile)
        {
            var projCollection = MakeProjectCollection();
            AddProject(projCollection, projectFile);

            projectCollections.Add(projectFile, projCollection);
        }

        private void AddProject(ProjectCollection collection, string projectFile)
        {
            if (IsProjectLoaded(projectFile) || !File.Exists(projectFile))
            {
                return;
            }

            var fsWatcher = new FileSystemWatcher(Path.GetDirectoryName(projectFile));
            var project = collection.LoadProject(projectFile);

            LoadProject(project);

            foreach (var projItem in GetProjectDepProjectFiles(project))
            {
                AddProject(collection, projItem);
                AddProjectDependency(projItem, project);
            }

            projWatchers.Add(projectFile, fsWatcher);

            fsWatcher.Changed += OnChanged;
            fsWatcher.Created += OnChanged;
            fsWatcher.Deleted += OnChanged;
            fsWatcher.Renamed += OnChanged;

            fsWatcher.IncludeSubdirectories = true;
            fsWatcher.EnableRaisingEvents = true;
        }

        private void LoadProject(Project project)
        {
            if (IsProjectLoaded(project.FullPath))
            {
                return;
            }

            loadedProjects.Add(project.FullPath, project);

            AddFileDependency(project.FullPath, project);

            foreach (var compileItem in GetProjectDepFiles(project))
            {
                AddFileDependency(compileItem, project);
            }

            foreach (var refItem in GetProjectRefHintFiles(project))
            {
                if (!dllDependencies.ContainsKey(refItem))
                {
                    dllDependencies.Add(refItem, new List<Project>());
                }

                dllDependencies[refItem].Add(project);
            }
        }

        private void AddProjectDependency(string file, Project project)
        {
            if (!projDependencies.ContainsKey(file))
            {
                projDependencies.Add(file, new List<Project>());
            }

            projDependencies[file].Add(project);
        }

        private bool IsProjectLoaded(string projectFile)
        {
            return loadedProjects.ContainsKey(projectFile);
        }

        private void AddFileDependency(string file, Project project)
        {
            if (!fileDependencies.ContainsKey(file))
            {
                fileDependencies.Add(file, new List<Project>());
            }
            
            fileDependencies[file].Add(project);
        }

        private void OnChanged(object sender, FileSystemEventArgs e)
        {
            string privateBinPath;

            if (IsInPrivateBin(e.FullPath, out privateBinPath))
            {
                var key = GetPathKey(privateBinPath);
                if (m_changedHooks.ContainsKey(key))
                {
                    m_changedHooks[key].Invoke(this, e);
                }
            }

            if (!fileDependencies.ContainsKey(e.FullPath))
            {
                return;
            }

            var deps = fileDependencies[e.FullPath];

            foreach (var depProject in deps)
            {
                if (e.FullPath == depProject.FullPath)
                {
                    ReloadProject(depProject);
                }

                ScheduleBuild(depProject);
           } 
        }

        private bool IsInPrivateBin(string p, out string privatePath)
        {
            var target = p;

            privatePath = null;

            while ((target = Path.GetDirectoryName(target)) != null)
            {
                var key = GetPathKey(target);

                if (m_changedHooks.ContainsKey(key))
                {
                    privatePath = target;

                    return true;
                }
            }

            return false;
        }

        private void ScheduleBuild(Project depProject)
        {
            Timer timer;
            if (m_buildTimers.TryGetValue(depProject.FullPath, out timer))
            {
                lock (timer)
                {
                    // reset
                    timer.Stop();
                    timer.Start();
                }

                return;
            }

            timer = new Timer(1000);

            timer.Elapsed += (sender, e) =>
            {
                timer.Stop();

                BuildProject(depProject);

                m_buildTimers.Remove(depProject.FullPath);
            };

            m_buildTimers.Add(depProject.FullPath, timer);

            timer.Start();
        }

        private void BuildProject(Project depProject)
        {
            Console.WriteLine("Build started {0}", depProject.FullPath);

            var buildContext = new BuildContext();

            PopulateBuildContext(depProject, buildContext);

            foreach (var project in buildContext.ScheduledProjects)
            {
                var path = GetPathKey(Path.Combine(Path.GetDirectoryName(project.FullPath), project.GetProperty("OutputPath").EvaluatedValue));
                var asmName = project.GetProperty("AssemblyName").EvaluatedValue;

                if (m_buildHooks.ContainsKey(path))
                {
                    var buildArgs = new BuildContextEventArgs(buildContext);
                    m_buildHooks[path].Invoke(this, buildArgs);
                }
            }

            buildContext.Build();
        }

        private string GetPathKey(string p)
        {
            p = p.TrimEnd(Path.DirectorySeparatorChar);
            return p.ToLower();
        }

        private void PopulateBuildContext(Project project, BuildContext buildContext)
        {
            buildContext.ScheduleProject(project);

            if (projDependencies.ContainsKey(project.FullPath))
            {
                foreach (var subProj in projDependencies[project.FullPath])
                {
                    PopulateBuildContext(subProj, buildContext);
                }
            }
        }

        private void ReloadProject(Project project)
        {
            foreach (var file in GetProjectDepFiles(project))
            {
                if (fileDependencies.ContainsKey(file))
                {
                    fileDependencies[file].Remove(project);
                }
            }

            project.ReevaluateIfNecessary();

            foreach (var compileItem in GetProjectDepFiles(project))
            {
                AddFileDependency(compileItem, project);
            }
        }

        private List<string> GetProjectDepFiles(Project project)
        {
            var files = new List<string>();

            foreach (var compileItem in project.GetItems("Compile"))
            {
                var path = Path.Combine(Path.GetDirectoryName(project.FullPath), compileItem.EvaluatedInclude);
                files.Add(path);
            }

            return files;
        }

        private List<string> GetProjectDepProjectFiles(Project project)
        {
            var files = new List<string>();

            foreach (var refItem in project.GetItems("ProjectReference"))
            {
                var path = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(project.FullPath), refItem.EvaluatedInclude));
                files.Add(path);
            }

            return files;
        }

        private List<string> GetProjectRefHintFiles(Project project)
        {
            var files = new List<string>();

            foreach (var refItem in project.GetItems("Reference"))
            {
                var hintPath = refItem.GetMetadata("HintPath");

                if (hintPath == null || string.IsNullOrEmpty(hintPath.EvaluatedValue))
                {
                    continue;
                }

                var path = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(project.FullPath), hintPath.EvaluatedValue));
                files.Add(path);
            }

            return files;
        }
    }
}
