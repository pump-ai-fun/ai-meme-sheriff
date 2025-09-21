using OpenAI;
using OpenAI.Chat;
using System.ClientModel;
using Microsoft.AI.Foundry.Local;

namespace AIMemeSherif
{
    /// <summary>
    /// Simple AI wrapper over Microsoft Foundry Local + OpenAI .NET client for chat and coin evaluation.
    /// </summary>
    internal class AI
    {
        /// <summary>
        /// Alias or model identifier from Foundry Local catalog. Example: "gpt-oss-20b" or "deepseek-r1-7b".
        /// </summary>
        public string AliasOrModelId { get; private set; } = "phi-4-mini";//"gpt-oss-20b"; // "deepseek-r1-7b"

        /// <summary>Manager for Foundry Local service and models.</summary>
        public FoundryLocalManager FoundryMgr { get; private set; }

        /// <summary>OpenAI compatible client targeting Foundry Local endpoint.</summary>
        public OpenAIClient Client { get; private set; }

        /// <summary>Chat client used for streaming completions.</summary>
        public ChatClient Chat { get; private set; }

        /// <summary>System prompt describing the roleplaying Pump Sheriff Agent.</summary>
        public SystemChatMessage SheriffDescription { get; private set; } = new SystemChatMessage(@"
You are a live streamer at Pump.fun roleplaying as as an anime-inspired sheriff with a stylish ""World Wide West"" vibe who protects the memecoin frontier on PumpFun.
- Always answer concisely without showing reasoning or step-by-step thoughts.
- Prompt is always a convoluted log string from the stream chat, so pick one of the newer messages to reply to.
- Always reply with 1 liner starting with the {username} of the selected message you are replying to.
- Never drift into sexual, discriminatory, religious, or otherwise offensive topics.
- Make every reply sound like part of a roleplay in this parody universe.");

        /// <summary>System prompt describing the trader persona for coin evaluation Agent.</summary>
        public SystemChatMessage CoinTraderDescription { get; private set; } = new SystemChatMessage(@"
You are a live streamer at Pump.fun roleplaying as an expert memecoin trader with razor-sharp instincts and the swagger of a market veteran who thrives in the chaos of PumpFun.
Your mission: judge coins fast and loud, dropping verdicts like alpha calls in a crowded pit.

# Style Rules

- Reply in one sharp paragraph only.
- Start with a tiered call: Strong Yes / Soft Yes / Soft No / Strong No.
- No step-by-step explanations, no reasoning chain.
- No emojis, no quotes, no markdown, no tables.
- Tone: alpha drop, pit-floor swagger, parody bravado.

# Holder & Risk Metrics (Axion / PumpFun data)

## Top 10 Holders:

- <25% = decentralized, bullish.
- 25–40% = watch zone.
- 40% = whales steering = bearish.

## Dev / Insiders / Snipers:

- 0% = clean.
- 0–5% = tolerable.
- 5% = strong red flag.

## Bundlers:

- <3% = safe.
- 3–5% = whales circling.
- 5% = unhealthy.

## LP Burned:

- 100% = trust-locked.
- <100% = shaky floor.

## Holders:

- <200 = ghost town.
- 200–500 = building.

500 = traction.

## Pro Traders:

- 50 = real activity.
- <10 = retail noise.

## Dex Paid/Unpaid:

- Paid = smoother liquidity.
- Unpaid = friction risk.

# Market Flow Metrics (Dexscreener / external data)

## Liquidity:

- <$20K = weak pool.
- $20K–$100K = tradable.
- $100K = strong pool.
- FDV / Market Cap:
- FDV < $1M = early play.
- $5M = heavy lift, less upside.

## Price Action (1h / 6h / 24h):

- Triple-digit % up = momentum.
- Flat or negative = cooling.

## Volume & Transactions:

- Volume > $100K / 24h = hot.
- Volume <$20K = dead zone.
- Buys ≈ Sells = balanced churn.
- Heavy Buys > Sells = momentum.

## Wallet Activity:

- Buyers >> Sellers = healthy inflow.
- Sellers >> Buyers = exit pressure.

## Verdict Rules

- Strong Yes → LP burned, good spread, >500 holders, high pro trader base, >$100K liquidity, strong volume, buyers flowing in.
- Soft Yes → Mostly clean metrics, but 1–2 caution flags (e.g., bundlers a bit high, whales creeping).
- Soft No → Weak traction, whales concentrated, or liquidity/volume shaky.
- Strong No → Dev/insider high, LP not burned, tiny holder base, or obvious sell pressure.

## Final Behavior

- Collapse all checks into a single tiered verdict + pit-style reasoning, e.g.:
- “Strong Yes, LP burned, volume ripping, buyers stacked heavier than sellers.”
- “Soft Yes, clean chart but bundlers crowding the edges.”
- “Soft No, traction’s there but whales and bundlers hold the wheel.”
- “Strong No, tiny holder base, weak pool, sell pressure screaming.”");

        /// <summary>System prompt describing the chat moderator Agent.</summary>
        public SystemChatMessage ChatModeratorDescription { get; private set; } = new SystemChatMessage(@"
You are a chat moderation system. Does the msg provide by the User is super offensive toward the streamer?
No reasoning or explanation, just reply Yes or No.");

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
                    SheriffDescription,
                    new UserChatMessage($"This is the last chat history for you to process: {chatHistory}")
                };
                var reply = Chat.CompleteChatStreamingAsync(prompt, new ChatCompletionOptions { MaxOutputTokenCount = 2048 });

                //var userFacingReplyStarted = false;
                //List<string> replyTags = new List<string> { "<|start|>", "assistant", "<|channel|>", "final", "<|message|>" };
                //if (AliasOrModelId != "gpt-oss-20b")
                //{
                //    replyTags = new List<string> { "</think>" };
                //}
                //int replyTagIndex = 0;
                //string full = "";
                await foreach (var update in reply)
                {
                    var updateText = update.ContentUpdate[0].Text;
                    //full += updateText;
                    //if (!userFacingReplyStarted)
                    //{
                    //    if (replyTagIndex < replyTags.Count && updateText.Contains(replyTags[replyTagIndex]))
                    //    {
                    //        replyTagIndex++;
                    //        if (replyTagIndex >= replyTags.Count)
                    //        {
                    //            userFacingReplyStarted = true;
                    //        }
                    //    }
                        
                    //}
                    //else
                    //{
                        userFacingReply += updateText;  
                    //}
                }
                userFacingReply = userFacingReply.Replace("<|return|>", string.Empty).Trim();
                if (userFacingReply.Length > 100)
                {
                    userFacingReply = userFacingReply.Substring(userFacingReply.Length - 100); // limit input size
                }

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
                //var userFacingReplyStarted = false;
                //List<string> replyTags = new List<string> { "<|start|>", "assistant", "<|channel|>", "final", "<|message|>" };
                //if (AliasOrModelId != "gpt-oss-20b")
                //{
                //    replyTags = new List<string> { "</think>" };
                //}
                //int replyTagIndex = 0;
                await foreach (var update in reply)
                {
                    var updateText = update.ContentUpdate[0].Text;
                    //if (!userFacingReplyStarted)
                    //{
                    //    if (replyTagIndex < replyTags.Count && updateText.Contains(replyTags[replyTagIndex]))
                    //    {
                    //        replyTagIndex++;
                    //        if (replyTagIndex >= replyTags.Count)
                    //        {
                    //            userFacingReplyStarted = true;
                    //        }
                    //    }
                    //}
                    //else
                    //{
                        userFacingReply += updateText;
                    //}
                }
                userFacingReply = userFacingReply.Replace("<|return|>", string.Empty).Trim();
                if (userFacingReply.Length > 200)
                {
                    userFacingReply = userFacingReply.Substring(userFacingReply.Length - 200); // limit input size
                }
            }
            catch
            {
                return string.Empty;
            }
            return userFacingReply;
        }

        public async Task<string> ModerateChat(string msg)
        {
            var userFacingReply = "";
            try
            {
                List<ChatMessage> prompt = new List<ChatMessage>
                {
                    ChatModeratorDescription,
                    new UserChatMessage(msg)
                };
                var reply = Chat.CompleteChatStreamingAsync(prompt, new ChatCompletionOptions { MaxOutputTokenCount = 2048 });
                //var userFacingReplyStarted = false;
                //List<string> replyTags = new List<string> { "<|start|>", "assistant", "<|channel|>", "final", "<|message|>" };
                //if (AliasOrModelId != "gpt-oss-20b")
                //{
                //    replyTags = new List<string> { "</think>" };
                //}
                //int replyTagIndex = 0;
                await foreach (var update in reply)
                {
                    var updateText = update.ContentUpdate[0].Text;
                    //if (!userFacingReplyStarted)
                    //{
                    //    if (replyTagIndex < replyTags.Count && updateText.Contains(replyTags[replyTagIndex]))
                    //    {
                    //        replyTagIndex++;
                    //        if (replyTagIndex >= replyTags.Count)
                    //        {
                    //            userFacingReplyStarted = true;
                    //        }
                    //    }
                    //}
                    //else
                    //{
                    userFacingReply += updateText;
                    //}
                }
                userFacingReply = userFacingReply.Replace("<|return|>", string.Empty).Trim();
                if (userFacingReply.Length > 200)
                {
                    userFacingReply = userFacingReply.Substring(userFacingReply.Length - 200); // limit input size
                }
            }
            catch
            {
                return string.Empty;
            }
            return userFacingReply;
        }
    }
}
