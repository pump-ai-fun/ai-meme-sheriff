using PumpSheriff;
using Microsoft.Playwright;

/// <summary>
/// App entry point. Wires up browsers, UI and AI, and runs the main loop.
/// </summary>
class Program
{
    /// <summary>
    /// Main startup routine.
    /// </summary>
    static async Task Main()
    {
        // The code starts with browser automation.
        // A proper screener will come later; for now encapsulate a few browsers behind a dummy screener class.
        var axiomBrowser = new AxiomBrowser()
        {
            // Position as desired for your setup; multiple monitors may require tuning
            Position = new Position() { X = 545, Y = -1278 },
            Size = new ViewportSize() { Width = 800, Height = 1142 },
        };
        var dexBrowser = new DexBrowser()
        {
            // Position as desired for your setup; multiple monitors may require tuning
            Position = new Position() { X = 545 + 810, Y = -1278 },
            Size = new ViewportSize() { Width = 500, Height = 1142 },
        };
        var solBrowser = new SolscanBrowser()
        {
            // Position as desired for your setup; multiple monitors may require tuning
            Position = new Position() { X = 2285, Y = 1110 },
            Size = new ViewportSize() { Width = 1000, Height = 500 },
        };
        var hackyScreener = new Screener()
        {
            Axiom = axiomBrowser,
            Dex = dexBrowser,
            Solscan = solBrowser,
            Position = new Position() { X = 545 + 1270, Y = -1278 },
            Size = new ViewportSize() { Width = 1000, Height = 1142 },
            MinHolders = 50,
            MinAthPct = 70,
            MinMarketCapLimitInK = 22,
            MinVolumeInK = 3,
        };
        var pumpChat = new PumpFunChatBrowser()
        {
            Position = new Position() { X = 900, Y = 1110 },
            Size = new ViewportSize() { Width = 1000, Height = 500 },
        };
        //await pumpChat.GoToAddress(coin we will be listening to chat);

        // Setup console and static parts of the UX
        // TODO: coordinates should be relative and handled internally
        UX.Initialize(); 
        UX.WriteTitle("V1  AI Meme Sheriff"); 
        UX.WriteSubtitle("Chat  with  the  AI  Sheriff  while  she  scans  the  World  Pump  West");
        UX.WriteSection("~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~", 0, 227, 157);
        UX.WriteDisclaimer("NOT financial advice or any investment service");
        UX.WriteDisclaimer("Just a dev with insomnia having fun on Pump Fun", 0, 278, 163);
        UX.WriteSection("~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~            ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~", 0, 226, 32);
        UX.WriteSection("Goals          ", 0, 392, 30);
        UX.WriteGoals("Dont break the code        xxx            Snipe Wallets  DB       |   xxx         Better AI Model", 0, 253, 37);
        UX.WriteGoals("50k", 0, 233, 37, ConsoleColor.DarkGreen);
        UX.WriteGoals("100k", 0, 360, 37, ConsoleColor.DarkGreen);
        UX.WriteGoals("200k", 0, 470, 37, ConsoleColor.DarkGreen);
        UX.WriteSection("~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~", 0, 227, 46);
        UX.WriteSection("Talking  with  Chat          ", 0, 227, 44);

        // Load animations (https://www.ascii-animator.com/ is great)
        // TODO: encapsulate animations per character and migrate ASCII video to a dedicated project
        Animations characterAnimation = new Animations(@"..\..\..\..\animations");

        // Instantiate AI (using MS Foundry Local)
        var memeSheriffAI = new AI();
        await memeSheriffAI.Initialize();

        var screenerTask = Task.Run(async () => { return string.Empty; }); // empty task
        var chatTask = Task.Run(async () => { return string.Empty; }); // empty task

        // Business logic loop
        while (true)
        {
            // Play random animation
            // TODO: play animation hinted by AI to reflect mood and actions
            UX.RenderAnimation(characterAnimation, "girl");
            UX.WriteSection($"    Looking  at  {hackyScreener.LatestCoinName}    ", 0, 50, 88);
            await Task.Delay(2000);

            // Screener runs async while we handle chat, animations and more in the loop
            if (screenerTask.IsCompleted)
            {
                string coinEval = screenerTask.Result;
                if (!string.IsNullOrEmpty(coinEval))
                {
                    UX.WriteSection("~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~", 0, 227, 85);
                    UX.WriteSection($"Scan   result   for   {hackyScreener.LatestCoinName}    ", 0, 227, 83);
                    UX.WriteCoinAddress(hackyScreener.LatestCoinAddress);
                    UX.WriteEvaluation(coinEval);
                    await Task.Delay(500);
                }

                // Restart Screener async task
                screenerTask = hackyScreener.Screen().ContinueWith(async t => {
                    var result = t.Result;
                    if (!string.IsNullOrEmpty(result))
                    {
                        result = await memeSheriffAI.EvaluateCoin(t.Result);
                    }
                    return result;
                }).Unwrap();
            }

            // Chat runs async while we handle chat
            if (chatTask.IsCompleted)
            {
                var chatReply = chatTask.Result;
                if (!string.IsNullOrEmpty(chatReply))
                {
                    UX.WriteReply(chatReply);
                    await Task.Delay(500);
                }

                // Restart Chat async task
                chatTask = pumpChat.GetLatestChatMessages().ContinueWith(async t =>
                {
                    var result = t.Result;
                    if (!string.IsNullOrEmpty(result))
                    {
                        result = await memeSheriffAI.TalkWithChat(result);
                    }
                    return result;
                }).Unwrap();
            }
        }
    }
}