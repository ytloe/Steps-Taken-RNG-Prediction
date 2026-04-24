// ===================================================================================
//  StepsTakenOnScreen Mod - ModEntry.cs
//  步数预测模组 - 主入口文件
// ===================================================================================

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.TokenizableStrings;
using System;
using System.Linq;
using System.Collections.Generic;
using StardewValley.Objects;
using StardewValley.Extensions;
using StardewValley.Locations;
using StardewValley.GameData.LocationContexts;
using System.Diagnostics.Metrics;
using StardewModdingAPI.Utilities;
using StardewValley.GameData.Machines;

namespace StepsTakenOnScreen
{
    public struct PredictionResult : IEquatable<PredictionResult>
    {
        public double DailyLuck;
        public string DishOfTheDayId;
        public int DishOfTheDayAmount;
        public string MailSenderName;
        public string WeatherAfterTomorrow;

        public bool Equals(PredictionResult other)
        {
            return this.DailyLuck == other.DailyLuck &&
                   this.DishOfTheDayId == other.DishOfTheDayId &&
                   this.DishOfTheDayAmount == other.DishOfTheDayAmount &&
                   this.MailSenderName == other.MailSenderName &&
                   this.WeatherAfterTomorrow== other.WeatherAfterTomorrow; 
        }
    }

    public class ModEntry : Mod
    {
        private ModConfig Config;
        private PredictionResult currentPrediction;
        private int targetSearchResultSteps = -1;
        private bool targetSearchCriteriaMet = false;
        private HashSet<int> futureRainyTotalDays = new HashSet<int>();
        private List<string> futureRainyDaysDisplay = new List<string>();

        // --- 状态监听缓存 ---
        private int lastPredictedSteps = -1;

        // --- 搜索框重算监听 ---
        private bool needsNewTargetSearch = true;

        private string[] targetDishIds;
        private string[] targetGifterNames;
        private bool predictionBoxVisible = true;

        public override void Entry(IModHelper helper)
        {
            this.Config = this.Helper.ReadConfig<ModConfig>();
            UpdateConfigDerivedVariables();

            helper.Events.GameLoop.GameLaunched += OnGameLaunched;
            helper.Events.GameLoop.DayStarted += OnDayStarted;
            helper.Events.Display.RenderedHud += OnRenderedHud;
            helper.Events.Input.ButtonsChanged += OnButtonsChanged;
            helper.Events.Input.ButtonPressed += OnButtonPressed;
        }

        #region 事件处理方法

        private void OnGameLaunched(object sender, GameLaunchedEventArgs e)
        {
            var configMenu = this.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (configMenu == null) return;

            configMenu.Register(
                mod: this.ModManifest,
                reset: () => {
                    this.Config = new ModConfig();
                    UpdateConfigDerivedVariables();
                    this.ForceFullUpdate();
                },
                save: () => {
                    this.Helper.WriteConfig(this.Config);
                    UpdateConfigDerivedVariables();
                    this.ForceFullUpdate();
                }
            );

            // 所有的Config UI绑定
            configMenu.AddSectionTitle(mod: this.ModManifest, text: () => "显示设置");
            configMenu.AddBoolOption(mod: this.ModManifest, name: () => this.Helper.Translation.Get("Config.DisplaySteps"), getValue: () => this.Config.DisplaySteps, setValue: value => this.Config.DisplaySteps = value);
            configMenu.AddBoolOption(mod: this.ModManifest, name: () => this.Helper.Translation.Get("Config.DisplayLuck"), getValue: () => this.Config.DisplayLuck, setValue: value => this.Config.DisplayLuck = value);
            configMenu.AddBoolOption(mod: this.ModManifest, name: () => this.Helper.Translation.Get("Config.DisplayGift"), getValue: () => this.Config.DisplayGift, setValue: value => this.Config.DisplayGift = value);
            configMenu.AddBoolOption(mod: this.ModManifest, name: () => this.Helper.Translation.Get("Config.DisplayDish"), getValue: () => this.Config.DisplayDish, setValue: value => this.Config.DisplayDish = value);
            configMenu.AddBoolOption(mod: this.ModManifest, name: () => this.Helper.Translation.Get("Config.DisplayWeather"), getValue: () => this.Config.DisplayWeather, setValue: value => this.Config.DisplayWeather = value);
            configMenu.AddBoolOption(mod: this.ModManifest, name: () => this.Helper.Translation.Get("Config.DisplayStorm"), getValue: () => this.Config.DisplayStorm, setValue: value => this.Config.DisplayStorm = value);

            configMenu.AddSectionTitle(mod: this.ModManifest, text: () => "位置偏移");
            configMenu.AddNumberOption(mod: this.ModManifest, name: () => this.Helper.Translation.Get("HorizontalOffset"), getValue: () => this.Config.PositionOffset.X, setValue: value => this.Config.PositionOffset = new Point(value, this.Config.PositionOffset.Y));
            configMenu.AddNumberOption(mod: this.ModManifest, name: () => this.Helper.Translation.Get("VerticalOffset"), getValue: () => this.Config.PositionOffset.Y, setValue: value => this.Config.PositionOffset = new Point(this.Config.PositionOffset.X, value));

            configMenu.AddSectionTitle(mod: this.ModManifest, text: () => "快捷键");
            configMenu.AddKeybindList(mod: this.ModManifest, name: () => this.Helper.Translation.Get("ModEnabled"), getValue: () => this.Config.ToggleKey, setValue: value => this.Config.ToggleKey = value);

            configMenu.AddSectionTitle(mod: this.ModManifest, text: () => "目标搜索设置");
            configMenu.AddNumberOption(mod: this.ModManifest, name: () => "目标运气值最小值", tooltip: () => "拖动到-0.101为禁用", getValue: () => (float)this.Config.TargetLuck, setValue: value => this.Config.TargetLuck = Math.Round(value, 3), min: -0.101f, max: 0.1f, interval: 0.001f);
            configMenu.AddTextOption(mod: this.ModManifest, name: () => "目标送礼人", tooltip: () => "填写邮件送礼人的英文名称。留空为禁用", getValue: () => this.Config.TargetGifter, setValue: value => this.Config.TargetGifter = value);
            configMenu.AddTextOption(mod: this.ModManifest, name: () => "目标特色菜", tooltip: () => "填写对应菜肴的物品代码。留空为禁用", getValue: () => this.Config.TargetDish, setValue: value => this.Config.TargetDish = value);
            configMenu.AddNumberOption(mod: this.ModManifest, name: () => "目标菜品最小数量", getValue: () => this.Config.TargetDishAmount, setValue: value => this.Config.TargetDishAmount = value, min: 0);
            configMenu.AddNumberOption(mod: this.ModManifest, name: () => "最大搜索步数", getValue: () => this.Config.TargetStepsLimit, setValue: value => this.Config.TargetStepsLimit = value, min: 100, max: 10000);
            configMenu.AddBoolOption(mod: this.ModManifest, name: () => "目标需要雷雨", tooltip: () => "是否将“后天需要是雷雨”作为搜索条件之一。", getValue: () => this.Config.WantStorm, setValue: value => this.Config.WantStorm = value);
        }

        private void OnDayStarted(object sender, DayStartedEventArgs e)
        {
            this.ForceFullUpdate();
        }

        private void OnButtonsChanged(object sender, ButtonsChangedEventArgs e)
        {
            if (this.Config.ToggleKey.JustPressed())
            {
                this.predictionBoxVisible = !this.predictionBoxVisible;
            }
        }

        private void OnButtonPressed(object sender, ButtonPressedEventArgs e)
        {
            if (Context.IsWorldReady && e.Button == SButton.F5)
            {
                this.Config = base.Helper.ReadConfig<ModConfig>();
                UpdateConfigDerivedVariables();
                this.Monitor.Log("配置已重新加载", LogLevel.Info);
                this.ForceFullUpdate();
            }
        }

        private void OnRenderedHud(object sender, RenderedHudEventArgs e)
        {
            if (!Context.IsWorldReady || !this.predictionBoxVisible) return;

            UpdatePredictionsIfNeeded();
            UpdateTargetSearchIfNeeded();

            List<IFormattedText> displayBlocks = BuildPredictionDisplayBlocks();
            if (displayBlocks.Count > 0)
            {
                DrawHelper.DrawHoverBox(e.SpriteBatch, displayBlocks, new Vector2(Config.PositionOffset.X, Config.PositionOffset.Y), Game1.viewport.Width);
            }
        }

        private void ForceFullUpdate()
        {
            this.lastPredictedSteps = -1;
            this.needsNewTargetSearch = true;
        }

        #endregion

        #region 天气预测系统

        // 专门预测婚礼的辅助方法
        private bool IsWeddingDay(WorldDate date)
        {
            if (!Game1.canHaveWeddingOnDay(date.DayOfMonth, date.Season)) return false;

            // 计算距离预测天数还有几天
            int daysUntil = date.TotalDays - Game1.Date.TotalDays;
            if (daysUntil < 0) return false;

            foreach (Farmer farmer in Game1.getAllFarmers())
            {
                if (farmer.spouse != null && farmer.isEngaged())
                {
                    if (farmer.friendshipData.TryGetValue(farmer.spouse, out Friendship f) && f.CountdownToWedding == daysUntil)
                        return true;
                }
                if (farmer.team.IsEngaged(farmer.UniqueMultiplayerID))
                {
                    long? spouse = farmer.team.GetSpouse(farmer.UniqueMultiplayerID);
                    if (spouse.HasValue)
                    {
                        var f2 = Game1.player.team.GetFriendship(farmer.UniqueMultiplayerID, spouse.Value);
                        if (f2 != null && f2.CountdownToWedding == daysUntil)
                            return true;
                    }
                }
            }
            return false;
        }

        // 整合了您提供的所有硬编码、节日、雨水图腾以及常规天气的字符串返回
        private string PredictWeatherForDate(WorldDate date, out bool unchangeable)
        {
            unchangeable = false;
            string weather = "Sun";

            // 1. 雨水图腾检查：仅影响明天（TotalDays + 1）
            if (date.TotalDays == Game1.Date.TotalDays + 1 && Game1.weatherForTomorrow == "Rain")
            {
                unchangeable = true;
                weather = "Rain";
            }

            // 2. 原版硬编码天气检查（复刻 getWeatherModificationsForDate 逻辑）

            int day_offset = date.TotalDays - Game1.Date.TotalDays;

            if (date.DayOfMonth == 1 || Game1.stats.DaysPlayed + day_offset <= 4) { weather = "Sun"; unchangeable = true; }
            if (Game1.stats.DaysPlayed + day_offset == 3) { weather = "Rain"; unchangeable = true; }
            if (Utility.isGreenRainDay(date.DayOfMonth, date.Season)) { weather = "GreenRain"; unchangeable = true; }
            if (date.Season == Season.Summer && date.DayOfMonth % 13 == 0) { weather = "Storm"; unchangeable = true; }
            if (Utility.isFestivalDay(date.DayOfMonth, date.Season)) { weather = "Festival"; unchangeable = true; }

            // 被动节日检查
            foreach (var festival in DataLoader.PassiveFestivals(Game1.content).Values)
            {
                if (date.DayOfMonth < festival.StartDay || date.DayOfMonth > festival.EndDay
                    || date.Season != festival.Season
                    || !GameStateQuery.CheckConditions(festival.Condition)
                    || festival.MapReplacements == null)
                    continue;

                foreach (string key in festival.MapReplacements.Keys)
                {
                    var loc = Game1.getLocationFromName(key);
                    if (loc != null && loc.InValleyContext())
                    {
                        weather = "Sun";
                        unchangeable = true;
                        break;
                    }
                }
            }

            // 3. 婚礼检查（婚礼覆盖一切天气为晴天/Wedding）
            if (IsWeddingDay(date))
            {
                weather = "Wedding";
                unchangeable = true;
            }

            if (unchangeable) { return weather; }

            // 4. 常规天气判断（完全使用您提供的代码，剔除了对其他逻辑的影响）
            switch (date.Season)
            {
                case Season.Summer:
                    {
                        int seed = Utility.CreateRandomSeed(date.TotalDays, Game1.uniqueIDForThisGame / 2, Game1.hash.GetDeterministicHashCode("summer_rain_chance"));
                        Random random = new Random(seed);
                        float chance = 0.12f + (float)date.DayOfMonth * 0.003f;
                        return random.NextDouble() < chance ? "Rain" : "Sun";
                    }
                case Season.Spring:
                case Season.Fall:
                    {
                        int seed = Utility.CreateRandomSeed(date.TotalDays, Game1.uniqueIDForThisGame / 2, Game1.hash.GetDeterministicHashCode("location_weather"));
                        Random random = new Random(seed);
                        return random.NextDouble() < 0.183 ? "Rain" : "Sun";
                    }
                default:
                    return "Sun";
            }
        }

        /// <summary>
        /// 更新未来7天雨天列表
        /// </summary>
        private void UpdateFutureRainyDays()
        {
            this.futureRainyDaysDisplay.Clear();
            this.futureRainyTotalDays.Clear();
            WorldDate today = new WorldDate(Game1.Date);

            for (int i = 1; i <= 7; i++)
            {
                WorldDate futureDate = new WorldDate(today);
                futureDate.TotalDays += i;

                // [替换] 获取对应日期的天气字符串
                string weather = PredictWeatherForDate(futureDate, out _);
                if (weather == "Rain" || weather == "Storm" || weather == "GreenRain")
                {
                    this.futureRainyTotalDays.Add(futureDate.TotalDays);
                    this.futureRainyDaysDisplay.Add(futureDate.DayOfMonth.ToString());
                }
            }
        }

        #endregion

        /// <summary>
        /// 模拟 Default 上下文天气条件对 Game1.random 的消耗，返回是否雷雨。
        /// tomorrowDate: 过夜后的日期（即Game1.Date+1，过夜时的Game1.dayOfMonth状态）
        /// nextDaysPlayed: 过夜后的DaysPlayed（Game1.stats.DaysPlayed+1）
        /// </summary>
        private bool SimulateDefaultWeatherRandom(
            Random random,
            WorldDate tomorrowDate,
            uint nextDaysPlayed)
        {
            // 以下所有条件使用 tomorrowDate 的状态（过夜后的游戏状态）
            Season season = tomorrowDate.Season;
            int dayOfMonth = tomorrowDate.DayOfMonth;
            bool daysPlayedOver28 = nextDaysPlayed >= 28;
            switch (season)
            {
                case Season.Summer:
                {
                    // SummerStorm: RANDOM .85
                    if (random.NextDouble() < 0.85)
                        return true;

                    // SummerStorm2: DAYS_PLAYED 28 && !day1 && !day2 → RANDOM .25
                    if (daysPlayedOver28 && dayOfMonth != 1 && dayOfMonth != 2)
                    {
                        if (random.NextDouble() < 0.25)
                            return true;
                    }
                    // SummerRain 命中，break
                    break;
                }

                case Season.Spring:
                case Season.Fall:
                {
                    // FallStorm: DAYS_PLAYED 28 && !day1 && !day2 → RANDOM .25
                    if (daysPlayedOver28 && dayOfMonth != 1 && dayOfMonth != 2)
                    {
                        if (random.NextDouble() < 0.25)
                            return true;
                    }
                    break;
                    // FallRain 命中
                }

                case Season.Winter:
                // WinterSnow: SYNCED → 无RANDOM消耗
                break;

            }
            return false;
        }

        /// <summary>
        /// 模拟 Island 上下文 random 消耗
        /// </summary>
        private bool SimulateIslandWeatherRandom(Random random, WorldDate tomorrowDate)
        {
            // Island的tomorrow同样是 tomorrowDate+1
            WorldDate weatherTargetDate = new WorldDate(tomorrowDate);
            weatherTargetDate.TotalDays += 1;

            // 姜岛无节日，无需节日判断
            // FirstVisitSun: !Visited_Island → 零消耗
            if (!Game1.player.mailReceived.Contains("Visited_Island"))
                return false;

            // Rain: RANDOM .24 → 消耗1次
            return random.NextDouble()<0.24;
        }

        #region 核心预测逻辑
        private PredictionResult PredictNextDayOutcomes(int stepsTaken)
        {
            PredictionResult result = new PredictionResult();

            // ===== 初始化随机数（与游戏完全一致）=====
            uint nextDaysPlayed = Game1.stats.DaysPlayed + 1;
            int seed = Utility.CreateRandomSeed(
                (int)Game1.uniqueIDForThisGame / 100,
                nextDaysPlayed * 10 + 1,
                stepsTaken);
            Random random = Utility.CreateRandom(seed);

            // dayOfMonth循环（明天的dayOfMonth）
            int nextDayOfMonth = Game1.dayOfMonth + 1 > 28 ? 1 : Game1.dayOfMonth + 1;
            for (int i = 0; i < nextDayOfMonth; i++) random.Next();

            // ===== UpdateDishOfTheDay =====
            string dishId;
            do { dishId = random.Next(194, 240).ToString(); }
            while (Utility.IsForbiddenDishOfTheDay(dishId));
            result.DishOfTheDayId = dishId;
            result.DishOfTheDayAmount = random.Next(1, 4 + ((random.NextDouble() < 0.08) ? 10 : 0));

            // ===== passTimeForObjects（隔夜加工）=====
            random.NextDouble(); // 不知名固定消耗
            int overnightMinutesElapsed = Utility.CalculateMinutesUntilMorning(Game1.timeOfDay);

            Utility.ForEachLocation(delegate (GameLocation location)
            {
                foreach (KeyValuePair<Vector2, StardewValley.Object> objPair in location.objects.Pairs)
                {
                    var obj = objPair.Value;
                    if (obj is Fence || obj is Furniture || obj.IsSprinkler()) continue;

                    if (obj is Mannequin mannequin)
                    {
                        DataLoader.Mannequins(Game1.content).TryGetValue(mannequin.ItemId, out var mdata);
                        if (random.NextDouble() < 0.001 && mdata?.Cursed == true)
                        {
                            if (Game1.timeOfDay > Game1.getTrulyDarkTime(location))
                            {
                                if (random.NextDouble() < 0.1) { }
                                else if (random.NextDouble() < 0.66)
                                { if (random.NextDouble() < 0.5) { } }
                                else random.Next(500, 4000);
                            }
                            else
                            {
                                if (random.NextDouble() < 0.66)
                                { if (random.NextDouble() < 0.5) { } }
                                else random.Next(500, 4000);
                            }
                        }
                    }

                    if (obj.heldObject.Value != null && obj.QualifiedItemId != "(BC)165")
                    {
                        var machineData = obj.GetMachineData();
                        bool readyForHarvest = false;
                        int minutesUntilReady = obj.MinutesUntilReady - overnightMinutesElapsed;
                        if (minutesUntilReady <= 0 && (machineData == null || !machineData.OnlyCompleteOvernight))
                            readyForHarvest = true;

                        if (machineData != null)
                        {
                            if (!readyForHarvest && machineData.WorkingEffects != null
                                && random.NextDouble() < (double)machineData.WorkingEffectChance)
                            {
                                if (obj is WoodChipper && location.farmers.Any()
                                    && random.NextDouble() < 0.35)
                                {
                                    for (int i = 0; i < 8; i++) random.Next(-48, 0);
                                }
                            }
                        }
                        else if (!readyForHarvest && random.NextDouble() < 0.33)
                        {
                            if (obj is WoodChipper && location.farmers.Any()
                                && random.NextDouble() < 0.35)
                            {
                                for (int i = 0; i < 8; i++) random.Next(-48, 0);
                            }
                        }
                    }
                }
                return true;
            }, includeInteriors: true, includeGenerated: false);

            // ===== 邮件送礼 =====
            result.MailSenderName = "";
            if (Utility.TryGetRandom(Game1.player.friendshipData, out var whichFriend, out var friendship, random) &&
                random.NextBool((double)(friendship.Points / 250) * 0.1) &&
                Game1.player.spouse != whichFriend &&
                DataLoader.Mail(Game1.content).ContainsKey(whichFriend))
            {
                result.MailSenderName = whichFriend;
            }

            random.NextDouble(); // 固定消耗

            // Mannequin诅咒检查（当前位置）
            if (Game1.player.shirtItem.Value != null && Game1.player.pantsItem.Value != null &&
                (Game1.player.currentLocation is FarmHouse ||
                 Game1.player.currentLocation is IslandFarmHouse ||
                 Game1.player.currentLocation is Shed))
            {
                foreach (StardewValley.Object value in Game1.player.currentLocation.objects.Values)
                {
                    if (value is Mannequin m2)
                    {
                        DataLoader.Mannequins(Game1.content).TryGetValue(m2.ItemId, out var mdata2);
                        if (mdata2?.Cursed == true) random.NextDouble();
                    }
                }
            }

            // ===== 运气=====
            result.DailyLuck = Math.Min(0.1,
                (double)random.Next(-100, 101) / 1000.0);

            //任务板生成物品大概率消耗1个随机数
            random.NextDouble();
            

            // 过夜后的日期状态
            WorldDate tomorrowDate = new WorldDate(Game1.Date);
            tomorrowDate.TotalDays += 1;

            // 换季Bush/Tree消耗（仅当明天是季节第1天）
            if (tomorrowDate.DayOfMonth == 1)
            {
                if (tomorrowDate.Season == Season.Summer)
                {
                    Utility.ForEachLocation(delegate (GameLocation location)
                    {
                        foreach (var pair in location.terrainFeatures.Pairs)
                            if (pair.Value is StardewValley.TerrainFeatures.Bush b
                                && b.size.Value == 1)
                                random.NextDouble();
                        if (location.largeTerrainFeatures != null)
                            foreach (var lf in location.largeTerrainFeatures)
                                if (lf is StardewValley.TerrainFeatures.Bush lb
                                    && lb.size.Value == 1)
                                    random.NextDouble();
                        return true;
                    }, includeInteriors: true, includeGenerated: false);
                }
                if (tomorrowDate.Season == Season.Fall)
                {
                    Utility.ForEachLocation(delegate (GameLocation location)
                    {
                        foreach (var pair in location.terrainFeatures.Pairs)
                            if (pair.Value is StardewValley.TerrainFeatures.Tree tree
                                && (tree.treeType.Value == "1" || tree.treeType.Value == "2")
                                && tree.growthStage.Value >= 5
                                && !tree.tapped.Value
                                && !(location is StardewValley.Locations.Town)
                                && !location.IsGreenhouse)
                                random.NextDouble();
                        return true;
                    }, includeInteriors: false, includeGenerated: false);
                }
            }

            //random.NextDouble();

            // 后天日期（用于天气查询）
            WorldDate dayAfterTomorrow = new WorldDate(Game1.Date);
            dayAfterTomorrow.TotalDays += 2;
            // 上下文天气消耗随机数
            bool simStorm = SimulateDefaultWeatherRandom(random, tomorrowDate, nextDaysPlayed);
            bool IslandRain = SimulateIslandWeatherRandom(random, tomorrowDate);

            string baseWeather = PredictWeatherForDate(dayAfterTomorrow, out bool unchangeable);

            result.WeatherAfterTomorrow = baseWeather;
            // 如果是强制天气，无视步数影响，直接赋值
            if (!unchangeable && baseWeather == "Rain" && simStorm) { result.WeatherAfterTomorrow = "Storm"; }

            // Desert：无消耗

            return result;
        }
        #endregion

        #region UI更新与渲染构建

        private void UpdatePredictionsIfNeeded()
        {
            int currentSteps = (int)Game1.stats.StepsTaken;

            // 仅计算当前步数的预测结果
            PredictionResult newPrediction = PredictNextDayOutcomes(currentSteps);

            // 如果状态发生变化（走路、机器变化、求雨、求婚等）
            if (this.lastPredictedSteps != currentSteps || !newPrediction.Equals(this.currentPrediction))
            {
                this.lastPredictedSteps = currentSteps;
                this.currentPrediction = newPrediction;
                this.needsNewTargetSearch = true;

                // 世界状态变化时，实时更新7天的天气预测！
                UpdateFutureRainyDays();
            }
        }

        private void UpdateTargetSearchIfNeeded()
        {
            if (this.Config.TargetLuck == -0.101 && string.IsNullOrEmpty(this.Config.TargetGifter) && string.IsNullOrEmpty(this.Config.TargetDish) && !this.Config.WantStorm)
            {
                this.targetSearchCriteriaMet = false;
                this.targetSearchResultSteps = -1;
                return;
            }

            int currentSteps = (int)Game1.stats.StepsTaken;

            if (this.needsNewTargetSearch || (this.targetSearchCriteriaMet && currentSteps > this.targetSearchResultSteps))
            {
                this.targetSearchCriteriaMet = false;
                this.needsNewTargetSearch = false;

                for (int steps = 0; steps < this.Config.TargetStepsLimit; steps++)
                {
                    int futureSteps = currentSteps + steps;
                    PredictionResult futurePrediction = PredictNextDayOutcomes(futureSteps);

                    bool luckMet = this.Config.TargetLuck == -0.101 || futurePrediction.DailyLuck >= this.Config.TargetLuck;
                    bool dishMet = string.IsNullOrEmpty(this.Config.TargetDish) || (this.targetDishIds.Contains(futurePrediction.DishOfTheDayId) && futurePrediction.DishOfTheDayAmount >= this.Config.TargetDishAmount);
                    bool gifterMet = string.IsNullOrEmpty(this.Config.TargetGifter) || this.targetGifterNames.Contains(futurePrediction.MailSenderName);

                    // [修改] 适配新的字符串天气判断
                    bool stormMet = !this.Config.WantStorm || futurePrediction.WeatherAfterTomorrow == "Storm";

                    if (luckMet && dishMet && gifterMet && stormMet)
                    {
                        this.targetSearchCriteriaMet = true;
                        this.targetSearchResultSteps = futureSteps;
                        break;
                    }
                }

                if (!this.targetSearchCriteriaMet)
                {
                    this.targetSearchResultSteps = currentSteps + this.Config.TargetStepsLimit;
                }
            }
        }

        private List<IFormattedText> BuildPredictionDisplayBlocks()
        {
            List<IFormattedText> blocks = new List<IFormattedText>();

            if (this.Config.DisplaySteps)
                blocks.Add(new FormattedText($"{this.Helper.Translation.Get("Text_TotalSteps")}：{(int)Game1.stats.StepsTaken}"));

            if (this.Config.DisplayDish)
            {
                string dishName = Game1.objectData.TryGetValue(this.currentPrediction.DishOfTheDayId, out var data) ? TokenParser.ParseText(data.DisplayName) : "???";
                blocks.Add(new FormattedText($"\n{this.Helper.Translation.Get("Text_TomorrowDish")}：{dishName} ({this.currentPrediction.DishOfTheDayAmount})"));
            }

            if (this.Config.DisplayGift)
            {
                string gifterName = string.IsNullOrEmpty(this.currentPrediction.MailSenderName) ? this.Helper.Translation.Get("Text_None") : Game1.getCharacterFromName(this.currentPrediction.MailSenderName)?.displayName ?? this.currentPrediction.MailSenderName;
                blocks.Add(new FormattedText($"\n{this.Helper.Translation.Get("Text_TomorrowGift")}：{gifterName}"));
            }

            if (this.Config.DisplayLuck)
                blocks.Add(new FormattedText($"\n{this.Helper.Translation.Get("Text_TomorrowLuck")}：{this.currentPrediction.DailyLuck.ToString("F3")}"));

            if (this.Config.DisplayWeather)
            {
                string rainDays = this.futureRainyDaysDisplay.Any() ? string.Join(", ", this.futureRainyDaysDisplay) : this.Helper.Translation.Get("Text_None");
                blocks.Add(new FormattedText($"\n{this.Helper.Translation.Get("Text_Rainy7Days")}：{rainDays}"));
            }

            if (this.Config.DisplayStorm)
            {
                blocks.Add(new FormattedText($"\n{this.Helper.Translation.Get("Text_AftermorrowStorm")}："));

                string w = this.currentPrediction.WeatherAfterTomorrow;

                if (w == "Storm")
                {
                    blocks.Add(new FormattedText($"{this.Helper.Translation.Get("Text_Stormy")}", Color.Red, true));
                }
                else if (w == "GreenRain")
                {
                    // [新增] 绿雨显示
                    blocks.Add(new FormattedText($"{this.Helper.Translation.Get("Text_GreenRain")}", Color.LimeGreen, true));
                }
                else if (w == "Rain")
                {
                    blocks.Add(new FormattedText($"{this.Helper.Translation.Get("Text_Rainy")}"));
                }
                else
                {
                    // 包含 Sun, Wedding, Festival 等所有不下雨的情况
                    blocks.Add(new FormattedText($"{this.Helper.Translation.Get("Text_NonRainy")}"));
                }
            }

            if (this.targetSearchResultSteps != -1)
            {
                blocks.Add(new FormattedText($"\n------\n{this.Helper.Translation.Get("Text_SearchTarget")}："));

                if (!string.IsNullOrEmpty(this.Config.TargetDish))
                    blocks.Add(new FormattedText($"\n{this.Helper.Translation.Get("Text_TargetDish")}：{this.Config.TargetDish} ({this.Config.TargetDishAmount})"));

                if (!string.IsNullOrEmpty(this.Config.TargetGifter))
                    blocks.Add(new FormattedText($"\n{this.Helper.Translation.Get("Text_TargetGift")}：{this.Config.TargetGifter}"));

                if (this.Config.TargetLuck != -0.101)
                    blocks.Add(new FormattedText($"\n{this.Helper.Translation.Get("Text_TargetLuck")}：{this.Config.TargetLuck} ~ 0.1"));

                if (this.Config.WantStorm)
                    blocks.Add(new FormattedText($"\n{this.Helper.Translation.Get("Text_TargetStorm")}：{this.Helper.Translation.Get("Text_Yes")}"));

                if (this.targetSearchCriteriaMet)
                    blocks.Add(new FormattedText($"\n{this.Helper.Translation.Get("Text_TargetSteps")}：{this.targetSearchResultSteps}"));
                else
                    blocks.Add(new FormattedText($"\n{this.Helper.Translation.Get("Text_TargetNotFound")}"));
            }

            return blocks;
        }

        private void UpdateConfigDerivedVariables()
        {
            this.targetDishIds = this.Config.TargetDish.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).Select(id => id.StartsWith("(O)") ? id.Replace("(O)", "").Trim() : id).ToArray();
            this.targetGifterNames = this.Config.TargetGifter.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToArray();
        }

        #endregion
    }
}