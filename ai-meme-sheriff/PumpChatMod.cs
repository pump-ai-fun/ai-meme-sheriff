using AIMemeSherif;
using Microsoft.Playwright;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace AIMemeSherif
{
    internal class ChatConfig
    {
        public List<string> BannedKeywords { get; set; } = new();
        public List<string> BannedMentions { get; set; } = new();
        public int MaxRepeatedMessages { get; set; } = int.MaxValue;
        public string AIPrompt { get; set; } = string.Empty;

        // Load from file
        public static async Task<ChatConfig> LoadAsync(string path)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"Config file not found: {path}");
            }

            string json = await File.ReadAllTextAsync(path);
            return JsonSerializer.Deserialize<ChatConfig>(json) ?? new ChatConfig();
        }

        // Save to file
        public async Task SaveAsync(string path)
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(this, options);
            await File.WriteAllTextAsync(path, json);
        }
    }

    internal class PumpChatMod : Browsing
    {
        public ChatConfig Config { get; private set; }
        public AI AIAgent { get; set; }
        public Dictionary<string, (TimeSpan lastTime, int counter)> RepeatedMessages { get; private set; } = new();

        public async Task Mod(string cfg, string url, CancellationToken cancelToken)
        {
            // Load cfg settings
            Config = await ChatConfig.LoadAsync(cfg);

            // Go to the website (expect  session to be sgined in as owner)
            await Navigate(url);

            // Start monitoring the chat and use AI + cfg to moderate it
            while (!cancelToken.IsCancellationRequested && cancelToken.CanBeCanceled)
            {
                try
                {
                    // Wait and go to the live chat area
                    var chatArea = Page.Locator("div:has-text('live chat')").First;
                    await chatArea.WaitForAsync(new LocatorWaitForOptions() { Timeout = 45000 });
                    //await Task.Delay(30000);
                    //await Page.Context.StorageStateAsync(new() { Path = Environment.GetEnvironmentVariable("PLAYWRIGHT_CACHE_PATH") });
                    await chatArea.ScrollIntoViewIfNeededAsync();

                    // Get all messages from chat area
                    RepeatedMessages.Clear();
                    var allMessages = chatArea.Locator("div[data-message-id]");
                    var allMessagesCount = await allMessages.CountAsync();

                    // Iterate over all messages
                    for (int m = 1; m < allMessagesCount; m++)
                    {
                        try
                        {
                            // Get core content of msg
                            var message = allMessages.Nth(m);
                            var text = await message.InnerTextAsync();
                            var username = await message.Locator("a").First.InnerTextAsync();

                            bool hammerOfJustice = false;
                            string hammerMsg = "";

                            // Check for banned words
                            var checkResult = CheckIfContainsBannedKeyword(text);
                            if (checkResult.found)
                            {
                                hammerMsg = $"'{username}' msg deleted for using word '{checkResult.keyword}'";
                                hammerOfJustice = true;
                            }
                            else
                            {
                                // Check of banned mentions
                                checkResult = CheckIfContainsBannedMention(text);
                                if (checkResult.found)
                                {
                                    hammerMsg = $"'{username}' msg deleted for using word '{checkResult.keyword}'";
                                    hammerOfJustice = true;
                                }
                                else
                                {
                                    // Check if repeated msg
                                    if (CheckIfRepeatedMessage(text))
                                    {
                                        UX.WriteReply($"'{username}' banned for spamming the same message several times: {text}");
                                        hammerOfJustice = true;
                                    }
                                    else
                                    {
                                        //Check AI judgment(last as it is the most resource hungry
                                        bool aiJudgement = await CheckWithAIModeration(text);
                                        if (aiJudgement)
                                        {
                                            hammerMsg = $"'{username}' msg deleted by AI judgement: '{checkResult.keyword}'";
                                            hammerOfJustice = true;
                                        }
                                    }
                                }
                            }

                            // Do we really want to ban? Let the hammer of justice fall!
                            if (hammerOfJustice)
                            {
                                // Click on the message menu button (top-right corner of the message)
                                //for(int t = 0; t < 4; t++)
                                //{
                                //    if (await message.IsVisibleAsync())
                                //    {
                                        var msgMenuButton = message.Locator("button").First;
                                        await msgMenuButton.ScrollIntoViewIfNeededAsync();
                                        await Task.Delay(500);
                                        await msgMenuButton.HoverAsync();
                                        await msgMenuButton.ClickAsync();
                                        await Task.Delay(800);
                                        await Page.Locator("div:has-text('Delete message')").Last.HoverAsync();
                                        await Task.Delay(800);
                                        await Page.Locator("div:has-text('Delete message')").Last.ClickAsync();
                                        await Task.Delay(4000);
                                        await Page.Mouse.ClickAsync(10, 10);
                                //    }
                                //    else break;
                                //}
                                UX.WriteReply(hammerMsg);
                            }
                        }
                        catch (Exception ex)
                        {
                            continue;
                        }
                    }
                }
                catch(Exception ex)
                {
                    continue;
                }
            }
        }

        /// <summary>
        /// Checks if a message contains any banned keyword.
        /// Case-insensitive, matches whole words or substrings.
        /// </summary>
        public (bool found, string keyword) CheckIfContainsBannedKeyword(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return (false, "");

            string keyword = "";
            bool found = Config.BannedKeywords.Any(banned =>
            {
                keyword = banned;
                return message.Contains(keyword, StringComparison.OrdinalIgnoreCase);
            });

            return (found, keyword);
        }

        /// <summary>
        /// Checks if a message contains any banned mention (e.g., "@user").
        /// Case-insensitive match.
        /// </summary>
        public (bool found, string keyword) CheckIfContainsBannedMention(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return (false, "");

            string keyword = "";
            bool found = Config.BannedMentions.Any(banned =>
            {
                keyword = banned;
                return message.Contains(keyword, StringComparison.OrdinalIgnoreCase);
            });

            return (found, keyword);
        }

        public bool CheckIfRepeatedMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return false;

            TimeSpan msgTime = TimeSpan.Parse(message.Substring(message.Length - 5));
            var msgContentStart = message.IndexOf("\n\n") + 2;
            var msgContentEnd = message.LastIndexOf("\n\n");
            var msgContent = message.Substring(msgContentStart, msgContentEnd - msgContentStart).Trim();

            if (!RepeatedMessages.ContainsKey(msgContent))
            {
                RepeatedMessages[msgContent] = (msgTime, 1);
            }
            else
            {
                TimeSpan now = DateTime.Now.TimeOfDay;
                if ((msgTime - RepeatedMessages[msgContent].lastTime).TotalMinutes < 2)
                {
                    RepeatedMessages[msgContent] = (msgTime, RepeatedMessages[msgContent].counter + 1);
                }
            }

            return RepeatedMessages[msgContent].counter >= Config.MaxRepeatedMessages;
        }

        public async Task<bool> CheckWithAIModeration(string message)
        {
            var aiReply = await AIAgent.ModerateChat(message);
            return aiReply.Trim().ToLower().Contains("yes");
        }
    }
}
