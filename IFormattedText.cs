using Microsoft.Xna.Framework;

namespace StepsTakenOnScreen;

internal interface IFormattedText
{
	Color? Color { get; }

	string Text { get; }

	bool Bold { get; }
}
