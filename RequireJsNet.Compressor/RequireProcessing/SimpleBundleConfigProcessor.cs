// RequireJS.NET
// Copyright VeriTech.io
// http://veritech.io
// Dual licensed under the MIT and GPL licenses:
// http://www.opensource.org/licenses/mit-license.php
// http://www.gnu.org/licenses/gpl.html

using System.Collections.Generic;
using System.IO;
using System.Linq;

using RequireJsNet.Configuration;
using RequireJsNet.Helpers;
using RequireJsNet.Models;

namespace RequireJsNet.Compressor
{
    internal class SimpleBundleConfigProcessor : ConfigProcessor
    {
        public SimpleBundleConfigProcessor(string projectPath, string packagePath, string entryPointOverride, List<string> filePaths)
        {
            ProjectPath = projectPath;
            FilePaths = filePaths;
            OutputPath = projectPath;
            EntryOverride = entryPointOverride;
            if (!string.IsNullOrWhiteSpace(packagePath))
            {
                OutputPath = packagePath;
            }

            EntryPoint = this.GetEntryPointPath();
        }

        public override List<Bundle> ParseConfigs()
        {
            if (!Directory.Exists(ProjectPath))
            {
                throw new DirectoryNotFoundException("Could not find project directory.");
            }

            FindConfigs();

            var loader = new ConfigLoader(
                FilePaths,
                new ExceptionThrowingLogger(),
                new ConfigLoaderOptions { ProcessBundles = true });

            Configuration = loader.Get();

            var bundles = new List<Bundle>();
            foreach (var bundleDefinition in Configuration.Bundles.BundleEntries.Where(r => !r.IsVirtual))
            {
                var bundle = new Bundle()
                {
                    Output = GetOutputPath(bundleDefinition.OutputPath, bundleDefinition.Name),
                    Files = bundleDefinition.BundleItems
                      .Select(r => new FileSpec(this.ResolvePhysicalPath(r.RelativePath), r.CompressionType))
                      .ToList(),
                    ContainingConfig = bundleDefinition.ContainingConfig,
                    BundleId = bundleDefinition.Name
                };
                bundles.Add(bundle);
            }

            this.WriteOverrideConfigs(bundles);

            return bundles;
        }

        private void WriteOverrideConfigs(List<Bundle> bundles)
        {
            var groupings = bundles.GroupBy(r => r.ContainingConfig).ToList();
            foreach (var grouping in groupings)
            {
                var path = RequireJsNet.Helpers.PathHelpers.GetOverridePath(grouping.Key);
                var writer = WriterFactory.CreateWriter(path, null);
                var collection = this.ComposeCollection(grouping.ToList());
                writer.WriteConfig(collection);
            }
        }

        private ConfigurationCollection ComposeCollection(List<Bundle> bundles)
        {
            var conf = new ConfigurationCollection();
            conf.Overrides = new List<CollectionOverride>();
            foreach (var bundle in bundles)
            {
                var scripts = bundle.Files.Select(r => PathHelpers.GetRequireRelativePath(EntryPoint, r.FileName)).ToList();
                var paths = new RequirePaths
                {
                    PathList = new List<RequirePath>()
                };
                foreach (var script in scripts)
                {
                    paths.PathList.Add(new RequirePath
                    {
                        Key = script,
                        Value = PathHelpers.GetRequireRelativePath(EntryPoint, bundle.Output)
                    });
                }

                var over = new CollectionOverride
                {
                    BundleId = bundle.BundleId,
                    BundledScripts = scripts,
                    Paths = paths
                };
                conf.Overrides.Add(over);
            }

            return conf;
        }
    }
}
