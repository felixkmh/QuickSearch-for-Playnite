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
            return baseUrl + $"v02/search/search/?key={Properties.Resources.ITAD}&q={Uri.EscapeDataString(name)}";
        }

        string makePriceQuery(string plain)
        {
            return baseUrl + $"v01/game/prices/?key={Properties.Resources.ITAD}&plains={Uri.EscapeDataString(plain)}&country=DE&region=eu1&shops=steam%2Cgog%2Chumble%2Cuplay%2Corigin%2Cepic";
        }

        public Task<IEnumerable<ISearchItem<string>>> GetItemsTask(string query, IReadOnlyList<Candidate> addedItems)
        {
            return Task.Run(() => 
            {
                if ((addedItems.FirstOrDefault()?.Score??0f) >= 0.85 && !query.EndsWith("+"))
                {
                    return null;
                }
                var input = query;
                var deals = new List<ISearchItem<string>>();
                if (query.EndsWith("+"))
                {
                    input = query.Substring(0, query.Length - 1);
                }

                using(var client = new HttpClient())
                {
                    try
                    {
                        var response = client.GetStringAsync(makeSearchQuery(input)).Result;
                        var json = JObject.Parse(response);
                        JArray results = (JArray)json["data"]["results"];
                        if (results.Count > 0)
                        {
                            var games = results.Select(g => new { title = (string)g["title"], plain = (string)g["plain"], id = (int)g["id"] });
                            var plains = string.Join(",", games.Select(g => g.plain));

                            if (games.Count() > 0)
                            {
                                var prices = client.GetStringAsync(makePriceQuery(plains)).Result;
                                var pricesJson = JObject.Parse(prices);
                                string currency = (string)pricesJson[".meta"]["currency"];
                                var data = (JObject)pricesJson["data"];
                                foreach(var p in games)
                                {
                                    var entries = (JArray)data[p.plain]["list"];
                                    if (entries.Count > 0)
                                    {
                                        var entry = (JObject)entries[0];
                                        var price = (float)entry["price_new"];
                                        var url = (string)entry["url"];
                                        var store = (string)entry["shop"]["name"];
                                        var itadUrl = (string)data[p.plain]["urls"]["game"];

                                        var item = new CommandItem(p.title, () => Process.Start(url), store, "Open Shop")
                                        {
                                            IconChar = IconChars.ShoppingCart,
                                            BottomLeft = price.ToString() + " " + currency,
                                            BottomRight = "IsThereAnyDeal.com API",
                                            TopRight = store,
                                            Keys = new List<ISearchKey<string>>() { new CommandItemKey() { Key = p.title, Weight = 1 } }
                                        };

                                        item.Actions.Add(new CommandAction { Name = "Go to ITAD", Action = () => Process.Start(itadUrl) });

                                        deals.Add(item);
                                    }
                                }
                            }

                        }
                    } catch (Exception) {}

                }

                return deals.AsEnumerable();
            });
        }
    }

    

}
