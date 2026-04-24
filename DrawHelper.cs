using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Menus;

namespace StepsTakenOnScreen
{

    // =====================================================
    //  DrawHelper 静态工具类
    // =====================================================
    internal static class DrawHelper
    {
        public static float GetSpaceWidth(SpriteFont font)
        {
            return font.MeasureString("A B").X - font.MeasureString("AB").X;
        }

        // ★ 修复：新增接受 List<IFormattedText> 的重载
        public static Vector2 DrawHoverBox(
            SpriteBatch spriteBatch,
            List<IFormattedText> textBlocks,
            Vector2 position,
            float wrapWidth)
        {
            // 先用偏移位置测量总尺寸
            Vector2 labelSize = spriteBatch.DrawTextBlock(
                Game1.smallFont,
                textBlocks,
                position + new Vector2(20f),
                wrapWidth - 40f  // 减去左右padding
            );

            // 画背景框
            IClickableMenu.drawTextureBox(
                spriteBatch,
                Game1.menuTexture,
                new Rectangle(0, 256, 60, 60),
                (int)position.X,
                (int)position.Y,
                (int)labelSize.X + 40,   // 左右各20px padding
                (int)labelSize.Y + 27,
                Color.White
            );

            // 再画一次文字（覆盖在框上层）
            spriteBatch.DrawTextBlock(
                Game1.smallFont,
                textBlocks,
                position + new Vector2(20f),
                wrapWidth - 40f
            );

            return labelSize + new Vector2(40f, 27f);
        }

        // 保留原有的 string 版本重载
        public static Vector2 DrawHoverBox(
            SpriteBatch spriteBatch,
            string label,
            in Vector2 position,
            float wrapWidth)
        {
            Vector2 labelSize = spriteBatch.DrawTextBlock(
                Game1.smallFont, label, position + new Vector2(20f), wrapWidth);

            IClickableMenu.drawTextureBox(
                spriteBatch,
                Game1.menuTexture,
                new Rectangle(0, 256, 60, 60),
                (int)position.X,
                (int)position.Y,
                (int)labelSize.X + 27 + 20,
                (int)labelSize.Y + 27,
                Color.White);

            spriteBatch.DrawTextBlock(
                Game1.smallFont, label, position + new Vector2(20f), wrapWidth);

            return labelSize + new Vector2(27f);
        }

        public static Vector2 DrawTextBlock(
            this SpriteBatch batch,
            SpriteFont font,
            string text,
            Vector2 position,
            float wrapWidth,
            Color? color = null,
            bool bold = false,
            float scale = 1f)
        {
            return batch.DrawTextBlock(font, new IFormattedText[]
            {
                new FormattedText(text, color, bold)
            }, position, wrapWidth, scale);
        }

        public static Vector2 DrawTextBlock(
            this SpriteBatch batch,
            SpriteFont font,
            IEnumerable<IFormattedText> text,
            Vector2 position,
            float wrapWidth,
            float scale = 1f)
        {
            if (text == null)
                return Vector2.Zero;

            float xOffset = 0f;
            float yOffset = 0f;
            float lineHeight = font.MeasureString("ABC").Y * scale;
            float spaceWidth = GetSpaceWidth(font) * scale;
            float blockWidth = 0f;
            float blockHeight = lineHeight;

            foreach (IFormattedText snippet in text)
            {
                if (snippet?.Text == null) continue;

                // ★ 修复：同时处理 \n 和 Environment.NewLine
                string normalizedText = snippet.Text.Replace("\r\n", "\n").Replace("\r", "\n");

                bool startSpace = normalizedText.StartsWith(" ");
                bool endSpace = normalizedText.EndsWith(" ");

                // ★ 修复：先按 \n 分行，再按空格分词
                string[] lines = normalizedText.Split('\n');
                IList<string> words = new List<string>();

                for (int lineIdx = 0; lineIdx < lines.Length; lineIdx++)
                {
                    if (lineIdx > 0)
                    {
                        // 插入换行符标记
                        words.Add("\n");
                    }

                    string line = lines[lineIdx];
                    if (string.IsNullOrEmpty(line)) continue;

                    string[] rawWords = line.Split(
                        new char[] { ' ' },
                        StringSplitOptions.RemoveEmptyEntries);

                    for (int i = 0; i < rawWords.Length; i++)
                    {
                        string word = rawWords[i];
                        if (startSpace && lineIdx == 0 && i == 0)
                            word = " " + word;
                        if (endSpace && lineIdx == lines.Length - 1 && i == rawWords.Length - 1)
                            word += " ";
                        words.Add(word);
                    }
                }

                bool isFirstOfLine = true;

                foreach (string word in words)
                {
                    // 处理换行
                    if (word == "\n")
                    {
                        xOffset = 0f;
                        yOffset += lineHeight;
                        blockHeight += lineHeight;
                        isFirstOfLine = true;
                        continue;
                    }

                    float wordWidth = font.MeasureString(word).X * scale;
                    float prependSpace = isFirstOfLine ? 0f : spaceWidth;

                    // 超宽自动换行
                    if (!isFirstOfLine && xOffset + prependSpace + wordWidth > wrapWidth)
                    {
                        xOffset = 0f;
                        yOffset += lineHeight;
                        blockHeight += lineHeight;
                        isFirstOfLine = true;
                        prependSpace = 0f;
                    }

                    Vector2 wordPosition = new Vector2(
                        position.X + xOffset + prependSpace,
                        position.Y + yOffset);

                    if (snippet.Bold)
                    {
                        Utility.drawBoldText(
                            batch, word, font, wordPosition,
                            snippet.Color ?? Color.Black, scale);
                    }
                    else
                    {
                        batch.DrawString(
                            font, word, wordPosition,
                            snippet.Color ?? Color.Black,
                            0f, Vector2.Zero, scale,
                            SpriteEffects.None, 1f);
                    }

                    float currentRight = xOffset + prependSpace + wordWidth;
                    if (currentRight > blockWidth)
                        blockWidth = currentRight;

                    xOffset += prependSpace + wordWidth;
                    isFirstOfLine = false;
                }
            }

            return new Vector2(blockWidth, blockHeight);
        }
    }
}