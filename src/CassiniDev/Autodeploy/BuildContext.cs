using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Build.Evaluation;
using System.Collections.ObjectModel;
using System.Reflection;
using System.IO;
using System.Globalization;

namespace CassiniDev.Autodeploy
{
    public class BuildContext
    {
        private Collection<Project> m_scheduledProjects = new Collection<Project>();

        public event EventHandler<AssemblyAvailableEventArgs> AssemblyAvailable;
        public event EventHandler<BuildStatusEventArgs> Complete;

        public ReadOnlyCollection<Project> ScheduledProjects
        {
            get
            {
                return new ReadOnlyCollection<Project>(m_scheduledProjects);
            }
        }

        internal void ScheduleProject(Project project)
        {
            m_scheduledProjects.Add(project);
        }

        public void Build()
        {
            var status = BuildStatus.Building;

            foreach (var project in m_scheduledProjects)
            {
                if (!project.Build())
                {
                    status = BuildStatus.Failed;
                    break;
                }

                if (AssemblyAvailable == null)
                    continue;

                var outputDir = project.GetProperty("OutputPath").EvaluatedValue;
                var asmName = project.GetProperty("AssemblyName").EvaluatedValue;

                var asmPath = Path.Combine(Path.GetDirectoryName(project.FullPath), outputDir, string.Format("{0}.dll", asmName));
                var bytes = File.ReadAllBytes(asmPath);

                try
                {
                    var name = new AssemblyName()
                    {
                        Name = asmName,
                        CodeBase = new Uri(asmPath).AbsoluteUri,
                        CultureInfo = CultureInfo.InvariantCulture
                    };

                    var args = new AssemblyAvailableEventArgs(name);

                    AssemblyAvailable(this, args);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                }
            }

            if (Complete != null)
            {
                if (status == BuildStatus.Building)
                {
                    status = BuildStatus.Success;
                }

                var args = new BuildStatusEventArgs()
                {
                    BuildStatus = status
                };

                Complete(this, args);
            }
        }
    }
}
