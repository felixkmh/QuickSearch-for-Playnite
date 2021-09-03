using Newtonsoft.Json;
using Newtonsoft.Json.Schema;
using Newtonsoft.Json.Schema.Generation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuickSearch.Models.ITAD
{
    public class RegionsResponse : SchemaHelper<RegionsResponse>
    {
        public class RegionsResult
        {
            [JsonProperty("countries")]
            public List<string> Countries = new List<string>();
            [JsonProperty("currency")]
            public CurrencyItem Currency;
        }

        public class CurrencyItem
        {
            [JsonProperty("code")]
            public string Code;
            [JsonProperty("sign")]
            public string Sign;
            [JsonProperty("delimiter")]
            public string Delimiter;
            [JsonProperty("left")]
            public bool Left;
            [JsonProperty("name")]
            public string Name;
            [JsonProperty("html")]
            public string HTML;
        }

        [JsonProperty("data")]
        public Dictionary<string, RegionsResult> Data;
    }

    public class RegionStoresResponse : SchemaHelper<RegionStoresResponse>
    {
        public class MetaData
        {
            [JsonProperty("region")]
            public string Region;
            [JsonProperty("country")]
            public string Country;
        }

        public class ShopItem
        {
            [JsonProperty("id")]
            public string ID;
            [JsonProperty("title")]
            public string Title;
            [JsonProperty("color")]
            public string Color;
        }


        [JsonProperty("data")]
        public List<ShopItem> Data = new List<ShopItem>();
        [JsonProperty(".meta")]
        public MetaData Meta;
    }

    public class SearchResponse : SchemaHelper<SearchResponse>
    {
        public class SearchResults
        {
            [JsonProperty("results")]
            public List<SearchResult> Data = new List<SearchResult>();
        };

        public class SearchResult
        {
            [JsonProperty("id")]
            public int ID;
            [JsonProperty("plain")]
            public string Plain;
            [JsonProperty("title")]
            public string Title;
        };

        [JsonProperty("data")]
        public SearchResults Results;
    }

    public class PricesResponse : SchemaHelper<PricesResponse>
    {
        public class PricesResult
        {
            [JsonProperty("list")]
            public List<PricesItem> Prices = new List<PricesItem>();
            [JsonProperty("urls")]
            public Dictionary<string, string> URLs = new Dictionary<string, string>();
        };

        public class PricesItem
        {
            [JsonProperty("price_new")]
            public decimal NewPrice;
            [JsonProperty("price_old")]
            public decimal OldPrice;
            [JsonProperty("price_cut")]
            public decimal PriceCut;
            [JsonProperty("url")]
            public string URL;
            [JsonProperty("shop")]
            public ShopItem Shop;
            [JsonProperty("drm")]
            public List<string> DRM = new List<string>();

            public class ShopItem
            {
                [JsonProperty("id")]
                public string ID;
                [JsonProperty("name")]
                public string Name;
            }
        }

        public class PricesMetaData
        {
            [JsonProperty("currency")]
            public string Currency;
        }

        [JsonProperty(".meta")]
        public PricesMetaData Meta;
        [JsonProperty("data")]
        public Dictionary<string, PricesResult> Data = new Dictionary<string, PricesResult>();
    }

}
