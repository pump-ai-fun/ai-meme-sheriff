using OpenAI;
using OpenAI.Chat;
using System.ClientModel;
using Microsoft.AI.Foundry.Local;

namespace PumpSheriff
{
    /// <summary>
    /// Simple AI wrapper over Microsoft Foundry Local + OpenAI .NET client for chat and coin evaluation.
    /// </summary>
    internal class AI
    {
        /// <summary>
        /// Alias or model identifier from Foundry Local catalog. Example: "gpt-oss-20b" or "deepseek-r1-7b".
        /// </summary>
        public string AliasOrModelId { get; private set; } = "gpt-oss-20b"; // "deepseek-r1-7b"

        /// <summary>Manager for Foundry Local service and models.</summary>
        public FoundryLocalManager FoundryMgr { get; private set; }

        /// <summary>OpenAI compatible client targeting Foundry Local endpoint.</summary>
        public OpenAIClient Client { get; private set; }

        /// <summary>Chat client used for streaming completions.</summary>
        public ChatClient Chat { get; private set; }

        /// <summary>System prompt describing the roleplaying sheriff persona for chat replies.</summary>
        public SystemChatMessage AgentSheriffDescription { get; private set; } = new SystemChatMessage(@"
You are a live streamer at Pump.fun roleplaying as as an anime-inspired sheriff with a stylish ""World Wide West"" vibe who protects the memecoin frontier on PumpFun.
- Always answer concisely without showing reasoning or step-by-step thoughts.
- Prompt is always a convoluted log string from the stream chat, so pick one of the newer messages to reply to.
- Always reply with 1 liner starting with the {username} of the selected message you are replying to.
- Never drift into sexual, discriminatory, religious, or otherwise offensive topics.
- Make every reply sound like part of a roleplay in this parody universe.");

        /// <summary>System prompt describing the trader persona for coin evaluations.</summary>
        public SystemChatMessage CoinTraderDescription { get; private set; } = new SystemChatMessage(@"
You are a live streamer at Pump.fun roleplaying as an expert memecoin trader with razor-sharp instincts and the swagger of a market veteran who thrives in the chaos of PumpFun.
- Always answer concisely without showing reasoning or step-by-step thoughts.
- Prompt pasted block of coin data copied from external sources such as Dexscreener, Pump.fun, Axiom, or other analytics sites.
- Reply with a 1 liner starting with a direct 'Yes' or 'No' followed by your reasoning.
- Keep it direct, sharp, and market-flavored without emotes or quotes. Just text, no tables or markdown.
- Make every reply sound like an alpha drop in the middle of a noisy trading pit, mixing serious market calls with parody-style bravado.");

        public AI()
        {
            FoundryMgr = new FoundryLocalManager();
        }

        /// <summary>
        /// Ensures Foundry Local is running, loads the model and initializes the chat client.
        /// </summary>
        public async Task Initialize()
        {
            // Initialize the Foundry Local service
            FoundryMgr = new FoundryLocalManager();
            if (!FoundryMgr.IsServiceRunning)
            {
                await FoundryMgr.StartServiceAsync(CancellationToken.None);
            }

            // Validate model exists in catalog
            var info = await FoundryMgr.GetModelInfoAsync(AliasOrModelId)
                       ?? throw new InvalidOperationException($"Model '{AliasOrModelId}' not found in catalog.");

            // Load the model and configure OpenAI compatible client targeting Foundry Local
            var modelInfo = await FoundryMgr.LoadModelAsync(AliasOrModelId);

            Client = new OpenAIClient(
                new ApiKeyCredential("local"), // Foundry Local doesn’t need real API keys
                new OpenAIClientOptions
                {
                    Endpoint = FoundryMgr.Endpoint // point to Foundry Local server
                }
            );

            Chat = Client.GetChatClient(modelInfo.ModelId);
        }

        /// <summary>
        /// Generates a concise, in-character reply to the latest chat history.
        /// </summary>
        /// <param name="chatHistory">Raw chat log text. Only the latest ~500 chars are used.</param>
        /// <returns>One-line user-facing reply or empty string on error.</returns>
        public async Task<string> TalkWithChat(string chatHistory)
        {
            if (chatHistory.Length > 500)
            {
                chatHistory = chatHistory.Substring(chatHistory.Length - 500); // limit input size
            }

            var userFacingReply = "";
            try
            {
                List<ChatMessage> prompt = new List<ChatMessage>
                {
                    AgentSheriffDescription,
                    new UserChatMessage($"This is the last chat history for you to process: {chatHistory}")
                };
                var reply = Chat.CompleteChatStreamingAsync(prompt, new ChatCompletionOptions { MaxOutputTokenCount = 2048 });

                var userFacingReplyStarted = false;
                List<string> replyTags = new List<string> { "<|start|>", "assistant", "<|channel|>", "final", "<|message|>" };
                if (AliasOrModelId != "gpt-oss-20b")
                {
                    replyTags = new List<string> { "</think>" };
                }
                int replyTagIndex = 0;
                await foreach (var update in reply)
                {
                    var updateText = update.ContentUpdate[0].Text;
                    if (!userFacingReplyStarted)
                    {
                        if (replyTagIndex < replyTags.Count && updateText.Contains(replyTags[replyTagIndex]))
                        {
                            replyTagIndex++;
                            if (replyTagIndex >= replyTags.Count)
                            {
                                userFacingReplyStarted = true;
                            }
                        }
                    }
                    else
                    {
                        userFacingReply += updateText;
                    }
                }
                userFacingReply = userFacingReply.Replace("<|return|>", string.Empty).Trim();
            }
            catch
            {
                return string.Empty;
            }
            return userFacingReply;
        }

        /// <summary>
        /// Produces a short buy/no-buy assessment over a pasted, raw analytics block.
        /// </summary>
        /// <param name="coinInfo">DexScreener/Pump.fun/Axiom/Solscan text block.</param>
        /// <returns>Single-line evaluation or empty string on error.</returns>
        public async Task<string> EvaluateCoin(string coinInfo)
        {
            var userFacingReply = "";
            try
            {
                List<ChatMessage> prompt = new List<ChatMessage>
                {
                    CoinTraderDescription,
                    new UserChatMessage(coinInfo)
                };
                var reply = Chat.CompleteChatStreamingAsync(prompt, new ChatCompletionOptions { MaxOutputTokenCount = 2048 });
                var userFacingReplyStarted = false;
                List<string> replyTags = new List<string> { "<|start|>", "assistant", "<|channel|>", "final", "<|message|>" };
                if (AliasOrModelId != "gpt-oss-20b")
                {
                    replyTags = new List<string> { "</think>" };
                }
                int replyTagIndex = 0;
                await foreach (var update in reply)
                {
                    var updateText = update.ContentUpdate[0].Text;
                    if (!userFacingReplyStarted)
                    {
                        if (replyTagIndex < replyTags.Count && updateText.Contains(replyTags[replyTagIndex]))
                        {
                            replyTagIndex++;
                            if (replyTagIndex >= replyTags.Count)
                            {
                                userFacingReplyStarted = true;
                            }
                        }
                    }
                    else
                    {
                        userFacingReply += updateText;
                    }
                }
                userFacingReply = userFacingReply.Replace("<|return|>", string.Empty).Trim();
            }
            catch
            {
                return string.Empty;
            }
            return userFacingReply;
        }
    }
}
