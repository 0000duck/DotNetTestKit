using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;

namespace CassiniDev.Configuration
{

    public class ConfigRewriter
    {
        private readonly ConfigSources sources;

        public ConfigRewriter(ConfigSources sources)
        {
            this.sources = sources;
        }

        public string Rewrite(string source, ConfigReplacements replacements)
        {
            var doc = ReadDocument(source);
            var root = doc.DocumentElement;

            foreach (var path in replacements.Paths)
            {
                var container = root.SelectSingleNode(string.Format("/configuration/{0}", path.Path));
                var configSource = container.Attributes["configSource"];

                if (configSource != null)
                {
                    container = ImportConfigSource(doc, container, configSource);
                }

                RewriteElements(container.SelectNodes("add"), path);
            }

            var writer = new StringWriter();

            doc.Save(writer);

            return writer.ToString();
        }

        private void RewriteElements(XmlNodeList nodes, ConfigElementPath path)
        {
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

        private XmlNode ImportConfigSource(XmlDocument doc, XmlNode container, XmlAttribute configSource)
        {
            var inclusionSource = sources.ResolveSource(configSource.Value);
            var xmlDoc = ReadDocument(inclusionSource);

            var importedContainer = doc.ImportNode(xmlDoc.DocumentElement, true);

            container.ParentNode.ReplaceChild(importedContainer, container);

            return importedContainer;
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
            }
            else if (valueForRewrite is object)
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

                attribute.Value = (string)property.GetValue(valueForRewrite, new object[0]);
            }
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

        public ConfigReplacementsBuilder() : this(new List<ConfigElementPath>())
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

        public ConfigReplacementBuilder() : this(new Dictionary<string, object>())
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
        private readonly ReadOnlyCollection<ConfigElementPath> paths;

        internal ConfigReplacements(List<ConfigElementPath> paths)
        {
            this.paths = paths.AsReadOnly();
        }

        public ReadOnlyCollection<ConfigElementPath> Paths
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