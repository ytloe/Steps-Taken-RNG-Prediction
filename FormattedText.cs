using Microsoft.Xna.Framework;

namespace StepsTakenOnScreen;

internal struct FormattedText : IFormattedText
{
	public string Text { get; }

	public Color? Color { get; }

	public bool Bold { get; }

	public FormattedText(string text, Color? color = null, bool bold = false)
	{
		this.Text = text;
		this.Color = color;
		this.Bold = bold;
	}
}
