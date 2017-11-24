using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.XmlConfiguration;

namespace CassiniDev.Tests
{
    [TestFixture]
    public class ConfigRewriteTest
    {
        [Test]
        public void RewriteSimpleAppConfig()
        {
            var rewrittenConfig = ForConfigAndReplacements(@"
                <configuration>
                    <appSettings>
                        <add key=""Test"" value=""true"" />
                    </appSettings>
                </configuration>",
                (builder) => builder.ForPath("appSettings", (replacementBuilder) =>
                    replacementBuilder.ForKey("Test", value: "false")));

            Assert.That(rewrittenConfig, Does.Contain(@"value=""false"""));
        }

        [Test]
        public void SkipRewritingAValueIfNotConfigured()
        {
            var rewrittenConfig = ForConfigAndReplacements(@"
                <configuration>
                    <appSettings>
                        <add key=""Test"" value=""true"" />
                    </appSettings>
                </configuration>",
                (builder) => builder.ForPath("appSettings", (replacementBuilder) =>
                    replacementBuilder.ForKey("A", value: "false")));

            Assert.That(rewrittenConfig, Does.Contain(@"value=""true"""));
        }

        private string ForConfigAndReplacements(string configSource, Func<ConfigReplacementsBuilder, ConfigReplacementsBuilder> withBuilder)
        {
            return OverwriteConfig(configSource, withBuilder(new ConfigReplacementsBuilder()).Build());
        }

        public string OverwriteConfig(string xmlConfig, ConfigReplacements replacements)
        {
            var doc = new XmlDocument();

            doc.LoadXml(xmlConfig);

            var root = doc.DocumentElement;

            foreach (var path in replacements.Paths)
            {
                var container = root.SelectSingleNode(string.Format("/configuration/{0}", path.Path));
                var nodes = container.SelectNodes("add");
                var nameValues = path.ConfigReplacement.NameValues;

                foreach (var node in nodes)
                {
                    if (!(node is XmlElement))
                    {
                        continue;
                    }

                    var element = (XmlElement)node;
                    var key = element.Attributes["key"];

                    var valueForRewrite = nameValues.Get(key.Value);

                    if (valueForRewrite != null)
                    {
                        element.SetAttribute("value", valueForRewrite);
                    }
                }
            }

            var writer = new StringWriter();

            doc.Save(writer);

            return writer.ToString();
        }

        public string TraverseConfigSection(string xmlConfig, string section)
        {
            return xmlConfig;
        }
    }

    public class ConfigReplacementsBuilder
    {
        private readonly List<ConfigElementPath> paths;

        public ConfigReplacementsBuilder(): this(new List<ConfigElementPath>())
        {
        }

        public ConfigReplacementsBuilder(List<ConfigElementPath> paths)
        {
            this.paths = paths;
        }

        public ConfigReplacementsBuilder ForPath(string path, Func<ConfigReplacementBuilder, ConfigReplacementBuilder> withReplacementBuilder)
        {
            var newPaths = new List<ConfigElementPath>(paths);
            var configReplacement = withReplacementBuilder.Invoke(new ConfigReplacementBuilder()).Build();

            newPaths.Add(new ConfigElementPath(path, configReplacement));

            return new ConfigReplacementsBuilder(newPaths);
        }

        public ConfigReplacements Build()
        {
            return new ConfigReplacements(paths);
        }
    }

    public class ConfigReplacementBuilder
    {
        private readonly NameValueCollection nameValues;

        public ConfigReplacementBuilder(): this(new NameValueCollection())
        {
        }

        public ConfigReplacementBuilder(NameValueCollection nameValue)
        {
            this.nameValues = nameValue;
        }

        public ConfigReplacement Build()
        {
            return new ConfigReplacement(nameValues);
        }

        public ConfigReplacementBuilder ForKey(string key, string value)
        {
            var values = new NameValueCollection(nameValues);

            values.Add(key, value);

            return new ConfigReplacementBuilder(values);
        }
    }

    public class ConfigReplacements
    {
        private readonly IReadOnlyCollection<ConfigElementPath> paths;

        internal ConfigReplacements(List<ConfigElementPath> paths)
        {
            this.paths = paths.AsReadOnly();
        }

        public IReadOnlyCollection<ConfigElementPath> Paths
        {
            get
            {
                return paths;
            }
        }
    }

    public class ConfigElementPath
    {
        private readonly string path;
        private readonly ConfigReplacement configReplacement;

        public ConfigReplacement ConfigReplacement
        {
            get
            {
                return configReplacement;
            }
        }

        public string Path
        {
            get
            {
                return path;
            }
        }

        public ConfigElementPath(string path, ConfigReplacement configReplacement)
        {
            this.path = path;
            this.configReplacement = configReplacement;
        }
    }

    public class ConfigReplacement
    {
        private NameValueCollection nameValues;

        public ConfigReplacement(NameValueCollection nameValues)
        {
            this.nameValues = nameValues;
        }

        public NameValueCollection NameValues
        {
            get
            {
                return nameValues;
            }

            set
            {
                nameValues = value;
            }
        }
    }
}
