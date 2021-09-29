using Playnite.SDK;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Octokit;
using LibGit2Sharp;
using System.IO;
using System.Diagnostics;
using QuickSearch.Views;
using System.Windows;
using System.Windows.Media.Imaging;

namespace QuickSearch.SearchItems
{
    public class AddonBrowser : ISearchSubItemSource<string>
    {
        public string Prefix => ResourceProvider.GetString("LOC_QS_AddonBrowserPrefix");

        public bool DisplayAllIfQueryIsEmpty => true;

        public IEnumerable<ISearchItem<string>> GetItems()
        {
            var dataPath = Path.Combine(SearchPlugin.Instance.GetPluginUserDataPath());
            var repoPath =Path.Combine(dataPath, "PlayniteAddonDatabase");
            if (!Directory.Exists(repoPath))
            {
                var client = new GitHubClient(new ProductHeaderValue("QuickSearch-for-Playnite"));
                var addonRepo = client.Repository.Get("JosefNemec", "PlayniteAddonDatabase").Result;
                LibGit2Sharp.Repository.Clone(addonRepo.CloneUrl, repoPath);
            }
            else
            {
                var repo = new LibGit2Sharp.Repository(repoPath);
                Commands.Pull(
                    repo,
                    new LibGit2Sharp.Signature(new Identity("felixkmh", "24227002+felixkmh@users.noreply.github.com"), DateTimeOffset.Now),
                    new PullOptions()
                );
            }
            var manifestFiles = System.IO.Directory.GetFiles(repoPath, "*.yaml", SearchOption.AllDirectories);
            var addonManifests = manifestFiles.AsParallel().Select(file =>
            {
                var deserializer = new YamlDotNet.Serialization.Deserializer();
                using (var yaml = File.OpenText(file))
                {
                    var manifest = deserializer.Deserialize<AddonManifestBase>(yaml);
                    return manifest;
                }
            }).OfType<AddonManifestBase>();
            return addonManifests.OrderBy(addon => addon.Type).ThenBy(addon => addon.Name).ThenBy(addon => addon.Author).Select(addon => new AddonItem(addon));
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
            addon.IsInstalled = isInstalled;
            this.addon = addon;
            Keys = new List<ISearchKey<string>> { 
                new CommandItemKey() { Key = addon.Name, Weight = 1 },
                new CommandItemKey() { Key = addon.Author, Weight = 1 }
            };
            if (!string.IsNullOrWhiteSpace(addon.ShortDescription))
            {
                Keys.Add(new CommandItemKey() { Key = addon.ShortDescription, Weight = 1 });
            }
            if (!string.IsNullOrWhiteSpace(addon.Description))
            {
                Keys.Add(new CommandItemKey() { Key = addon.Description, Weight = 1 });
            }
            Actions = new[] { new CommandAction() { Action = () => { Process.Start($"playnite://playnite/installaddon/{addon.AddonId}").Dispose(); }, Name = isInstalled ? ResourceProvider.GetString("LOC_QS_UpdateAction") : ResourceProvider.GetString("LOC_QS_InstallAction") } };
            if (Uri.TryCreate(addon.IconUrl, UriKind.RelativeOrAbsolute, out var uri))
            {
                Icon = uri;
            } else
            {
                Icon = (System.Windows.Application.Current.FindResource("TrayIcon") as BitmapImage).UriSource;
            }
        }

        public IList<ISearchKey<string>> Keys { get; } = null;

        public IList<ISearchAction<string>> Actions { get; } = null;

        public ScoreMode ScoreMode => ScoreMode.WeightedMaxScore;

        public Uri Icon { get; } = null;

        public string TopLeft => $"{addon.Name}";

        public string TopRight => string.Format(ResourceProvider.GetString("LOC_QS_AddonByDev"), AddonTypeToString(addon.Type), addon.Author);

        public string BottomLeft => addon.ShortDescription;

        public string BottomCenter => null;

        public string BottomRight => null;

        public char? IconChar => null;

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
}
