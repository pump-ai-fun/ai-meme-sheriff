using Figgle;
using Figgle.Fonts;
using System.Text;

namespace PumpSheriff
{
    /// <summary>
    /// Console-based UI utilities for titles, chat bubbles, evaluations and ASCII animations.
    /// </summary>
    internal class UX
    {
        public static int LeftPadding { get; set; } = 12;
        public static string Conversation { get; set; } = string.Empty;
        public static int ConversationMaxLength { get; set; } = 1500;
        public static int ConversationLineLength { get; set; } = 140;

        /// <summary>
        /// Prepares the console window and buffer for the dashboard.
        /// </summary>
        public static void Initialize()
        {
            if (Console.LargestWindowWidth < 2000 || Console.LargestWindowHeight < 1000)
            {
                Console.SetBufferSize(2000, 1000);
            }
            Console.SetWindowSize(Console.LargestWindowWidth, Console.LargestWindowHeight);
            Console.SetWindowPosition(Console.CursorLeft, Console.CursorTop);
            Console.OutputEncoding = Encoding.UTF8;
            Console.CursorVisible = false;
            Console.BackgroundColor = ConsoleColor.Black;
            Console.ForegroundColor = ConsoleColor.White;
            Console.Clear();
        }

        /// <summary>
        /// Renders an ASCII animation frame sequence.
        /// </summary>
        public static void RenderAnimation(Animations animation, string characterId, string typeId = "", int index = -1)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            if (string.IsNullOrEmpty(typeId))
                typeId = animation.Frames[characterId].Keys.ElementAt(new Random().Next(0, animation.Frames[characterId].Count));
            if (index < 0) // fetch random animation of the same type
                index = new Random().Next(0, animation.Frames[characterId][typeId].Count);
            foreach (var frame in animation.Frames[characterId][typeId][index])
            {
                Console.SetCursorPosition(0, 0);  // TODO: filter during load from file and not every render...
                string leftFrame = frame.Substring(animation.AnimWidthCharacters * 4);
                string rightFrame = leftFrame.Substring(0, leftFrame.Length - animation.AnimWidthCharacters * 14);
                rightFrame = rightFrame.Replace($"\n{new string(' ', 35)}", "\n");
                rightFrame = rightFrame.Replace($"{new string(' ', 35)}\n", "\n");
                Console.Write(rightFrame);
                Thread.Sleep(150);
            }
            Console.ForegroundColor = ConsoleColor.White;
        }

        public static void WriteTitle(string text, int scale = 2, int leftPadding = 240)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.SetCursorPosition(0, 0);
            Write(text, scale, FiggleFonts.Georgia11, leftPadding);
            Console.ForegroundColor = ConsoleColor.White;
        }

        public static void WriteSection(string text, int scale, int leftPadding, int topPadding)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.SetCursorPosition(0, topPadding);
            Write(text, scale, FiggleFonts.Ivrit, leftPadding);
            Console.ForegroundColor = ConsoleColor.White;
        }

        public static void WriteSubtitle(string text, int scale = 1, int leftPadding = 260)
        {
            Console.SetCursorPosition(0, 22);
            Write(text, scale, FiggleFonts.Doom, leftPadding);
        }

        public static void WriteDisclaimer(string text, int scale = 0, int leftPadding = 275, int topPadding = 160)
        {
            Console.SetCursorPosition(0, topPadding);
            Write(text, scale, FiggleFonts.BroadwayKB, leftPadding);
        }

        public static void WriteGoals(string text, int scale = 0, int leftPadding = 226, int topPadding = 32, ConsoleColor color = ConsoleColor.White)
        {
            Console.ForegroundColor = color;
            Console.SetCursorPosition(0, topPadding);
            Write(text, scale, FiggleFonts.Ivrit, leftPadding);
            Console.ForegroundColor = ConsoleColor.White;
        }

        public static void WriteCoinAddress(string text, int scale = 0, int leftPadding = 226, int topPadding = 88, ConsoleColor color = ConsoleColor.Yellow)
        {
            Console.ForegroundColor = color;
            Console.SetCursorPosition(leftPadding, topPadding);
            Write(text, scale, FiggleFonts.Ivrit, leftPadding);
            Console.ForegroundColor = ConsoleColor.White;
        }

        public static void WriteReasoning(string text, bool reset = false, int scale = 0, int leftPadding = 236, int topPadding = 53)
        {
            if (reset)
            {
                Conversation = string.Empty;
                string emptyLine = new string(' ', Console.BufferWidth - leftPadding);
                for (int i = 0; i < 34; i++)
                {
                    Console.SetCursorPosition(leftPadding, topPadding + i);
                    Console.Write(emptyLine);
                }
            }

            if (Conversation.Length > ConversationMaxLength)
            {
                if (!Conversation.EndsWith(" ..."))
                    Conversation += " ...";
            }
            else
                Conversation += text.Replace("\n", "").Replace("  ", " ").Replace("  ", " ");

            var chunks = Enumerable.Range(0, (Conversation.Length + ConversationLineLength - 1) / ConversationLineLength)
                                   .Select(i => Conversation.Substring(i * ConversationLineLength,
                                                Math.Min(ConversationLineLength, Conversation.Length - i * ConversationLineLength))).ToList();
            for (int c = 0; c < chunks.Count; c++)
            {
                Console.SetCursorPosition(leftPadding, topPadding + c * 3);
                Write(chunks[c], scale, FiggleFonts.ThreePoint, leftPadding);
            }
        }

        public static void WriteReply(string text, int scale = 0, int leftPadding = 230, int topPadding = 50)
        {
            string emptyLine = new string(' ', 620 - leftPadding);
            for (int i = 0; i < 32; i++)
            {
                Console.SetCursorPosition(leftPadding, topPadding + i);
                Console.Write(emptyLine);
            }

            var replyLineLength = (int)(ConversationLineLength / 3);
            var chunks = Enumerable.Range(0, (text.Length + replyLineLength - 1) / replyLineLength)
                                   .Select(i => text.Substring(i * replyLineLength,
                                                Math.Min(replyLineLength, text.Length - i * replyLineLength))).ToList();
            for (int c = 0; c < chunks.Count; c++)
            {
                Console.SetCursorPosition(leftPadding, topPadding + c * 8);
                Write(chunks[c], scale, FiggleFonts.NancyJ, leftPadding);
            }
        }

        public static void WriteEvaluation(string text, int scale = 0, int leftPadding = 230, int topPadding = 96)
        {
            string emptyLine = new string(' ', 622 - leftPadding);
            for (int i = 0; i < 35; i++)
            {
                Console.SetCursorPosition(leftPadding, topPadding + i);
                Console.Write(emptyLine);
            }

            var replyLineLength = (int)(ConversationLineLength / 2.5);
            var chunks = Enumerable.Range(0, (text.Length + replyLineLength - 1) / replyLineLength)
                                   .Select(i => text.Substring(i * replyLineLength,
                                                Math.Min(replyLineLength, text.Length - i * replyLineLength))).ToList();
            for (int c = 0; c < chunks.Count; c++)
            {
                Console.SetCursorPosition(leftPadding, topPadding + c * 6);
                Write(chunks[c], scale, FiggleFonts.FourMax, leftPadding);
            }
        }

        private static void Write(string text, int scale = 5, FiggleFont? font = null, int leftPadding = 0)
        {
            if (scale < 1) scale = 1;
            Console.OutputEncoding = Encoding.UTF8;

            font ??= FiggleFonts.Standard;

            // 1) Render base FIGlet
            var baseArt = font.Render(text).TrimEnd('\n', '\r');

            // 2) Scale it
            if (scale > 0)
            {
                baseArt = ScaleAsciiBlock(baseArt, scale);
            }

            // 3) Print
            var allLines = baseArt.Replace("\r", "").Split('\n');
            int startTopPos = Console.CursorTop;
            for (int i = 0; i < allLines.Length; i++)
            {
                Console.SetCursorPosition(leftPadding, startTopPos + i);
                Console.Write(allLines[i]);
            }
        }

        /// <summary>Nearest-neighbor scale of a multiline ASCII block.</summary>
        private static string ScaleAsciiBlock(string ascii, int scale, int leftPadding = 0)
        {
            var lines = ascii.Replace("\r", "").Split('\n');
            var sb = new StringBuilder(lines.Length * scale * ((lines.Length > 0 ? lines[0].Length : 0) * scale + 1));

            foreach (var line in lines)
            {
                // Prebuild the horizontally scaled line
                var hsb = new StringBuilder(line.Length * scale);
                foreach (var ch in line)
                    hsb.Append(new string(ch, scale));

                var scaledLine = hsb.ToString();
                // Repeat vertically
                for (int i = 0; i < scale; i++)
                    sb.AppendLine(new string(' ', leftPadding + LeftPadding) + scaledLine);
            }
            return sb.ToString();
        }
    }
}
