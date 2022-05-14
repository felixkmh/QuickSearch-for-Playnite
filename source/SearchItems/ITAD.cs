using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using QuickSearch.SearchItems;
using System.Resources;
using System.Globalization;
using Newtonsoft.Json.Schema;
using Newtonsoft.Json;
using QuickSearch.Models.ITAD;
using Playnite.SDK;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Data;
using System.Windows.Controls.Primitives;
using System.Text.RegularExpressions;
using System.Windows.Media.Imaging;

namespace QuickSearch.SearchItems
{
    class ITADItemSource : ISearchSubItemSource<string>
    {
        public string Prefix => string.Format(ResourceProvider.GetString("LOC_QS_SearchOnAction"), "ITAD");

        public bool DisplayAllIfQueryIsEmpty => false;

        public IEnumerable<ISearchItem<string>> GetItems(string query)
        {
            return null;
        }

        readonly string baseUrl = @"https://api.isthereanydeal.com/";

        string MakeSearchQuery(string name)
        {
            return baseUrl + $"v02/search/search/?key={Properties.Resources.ITAD}&q={Uri.EscapeDataString(name)}&limit=20";
        }

        string MakePriceQuery(string plain)
        {
            var c = string.IsNullOrEmpty(country) ? string.Empty : $"&country={Uri.EscapeDataString(country)}";
            var r = string.IsNullOrEmpty(region) ? string.Empty : $"&region={Uri.EscapeDataString(region)}";
            var shops = SearchPlugin.Instance.Settings.EnabledITADShops.Where(s => s.Value.Enabled).Select(s => s.Key);
            return baseUrl + $"v01/game/prices/?key={Properties.Resources.ITAD}&plains={Uri.EscapeDataString(plain)}{r}{c}&shops={Uri.EscapeDataString(string.Join(",", shops))}";
        }

        bool GetRegion()
        {
            using (var client = new HttpClient())
            {
                try
                {
                    var response = client.GetStringAsync(@"https://api.isthereanydeal.com/v01/web/regions/").Result;
                    var json = JObject.Parse(response);
                    if (json.IsValid(RegionsResponse.Schema))
                    {
                        var regionsResponse = JsonConvert.DeserializeObject<RegionsResponse>(response);
                        var currentRegion = RegionInfo.CurrentRegion.TwoLetterISORegionName;
                        var regKeyGeoId = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Control Panel\International\Geo");
                        var geoID = (string)regKeyGeoId.GetValue("Nation");
                        var allRegions = CultureInfo.GetCultures(CultureTypes.SpecificCultures).Select(x => new RegionInfo(x.ToString()));
                        var regionInfo = allRegions.FirstOrDefault(r => r.GeoId == Int32.Parse(geoID));
                        var data = (JObject)json["data"];
                        var foundRegion = regionsResponse.Data.FirstOrDefault(i => i.Value.Countries.Contains(regionInfo.TwoLetterISORegionName));
                        if (foundRegion.Value != null || currentRegion == "150")
                        {
                            if (currentRegion == "150")
                            {
                                currencySign = "€";
                                country = null;
                                region = "eu1";
                            } else
                            {
                                currencySign = foundRegion.Value.Currency.Sign;
                                country = regionInfo.TwoLetterISORegionName;
                                region = foundRegion.Key;
                            }

                            var storesResponse = client.GetStringAsync($"https://api.isthereanydeal.com/v02/web/stores/?region={region}").Result;

                            var parsedStores = JObject.Parse(storesResponse);
                            
                            if (parsedStores.IsValid(RegionStoresResponse.Schema))
                            {
                                var stores = JsonConvert.DeserializeObject<RegionStoresResponse>(storesResponse);
                                foreach(var store in stores.Data)
                                {
                                    if (!SearchPlugin.Instance.Settings.EnabledITADShops.ContainsKey(store.ID))
                                    {
                                        SearchPlugin.Instance.Settings.EnabledITADShops.Add(store.ID, new SearchSettings.ITADShopOption(store.Title) { Enabled = defaultStores.Contains(store.ID)});
                                    }
                                }
                                var toRemove = SearchPlugin.Instance.Settings.EnabledITADShops.Keys.ToArray().Where(key => !stores.Data.Any(s => s.ID == key)).ToArray();
                                foreach (var key in toRemove)
                                {
                                    SearchPlugin.Instance.Settings.EnabledITADShops.Remove(key);
                                }
                            }

                            return true;
                        }
                    }
                } catch (Exception e)
                {
                    SearchPlugin.logger.Debug(e, "Could not retrieve regions from ITAD Api");
                }
            }
            region = string.Empty;
            country = string.Empty;
            currencySign = string.Empty;
            return false;
        }

        string region = null;
        string country = null;
        string currencySign = null;
        readonly List<string> defaultStores = new List<string>() { "steam", "battlenet", "gog", "microsoft", "origin", "squenix", "uplay", "epic"};

        public Task<IEnumerable<ISearchItem<string>>> GetItemsTask(string query, IReadOnlyList<Candidate> addedItems)
        {
            if (!SearchPlugin.Instance.Settings.ITADEnabled)
            {
                return null;
            }
            var input = query;
            bool isBelowThreshold = (addedItems.FirstOrDefault()?.Score ?? 0f) < SearchPlugin.Instance.Settings.ITADThreshold;
            bool overrideStringPresent = !string.IsNullOrWhiteSpace(SearchPlugin.Instance.Settings.ITADOverride) && query.EndsWith(SearchPlugin.Instance.Settings.ITADOverride);
            if (!isBelowThreshold && overrideStringPresent)
            {
                input = query.Substring(0, query.Length - SearchPlugin.Instance.Settings.ITADOverride.Length);
            }

            if (!isBelowThreshold && !overrideStringPresent || string.IsNullOrWhiteSpace(query))
            {
                return null;
            }
           
            var deals = new List<ISearchItem<string>>();

            return Task.Run(() => 
            {
                if (region == null)
                {
                    GetRegion();
                }
                
                using (var client = new HttpClient())
                {
                    try
                    {
                        var response = client.GetStringAsync(MakeSearchQuery(input)).Result;
                        var json = JObject.Parse(response);
                        if (json.IsValid(SearchResponse.Schema))
                        {
                            var searchResponse = JsonConvert.DeserializeObject<SearchResponse>(response);

                            if (searchResponse.Results.Data.Count > 0)
                            {
                                var games = searchResponse.Results.Data;
                                var plains = string.Join(",", searchResponse.Results.Data.Select(g => g.Plain));

                                if (games.Count() > 0)
                                {
                                    var prices = client.GetStringAsync(MakePriceQuery(plains)).Result;
                                    var pricesJson = JObject.Parse(prices);
                                    if (pricesJson.IsValid(PricesResponse.Schema))
                                    {
                                        var pricesResponse = JsonConvert.DeserializeObject<PricesResponse>(prices);

                                        foreach (var game in pricesResponse.Data.Where(g => g.Value.Prices.Count > 0))
                                        {
                                            var bestPrice = game.Value.Prices[0];
                                            var worstPrice = game.Value.Prices.Last();
                                            var priceRange = bestPrice.NewPrice.ToString("0.00") + currencySign;
                                            if (bestPrice.NewPrice != worstPrice.NewPrice)
                                            {
                                                priceRange += " - " + worstPrice.NewPrice.ToString("0.00") + currencySign;
                                            }
                                            var title = games.First(g => g.Plain == game.Key).Title;

                                            var item = new CommandItem(title, () => Process.Start(bestPrice.URL).Dispose(), bestPrice.Shop.Name, bestPrice.Shop.Name)
                                            {
                                                IconChar = IconChars.ShoppingCart,
                                                BottomLeft = priceRange,
                                                BottomRight = "IsThereAnyDeal.com API" + (string.IsNullOrEmpty(region) ? string.Empty : $" - {region.ToUpper()}{(country != null ? ", " : string.Empty)}{country?.ToUpper() ?? string.Empty}"),
                                                // TopRight = "Available in " + game.Value.Prices.Count.ToString() + " Shop" + (game.Value.Prices.Count != 1 ? "s" : string.Empty),
                                                Keys = new List<ISearchKey<string>>() { new CommandItemKey() { Key = title, Weight = 1 } }
                                            };


                                            for (int i = 1; i < game.Value.Prices.Count; ++i)
                                            {
                                                PricesResponse.PricesItem pricesItem = game.Value.Prices[i];
                                                item.Actions.Add(new CommandAction { Name = pricesItem.Shop.Name, Action = () => Process.Start(pricesItem.URL) });
                                            }

                                            Application.Current.Dispatcher.Invoke(() => {
                                                var stackPanel = new StackPanel();

                                                if (game.Value.Prices.FirstOrDefault(p => p.Shop.Name == "Steam") is PricesResponse.PricesItem steamItem)
                                                {
                                                    var appIdRegex = new Regex(@"(?<=app\/)(\w*)(?=\/)");
                                                    if (appIdRegex.IsMatch(steamItem.URL))
                                                    {
                                                        var appId = appIdRegex.Match(steamItem.URL).Value;
                                                        var logoUrl = string.Format("https://steamcdn-a.akamaihd.net/steam/apps/{0}/logo.png", appId);
                                                        if (Uri.TryCreate(logoUrl, UriKind.RelativeOrAbsolute, out var uri))
                                                        {
                                                            var bitmap = new BitmapImage();
                                                            bitmap.BeginInit();
                                                            bitmap.UriSource = uri;
                                                            bitmap.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
                                                            bitmap.CacheOption = BitmapCacheOption.OnDemand;
                                                            bitmap.EndInit();
                                                            var logo = new Image
                                                            {
                                                                Source = bitmap,
                                                                MaxHeight = 100,
                                                                Stretch = Stretch.Uniform, StretchDirection = StretchDirection.Both,
                                                                Margin = new Thickness(0, 0, 0, 10)
                                                            };
                                                            stackPanel.Children.Add(logo);
                                                        }
                                                    }
                                                }

                                                var dataGrid = new DataGrid()
                                                {
                                                    ItemsSource = game.Value.Prices.Select(deal => new { Shop = deal.Shop.Name, Price = $"{deal.NewPrice:0.00}{currencySign}" }),
                                                    AutoGenerateColumns = false,
                                                    HeadersVisibility = DataGridHeadersVisibility.None,
                                                    CanUserAddRows = false,
                                                    CanUserDeleteRows = false,
                                                    CanUserReorderColumns = false,
                                                    CanUserResizeRows = false,
                                                    CanUserResizeColumns = false,
                                                    CanUserSortColumns = false,
                                                    Background = null,
                                                    SelectionMode = DataGridSelectionMode.Single,
                                                    IsHitTestVisible = false,
                                                    BorderBrush = null,
                                                    HorizontalGridLinesBrush = null,
                                                    VerticalGridLinesBrush = null
                                                };

                                                dataGrid.Columns.Add(new DataGridTextColumn { Binding = new Binding("Shop"), Header = "Shop", IsReadOnly = true });
                                                dataGrid.Columns.Add(new DataGridTextColumn { Binding = new Binding("Price"), Header = "Price", IsReadOnly = true });
                                                stackPanel.Children.Add(new TextBlock
                                                {
                                                    Text = $"{title} Deals:",
                                                    Foreground = ResourceProvider.GetResource<Brush>("TextBrush") ?? Brushes.White,
                                                    Margin = new Thickness { Bottom = 10, Left = 10, Right = 10},
                                                    HorizontalAlignment = HorizontalAlignment.Center,
                                                    TextWrapping = TextWrapping.Wrap
                                                });
                                                stackPanel.Children.Add(dataGrid);
                                                item.DetailsView = stackPanel;
                                            });


                                            item.Actions.Add(new CommandAction { Name = "Go to ITAD", Action = () => Process.Start(game.Value.URLs["game"]) });

                                            deals.Add(item);
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception e) { SearchPlugin.logger.Debug(e, "Failed request to ITAD"); }
                }
                return deals.AsEnumerable();
            });
        }

        public IEnumerable<ISearchItem<string>> GetItems()
        {
            return null;
        }
    }

    

}
