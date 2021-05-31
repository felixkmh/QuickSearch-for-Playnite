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

namespace QuickSearch.SearchItems
{
    class ITADItemSource : ISearchItemSource<string>
    {
        public bool DependsOnQuery => true;

        public IEnumerable<ISearchItem<string>> GetItems(string query)
        {
            return null;
        }

        string baseUrl = @"https://api.isthereanydeal.com/";

        string makeSearchQuery(string name)
        {
            return baseUrl + $"v02/search/search/?key={Properties.Resources.ITAD}&q={Uri.EscapeDataString(name)}&limit=20";
        }

        string makePriceQuery(string plain)
        {
            var c = string.IsNullOrEmpty(country) ? string.Empty : $"&country={Uri.EscapeDataString(country)}";
            var r = string.IsNullOrEmpty(region) ? string.Empty : $"&region={Uri.EscapeDataString(region)}";
            var shops = SearchPlugin.Instance.settings.EnabledITADShops.Where(s => s.Value.Enabled).Select(s => s.Key);
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
                                    if (!SearchPlugin.Instance.settings.EnabledITADShops.ContainsKey(store.ID))
                                    {
                                        SearchPlugin.Instance.settings.EnabledITADShops.Add(store.ID, new SearchSettings.ITADShopOption(store.Title) { Enabled = defaultStores.Contains(store.ID)});
                                    }
                                }
                                var toRemove = SearchPlugin.Instance.settings.EnabledITADShops.Keys.ToArray().Where(key => !stores.Data.Any(s => s.ID == key)).ToArray();
                                foreach (var key in toRemove)
                                {
                                    SearchPlugin.Instance.settings.EnabledITADShops.Remove(key);
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
        List<string> defaultStores = new List<string>() { "steam", "battlenet", "gog", "microsoft", "origin", "squenix", "uplay", "epic"};

        public Task<IEnumerable<ISearchItem<string>>> GetItemsTask(string query, IReadOnlyList<Candidate> addedItems)
        {
            if (!SearchPlugin.Instance.settings.ITADEnabled)
            {
                return null;
            }
            return Task.Run(() => 
            {
                if (region == null)
                {
                    GetRegion();
                }
                bool isBelowThreshold = (addedItems.FirstOrDefault()?.Score ?? 0f) < SearchPlugin.Instance.settings.ITADThreshold;
                bool overrideStringPresent = !string.IsNullOrWhiteSpace(SearchPlugin.Instance.settings.ITADOverride) && query.EndsWith(SearchPlugin.Instance.settings.ITADOverride);
                if (!isBelowThreshold && !overrideStringPresent)
                {
                    return null;
                }
                var input = query;
                var deals = new List<ISearchItem<string>>();
                if (!isBelowThreshold && overrideStringPresent)
                {
                    input = query.Substring(0, query.Length - SearchPlugin.Instance.settings.ITADOverride.Length);
                }

                using (var client = new HttpClient())
                {
                    try
                    {
                        var response = client.GetStringAsync(makeSearchQuery(input)).Result;
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
                                    var prices = client.GetStringAsync(makePriceQuery(plains)).Result;
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
                                            var item = new CommandItem(title, () => Process.Start(bestPrice.URL), bestPrice.Shop.Name, bestPrice.Shop.Name)
                                            {
                                                IconChar = IconChars.ShoppingCart,
                                                BottomLeft = priceRange,
                                                BottomRight = "IsThereAnyDeal.com API" + (string.IsNullOrEmpty(region) ? string.Empty : $" - {region.ToUpper()}{(country!=null?", ":string.Empty)}{country?.ToUpper()??string.Empty}"),
                                                // TopRight = "Available in " + game.Value.Prices.Count.ToString() + " Shop" + (game.Value.Prices.Count != 1 ? "s" : string.Empty),
                                                Keys = new List<ISearchKey<string>>() { new CommandItemKey() { Key = title, Weight = 1 } }
                                            };


                                            for (int i = 1; i < game.Value.Prices.Count; ++i)
                                            {
                                                PricesResponse.PricesItem pricesItem = game.Value.Prices[i];
                                                item.Actions.Add(new CommandAction { Name = pricesItem.Shop.Name, Action = () => Process.Start(pricesItem.URL) });
                                            }

                                            item.Actions.Add(new CommandAction { Name = "Go to ITAD", Action = () => Process.Start(game.Value.URLs["game"]) });

                                            deals.Add(item);
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception) { }
                }
                return deals.AsEnumerable();
            });
        }
    }

    

}
