using AIMemeSherif;
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
        // Setup console and static parts of the UX
        // TODO: coordinates should be relative and handled internally
        UX.Initialize();
        UX.WriteTitle("V2  AI Meme Sheriff");
        UX.WriteSubtitle("Chat  with  the  AI  Sheriff  while  she  scans  the  World  Pump  West");
        UX.WriteSection("~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~", 0, 227, 157);
        UX.WriteDisclaimer("NOT financial advice or any investment service");
        UX.WriteDisclaimer("Just a dev with insomnia having fun on Pump Fun", 0, 278, 164);
        UX.WriteSection("~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~            ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~", 0, 226, 32);
        UX.WriteSection("Goals          ", 0, 392, 30);
        UX.WriteGoals("Today we are testing our Chat Moderation and stability of PHI 4 Model", 0, 244, 37);
        UX.WriteSection("~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~", 0, 227, 46);
        UX.WriteSection("Moderating our Chat          ", 0, 227, 44);

        // Load animations (https://www.ascii-animator.com/ is great)
        // TODO: encapsulate animations per character and migrate ASCII video to a dedicated project
        Animations characterAnimation = new Animations(@"..\..\..\..\animations");

        // Instantiate AI (using MS Foundry Local)
        var memeSheriffAI = new AI();
        await memeSheriffAI.Initialize();

        // Start o new pump live chat moderator
        PumpChatMod chatMod = new PumpChatMod()
        {
            Position = new Position() { X = 550, Y = -1278 },
            Size = new ViewportSize() { Width = 1100, Height = 1142 },
            AIAgent = memeSheriffAI
        };
        using var cts = new CancellationTokenSource();
        var moderationTask = chatMod.Mod(
            @".\chat-mod-cfg.json",
            "https://pump.fun/coin/H8xQ6poBjB9DTPMDTKWzWPrnxu4bDEhybxiouF8Ppump",
            cts.Token
        );


        // Instantiate everyhting the Screening logic needs
        var axiomBrowser = new AxiomBrowser()
        {
            // Position as desired for your setup; multiple monitors may require tuning
            Position = new Position() { X = 1915, Y = -1200 },
            Size = new ViewportSize() { Width = 800, Height = 1142 },
        };
        var dexBrowser = new DexBrowser()
        {
            // Position as desired for your setup; multiple monitors may require tuning
            Position = new Position() { X = 2015, Y = -1100 },
            Size = new ViewportSize() { Width = 500, Height = 1090 },
        };
        var hackyScreener = new Screener()
        {
            Axiom = axiomBrowser,
            Dex = dexBrowser,
            Position = new Position() { X = 1815, Y = -1278 },
            Size = new ViewportSize() { Width = 1000, Height = 1142 },
            MinHolders = 50,
            MinAthPct = 70,
            MinMarketCapLimitInK = 22,
            MinVolumeInK = 3,
        };
        var screenerTask = Task.Run(async () => { return string.Empty; }); // empty task

        // Business logic loop
        while (true)
        {
            try
            {
                // Updater timestamp in the botton
                UX.WriteSection(DateTime.Now.ToString("yyyy - MM - dd   ~   HH : mm : ss" + "      "), 0, 227, 155);
                
                // Play one of our awesome animation randomly
                UX.RenderAnimation(characterAnimation, "girl");

                // Yes, no need to rush my friends
                await Task.Delay(new Random().Next(750, 4000));

                // Screener runs async while we handle chat, animations and more in the loop
                if (screenerTask.IsCompleted)
                {
                    if (screenerTask.IsCompletedSuccessfully)
                    {
                        string coinEval = screenerTask.Result;
                        if (!string.IsNullOrEmpty(coinEval))
                        {
                            UX.WriteSection("~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~", 0, 227, 78);
                            UX.WriteSection($"Scan   result   for   {hackyScreener.LatestCoinName}    ", 0, 227, 76);
                            UX.WriteCoinAddress(hackyScreener.LatestCoinAddress);
                            UX.WriteEvaluation(coinEval);
                            await Task.Delay(500);
                        }
                    }

                    // Restart Screener async task
                    screenerTask = hackyScreener.Screen().ContinueWith(async t =>
                    {
                        var result = t.Result;
                        if (!string.IsNullOrEmpty(result))
                        {
                            result = await memeSheriffAI.EvaluateCoin(t.Result);
                        }
                        return result;
                    }).Unwrap();
                }
            }
            catch (Exception ex)
            {
                await Task.Delay(2000);
            }
        }
    }
}