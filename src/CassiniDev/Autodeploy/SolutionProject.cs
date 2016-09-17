using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;

namespace CassiniDev.Autodeploy
{
    public class SolutionProject
    {
        static readonly Type s_ProjectInSolution;
        static readonly PropertyInfo s_ProjectInSolution_ProjectName;
        static readonly PropertyInfo s_ProjectInSolution_RelativePath;
        static readonly PropertyInfo s_ProjectInSolution_ProjectGuid;

        private object m_solutionProject;

        static SolutionProject()
        {
            s_ProjectInSolution = Type.GetType("Microsoft.Build.Construction.ProjectInSolution, Microsoft.Build, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a", false, false);

            if (s_ProjectInSolution != null)
            {
                s_ProjectInSolution_ProjectName = s_ProjectInSolution.GetProperty("ProjectName", BindingFlags.NonPublic | BindingFlags.Instance);
                s_ProjectInSolution_RelativePath = s_ProjectInSolution.GetProperty("RelativePath", BindingFlags.NonPublic | BindingFlags.Instance);
                s_ProjectInSolution_ProjectGuid = s_ProjectInSolution.GetProperty("ProjectGuid", BindingFlags.NonPublic | BindingFlags.Instance);
            }
        }
        
        public SolutionProject(object solutionParser)
        {
            //var genType = Type.GetType("Microsoft.Build.Construction.SolutionProjectGenerator, Microsoft.Build, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a", false, false);



            //var types = s_ProjectInSolution.Assembly.GetTypes();
            var ctor = s_ProjectInSolution.GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public, null, new[] { solutionParser.GetType() }, null);

            //var ctors = s_ProjectInSolution.GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            //var ctor = ctors[0];
            //var args = ctor.GetParameters();
            //var methods = s_ProjectInSolution.GetMethods(BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Public | BindingFlags.Instance);
            m_solutionProject = ctor.Invoke(new object[] { solutionParser });
        }

        public string ProjectName
        {
            get
            {
                return s_ProjectInSolution_ProjectName.GetValue(m_solutionProject, null) as string;
            }
            set
            {
                s_ProjectInSolution_ProjectName.SetValue(m_solutionProject, value, null);
            }
        }

        public string RelativePath
        {
            get
            {
                return s_ProjectInSolution_RelativePath.GetValue(m_solutionProject, null) as string;
            }
            set
            {
                s_ProjectInSolution_RelativePath.SetValue(m_solutionProject, value, null);
            }
        }

        public string ProjectGuid
        {
            get
            {
                return s_ProjectInSolution_ProjectGuid.GetValue(m_solutionProject, null) as string;
            }
            set
            {
                s_ProjectInSolution_ProjectGuid.SetValue(m_solutionProject, value, null);
            }
        }

        public object Object
        {
            get
            {
                return m_solutionProject;
            }
        }
    }
}