using Playnite.SDK;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LibGit2Sharp;
using System.IO;
using System.Diagnostics;
using QuickSearch.Views;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Runtime.CompilerServices;
using YamlDotNet.Serialization;
using Playnite.SDK.Models;
using Playnite.SDK.Data;

[assembly: InternalsVisibleTo("QuickSearch")]
namespace QuickSearch.SearchItems
{
    public class AddonBrowser : ISearchSubItemSource<string>
    {
        public string Prefix => ResourceProvider.GetString("LOC_QS_Addons");

        public bool DisplayAllIfQueryIsEmpty => true;

        public IEnumerable<ISearchItem<string>> GetItems()
        {
            try
            {
                var dataPath = Path.Combine(SearchPlugin.Instance.GetPluginUserDataPath());
                var repoPath = Path.Combine(dataPath, "PlayniteAddonDatabase");
                if (!Directory.Exists(repoPath))
                {
                    LibGit2Sharp.Repository.Clone("https://github.com/JosefNemec/PlayniteAddonDatabase.git", repoPath);
                }
                else
                {
                    var repo = new LibGit2Sharp.Repository(repoPath);
                    if (repo is LibGit2Sharp.Repository)
                    {
                        Commands.Pull(
                            repo,
                            new LibGit2Sharp.Signature(new Identity("felixkmh", "24227002+felixkmh@users.noreply.github.com"), DateTimeOffset.Now),
                            new PullOptions()
                        );
                    }
                }
                if (Directory.Exists(repoPath))
                {
                    var manifestFiles = System.IO.Directory.GetFiles(repoPath, "*.yaml", SearchOption.AllDirectories);
                    var addonManifests = manifestFiles.AsParallel().Select(file =>
                    {
                        using (var yaml = File.OpenText(file))
                        {
                            IDeserializer deserializer = new DeserializerBuilder()
                                .IgnoreUnmatchedProperties()
                                .Build();
                            var manifest = deserializer.Deserialize<AddonManifestBase>(yaml);
                            return manifest;
                        }
                    }).OfType<AddonManifestBase>();
                    return addonManifests.OrderBy(addon => addon.Type).ThenBy(addon => addon.Name).ThenBy(addon => addon.Author).Select(addon => new AddonItem(addon));
                }
            }
            catch (Exception ex)
            {
                SearchPlugin.logger.Error(ex, "Could not Update Addon Database");
            }
            return null;
        }

        public IEnumerable<ISearchItem<string>> GetItems(string query)
        {
            return null;
        }

        public Task<IEnumerable<ISearchItem<string>>> GetItemsTask(string query, IReadOnlyList<Candidate> addedItems)
        {
            return null;
        }
    }

    public class AddonItem : ISearchItem<string>
    {
        AddonManifestBase addon;
        static AddonDetailsView detailsView = null;

        private string AddonTypeToString(AddonType type)
        {
            switch (type)
            {
                case AddonType.GameLibrary:
                    return ResourceProvider.GetString("LOC_QS_LibraryIntegration");
                case AddonType.MetadataProvider:
                    return ResourceProvider.GetString("LOC_QS_MetadataProvider");
                case AddonType.Generic:
                    return ResourceProvider.GetString("LOC_QS_GenericExtension");
                case AddonType.ThemeDesktop:
                    return ResourceProvider.GetString("LOC_QS_DesktopTheme");
                case AddonType.ThemeFullscreen:
                    return ResourceProvider.GetString("LOC_QS_FullscreenTheme");
                default:
                    return null;
            }
        }

        public AddonItem(AddonManifestBase addon)
        {
            var api = SearchPlugin.Instance.PlayniteApi;
            var isInstalled = api.Addons.Addons.Contains(addon.AddonId);
            var isDisabled = isInstalled && api.Addons.DisabledAddons.Contains(addon.AddonId);
            addon.IsEnabled = !isDisabled;
            if (addon.Type == AddonType.ThemeDesktop || addon.Type == AddonType.ThemeFullscreen)
            {
                var typeFolder = addon.Type == AddonType.ThemeDesktop ? "Desktop" : "Fullscreen";
                var themePath = Path.Combine(api.Paths.ConfigurationPath, "Themes", typeFolder, addon.AddonId);
                if (Directory.Exists(themePath))
                {
                    isInstalled = File.Exists(Path.Combine(themePath, "theme.yaml"));
                }
            }
            addon.IsInstalled = isInstalled;
            this.addon = addon;
            Keys = new List<ISearchKey<string>> {
                new CommandItemKey() { Key = addon.Name ?? "Add-on Name", Weight = 1 },
                new CommandItemKey() { Key = addon.Author ?? "Unknown Author", Weight = 1 },
            };
            if (addon.Tags is List<string> tags && tags.Count > 0)
            {
                var tagString = string.Join(" ", tags);
                if (!string.IsNullOrWhiteSpace(tagString))
                {
                    Keys.Add(new CommandItemKey() { Key = tagString, Weight = 0.95f });
                }
            }

            if (!string.IsNullOrWhiteSpace(addon.ShortDescription))
            {
                Keys.Add(new CommandItemKey() { Key = addon.ShortDescription, Weight = 0.95f });
            }

            if (!string.IsNullOrWhiteSpace(addon.Description))
            {
                Keys.Add(new CommandItemKey() { Key = addon.Description, Weight = 0.95f });
            }
            if (isInstalled)
            {
                if (Uri.TryCreate(addon.IconUrl, UriKind.RelativeOrAbsolute, out var uri))
                {
                    Icon = uri;
                }
            }
            if (Icon == null)
            {
                if (Uri.TryCreate(addon.IconUrl, UriKind.RelativeOrAbsolute, out var uri))
                {
                    Icon = uri;
                } else
                {
                    Icon = (System.Windows.Application.Current.FindResource("TrayIcon") as BitmapImage).UriSource;
                }
            }
        }

        public IList<ISearchKey<string>> Keys { get; } = null;

        public IList<ISearchAction<string>> Actions => GetActions(addon);

        public ScoreMode ScoreMode => ScoreMode.WeightedMaxScore;

        public Uri Icon { get; } = null;

        public string TopLeft => addon.Name;

        public string TopRight => string.Format(ResourceProvider.GetString("LOC_QS_AddonByDev"), AddonTypeToString(addon.Type), addon.Author);

        public string BottomLeft => addon.ShortDescription;

        public string BottomCenter => null;

        public string BottomRight => null;

        public char? IconChar => addon.IsEnabled ? (addon.IsInstalled ? '' : default) : '';

        public FrameworkElement DetailsView
        {
            get
            {
                if (detailsView == null)
                {
                    detailsView = new AddonDetailsView() { DataContext = null };
                }
                detailsView.DataContext = addon;
                return detailsView;
            }
        }

        static private IList<ISearchAction<string>> GetActions(AddonManifestBase addon)
        {
            var actions = new List<ISearchAction<string>> { new CommandAction() { Action = () => { var p = Process.Start($"playnite://playnite/installaddon/{addon.AddonId}"); p?.Close(); p?.Dispose(); }, Name = addon.IsInstalled ? ResourceProvider.GetString("LOC_QS_UpdateAction") : ResourceProvider.GetString("LOC_QS_InstallAction") } };

            if (addon.IsInstalled)
            {
                var api = SearchPlugin.Instance.PlayniteApi;
                var installationBasePath = api.Paths.ConfigurationPath;
                var installationPath = Path.Combine(installationBasePath, "Extensions", addon.AddonId);
                if (Directory.Exists(installationPath))
                {
                    var extensionManifestPath = Path.Combine(installationPath, "extension.yaml");
                    actions.Add(new CommandAction() { Action = () => { var p = Process.Start(installationPath); p?.Close(); p?.Dispose(); }, Name = ResourceProvider.GetString("LOC_QS_InstallationData") });
                    if (File.Exists(extensionManifestPath))
                    {
                        try
                        {
                            if (ExtensionManifest.FromFile(extensionManifestPath) is ExtensionManifest manifest)
                            {
                                var dataBasePath = api.Paths.ExtensionsDataPath;
                                var plugin = api.Addons.Plugins
                                    .FirstOrDefault(p => (p.GetType().Assembly.GetName().Name + ".dll").Equals(manifest.Module, StringComparison.OrdinalIgnoreCase));
                                if (plugin != null)
                                {
                                    actions.Insert(1, new CommandAction() { Action = () => { var p = Process.Start(plugin.GetPluginUserDataPath()); p?.Close(); p?.Dispose(); }, Name = ResourceProvider.GetString("LOC_QS_UserData") });
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            SearchPlugin.logger.Error(ex, $"Couldn't parse extension.yaml file at {extensionManifestPath}");
                        }
                    }
                }
                //if (addon.Type == AddonType.Generic || addon.Type == AddonType.GameLibrary)
                //{
                //    if (addon.IsEnabled)
                //    {
                //        actions.Add(new CommandAction { Action = () => { api.Addons.DisabledAddons.AddMissing(addon.AddonId); addon.IsEnabled = false; }, CloseAfterExecute = false, Name = "Disable" });
                //    } else
                //    {
                //        actions.Add(new CommandAction { Action = () => { api.Addons.DisabledAddons.Remove(addon.AddonId); addon.IsEnabled = true; }, CloseAfterExecute = false, Name = "Enable" });
                //    }
                //}
            }

            return actions;
        }
    }

    // see https://github.com/JosefNemec/Playnite/blob/master/source/Playnite/Manifests/AddonManifests.cs for following types
    public enum AddonType
    {
        GameLibrary,
        MetadataProvider,
        Generic,
        ThemeDesktop,
        ThemeFullscreen
    }

    public class AddonManifestBase : ObservableObject
    {
        static Octokit.GitHubClient GetGithubClient()
        {
            var client = new Octokit.GitHubClient(new Octokit.ProductHeaderValue("QuickSearch-for-Playnite"));
            return client;
        }
        internal static Octokit.GitHubClient gitHub = GetGithubClient();

        private DownloadStats downloadStats = null;

        private string GetDownloadString()
        {
            var stats = downloadStats ?? GetDownloadCount();
            downloadStats = stats;
            if ((downloadStats?.total ?? -1) < 0)
            {
                return null;
            } else
            {
                var sb = new StringBuilder();
                sb.Append("Total Downloads: ");
                if (stats.total >= 1_000_000_000)
                    sb.Append((stats.total / 1_000_000_000.0).ToString("#,##0.###") + "G");
                else if (stats.total >= 1_000_000)
                    sb.Append((stats.total / 1_000_000.0).ToString("#,##0.##") + "M");
                else if (stats.total >= 10_000)
                    sb.Append((stats.total / 1_000.0).ToString("#,##0.#") + "K");
                else
                    sb.Append(stats.total.ToString("#,##0.#"));

                sb.Append(", Latest: ");
                if (stats.latest >= 1_000_000_000)
                    sb.Append((stats.latest / 1_000_000_000.0).ToString("#,##0.###") + "G");
                else if (stats.latest >= 1_000_000)
                    sb.Append((stats.latest / 1_000_000.0).ToString("#,##0.##") + "M");
                else if (stats.latest >= 10_000)
                    sb.Append((stats.latest / 1_000.0).ToString("#,##0.#") + "K");
                else
                    sb.Append(stats.latest.ToString("#,##0.#"));
                return sb.ToString();
            }
        }

        class DownloadStats
        {
            public Int64 total = 0;
            public Int64 latest = 0;
        }

        private DownloadStats GetDownloadCount()
        {
            var stats = new DownloadStats();
            var source = SourceUrl;
            if (source.StartsWith("https://github.com/", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    if (gitHub.GetLastApiInfo() is Octokit.ApiInfo info)
                    {
                        if (info.RateLimit.Remaining <= 0)
                        {
                            var limits = gitHub.Miscellaneous.GetRateLimits().Result;
                            return null;
                        }
                    }
                    var split = source.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                    var userName = split[2];
                    var repoName = split[3];
                    var response = gitHub.Repository.Get(userName, repoName);
                    var installerManifest = InstallerManifest;
                    response.Wait();
                    if (response.IsCompleted && response.Result is Octokit.Repository repo && installerManifest is AddonInstallerManifest)
                    {
                        var releaseResponse = gitHub.Repository.Release.GetAll(repo.Id);
                        releaseResponse.Wait();
                        if (releaseResponse.Result is IReadOnlyList<Octokit.Release> releases)
                        {
                            if (releases.Count == 0) return null;
                            if (installerManifest.Packages.Count > 0)
                            {
                                var latest = InstallerManifest.Packages.FirstOrDefault(p => installerManifest.Packages.All(o => o.Version <= p.Version));
                                if (latest.PackageUrl.StartsWith($"https://github.com/{userName}/{repoName}/releases/", StringComparison.OrdinalIgnoreCase))
                                {
                                    var splitUrl = latest.PackageUrl.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                                    var tagName = splitUrl[6];
                                    var fileName = splitUrl[7];
                                    if (releases.FirstOrDefault(r => r.TagName == tagName) is Octokit.Release release &&
                                        release.Assets.FirstOrDefault(a => a.Name == fileName) is Octokit.ReleaseAsset asset)
                                    {
                                        stats.latest = asset.DownloadCount;
                                    }
                                }
                            }
                            foreach (var package in installerManifest.Packages)
                            {
                                if (package.PackageUrl.StartsWith($"https://github.com/{userName}/{repoName}/releases/", StringComparison.OrdinalIgnoreCase))
                                {
                                    var splitUrl = package.PackageUrl.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                                    var tagName = splitUrl[6];
                                    var fileName = splitUrl[7];
                                    if (releases.FirstOrDefault(r => r.TagName == tagName) is Octokit.Release release &&
                                        release.Assets.FirstOrDefault(a => a.Name == fileName) is Octokit.ReleaseAsset asset)
                                    {
                                        stats.total += asset.DownloadCount;
                                    }
                                }
                            }
                        }
                        return stats;
                    }
                }
                catch (Exception ex)
                {
                    SearchPlugin.logger.Debug(ex, $"Couldn't retrieve download count for {Name}");
                }
            }
            return null;
        }

        public class AddonUserAgreement
        {
            public DateTime Updated { get; set; }
            public string AgreementUrl { get; set; }
        }

        public class AddonScreenshot
        {
            public string Thumbnail { get; set; }
            public string Image { get; set; }
        }

        [DontSerialize]
        public string DownloadString => GetDownloadString();

        public string IconUrl { get; set; }
        public List<AddonScreenshot> Screenshots { get; set; }
        public AddonType Type { get; set; }
        public string InstallerManifestUrl { get; set; }
        public string ShortDescription { get; set; }
        public string Description { get; set; }
        public string Name { get; set; }
        public string AddonId { get; set; }
        public string Author { get; set; }
        public Dictionary<string, string> Links { get; set; }
        public List<string> Tags { get; set; }
        public AddonUserAgreement UserAgreement { get; set; }
        public string SourceUrl { get; set; }
        [Newtonsoft.Json.JsonIgnore]
        [YamlDotNet.Serialization.YamlIgnore]
        public bool IsInstalled { get; set; } = false;
        [Newtonsoft.Json.JsonIgnore]
        [YamlDotNet.Serialization.YamlIgnore]
        public bool IsEnabled { get; set; } = false;
        [YamlDotNet.Serialization.YamlIgnore]
        public string InstallationPath { get; } = null;
        [YamlDotNet.Serialization.YamlIgnore]
        public string ExtensionManifest { get; } = null;
        [YamlDotNet.Serialization.YamlIgnore]
        private AddonInstallerManifest installerManifest = null;
        [YamlDotNet.Serialization.YamlIgnore]
        public AddonInstallerManifest InstallerManifest
        {
            get
            {
                if (installerManifest == null)
                {
                    try
                    {
                        using (var client = new System.Net.Http.HttpClient())
                        {
                            using (var response = client.GetStringAsync(InstallerManifestUrl))
                            {
                                response.Wait();
                                if (!response.IsFaulted)
                                {
                                    var yaml = response.Result;
                                    var deserializer = new YamlDotNet.Serialization.Deserializer();
                                    var manifest = deserializer.Deserialize<AddonInstallerManifest>(yaml);
                                    installerManifest = manifest;
                                    installerManifest.Packages = installerManifest.Packages.OrderByDescending(p => p.Version).ToList();
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        SearchPlugin.logger.Error(ex, $"Failed to download InstallerManifest for \"{Name}\"");
                    }
                }
                return installerManifest;
            }
        }
    }

    public class AddonInstallerManifest
    {
        public string AddonId { get; set; }
        public List<AddonInstallerPackage> Packages { get; set; }
        public AddonType AddonType { get; set; }

        //public AddonInstallerPackage GetLatestCompatiblePackage()
        //{
        //    if (!Packages.HasItems())
        //    {
        //        return null;
        //    }

        //    var apiVersion = GetApiVersion(AddonType);
        //    return GetLatestCompatiblePackage(apiVersion);
        //}

        public AddonInstallerPackage GetLatestCompatiblePackage(System.Version apiVersion)
        {
            if (!Packages.HasItems())
            {
                return null;
            }

            return Packages.
                Where(a => a.RequiredApiVersion.Major == apiVersion.Major && a.RequiredApiVersion <= apiVersion).
                OrderByDescending(a => a.Version).FirstOrDefault();
        }

        //private static Version GetApiVersion(AddonType type)
        //{
        //    switch (type)
        //    {
        //        case AddonType.GameLibrary:
        //        case AddonType.MetadataProvider:
        //        case AddonType.Generic:
        //            return SdkVersions.SDKVersion;
        //        case AddonType.ThemeDesktop:
        //            return ThemeManager.DesktopApiVersion;
        //        case AddonType.ThemeFullscreen:
        //            return ThemeManager.FullscreenApiVersion;
        //    }

        //    return new Version(999, 0);
        //}
    }

    public class AddonInstallerPackage
    {
        public System.Version Version { get; set; }
        public string PackageUrl { get; set; }
        public System.Version RequiredApiVersion { get; set; }
        public DateTime ReleaseDate { get; set; }
        public List<string> Changelog { get; set; }
    }

    public enum ExtensionType
    {
        GenericPlugin,
        GameLibrary,
        Script,
        MetadataProvider
    }

    public class BaseExtensionManifest
    {
        public string Id { get; set; }

        public string Name { get; set; }

        public string Author { get; set; }

        public string Version { get; set; }

        public List<Link> Links { get; set; }

        [YamlIgnore]
        public string DirectoryPath { get; set; }

        [YamlIgnore]
        public string DirectoryName { get; set; }

        [YamlIgnore]
        public string DescriptionPath { get; set; }

        public void VerifyManifest()
        {
            if (!System.Version.TryParse(Version, out var extver))
            {
                throw new Exception("Extension version string must be a real version!");
            }
        }
    }

    public class ExtensionManifest : BaseExtensionManifest
    {
        [YamlIgnore]
        public bool IsExternalDev { get; set; }

        //[YamlIgnore]
        //public bool IsCompatible { get; } = false;

        public string Module { get; set; }

        public string Icon { get; set; }

        public ExtensionType Type { get; set; }

        public ExtensionManifest()
        {
        }

        public static ExtensionManifest FromFile(string descriptorPath)
        {
            var deserializer = new DeserializerBuilder().IgnoreUnmatchedProperties().Build();
            var description = deserializer.Deserialize<ExtensionManifest>(File.ReadAllText(descriptorPath));
            description.DescriptionPath = descriptorPath;
            description.DirectoryPath = Path.GetDirectoryName(descriptorPath);
            description.DirectoryName = Path.GetFileNameWithoutExtension(description.DirectoryPath);
            return description;
        }
    }
}
