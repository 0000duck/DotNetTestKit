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
        public void RewriteAComplexElement()
        {
            var rewrittenConfig = ForConfigAndReplacements(@"
                <configuration>
                    <connectionStrings>
                        <add name=""Test"" connectionString=""ConnectionString"" providerName=""ProviderName"" />
                    </connectionStrings>
                </configuration>",
                (builder) => builder.ForPath("connectionStrings", (replacementBuilder) =>
                    replacementBuilder.WithKeyName("name").ForKey("Test", value: new
                    {
                        connectionString = "ConnectionString-M",
                        providerName = "ProviderName-M"
                    })));

            Assert.That(rewrittenConfig, Does.Contain(@"connectionString=""ConnectionString-M"""));
        }

        [Test]
        public void SkipRewritingAValueIfNotConfigured()
        {
            var rewrittenConfig = ForConfigAndReplacements(@"
                <configuration>
                    <appSettings>
                        <add key=""Test"" value=""true"" />
                        <add key=""A"" value=""true"" />
                    </appSettings>
                </configuration>",
                (builder) => builder.ForPath("appSettings", (replacementBuilder) =>
                    replacementBuilder.ForKey("A", value: "false")));

            Assert.That(rewrittenConfig, Does.Contain(@"<add key=""Test"" value=""true"" />"));
        }

        [Test]
        public void CombineRewrittenConfig()
        {
            var givenAppSettings =
                @"<appSettings>
                    <add key=""Test"" value=""true"" />
                </appSettings>";

            var rewrittenConfig = ForConfigAndReplacements(@"
                <configuration>
                    <appSettings configSource=""a/b/c/appSettings.config"" />
                </configuration>",
                (builder) => builder.ForPath("appSettings", (replacementBuilder) =>
                    replacementBuilder.ForKey("Test", value: "false")),
                new Dictionary<string, string>()
                {
                    { "a/b/c/appSettings.config", givenAppSettings }
                });

            Assert.That(rewrittenConfig, Does.Contain(@"<add key=""Test"" value=""false"" />"));
        }

        [Test]
        public void FailIfValueProvidedDoesNotExist()
        {
            Assert.Throws<Exception>(() => ForConfigAndReplacements(@"
                <configuration>
                    <appSettings>
                        <add key=""Test"" value=""true"" />
                    </appSettings>
                </configuration>",
                (builder) => builder.ForPath("appSettings", (replacementBuilder) =>
                    replacementBuilder.ForKey("A", value: "false"))));
        }

        private string ForConfigAndReplacements(string configSource, Func<ConfigReplacementsBuilder, ConfigReplacementsBuilder> withBuilder, Dictionary<string, string> extraFiles = null)
        {
            if (extraFiles == null)
            {
                extraFiles = new Dictionary<string, string>();
            }        

            return OverwriteConfig(new ConfigSources(extraFiles), configSource, withBuilder(new ConfigReplacementsBuilder()).Build());
        }

        public string OverwriteConfig(ConfigSources sources, string xmlConfig, ConfigReplacements replacements)
        {
            var doc = ReadDocument(xmlConfig);

            var root = doc.DocumentElement;

            foreach (var path in replacements.Paths)
            {
                var container = root.SelectSingleNode(string.Format("/configuration/{0}", path.Path));
                var configSource = container.Attributes["configSource"];

                if (configSource != null)
                {
                    var inclusionSource = sources.ResolveSource(configSource.Value);
                    var xmlDoc = ReadDocument(inclusionSource);

                    var importedContainer = doc.ImportNode(xmlDoc.DocumentElement, true);

                    container.ParentNode.ReplaceChild(importedContainer, container);

                    container = importedContainer;
                }

                var nodes = container.SelectNodes("add");
                var nameValues = path.ConfigReplacement.NameValues;
                var keyName = path.ConfigReplacement.KeyName;
                var unusedValues = new HashSet<string>(nameValues.Keys);

                foreach (var node in nodes)
                {
                    if (!(node is XmlElement))
                    {
                        continue;
                    }

                    var element = (XmlElement)node;
                    var key = element.Attributes[keyName];

                    if (key == null)
                    {
                        throw new Exception(string.Format("Key \"{0}\" not found for {1}", keyName, path.Path));
                    }

                    object valueForRewrite;

                    if (nameValues.TryGetValue(key.Value, out valueForRewrite))
                    {
                        RewriteElement(element, valueForRewrite);

                        unusedValues.Remove(key.Value);
                    }
                }

                if (unusedValues.Count > 0)
                {
                    throw new Exception(string.Format("Value was not used: {0}", unusedValues.First()));
                }
            }

            var writer = new StringWriter();

            doc.Save(writer);

            return writer.ToString();
        }

        private XmlDocument ReadDocument(string source)
        {
            var doc = new XmlDocument();

            doc.LoadXml(source);

            return doc;
        }

        private void RewriteElement(XmlElement element, object valueForRewrite)
        {
            if (valueForRewrite is string)
            {
                element.SetAttribute("value", valueForRewrite.ToString());
            } else if (valueForRewrite is object)
            {
                RewriteElementWithComplextObject(element, valueForRewrite);
            }
        }

        private void RewriteElementWithComplextObject(XmlElement element, object valueForRewrite)
        {
            var valueType = valueForRewrite.GetType();

            foreach (var property in valueType.GetProperties())
            {
                var fieldName = property.Name;
                var attribute = element.Attributes[fieldName];

                attribute.Value = (string)property.GetValue(valueForRewrite);
            }
        }

        public string TraverseConfigSection(string xmlConfig, string section)
        {
            return xmlConfig;
        }
    }

    public class ConfigSources
    {
        private readonly Dictionary<string, string> sources;

        public ConfigSources(Dictionary<string, string> sources)
        {
            this.sources = sources;
        }

        public string ResolveSource(string path)
        {
            return sources[path];
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
        private readonly string keyName;
        private readonly Dictionary<string, object> nameValues;

        public ConfigReplacementBuilder(): this(new Dictionary<string, object>())
        {
        }

        public ConfigReplacementBuilder(Dictionary<string, object> nameValues, string keyName = "key")
        {
            this.keyName = keyName;
            this.nameValues = nameValues;
        }

        public ConfigReplacement Build()
        {
            return new ConfigReplacement(keyName, nameValues);
        }

        public ConfigReplacementBuilder ForKey(string key, string value)
        {
            return ForKey(key, (object)value);
        }

        public ConfigReplacementBuilder ForKey(string key, object value)
        {
            var values = new Dictionary<string, object>(nameValues);

            values.Add(key, value);

            return new ConfigReplacementBuilder(values, keyName: keyName);
        }

        public ConfigReplacementBuilder WithKeyName(string name)
        {
            return new ConfigReplacementBuilder(nameValues, keyName: name);
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
        private readonly string keyName;
        private Dictionary<string, object> nameValues;

        public ConfigReplacement(string keyName, Dictionary<string, object> nameValues)
        {
            this.keyName = keyName;
            this.nameValues = nameValues;
        }

        public string KeyName
        {
            get
            {
                return keyName;
            }
        }

        public Dictionary<string, object> NameValues
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
