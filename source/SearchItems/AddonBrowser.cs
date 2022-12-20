using LibGit2Sharp;
using Playnite.SDK;
using Playnite.SDK.Data;
using Playnite.SDK.Models;
using QuickSearch.Views;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using YamlDotNet.Serialization;

[assembly: InternalsVisibleTo("QuickSearch")]

namespace QuickSearch.SearchItems
{
    // see https://github.com/JosefNemec/Playnite/blob/master/source/Playnite/Manifests/AddonManifests.cs for following types
    public enum AddonType
    {
        GameLibrary,
        MetadataProvider,
        Generic,
        ThemeDesktop,
        ThemeFullscreen
    }

    public enum ExtensionType
    {
        GenericPlugin,
        GameLibrary,
        Script,
        MetadataProvider
    }

    public class AddonBrowser : ISearchSubItemSource<string>
    {
        private Lazy<List<AddonItem>> cachedItems = null;

        public AddonBrowser()
        {
            cachedItems = new Lazy<List<AddonItem>>(InitAddonItems);
        }

        public string DataPath => Path.Combine(SearchPlugin.Instance.GetPluginUserDataPath());
        public bool DisplayAllIfQueryIsEmpty => true;
        public string Prefix => ResourceProvider.GetString("LOC_QS_Addons");
        public string RepoPath => Path.Combine(DataPath, "PlayniteAddonDatabase");

        public IEnumerable<ISearchItem<string>> GetItems()
        {
            if (cachedItems.IsValueCreated)
            {
                if (UpdateRepository() is RepoUpdate repoUpdate)
                {
                    //SearchPlugin.Instance.PlayniteApi.Dialogs
                    //    .ShowMessage($"Removed:\n{string.Join("\n", repoUpdate.OldPaths)}\nAdded:\n{string.Join("\n", repoUpdate.NewPaths)}", "AddonDatabase-Updates");
                    try
                    {
                        Comparison<AddonItem> comparison = (a, b) =>
                                                {
                                                    var byType = a.addon.Type.CompareTo(b.addon.Type);
                                                    if (byType != 0) return byType;
                                                    var byName = a.addon.Name.CompareTo(b.addon.Name);
                                                    if (byName != 0) return byName;
                                                    return a.addon.Author.CompareTo(b.addon.Author);
                                                };
                        cachedItems.Value.RemoveAll(item => repoUpdate.OldPaths.Contains(item.addon.LocalPath));
                        var toAdd = DeserializeManifests(repoUpdate.NewPaths).ToList();
                        cachedItems.Value.InsertRangeSorted(toAdd, comparison);
                    }
                    catch (Exception)
                    {
                        //SearchPlugin.Instance.PlayniteApi.Dialogs
                        //    .ShowMessage($"{ex.Message}\n{ex.StackTrace}", "AddonDatabase-Update Errors");
                    }
                }
                cachedItems.Value.AsParallel().ForEach(item => item.addon.Reset());
            }
            return cachedItems.Value;
        }

        public IEnumerable<ISearchItem<string>> GetItems(string query)
        {
            return null;
        }

        public Task<IEnumerable<ISearchItem<string>>> GetItemsTask(string query, IReadOnlyList<Candidate> addedItems)
        {
            return null;
        }

        private List<AddonItem> InitAddonItems()
        {
            try
            {
                if (!Directory.Exists(RepoPath))
                {
                    LibGit2Sharp.Repository.Clone("https://github.com/JosefNemec/PlayniteAddonDatabase.git", RepoPath);
                }
                else
                {
                    UpdateRepository();
                }
                if (Directory.Exists(RepoPath))
                {
                    var addonDir = Path.Combine(RepoPath, "addons");
                    var manifestFiles = System.IO.Directory.GetFiles(addonDir, "*.yaml", SearchOption.AllDirectories);
                    var items = DeserializeManifests(manifestFiles);
                    return items.ToList();
                }
            }
            catch (Exception ex)
            {
                SearchPlugin.logger.Error(ex, "Could not Update Addon Database");
            }
            return null;
        }

        private static IEnumerable<AddonItem> DeserializeManifests(IEnumerable<string> manifestFiles)
        {
            var addonManifests = manifestFiles.AsParallel().Select(file =>
            {
                using (var yaml = File.OpenText(file))
                {
                    IDeserializer deserializer = new DeserializerBuilder()
                        .IgnoreUnmatchedProperties()
                        .Build();
                    var manifest = deserializer.Deserialize<AddonManifestBase>(yaml);
                    if (manifest != null) manifest.LocalPath = file;
                    return manifest;
                }
            }).OfType<AddonManifestBase>();
            var items = addonManifests
                .OrderBy(addon => addon.Type)
                .ThenBy(addon => addon.Name)
                .ThenBy(addon => addon.Author)
                .Select(addon => new AddonItem(addon));
            return items;
        }

        private class RepoUpdate
        {
            public HashSet<string> OldPaths { get; } = new HashSet<string>();
            public HashSet<string> NewPaths { get; } = new HashSet<string>();
        }

        private RepoUpdate UpdateRepository()
        {
            using (var repo = new LibGit2Sharp.Repository(RepoPath))
            {
                if (repo is LibGit2Sharp.Repository)
                {
                    repo.Reset(ResetMode.Hard);
                    var currentCommit = repo.Head.Tip;
                    var mergeResult = Commands.Pull(
                        repo,
                        new LibGit2Sharp.Signature(new Identity("felixkmh", "24227002+felixkmh@users.noreply.github.com"), DateTimeOffset.Now),
                        new PullOptions() { MergeOptions = new MergeOptions { MergeFileFavor = MergeFileFavor.Theirs } }
                    );
                    if (mergeResult.Status != MergeStatus.UpToDate)
                    {
                        var diff = repo.Diff.Compare<TreeChanges>(currentCommit.Tree, mergeResult.Commit.Tree);
                        var repoUpdate = new RepoUpdate();
                        foreach (var change in diff)
                        {
                            if (change.OldExists && change.OldPath.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase))
                            {
                                repoUpdate.OldPaths.Add(Path.Combine(RepoPath, change.OldPath.Replace("/","\\")));
                            }
                            if (change.Exists && change.Path.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase))
                            {
                                repoUpdate.NewPaths.Add(Path.Combine(RepoPath, change.Path.Replace("/", "\\")));
                            }
                        }
                        return repoUpdate;
                    }
                }
            }
            return null;
        }
    }

    public class AddonInstallerManifest
    {
        public string AddonId { get; set; }
        public AddonType AddonType { get; set; }
        public List<AddonInstallerPackage> Packages { get; set; }
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
        public List<string> Changelog { get; set; }
        public string PackageUrl { get; set; }
        public DateTime ReleaseDate { get; set; }
        public System.Version RequiredApiVersion { get; set; }
        public System.Version Version { get; set; }
    }

    public sealed class AddonItem : ISearchItem<string>
    {
        internal AddonManifestBase addon;
        internal static AddonDetailsView detailsView = null;

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
                }
                else
                {
                    Icon = (System.Windows.Application.Current.FindResource("TrayIcon") as BitmapImage).UriSource;
                }
            }
        }

        public IList<ISearchAction<string>> Actions => GetActions(addon);

        public string BottomCenter => null;

        public string BottomLeft => addon.ShortDescription;

        public string BottomRight => null;

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

        public Uri Icon { get; } = null;

        public char? IconChar => addon.IsEnabled ? (addon.IsInstalled ? '' : default) : '';

        public IList<ISearchKey<string>> Keys { get; } = null;

        public ScoreMode ScoreMode => ScoreMode.WeightedMaxScore;

        public string TopLeft => addon.Name;

        public string TopRight => string.Format(ResourceProvider.GetString("LOC_QS_AddonByDev"), AddonTypeToString(addon.Type), addon.Author);

        private static IList<ISearchAction<string>> GetActions(AddonManifestBase addon)
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
    }

    public class AddonManifestBase : ObservableObject
    {
        internal static Octokit.GitHubClient gitHub = GetGithubClient();

        private DownloadStats downloadStats = null;

        [YamlDotNet.Serialization.YamlIgnore]
        private AddonInstallerManifest installerManifest = null;

        public string AddonId { get; set; }

        public string Author { get; set; }

        public string Description { get; set; }

        [YamlDotNet.Serialization.YamlIgnore]
        public string DownloadString => GetDownloadString();

        [YamlDotNet.Serialization.YamlIgnore]
        public string LocalPath { get; set; }

        [YamlDotNet.Serialization.YamlIgnore]
        public string ExtensionManifest { get; } = null;

        public string IconUrl { get; set; }

        [YamlDotNet.Serialization.YamlIgnore]
        public string InstallationPath { get; } = null;

        [YamlDotNet.Serialization.YamlIgnore]
        public AddonInstallerManifest InstallerManifest
        {
            get
            {
                if (installerManifest == null)
                {
                    try
                    {
                        var deserializer = new DeserializerBuilder()
                            .IgnoreUnmatchedProperties()
                            .Build();
                        using (var client = new System.Net.Http.HttpClient())
                        {
                            using (var response = client.GetStringAsync(InstallerManifestUrl))
                            {
                                response.Wait();
                                if (!response.IsFaulted)
                                {
                                    var yaml = response.Result;
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

        public string InstallerManifestUrl { get; set; }

        [Newtonsoft.Json.JsonIgnore]
        [YamlDotNet.Serialization.YamlIgnore]
        public bool IsEnabled { get; set; } = false;

        [Newtonsoft.Json.JsonIgnore]
        [YamlDotNet.Serialization.YamlIgnore]
        public bool IsInstalled { get; set; } = false;

        public Dictionary<string, string> Links { get; set; }

        public string Name { get; set; }

        public List<AddonScreenshot> Screenshots { get; set; }

        public string ShortDescription { get; set; }

        public string SourceUrl { get; set; }

        public List<string> Tags { get; set; }

        public AddonType Type { get; set; }

        public AddonUserAgreement UserAgreement { get; set; }

        public void Reset()
        {
            downloadStats = null;
            installerManifest = null;
        }

        private static Octokit.GitHubClient GetGithubClient()
        {
            var client = new Octokit.GitHubClient(new Octokit.ProductHeaderValue("QuickSearch-for-Playnite"));
            return client;
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

        private string GetDownloadString()
        {
            var stats = downloadStats ?? GetDownloadCount();
            downloadStats = stats;
            if ((downloadStats?.total ?? -1) < 0)
            {
                return null;
            }
            else
            {
                string total;
                if (stats.total >= 1_000_000_000)
                    total = ((stats.total / 1_000_000_000.0).ToString("#,##0.###") + "G");
                else if (stats.total >= 1_000_000)
                    total = ((stats.total / 1_000_000.0).ToString("#,##0.##") + "M");
                else if (stats.total >= 10_000)
                    total = ((stats.total / 1_000.0).ToString("#,##0.#") + "K");
                else
                    total = (stats.total.ToString("#,##0.#"));
                string latest;

                if (stats.latest >= 1_000_000_000)
                    latest = ((stats.latest / 1_000_000_000.0).ToString("#,##0.###") + "G");
                else if (stats.latest >= 1_000_000)
                    latest = ((stats.latest / 1_000_000.0).ToString("#,##0.##") + "M");
                else if (stats.latest >= 10_000)
                    latest = ((stats.latest / 1_000.0).ToString("#,##0.#") + "K");
                else
                    latest = (stats.latest.ToString("#,##0.#"));
                return string.Format(ResourceProvider.GetString("LOC_QS_DownloadStats"), total, latest);
            }
        }

        public class AddonScreenshot
        {
            public string Image { get; set; }
            public string Thumbnail { get; set; }
        }

        public class AddonUserAgreement
        {
            public string AgreementUrl { get; set; }
            public DateTime Updated { get; set; }
        }

        private class DownloadStats
        {
            public Int64 latest = 0;
            public Int64 total = 0;
        }
    }

    public class BaseExtensionManifest
    {
        public string Author { get; set; }

        [YamlIgnore]
        public string DescriptionPath { get; set; }

        [YamlIgnore]
        public string DirectoryName { get; set; }

        [YamlIgnore]
        public string DirectoryPath { get; set; }

        public string Id { get; set; }

        public List<Link> Links { get; set; }
        public string Name { get; set; }
        public string Version { get; set; }

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
        public ExtensionManifest()
        {
        }

        public string Icon { get; set; }

        [YamlIgnore]
        public bool IsExternalDev { get; set; }

        //[YamlIgnore]
        //public bool IsCompatible { get; } = false;

        public string Module { get; set; }
        public ExtensionType Type { get; set; }

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