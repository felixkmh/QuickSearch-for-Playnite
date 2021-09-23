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
            public List<string> Countries { get; set; } = new List<string>();
            [JsonProperty("currency")]
            public CurrencyItem Currency { get; set; }
        }

        public class CurrencyItem
        {
            [JsonProperty("code")]
            public string Code { get; set; }
            [JsonProperty("sign")]
            public string Sign { get; set; }
            [JsonProperty("delimiter")]
            public string Delimiter { get; set; }
            [JsonProperty("left")]
            public bool Left { get; set; }
            [JsonProperty("name")]
            public string Name { get; set; }
            [JsonProperty("html")]
            public string HTML { get; set; }
        }

        [JsonProperty("data")]
        public Dictionary<string, RegionsResult> Data { get; set; }
    }

    public class RegionStoresResponse : SchemaHelper<RegionStoresResponse>
    {
        public class MetaData
        {
            [JsonProperty("region")]
            public string Region { get; set; }
            [JsonProperty("country")]
            public string Country { get; set; }
        }

        public class ShopItem
        {
            [JsonProperty("id")]
            public string ID { get; set; }
            [JsonProperty("title")]
            public string Title { get; set; }
            [JsonProperty("color")]
            public string Color { get; set; }
        }


        [JsonProperty("data")]
        public List<ShopItem> Data { get; set; } = new List<ShopItem>();
        [JsonProperty(".meta")]
        public MetaData Meta { get; set; }
    }

    public class SearchResponse : SchemaHelper<SearchResponse>
    {
        public class SearchResults
        {
            [JsonProperty("results")]
            public List<SearchResult> Data { get; set; } = new List<SearchResult>();
        };

        public class SearchResult
        {
            [JsonProperty("id")]
            public int ID { get; set; }
            [JsonProperty("plain")]
            public string Plain { get; set; }
            [JsonProperty("title")]
            public string Title { get; set; }
        };

        [JsonProperty("data")]
        public SearchResults Results { get; set; }
    }

    public class PricesResponse : SchemaHelper<PricesResponse>
    {
        public class PricesResult
        {
            [JsonProperty("list")]
            public List<PricesItem> Prices { get; set; } = new List<PricesItem>();
            [JsonProperty("urls")]
            public Dictionary<string, string> URLs { get; set; } = new Dictionary<string, string>();
        };

        public class PricesItem
        {
            [JsonProperty("price_new")]
            public decimal NewPrice { get; set; }
            [JsonProperty("price_old")]
            public decimal OldPrice { get; set; }
            [JsonProperty("price_cut")]
            public decimal PriceCut { get; set; }
            [JsonProperty("url")]
            public string URL { get; set; }
            [JsonProperty("shop")]
            public ShopItem Shop { get; set; }
            [JsonProperty("drm")]
            public List<string> DRM { get; set; } = new List<string>();

            public class ShopItem
            {
                [JsonProperty("id")]
                public string ID { get; set; }
                [JsonProperty("name")]
                public string Name { get; set; }
            }
        }

        public class PricesMetaData
        {
            [JsonProperty("currency")]
            public string Currency { get; set; }
        }

        [JsonProperty(".meta")]
        public PricesMetaData Meta { get; set; }
        [JsonProperty("data")]
        public Dictionary<string, PricesResult> Data { get; set; } = new Dictionary<string, PricesResult>();
    }

}
