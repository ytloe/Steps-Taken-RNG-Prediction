﻿using StardewModdingAPI;
using StardewModdingAPI.Utilities;
using Microsoft.Xna.Framework;

namespace StepsTakenOnScreen;

internal class ModConfig
{
	public bool DisplaySteps { get; set; } = true;

	public bool DisplayLuck { get; set; } = true;

	public bool DisplayGift { get; set; } = true;

	public bool DisplayDish { get; set; } = true;

	public bool DisplayThander { get; set; } = true;

    public Point PositionOffset { get; set; } = new Point(300, 0);

    public double TargetLuck { get; set; } = -1.0;

	public string TargetGifter { get; set; } = "";

	public string TargetDish { get; set; } = "";

	public int TargetDishAmount { get; set; } = 0;

	public int TargetStepsLimit { get; set; } = 1000;

    public bool ShowPredictionBox { get; set; } = true;
    public KeybindList ToggleKey { get; set; } = new KeybindList(
        new Keybind(SButton.P, SButton.LeftControl)
    );
}
