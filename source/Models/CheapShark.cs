using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuickSearch.Models.CheapShark
{
    [JsonArray]
    public class GameResponse
    {
        public List<GameInfo> GameInfos { get; set; } = new List<GameInfo>();
    }

    public class GameInfo
    {
        [JsonProperty("gameID")]
        public string GameID { get; set; }
        [JsonProperty("steamAppID")]
        public int SteamAppID { get; set; }
        [JsonProperty("cheapest")]
        public float Cheapest { get; set; }
        [JsonProperty("cheapestDealID")]
        public string CheapestDealID { get; set; }
        [JsonProperty("external")]
        public string External { get; set; }
        [JsonProperty("thumb")]
        public string Thumb { get; set; }
        [JsonProperty("internalName")]
        public string InternalName { get; set; }
    }
}
