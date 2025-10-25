// ===================================================================================
//  StepsTakenOnScreen Mod - ModEntry.cs
//  步数预测模组 - 主入口文件
// ===================================================================================

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Delegates;
using StardewValley.Extensions;
using StardewValley.GameData;
using StardewValley.Objects;
using StardewValley.TokenizableStrings;
using System;
using System.Linq;
using System.Collections.Generic;
using StardewValley.GameData.LocationContexts;
using System.Globalization;
using StardewModdingAPI.Utilities;
using StardewValley.Locations;

namespace StepsTakenOnScreen
{
    public struct PredictionResult
    {
        public double DailyLuck;
        public string DishOfTheDayId;
        public int DishOfTheDayAmount;
        public string MailSenderName;
        public bool IsRainyDayAfterTomorrow;
        public bool WillBeStormyDayAfterTomorrow;
    }

    public class ModEntry : Mod
    {
        private ModConfig Config;
        private PredictionResult currentPrediction;
        private int targetSearchResultSteps = -1;
        private bool targetSearchCriteriaMet = false;
        private HashSet<int> futureRainyTotalDays = new HashSet<int>();
        private List<string> futureRainyDaysDisplay = new List<string>();
        private int lastPredictedSteps = -1;
        private uint lastPredictedDaysPlayed = 0;
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
            helper.Events.Input.ButtonReleased += OnButtonReleased;
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
                    this.lastPredictedSteps = -1;
                    this.needsNewTargetSearch = true;
                },
                save: () => {
                    this.Helper.WriteConfig(this.Config);
                    UpdateConfigDerivedVariables();
                    this.lastPredictedSteps = -1;
                    this.needsNewTargetSearch = true;
                }
            );

            configMenu.AddSectionTitle(mod: this.ModManifest, text: () => "显示设置");
            configMenu.AddBoolOption(mod: this.ModManifest, name: () => this.Helper.Translation.Get("DisplaySteps"), tooltip: () => "是否在预测框中显示当前的步数。", getValue: () => this.Config.DisplaySteps, setValue: value => this.Config.DisplaySteps = value);
            configMenu.AddBoolOption(mod: this.ModManifest, name: () => this.Helper.Translation.Get("DisplayLuck"), tooltip: () => "是否在预测框中显示明天的运气值。", getValue: () => this.Config.DisplayLuck, setValue: value => this.Config.DisplayLuck = value);
            configMenu.AddBoolOption(mod: this.ModManifest, name: () => this.Helper.Translation.Get("DisplayGift"), tooltip: () => "是否在预测框中显示明天可能送礼的村民。", getValue: () => this.Config.DisplayGift, setValue: value => this.Config.DisplayGift = value);
            configMenu.AddBoolOption(mod: this.ModManifest, name: () => this.Helper.Translation.Get("DisplayDish"), tooltip: () => "是否在预测框中显示明天酒吧的特色菜。", getValue: () => this.Config.DisplayDish, setValue: value => this.Config.DisplayDish = value);
            configMenu.AddBoolOption(mod: this.ModManifest, name: () => this.Helper.Translation.Get("DisplayWeather"), tooltip: () => "是否在预测框中显示未来7天的雨天。", getValue: () => this.Config.DisplayWeather, setValue: value => this.Config.DisplayWeather = value);
            configMenu.AddBoolOption(mod: this.ModManifest, name: () => "显示雷雨预测", tooltip: () => "是否显示未来雨天，以及后天是否可控为雷雨。", getValue: () => this.Config.DisplayThunder, setValue: value => this.Config.DisplayThunder = value);

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
            this.needsNewTargetSearch = true;
            UpdateFutureRainyDays();
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
                this.lastPredictedSteps = -1;
                this.needsNewTargetSearch = true;
            }
        }

        private void OnButtonReleased(object sender, ButtonReleasedEventArgs e)
        {
            if (Context.IsWorldReady && (e.Button.IsActionButton() || e.Button.IsUseToolButton()))
            {
                Vector2 tile = e.Cursor.GrabTile;
            }
        }

        private void OnRenderedHud(object sender, RenderedHudEventArgs e)
        {
            if (!Context.IsWorldReady || !this.predictionBoxVisible) return;

            UpdatePredictionsIfNeeded();
            UpdateTargetSearchIfNeeded();

            string displayText = BuildPredictionDisplayString();
            if (!string.IsNullOrEmpty(displayText))
            {
                DrawHelper.DrawHoverBox(e.SpriteBatch, displayText, new Vector2(Config.PositionOffset.X, Config.PositionOffset.Y), Game1.viewport.Width);
            }
        }

        #endregion

        #region 天气预测系统

        private string PredictWeatherForDate(WorldDate date)
        {
            string hardcodedWeather = Game1.getWeatherModificationsForDate(date, "Sun");
            if (hardcodedWeather != "Sun")
            {
                return hardcodedWeather;
            }

            switch (date.Season)
            {
                case Season.Summer:
                    Random r = Utility.CreateRandom(date.Year * 777, Game1.uniqueIDForThisGame);
                    int[] possible_days = new int[] { 5, 6, 7, 14, 15, 16, 18, 23 };
                    if (date.DayOfMonth == r.ChooseFrom(possible_days))
                    {
                        return "GreenRain";
                    }

                    int summerSeed = Utility.CreateRandomSeed(date.TotalDays, (int)Game1.uniqueIDForThisGame / 2, Game1.hash.GetDeterministicHashCode("summer_rain_chance"));
                    Random summerRng = new Random(summerSeed);
                    float summerChance = 0.12f + (float)date.DayOfMonth * 0.003f;
                    if (summerRng.NextDouble() < summerChance)
                    {
                        return "Rain";
                    }
                    break;
                case Season.Spring:
                case Season.Fall:
                    int springFallSeed = Utility.CreateRandomSeed(Game1.hash.GetDeterministicHashCode("location_weather"), (int)Game1.uniqueIDForThisGame, date.TotalDays);
                    Random springFallRng = new Random(springFallSeed);
                    if (springFallRng.NextDouble() < 0.183f)
                    {
                        return "Rain";
                    }
                    break;
                case Season.Winter:
                    int winterSeed = Utility.CreateRandomSeed(Game1.hash.GetDeterministicHashCode("location_weather"), (int)Game1.uniqueIDForThisGame, date.TotalDays);
                    Random winterRng = new Random(winterSeed);
                    if (winterRng.NextDouble() < 0.63f)
                    {
                        return "Snow";
                    }
                    break;
            }
            return "Sun";
        }

        private void UpdateFutureRainyDays()
        {
            this.futureRainyDaysDisplay.Clear();
            this.futureRainyTotalDays.Clear();
            WorldDate today = new WorldDate(Game1.Date);

            for (int i = 1; i <= 7; i++)
            {
                WorldDate futureDate = new WorldDate(today);
                futureDate.TotalDays += i;
                string predictedWeather = PredictWeatherForDate(futureDate);

                if (predictedWeather == "Rain" || predictedWeather == "Storm" || predictedWeather == "GreenRain")
                {
                    this.futureRainyTotalDays.Add(futureDate.TotalDays);
                    this.futureRainyDaysDisplay.Add(futureDate.DayOfMonth.ToString());
                }
            }
        }

        #endregion

        #region 核心预测逻辑

        private PredictionResult PredictNextDayOutcomes(int stepsTaken)
        {
            PredictionResult result = new PredictionResult();

            WorldDate dayAfterTomorrow = new WorldDate(Game1.Date);
            dayAfterTomorrow.TotalDays += 2;
            result.IsRainyDayAfterTomorrow = this.futureRainyTotalDays.Contains(dayAfterTomorrow.TotalDays);

            // 1. 创建模拟明晚会使用的 random 对象
            uint nextDay = Game1.stats.DaysPlayed + 1;
            int seed = Utility.CreateRandomSeed((int)Game1.uniqueIDForThisGame / 100, nextDay * 10 + 1, stepsTaken);
            Random random = Utility.CreateRandom(seed);

            // 2. 模拟所有已知的、在天气判断之前的 RNG 消耗过程
            // a. 日期消耗
            int nextDayOfMonth = Game1.dayOfMonth + 1 > 28 ? 1 : Game1.dayOfMonth + 1;
            for (int i = 0; i < nextDayOfMonth; i++) random.Next();

            // b. 特色菜消耗
            string dishId;
            do { dishId = random.Next(194, 240).ToString(); } while (Utility.IsForbiddenDishOfTheDay(dishId));
            result.DishOfTheDayId = dishId;
            result.DishOfTheDayAmount = random.Next(1, 4 + ((random.NextDouble() < 0.08) ? 10 : 0));

            random.NextDouble();//不知名消耗

            // 遍历所有地点，模拟passTimeForObjects方法假人模特的消耗
            foreach (GameLocation location in Game1.locations)
            {
                foreach (KeyValuePair<Vector2, StardewValley.Object> objPair in location.netObjects.Pairs)
                {
                    var obj = objPair.Value;
                    // 跳过不消耗随机数条件
                    if (obj is Fence || obj is Furniture || obj.IsSprinkler()|| location == null)
                        continue;
                    // 手动处理Mannequin的特殊消耗
                    if (obj is Mannequin mannequin)
                    {
                        if (random.NextDouble() < 0.001) // 0.1%触发条件
                        {
                            DataLoader.Mannequins(Game1.content).TryGetValue(mannequin.ItemId, out var data);
                            if (data?.Cursed == true)
                            {
                                if (Game1.timeOfDay > Game1.getTrulyDarkTime(location))
                                {
                                    if (random.NextDouble() < 0.1) random.NextDouble(); // emitGhost
                                }
                                else if (random.NextDouble() < 0.66) random.NextDouble();
                                else random.Next(500, 4000); // 震动消耗
                            }
                        }
                    }
                    // 处理其他所有机器的通用消耗（包括假人和电话也都调用了Object.cs中的核心逻辑）
                    if (obj.heldObject.Value != null && obj.QualifiedItemId != "(BC)165")
                    {
                        var machineData = obj.GetMachineData();
                        var minutesUntilReady = obj.minutesUntilReady.Value - (int) Utility.CalculateMinutesUntilMorning(Game1.timeOfDay);
                        bool readyForHarvest = false;
                        if (minutesUntilReady <= 0 && (machineData == null || !machineData.OnlyCompleteOvernight || Game1.newDaySync.hasInstance()))
                        {
                            readyForHarvest = true;
                        }
                        // 机器工作特效消耗
                        if (machineData != null && !readyForHarvest && machineData.WorkingEffects != null)
                        {
                            if (random.NextDouble() < machineData.WorkingEffectChance)
                            {
                                if (obj is WoodChipper && location.farmers.Any() && Game1.random.NextDouble() < 0.35)
                                {
                                    for (int i = 0; i < 8; i++)
                                    {
                                        random.Next(-48, 0);
                                    }
                                }
                            }
                        }
                        // 非机器类普通消耗（33%概率）
                        else if (!readyForHarvest && random.NextDouble() < 0.33)
                        {
                            if (obj is WoodChipper && location.farmers.Any() && Game1.random.NextDouble() < 0.35)
                            {
                                for (int i = 0; i < 8; i++)
                                {
                                    random.Next(-48, 0);
                                }
                            }
                        }
                    }
                }
            }

            result.MailSenderName = "";
            // c. 好友邮件消耗
            if (Utility.TryGetRandom(Game1.player.friendshipData, out var whichFriend, out var friendship, random) &&
                random.NextBool((double)(friendship.Points / 250) * 0.1) &&
                Game1.player.spouse != whichFriend &&
                DataLoader.Mail(Game1.content).ContainsKey(whichFriend))
            {
                result.MailSenderName = whichFriend;
            }
            random.NextDouble();// dayupdate稻草人协会邮件消耗
            // dayupdate诅咒假人换衣服消耗
            if (Game1.player.shirtItem.Value != null && Game1.player.pantsItem.Value != null &&
                (
                 (Game1.player.currentLocation is FarmHouse) ||
                 (Game1.player.currentLocation is IslandFarmHouse) ||
                 (Game1.player.currentLocation is Shed))
                )
            {
                foreach (StardewValley.Object value in Game1.player.currentLocation.netObjects.Values)
                {
                    if (value is Mannequin mannequin)
                    {
                        DataLoader.Mannequins(Game1.content).TryGetValue(mannequin.ItemId, out var data);
                        if (data?.Cursed == true)
                        {
                            random.NextDouble();
                        }
                    }
                }
            }

            // d. 每日运气消耗
            result.DailyLuck = Math.Min(0.1, (double)random.Next(-100, 101) / 1000.0);
            // e. 天气转化消耗
            result.WillBeStormyDayAfterTomorrow = false;
            if (result.IsRainyDayAfterTomorrow)
            {
                WorldDate tomorrow = new WorldDate(Game1.Date);
                tomorrow.TotalDays += 1;
                Season season = dayAfterTomorrow.Season;
                bool isPastFirstSeason = (Game1.stats.DaysPlayed + 1) >= 28;
                bool isAfterDay2 = tomorrow.DayOfMonth > 2;

                if (season == Season.Summer)
                {
                    if (random.NextDouble() < 0.85) { result.WillBeStormyDayAfterTomorrow = true; }
                    else if (isPastFirstSeason && isAfterDay2 && random.NextDouble() < 0.25) { result.WillBeStormyDayAfterTomorrow = true; }
                }
                else if (season == Season.Spring || season == Season.Fall)
                {
                    if (isPastFirstSeason && isAfterDay2 && random.NextDouble() < 0.25) { result.WillBeStormyDayAfterTomorrow = true; }
                }
            }

            return result;
        }

        #endregion

        #region UI更新与显示

        private void UpdatePredictionsIfNeeded()
        {
            if (this.lastPredictedSteps != (int)Game1.stats.StepsTaken || this.lastPredictedDaysPlayed != Game1.stats.DaysPlayed)
            {
                this.lastPredictedSteps = (int)Game1.stats.StepsTaken;
                this.lastPredictedDaysPlayed = Game1.stats.DaysPlayed;
                this.currentPrediction = PredictNextDayOutcomes(this.lastPredictedSteps);
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

            if (this.needsNewTargetSearch || (this.targetSearchCriteriaMet && (int)Game1.stats.StepsTaken > this.targetSearchResultSteps))
            {
                this.targetSearchCriteriaMet = false;
                int startSteps = (int)Game1.stats.StepsTaken;
                for (int steps = 0; steps < this.Config.TargetStepsLimit; steps++)
                {
                    int futureSteps = startSteps + steps;
                    PredictionResult futurePrediction = PredictNextDayOutcomes(futureSteps);

                    bool luckMet = this.Config.TargetLuck == -0.101 || futurePrediction.DailyLuck >= this.Config.TargetLuck;
                    bool dishMet = string.IsNullOrEmpty(this.Config.TargetDish) || (this.targetDishIds.Contains(futurePrediction.DishOfTheDayId) && futurePrediction.DishOfTheDayAmount >= this.Config.TargetDishAmount);
                    bool gifterMet = string.IsNullOrEmpty(this.Config.TargetGifter) || this.targetGifterNames.Contains(futurePrediction.MailSenderName);
                    bool stormMet = !this.Config.WantStorm || futurePrediction.WillBeStormyDayAfterTomorrow;

                    if (luckMet && dishMet && gifterMet && stormMet)
                    {
                        this.targetSearchCriteriaMet = true;
                        this.targetSearchResultSteps = futureSteps;
                        break;
                    }
                }

                if (!this.targetSearchCriteriaMet)
                {
                    this.targetSearchResultSteps = startSteps + this.Config.TargetStepsLimit;
                }

                this.needsNewTargetSearch = false;
            }
        }

        private string BuildPredictionDisplayString()
        {
            List<string> lines = new List<string>();
            if (this.Config.DisplaySteps) lines.Add(this.Helper.Translation.Get("DisplaySteps") + ": " + (int)Game1.stats.StepsTaken);
            if (this.Config.DisplayLuck) lines.Add(this.Helper.Translation.Get("DisplayLuck") + ": " + this.currentPrediction.DailyLuck);
            if (this.Config.DisplayDish) { string dishName = TokenParser.ParseText(Game1.objectData[this.currentPrediction.DishOfTheDayId].DisplayName); lines.Add(this.Helper.Translation.Get("DisplayDish") + ": " + dishName + " (" + this.currentPrediction.DishOfTheDayAmount + ")"); }
            if (this.Config.DisplayGift) { string gifterName = string.IsNullOrEmpty(this.currentPrediction.MailSenderName) ? "无" : Game1.getCharacterFromName(this.currentPrediction.MailSenderName).displayName; lines.Add(this.Helper.Translation.Get("DisplayGift") + ": " + gifterName); }
            if (this.Config.DisplayWeather) 
            {
                if (this.futureRainyDaysDisplay.Any())
                {
                    lines.Add(this.Helper.Translation.Get("DisplayWeather") + ": " + string.Join(", ", this.futureRainyDaysDisplay));
                }
                else
                {
                    lines.Add("未来7天无雨天");
                }
            }
            if (this.Config.DisplayThunder)
            {
                string label = "后天雷雨预测";
                string resultText;
                if (this.currentPrediction.IsRainyDayAfterTomorrow)
                {
                    resultText = this.currentPrediction.WillBeStormyDayAfterTomorrow ? "是" : "否";
                }
                else
                {
                    resultText = "非雨天";
                }
                lines.Add(label + ": " + resultText);
            }

            if (this.targetSearchResultSteps != -1)
            {
                lines.Add("");
                if (this.targetSearchCriteriaMet) lines.Add("目标达成所需步数: " + this.targetSearchResultSteps);
                else lines.Add("在 " + this.targetSearchResultSteps + " 步内未找到目标");
                lines.Add("搜索条件:");
                if (this.Config.TargetLuck != -0.101) lines.Add("- 运气: " + this.Config.TargetLuck);
                if (!string.IsNullOrEmpty(this.Config.TargetDish)) lines.Add("- 菜品: " + this.Config.TargetDish);
                if (!string.IsNullOrEmpty(this.Config.TargetGifter)) lines.Add("- 送礼人: " + this.Config.TargetGifter);
                if (this.Config.WantStorm) lines.Add("- 需要雷雨");
            }
            return string.Join(Environment.NewLine, lines);
        }

        #endregion

        #region 辅助方法

        private void UpdateConfigDerivedVariables()
        {
            this.targetDishIds = this.Config.TargetDish.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).Select(id => id.StartsWith("(O)") ? id.Replace("(O)", "").Trim() : id).ToArray();
            this.targetGifterNames = this.Config.TargetGifter.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToArray();
        }

        #endregion
    }
}