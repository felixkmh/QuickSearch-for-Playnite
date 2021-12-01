using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;

namespace QuickSearch.SearchItems
{
    public class MetadataSource : ISearchSubItemSource<string>
    {
        internal MetadataPlugin metadataPlugin;

        public MetadataSource(MetadataPlugin plugin)
        {
            if (plugin is null) throw new ArgumentNullException(nameof(plugin));
            metadataPlugin = plugin;
        }

        public string Prefix => metadataPlugin.Name;

        public bool DisplayAllIfQueryIsEmpty => false;

        public IEnumerable<ISearchItem<string>> GetItems() => Array.Empty<ISearchItem<string>>();

        public IEnumerable<ISearchItem<string>> GetItems(string query) => Array.Empty<ISearchItem<string>>();

        public Task<IEnumerable<ISearchItem<string>>> GetItemsTask(string query, IReadOnlyList<Candidate> addedItems)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return null;
            }

            var metadataArgs = new GetMetadataFieldArgs();

            return Task.Run(() =>
            {
                var game = new Game(query);
                var gameMetadata = new GameMetadata();
                var requestOptions = new MetadataRequestOptions(game, true);

                List<ISearchItem<string>> items = new List<ISearchItem<string>>();

                using (var provider = metadataPlugin.GetMetadataProvider(requestOptions))
                {
                    var item = new CommandItem();
                    var fields = provider.AvailableFields;
                    
                    if (fields.Contains(MetadataField.Icon))
                    {
                        gameMetadata.Icon = provider.GetIcon(new GetMetadataFieldArgs());
                    }
                    if (fields.Contains(MetadataField.Platform))
                    {
                        gameMetadata.Platforms = provider.GetPlatforms(new GetMetadataFieldArgs()).ToHashSet();
                    }
                    if (fields.Contains(MetadataField.Genres))
                    {
                        gameMetadata.Genres = provider.GetGenres(new GetMetadataFieldArgs()).ToHashSet();
                    }
                    if (fields.Contains(MetadataField.Name) && provider.GetName(new GetMetadataFieldArgs()) is string name)
                    {
                        gameMetadata.Name = name;
                        var tempGame = SearchPlugin.Instance.PlayniteApi.Database.ImportGame(gameMetadata);
                        items.Add(new GameSearchItem(tempGame));
                        if (SearchPlugin.Instance.PlayniteApi.Database.Games.Remove(tempGame.Id))
                        {

                        }
                        else
                        {

                        }
                    }
                        
                }

                return items.AsEnumerable();
            }, metadataArgs.CancelToken);
        }
    }
}
