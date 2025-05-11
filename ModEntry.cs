using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Extensions;
using StardewValley.GameData;
using StardewValley.Objects;
using StardewValley.TokenizableStrings;


namespace StepsTakenOnScreen;

public class ModEntry : Mod
{
	private ModConfig Config;

	private double dailyLuck;

	private string dishOfTheDay;

	private int dishOfTheDayAmount;

	private string mailPerson;

	private int lastStepsTakenCalculation = -1;

	private int daysPlayedCalculation = -1;

	private int targetStepsCalculation = -1;

	private int targetDay = -1;

	private string labelHorizontalOffset;

	private string labelVerticalOffset;

	private string labelStepsTaken;

	private string labelDailyLuck;

	private string labelDish;

	private string labelGift;

	//private string labelThander;

	private string labelSearch;

	private string[] dishValues;

	private string[] giftValues;

	private bool locationsChecked = false;

	private int extraCalls = 0;

	private bool targetFound = false;

	private bool predictionBoxVisible = true;

	public override void Entry(IModHelper helper)
	{
		helper.Events.Input.ButtonPressed += OnButtonPressed;
		helper.Events.Input.ButtonReleased += OnButtonReleased;
		helper.Events.Display.RenderedHud += OnRenderedHud;
		helper.Events.GameLoop.DayStarted += OnDayStarted;
		this.Config = this.Helper.ReadConfig<ModConfig>();
		this.dishValues = this.Config.TargetDish.Split(',');
		this.giftValues = this.Config.TargetGifter.Split(',');
		helper.Events.GameLoop.GameLaunched += OnGameLaunched;
		helper.Events.Input.ButtonsChanged += OnButtonsChanged;
    }

	private void OnGameLaunched(object sender, GameLaunchedEventArgs e)
	{
		var configMenu = this.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
		if (configMenu == null) return;

		configMenu.Register(
				mod: ModManifest,
				reset: () => Config = new ModConfig(),
				save: () => Helper.WriteConfig(Config)
		);

		// 位置偏移
		configMenu.AddNumberOption(
		mod: ModManifest,
		name: () => this.Helper.Translation.Get("Config.HorizontalOffset"),
		tooltip: () => this.Helper.Translation.Get("Config.HorizontalOffset.Tooltip"),
		getValue: () => Config.PositionOffset.X,
		setValue: value => Config.PositionOffset = new Point(value, Config.PositionOffset.Y)
);

		configMenu.AddNumberOption(
				mod: ModManifest,
				name: () => this.Helper.Translation.Get("Config.VerticalOffset"),
				tooltip: () => this.Helper.Translation.Get("Config.VerticalOffset.Tooltip"),
				getValue: () => Config.PositionOffset.Y,
				setValue: value => Config.PositionOffset = new Point(Config.PositionOffset.X, value)
		);

		// 显示开关
		configMenu.AddKeybindList(
			mod: ModManifest,
            name: () => this.Helper.Translation.Get("Config.ModEnabled"),
            tooltip: () => this.Helper.Translation.Get("Config.ModEnabled.Tooltip"),
            //name: () => "开关预测框快捷键",
			//tooltip: () => "控制预测框显示的快捷键组合",
			getValue: () => Config.ToggleKey,
			setValue: value => Config.ToggleKey = value
		);

		// 各预测项开关
		configMenu.AddBoolOption(
				mod: ModManifest,
                name: () => this.Helper.Translation.Get("Config.DisplaySteps"),
                //name: () => "显示步数统计",
				getValue: () => Config.DisplaySteps,
				setValue: value => Config.DisplaySteps = value
		);

		configMenu.AddBoolOption(
				mod: ModManifest,
                name: () => this.Helper.Translation.Get("Config.DisplayLuck"),
                //name: () => "显示运气预测",
				getValue: () => Config.DisplayLuck,
				setValue: value => Config.DisplayLuck = value
		);

		configMenu.AddBoolOption(
				mod: ModManifest,
                name: () => this.Helper.Translation.Get("Config.DisplayGift"),
                //name: () => "显示邮件预测",
                getValue: () => Config.DisplayGift,
				setValue: value => Config.DisplayGift = value
		);

		configMenu.AddBoolOption(
				mod: ModManifest,
                name: () => this.Helper.Translation.Get("Config.DisplayDish"),
                //name: () => "显示特色菜预测",
                getValue: () => Config.DisplayDish,
				setValue: value => Config.DisplayDish = value
		);
        //configMenu.AddBoolOption(
        //        mod: ModManifest,
        //        name: () => this.Helper.Translation.Get("Config.DisplayThander"),
        //        //name: () => "显示雷雨预测",
        //        getValue: () => Config.DisplayThander,
        //        setValue: value => Config.DisplayThander = value
        //);

    }
    private void OnButtonsChanged(object sender, ButtonsChangedEventArgs e)
	{
		if (this.Config.ToggleKey.JustPressed())
		{
			predictionBoxVisible = !predictionBoxVisible;
			Monitor.Log($"预测框显示状态已切换: {predictionBoxVisible}");
		}
	}

	private void OnDayStarted(object sender, DayStartedEventArgs e)
	{
		this.locationsChecked = false;
	}

	private void OnButtonPressed(object sender, ButtonPressedEventArgs e)
	{
		if (Context.IsWorldReady && e.Button == SButton.F5)
		{
			this.Config = base.Helper.ReadConfig<ModConfig>();
			base.Monitor.Log("Config reloaded", LogLevel.Info);
			this.dishValues = this.Config.TargetDish.Split(',');
			this.giftValues = this.Config.TargetGifter.Split(',');
			this.targetStepsCalculation = 0;
		}
	}

	private void OnButtonReleased(object sender, ButtonReleasedEventArgs e)
	{
		if (Context.IsWorldReady && (e.Button.IsActionButton() || e.Button.IsUseToolButton()))
		{
			Vector2 tile = e.Cursor.GrabTile;
			StardewValley.Object obj = Game1.currentLocation.getObjectAtTile((int)tile.X, (int)tile.Y);
			if (obj != null && obj.MinutesUntilReady > 0)
			{
				this.locationsChecked = false;
			}
		}
	}

	private void OnRenderedHud(object sender, RenderedHudEventArgs e)
	{
		if (!predictionBoxVisible) return;
		SpriteBatch spriteBatch = Game1.spriteBatch;
		bool furnitureChanged = false;
		if (!this.locationsChecked)
		{
			int oldExtraCalls = this.extraCalls;
			this.CheckLocations();
			furnitureChanged = this.extraCalls != oldExtraCalls;
		}
		if ((this.Config.DisplayDish || this.Config.DisplayGift || this.Config.DisplayLuck) && (furnitureChanged || this.lastStepsTakenCalculation != Game1.stats.StepsTaken || this.daysPlayedCalculation != Game1.stats.DaysPlayed))
		{
			this.lastStepsTakenCalculation = (int)Game1.stats.StepsTaken;
			this.daysPlayedCalculation = (int)Game1.stats.DaysPlayed;
			this.CalculatePredictions(this.lastStepsTakenCalculation, out this.dailyLuck, out this.dishOfTheDay, out this.dishOfTheDayAmount, out this.mailPerson);
		}
		string str = "";
		if (this.Config.DisplaySteps)
		{
			str = this.InsertLine(str, this.GetStepsTaken());
		}
		if (this.Config.DisplayLuck)
		{
			str = this.InsertLine(str, this.GetLuck());
		}
		if (this.Config.DisplayDish)
		{
			str = this.InsertLine(str, this.GetDishOfTheDay());
		}
		if (this.Config.DisplayGift)
		{
			str = this.InsertLine(str, this.GetMailPerson());
		}
		//if (this.Config.DisplayThander)
		//{
  //          str = this.InsertLine(str, this.GetThander());
  //      }




		if (this.Config.TargetLuck != -1.0 || this.Config.TargetGifter != "" || this.Config.TargetDish != "")
		{
			if (furnitureChanged || Game1.stats.Get("stepsTaken") > this.targetStepsCalculation || Game1.stats.Get("daysPlayed") != this.targetDay)
			{
				this.targetFound = false;
				this.targetDay = (int)Game1.stats.Get("daysPlayed");
				for (int steps = 0; steps < this.Config.TargetStepsLimit; steps++)
				{
					this.targetStepsCalculation = steps + (int)Game1.stats.Get("stepsTaken");
					this.CalculatePredictions(this.targetStepsCalculation, out var luck, out var dish, out var dishAmount, out var person);
					if ((this.Config.TargetLuck == -1.0 || luck >= this.Config.TargetLuck) && (this.Config.TargetDish == "" || (this.dishValues.Contains(this.GetDishOfTheDayValue(dish)) && dishAmount >= this.Config.TargetDishAmount)) && (this.Config.TargetGifter == "" || this.giftValues.Contains(person)))
					{
						this.targetFound = true;
						break;
					}
				}
			}
			str = this.InsertLine(str, "");
			str = ((!this.targetFound) ? this.InsertLine(str, "Criteria not met after searching to step count: " + this.targetStepsCalculation) : this.InsertLine(str, "Steps required to hit target: " + this.targetStepsCalculation));
			str = this.InsertLine(str, "Criteria:");
			if (this.Config.TargetLuck != -1.0)
			{
				str = this.InsertLine(str, "Luck: " + this.Config.TargetLuck);
			}
			if (this.Config.TargetDish != "")
			{
				str = this.InsertLine(str, "Dish: " + this.Config.TargetDish.ToString());
			}
			if (this.Config.TargetGifter != "")
			{
				str = this.InsertLine(str, "Gifter: " + this.Config.TargetGifter.ToString());
			}
		}
		if (str != "")
		{
			DrawHelper.DrawHoverBox(
	spriteBatch: e.SpriteBatch,
	label: str,
	position: new Vector2(Config.PositionOffset.X, Config.PositionOffset.Y),
	wrapWidth: Game1.viewport.Width
);
		}
	}

	private string GetStepsTaken()
	{
        this.labelStepsTaken = this.Helper.Translation.Get("DisplaySteps");
        return this.labelStepsTaken + ": " + Game1.stats.Get("stepsTaken");
	}

	private string GetLuck()
	{
        this.labelDailyLuck = this.Helper.Translation.Get("DisplayLuck");
        return this.labelDailyLuck + ": " + this.dailyLuck;
	}

	private string GetDishOfTheDay()
	{
        this.labelDish = this.Helper.Translation.Get("DisplayDish");
        return this.labelDish + ": " + this.GetDishOfTheDayValue(this.dishOfTheDay) + " (" + this.dishOfTheDayAmount + ")";
	}

	private string GetDishOfTheDayValue(string dish)
	{
		return TokenParser.ParseText(Game1.objectData[dish].DisplayName);
	}

	private string GetMailPerson()
	{
        this.labelGift = this.Helper.Translation.Get("DisplayGift");
        return this.labelGift + ": " + this.GetMailPersonValue(this.mailPerson);
    }

    private string GetMailPersonValue(string who)
    {
        if (who != "") return Game1.getCharacterFromName(who).displayName;
		return "";
    }
	//private string GetThander() 
	//{
 //       this.labelThander = this.Helper.Translation.Get("DisplayThander");
 //       return this.labelThander + ": "+ "";//todo 返回后天是否为雷雨
 //   }


    private void CheckLocations()
	{
		if (this.locationsChecked)
		{
			return;
		}
		this.locationsChecked = true;
		this.extraCalls = 0;
		int minutesUntilMorning = Utility.CalculateMinutesUntilMorning(Game1.timeOfDay);
		foreach (GameLocation location in Game1.locations)
		{
			foreach (KeyValuePair<Vector2, StardewValley.Object> pair2 in location.objects.Pairs)
			{
				StardewValley.Object item = pair2.Value;
				if (item.heldObject.Value != null && !item.name.Contains("Table") && (!item.bigCraftable.Value || item.ParentSheetIndex != 165) && (!item.name.Equals("Bee House") || location.IsOutdoors) && item.MinutesUntilReady - minutesUntilMorning > 0)
				{
					this.extraCalls++;
				}
			}
		}
	}

	private void OvernightLightning(Random random)
	{
		int numberOfLoops = (2300 - Game1.timeOfDay) / 100;
		for (int i = 1; i <= numberOfLoops; i++)
		{
		}
	}

	private void PerformLightningUpdate(int time_of_day, Random game1random)
	{
	}

	private void CalculatePredictions(int steps, out double dailyLuck, out string dishOfTheDay, out int dishOfTheDayAmount, out string mailPerson)
	{
		this.CheckLocations();
		base.Monitor.Log(this.extraCalls.ToString());
		int season = 1;
		switch (Game1.currentSeason)
		{
			case "spring":
				season = 1;
				break;
			case "summer":
				season = 2;
				break;
			case "fall":
				season = 3;
				break;
			case "winter":
				season = 4;
				break;
		}
		int dayOfMonth = Game1.dayOfMonth + 1;
		if (dayOfMonth == 29)
		{
			dayOfMonth = 1;
			season++;
			if (season == 5)
			{
				season = 1;
			}
		}
		int daysPlayed = (int)(Game1.stats.DaysPlayed + 1);
		Random random = Utility.CreateRandom(Utility.CreateRandomSeed((int)Game1.uniqueIDForThisGame / 100, daysPlayed * 10 + 1, steps));
		for (int index = 0; index < dayOfMonth; index++)
		{
			random.Next();//准确模拟用，跳过天数的无用随机数
        }
		do//开始预测酒吧特色菜
		{
			dishOfTheDay = random.Next(194, 240).ToString();
		}
		while (Utility.IsForbiddenDishOfTheDay(dishOfTheDay));
		dishOfTheDayAmount = random.Next(1, 4 + ((random.NextDouble() < 0.08) ? 10 : 0));
		random.NextDouble();//准确模拟用，跳过邮件的无用随机数
		for (int i = 0; i < this.extraCalls; i++)
		{
			random.Next();
		}
		mailPerson = "";//开始预测邮件送礼人
		if (Utility.TryGetRandom(Game1.player.friendshipData, out var whichFriend, out var friendship, random) && 
			random.NextBool((double)(friendship.Points / 250) * 0.1) && 
			Game1.player.spouse != whichFriend && 
			DataLoader.Mail(Game1.content).ContainsKey(whichFriend))
		{
			mailPerson = whichFriend;
		}
		random.NextDouble();//准确模拟用，跳过无用随机数
        foreach (StardewValley.Object value in Utility.getHomeOfFarmer(Game1.player).netObjects.Values)//开始预测每日运气
		{
			if (value is Mannequin mannequin)
			{
				MannequinData data = null;
				if (data == null && !DataLoader.Mannequins(Game1.content).TryGetValue(mannequin.ItemId, out data))
				{
					data = null;
				}
				if (data.Cursed && random.NextDouble() < 0.005 && mannequin.swappedWithFarmerTonight.Value)
				{
				}
			}
		}
		dailyLuck = (double)random.Next(-100, 101) / 1000.0;
	}

	private string InsertLine(string str, string newStr)
	{
		if (str == "")
		{
			return newStr;
		}
		return str + "\r\n" + newStr;
	}
}
