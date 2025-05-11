using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Menus;

namespace StepsTakenOnScreen;

internal static class DrawHelper
{
	public static float GetSpaceWidth(SpriteFont font)
	{
		return font.MeasureString("A B").X - font.MeasureString("AB").X;
	}

	public static Vector2 DrawHoverBox(SpriteBatch spriteBatch, string label, in Vector2 position, float wrapWidth)
	{
		Vector2 labelSize = spriteBatch.DrawTextBlock(Game1.smallFont, label, position + new Vector2(20f), wrapWidth);
		IClickableMenu.drawTextureBox(spriteBatch, Game1.menuTexture, new Rectangle(0, 256, 60, 60), (int)position.X, (int)position.Y, (int)labelSize.X + 27 + 20, (int)labelSize.Y + 27, Color.White);
		spriteBatch.DrawTextBlock(Game1.smallFont, label, position + new Vector2(20f), wrapWidth);
		return labelSize + new Vector2(27f);
	}

	public static Vector2 DrawTextBlock(this SpriteBatch batch, SpriteFont font, string text, Vector2 position, float wrapWidth, Color? color = null, bool bold = false, float scale = 1f)
	{
		return batch.DrawTextBlock(font, new IFormattedText[1]
		{
			new FormattedText(text, color, bold)
		}, position, wrapWidth, scale);
	}

	public static Vector2 DrawTextBlock(this SpriteBatch batch, SpriteFont font, IEnumerable<IFormattedText> text, Vector2 position, float wrapWidth, float scale = 1f)
	{
		if (text == null)
		{
			return new Vector2(0f, 0f);
		}
		float xOffset = 0f;
		float yOffset = 0f;
		float lineHeight = font.MeasureString("ABC").Y * scale;
		float spaceWidth = DrawHelper.GetSpaceWidth(font) * scale;
		float blockWidth = 0f;
		float blockHeight = lineHeight;
		foreach (IFormattedText snippet in text)
		{
			if (snippet?.Text == null)
			{
				continue;
			}
			bool startSpace = snippet.Text.StartsWith(" ");
			bool endSpace = snippet.Text.EndsWith(" ");
			IList<string> words = new List<string>();
			string[] rawWords = snippet.Text.Split(new char[1] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
			int i = 0;
			for (int last = rawWords.Length - 1; i <= last; i++)
			{
				string word = rawWords[i];
				if (startSpace && i == 0)
				{
					word = " " + word;
				}
				if (endSpace && i == last)
				{
					word += " ";
				}
				string wordPart = word;
				int newlineIndex;
				while ((newlineIndex = wordPart.IndexOf(Environment.NewLine, StringComparison.InvariantCulture)) >= 0)
				{
					if (newlineIndex == 0)
					{
						words.Add(Environment.NewLine);
						wordPart = wordPart.Substring(Environment.NewLine.Length);
					}
					else if (newlineIndex > 0)
					{
						words.Add(wordPart.Substring(0, newlineIndex));
						words.Add(Environment.NewLine);
						wordPart = wordPart.Substring(newlineIndex + Environment.NewLine.Length);
					}
				}
				if (wordPart.Length > 0)
				{
					words.Add(wordPart);
				}
			}
			bool isFirstOfLine = true;
			foreach (string word2 in words)
			{
				float wordWidth = font.MeasureString(word2).X * scale;
				float prependSpace = (isFirstOfLine ? 0f : spaceWidth);
				if (word2 == Environment.NewLine || (wordWidth + xOffset + prependSpace > wrapWidth && (int)xOffset != 0))
				{
					xOffset = 0f;
					yOffset += lineHeight;
					blockHeight += lineHeight;
					isFirstOfLine = true;
				}
				if (!(word2 == Environment.NewLine))
				{
					Vector2 wordPosition = new Vector2(position.X + xOffset + prependSpace, position.Y + yOffset);
					if (snippet.Bold)
					{
						Utility.drawBoldText(batch, word2, font, wordPosition, snippet.Color ?? Color.Black, scale);
					}
					else
					{
						batch.DrawString(font, word2, wordPosition, snippet.Color ?? Color.Black, 0f, Vector2.Zero, scale, SpriteEffects.None, 1f);
					}
					if (xOffset + wordWidth + prependSpace > blockWidth)
					{
						blockWidth = xOffset + wordWidth + prependSpace;
					}
					xOffset += wordWidth + prependSpace;
					isFirstOfLine = false;
				}
			}
		}
		return new Vector2(blockWidth, blockHeight);
	}
}
