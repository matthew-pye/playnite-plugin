using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Playnite.SDK;
using Playnite.SDK.Data;
using ProtoBuf;
using RomM.Models.RomM.Rom;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Drawing.Printing;
using System.IO;
using System.IO.Packaging;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace RomM.Integrations
{
    class SourceLink
    {
        public string Name { get; set; } = "RetroAchievements";
        public string GameName { get; set; }
        public string Url { get; set; }
    }

    class EstimateTimeToUnlock
    {
        public int DataCount { get; set; } = 0;
        public int EstimateTimeMin { get; set; } = 0;
        public int EstimateTimeMax { get; set; } = 0;
    }

    class Achievement
    {

        public string Name { get; set; }
        public string ApiName { get; set; } = "";
        public string Description { get; set; }
        public string UrlUnlocked { get; set; }
        public string UrlLocked { get; set; }
        public DateTime? DateUnlocked { get; set; }
        public DateTime? DateUnlockedRaHardCore { get; set; }
        public bool IsHidden { get; set; }
        public int Percent { get; set; }
        public int GamerScore { get; set; }
        public int CategoryOrder { get; set; } = 0;
        public string CategoryIcon { get; set; } = "";
        public string Category { get; set; } = "";
        public string ParentCategory { get; set; } = "";
        public string CategoryRpcs3 { get; set; } = "";
        public bool NoRarety { get; set; } = false;
    }

    class SuccessStoryRA
    {
        public bool IsManual { get; set; } = false;

        public bool IsIgnored { get; set; } = false;

        public SourceLink SourcesLink { get; set; }

        public List<string> ItemsStats { get; set; } = new List<string>();

        public bool ShowStats { get; set; } = true;

        public bool IsEmulators { get; set; } = false;

        public EstimateTimeToUnlock EstimateTime { get; set; }

        public int? RAgameID { get; set; }

        public List<Achievement> Items { get; set; }

        public DateTime DateLastRefresh { get; set; }

        public bool GameExist { get; set; }

        public string Id { get; set; }
        public string Name { get; set; }
    }

    public class SuccessStoryConfig
    {
        [JsonProperty("EnableRetroAchievements")]
        public bool Enabled { get; set; } = false;
        [JsonProperty("RetroAchievementsUser")]
        public string User { get; set; } = string.Empty;
        [JsonProperty("RetroAchievementsKey")]
        public string APIKey { get; set; } = string.Empty;
    }

   public class SuccessStory
   {
       public ILogger Logger => LogManager.GetLogger();

       string PluginPath;
       SuccessStoryConfig Config;

        public SuccessStory(string pluginPath)
        {
            PluginPath = pluginPath;
            string configPath = PluginPath + "\\config.json";

            using (StreamReader reader = new StreamReader(configPath))
            using (JsonTextReader jsonReader = new JsonTextReader(reader))
            {
                var jsonFile = JObject.Parse(reader.ReadToEnd());
                Config = jsonFile.ToObject<SuccessStoryConfig>();
            }
        }

        public static async Task<HttpResponseMessage> GetAsyncWithParams(string baseUrl, NameValueCollection queryParams)
        {
            var uriBuilder = new UriBuilder(baseUrl);
            var query = HttpUtility.ParseQueryString(uriBuilder.Query);

            foreach (string key in queryParams)
            {
                query[key] = queryParams[key];
            }

            uriBuilder.Query = query.ToString();

            return await HttpClientSingleton.Instance.GetAsync(uriBuilder.Uri);
        }

        public void RefreshConfig()
        {
            string configPath = PluginPath + "\\config.json";

            using (StreamReader reader = new StreamReader(configPath))
            using (JsonTextReader jsonReader = new JsonTextReader(reader))
            {
                var jsonFile = JObject.Parse(reader.ReadToEnd());
                Config = jsonFile.ToObject<SuccessStoryConfig>();
            }
        }

        public bool IsRAEnabled() { return Config.Enabled; }

        public void AddGame(string GameID, string GameName, int? RA_ID)
        {
            SuccessStoryRA Game = new SuccessStoryRA();
            Game.SourcesLink = new SourceLink();
            Game.EstimateTime = new EstimateTimeToUnlock();

            Game.Id = GameID;
            Game.Name = GameName;
            Game.RAgameID = RA_ID;  

            Game.SourcesLink.Url = $"https://retroachievements.org/game/{RA_ID}";

            Game.DateLastRefresh = DateTime.Now.ToUniversalTime();
            Game.GameExist = true;

            List<Achievement> achievements = new List<Achievement>();

            try
            {
                string url = $"https://retroachievements.org/API/API_GetGameInfoAndUserProgress.php?";

                NameValueCollection queryParams = new NameValueCollection
                    {
                        { "g", RA_ID.ToString() },
                        { "y", Config.APIKey },
                        { "u", Config.User }
                    };

                HttpResponseMessage response = GetAsyncWithParams(url, queryParams).GetAwaiter().GetResult();
                response.EnsureSuccessStatusCode();

                Stream body = response.Content.ReadAsStreamAsync().GetAwaiter().GetResult();
                string responseString;

                using (StreamReader reader = new StreamReader(body))
                {
                    responseString = reader.ReadToEnd();
                }

                dynamic resultObj = Serialization.FromJson<dynamic>(responseString);

                int numDistinctPlayersCasual = resultObj["NumDistinctPlayersCasual"] == null ? 0 : (int)resultObj["NumDistinctPlayersCasual"];

                Game.SourcesLink.Name = (string)resultObj["Title"];

                if (resultObj["Achievements"] != null && !responseString.Contains("\"Achievements\":{}"))
                {
                    foreach (dynamic item in resultObj["Achievements"])
                    {
                        foreach (dynamic it in item)
                        {
                            Achievement cheevo = new Achievement();
                            cheevo.Name = (string)it["Title"];
                            cheevo.Description = (string)it["Description"];
                            cheevo.UrlUnlocked = $"https://s3-eu-west-1.amazonaws.com/i.retroachievements.org/Badge/{it["BadgeName"]}.png";
                            cheevo.UrlLocked = $"https://s3-eu-west-1.amazonaws.com/i.retroachievements.org/Badge/{it["BadgeName"]}_lock.png";
                            cheevo.DateUnlocked = (it["DateEarned"] == null) ? (DateTime?)null : Convert.ToDateTime((string)it["DateEarned"]);
                            cheevo.DateUnlockedRaHardCore = (it["DateEarnedHardcore"] == null) ? (DateTime?)null : Convert.ToDateTime((string)it["DateEarnedHardcore"]);
                            cheevo.Percent = it["NumAwarded"] == null || (int)it["NumAwarded"] == 0 || numDistinctPlayersCasual == 0 ? 100 : (int)it["NumAwarded"] * 100 / numDistinctPlayersCasual;
                            cheevo.GamerScore = it["Points"] == null ? 0 : (int)it["Points"];

                            achievements.Add(cheevo);
                        }
                    }
                }

                Game.Items = achievements;
            }
            catch (Exception e)
            {
                Logger.Error(e.ToString());
            }

            JsonSerializer serializer = new JsonSerializer();
            serializer.NullValueHandling = NullValueHandling.Include;

            using (StreamWriter sw = new StreamWriter($"{PluginPath}\\SuccessStory\\{GameID}.json"))
            using (JsonWriter writer = new JsonTextWriter(sw))
            {
                serializer.Serialize(writer, Game);
            }
        }

    }
}
