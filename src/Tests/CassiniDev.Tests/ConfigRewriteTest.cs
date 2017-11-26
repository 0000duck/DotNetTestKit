using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using CassiniDev.Configuration;
using System.Text;

namespace CassiniDev.Tests
{
    [TestFixture]
    public class ConfigRewriterTest
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
        public void RewriteSimpleAppConfigWithProvidedValues()
        {
            var rewrittenConfig = ForConfigAndReplacements(@"
                <configuration>
                    <appSettings>
                        <add key=""Test"" value=""true"" />
                    </appSettings>
                </configuration>",
                (builder) => builder.ForPathWithValues("appSettings", new
                {
                    Test = "false"
                }));

            Assert.That(rewrittenConfig, Does.Contain(@"value=""false"""));
        }

        [Test]
        public void RewriteWithoutBOMSymbols()
        {
            var rewrittenConfig = ForConfigAndReplacements(@"
                <configuration>
                    <appSettings>
                        <add key=""Test"" value=""true"" />
                    </appSettings>
                </configuration>",
                (builder) => builder.ForPathWithValues("appSettings", new
                {
                    Test = "false"
                }));

            var encoding = Encoding.UTF8;

            var bytes = encoding.GetBytes(rewrittenConfig);
            var expectedFirstByte = (byte)'<';

            Assert.That(bytes[0], Is.EqualTo(expectedFirstByte));
        }

        [Test]
        public void RetainXmlDeclaration()
        {
            var rewrittenConfig = ForConfigAndReplacements(
                @"<?xml version=""1.0"" encoding=""utf-8""?>
                <configuration>
                    <appSettings>
                        <add key=""Test"" value=""true"" />
                    </appSettings>
                </configuration>",
                (builder) => builder.ForPathWithValues("appSettings", new
                {
                    Test = "false"
                }));

            Assert.That(rewrittenConfig, Does.Contain(@"encoding=""utf-8"""));
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

            return new ConfigRewriter(new ConfigSources(extraFiles)).Rewrite(configSource, withBuilder(new ConfigReplacementsBuilder()).Build());
        }
    }
}
