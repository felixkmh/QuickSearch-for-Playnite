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

namespace QuickSearch.SearchItems
{
    class CheapSharkItemSource : ISearchSubItemSource<string>
    {
        public string Prefix => string.Format(ResourceProvider.GetString("LOC_QS_SearchOnAction"), "CheapShark");

        public bool DisplayAllIfQueryIsEmpty => false;

        public IEnumerable<ISearchItem<string>> GetItems(string query)
        {
            return null;
        }

        readonly string baseUrl = @"https://www.cheapshark.com/api/1.0/";

        string MakeSearchQuery(string name)
        {
            return baseUrl + $"games?title={Uri.EscapeDataString(name)}&limit=20";
        }

        public Task<IEnumerable<ISearchItem<string>>> GetItemsTask(string query, IReadOnlyList<Candidate> addedItems)
        {
            if (!SearchPlugin.Instance.Settings.ITADEnabled)
            {
                return null;
            }
            return Task.Run(() =>
            {
                var input = query.Trim();

                if (string.IsNullOrWhiteSpace(input))
                {
                    return null;
                }

                var deals = new List<ISearchItem<string>>();

                using (var client = new HttpClient())
                {
                    try
                    {
                        var response = client.GetAsync(MakeSearchQuery(input)).Result;
                        if (response.StatusCode != System.Net.HttpStatusCode.OK)
                        {
                            return null;
                        }
                        JArray jArray = JArray.Parse(response.Content.ReadAsStringAsync().Result);
                        if (jArray.Count > 0)
                        {
                            foreach(var info in jArray)
                            {
                                var title = (string)info["external"];
                                var item = new CommandItem(title, () => Process.Start($"https://www.cheapshark.com/redirect?dealID={(string)info["cheapestDealID"]}").Dispose() , "", "Open", (string)info["thumb"])
                                {
                                    IconChar = IconChars.ShoppingCart,
                                    BottomLeft = ((float)info["cheapest"]).ToString("0.00") + " USD",
                                    BottomRight = "CheapShark.com API",
                                    Keys = new List<ISearchKey<string>>() { new CommandItemKey() { Key = title, Weight = 1 } }
                                };

                                deals.Add(item);

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
