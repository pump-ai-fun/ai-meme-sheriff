using AIMemeSherif;
using Microsoft.Playwright;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
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
        public Dictionary<string, int> RepeatedMessages { get; private set; } = new();

        public async Task Mod(string cfg, string url, CancellationToken cancelToken)
        {
            // Load cfg settings
            Config = await ChatConfig.LoadAsync(cfg);

            // Go to the website (expect  session to be sgined in as owner)
            await Navigate(url);

            // Wait and go to the live chat area
            var chatArea = Page.Locator("div:has-text('live chat')").First;
            await chatArea.WaitForAsync(new LocatorWaitForOptions() { Timeout = 45000 });
            //await Task.Delay(30000);
            //await Page.Context.StorageStateAsync(new() { Path = Environment.GetEnvironmentVariable("PLAYWRIGHT_CACHE_PATH") });
            await chatArea.ScrollIntoViewIfNeededAsync();

            // Start monitoring the chat and use AI + cfg to moderate it
            while (!cancelToken.IsCancellationRequested && cancelToken.CanBeCanceled)
            {
                // Get all messages from chat area
                var allMessages = chatArea.Locator("div[data-message-id]");
                var allMessagesCount = await allMessages.CountAsync();

                // Iterate over all messages
                for (int m = 1; m < allMessagesCount; m++)
                {
                    // Get core content of msg
                    var message = allMessages.Nth(m);
                    var text = await message.InnerTextAsync();
                    var username = await message.Locator("a").First.InnerTextAsync();

                    bool hammerOfJustice = false;

                    // Check for banned words
                    var checkResult = CheckIfContainsBannedKeyword(text);
                    if (checkResult.found)
                    {
                        UX.WriteReply($"'{username}' banned for using word '{checkResult.keyword}'");
                        hammerOfJustice = true;
                    }
                    else
                    {
                        // Check of banned mentions
                        checkResult = CheckIfContainsBannedMention(text);
                        if (checkResult.found)
                        {
                            UX.WriteReply($"'{username}' banned for using word '{checkResult.keyword}'");
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
                                //Check AI judgment (last as it is the most resource hungry
                                text = " you are a peace of shit streamer mdf go dieeeeee and i hate you stop douing this!!!!! !!!!!! !!!!! !!!!!! !!!!! !!!!!! !!!!! !!!!!!";
                                bool aiJudgement = await CheckWithAIModeration(text);
                                if (aiJudgement)
                                {
                                    UX.WriteReply($"'{username}' banned by AI judgement on msg: {text}");
                                    hammerOfJustice = true;
                                }
                            }
                        }
                    }

                    // Do we really want to ban? Let the hammer of justice fall!
                    if (hammerOfJustice)
                    {
                        // Click on the message menu button (top-right corner of the message)
                        //var msgMenuButton = message.Locator("button").First;
                        //await msgMenuButton.ClickAsync();
                        // TODO BAN - code to be updated live during stream while testing
                    }
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
            if (RepeatedMessages.ContainsKey(message))
            {
                RepeatedMessages[message]++;
            }
            else
            {
                RepeatedMessages.Add(message, 1);
            }
            return RepeatedMessages[message] > Config.MaxRepeatedMessages;
        }

        public async Task<bool> CheckWithAIModeration(string message)
        {
            var aiReply = await AIAgent.ModerateChat(message);
            return aiReply.Trim().ToLower().Contains("yes");
        }
    }
}
