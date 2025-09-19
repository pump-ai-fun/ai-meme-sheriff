using Microsoft.Playwright;

namespace PumpSheriff
{
    /// <summary>
    /// Base helper for Playwright-based browsing with preconfigured window position, size and context.
    /// </summary>
    internal class Browsing
    {
        /// <summary>Gets or sets the Playwright instance used for browser automation.</summary>
        public IPlaywright PlaywrightContainer { get; set; } = null;

        /// <summary>Gets or sets the browser instance used for interacting with web content.</summary>
        public IBrowser Browser { get; set; } = null;

        /// <summary>Gets or sets the browser context associated with the current operation.</summary>
        public IBrowserContext BrowserContext { get; set; } = null;

        /// <summary>Gets or sets the current page instance used for navigation or content rendering.</summary>
        public IPage Page { get; set; } = null;

        /// <summary>Indicates whether the Playwright environment has been fully initialized.</summary>
        public bool IsInitialized { get; private set; } = false;

        /// <summary>Desired top-left window position.</summary>
        public Position Position { get; set; } = new Position() { X = 0, Y = 0 };

        /// <summary>Desired viewport size for the browser context.</summary>
        public ViewportSize Size { get; set; } = new ViewportSize() { Width = 1000, Height = 1200 };

        /// <summary>Zoom factor to apply (currently unused).</summary>
        public double Zoom { get; set; } = 1.0;

        /// <summary>
        /// Ensure Playwright, Browser, Context and Page are ready for usage.
        /// </summary>
        virtual public async Task Initialize()
        {
            if (!IsInitialized)
            {
                if (PlaywrightContainer == null)
                {
                    PlaywrightContainer = await Playwright.CreateAsync();
                }

                if (Browser == null)
                {
                    List<string> playwrightArgs = new List<string>
                    {
                        $"--window-position={Position.X},{Position.Y}",
                        $"--window-size={Size.Width},{Size.Height}",
                        "--disable-blink-features=AutomationControlled",
                    };
                    Browser = await PlaywrightContainer.Chromium.LaunchAsync(new() { Channel = "msedge", Headless = false, Args = playwrightArgs });
                }

                if (BrowserContext == null)
                {
                    string? browserCachePath = Environment.GetEnvironmentVariable("PLAYWRIGHT_CACHE_PATH");
                    if (string.IsNullOrEmpty(browserCachePath))
                    {
                        throw new InvalidOperationException("Environment variable 'PLAYWRIGHT_CACHE_PATH' is not set.");
                    }

                    BrowserContext = await Browser.NewContextAsync(new BrowserNewContextOptions()
                    {
                        ViewportSize = new ViewportSize { Width = Size.Width, Height = Size.Height },
                        IgnoreHTTPSErrors = true,
                        StorageStatePath = browserCachePath,
                        UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/140.0.0.0 Safari/537.36",
                        Permissions = new string[] { "clipboard-read", "clipboard-write" }
                    });
                }

                if (Page == null)
                {
                    Page = await BrowserContext.NewPageAsync();
                    Page.SetDefaultTimeout(15000); // 15 seconds
                }

                IsInitialized = true;
            }
        }

        /// <summary>
        /// Navigate to a specified URL.
        /// </summary>
        virtual public async Task Navigate(string url)
        {
            await Initialize();
            await Page.GotoAsync(url);
        }

        /// <summary>
        /// Navigate to an address (token/wallet/etc). Derived classes should implement details.
        /// </summary>
        virtual public async Task GoToAddress(string address)
        {
            throw new NotImplementedException("This method should be implemented in a derived class.");
        }

        /// <summary>
        /// Helper to return a locator by CSS/text selector.
        /// </summary>
        virtual public ILocator GetInfo(string locator)
        {
            return Page.Locator(locator);
        }
    }

    /// <summary>Wrapper for Axiom site interactions.</summary>
    internal class AxiomBrowser : Browsing
    {
        /// <inheritdoc />
        override public async Task GoToAddress(string address)
        {
            await Navigate($"https://axiom.trade");
            await Page.Locator("button:has(span:has-text('Search by token or CA'))").ClickAsync();
            await Task.Delay(1500);
            await Page.Keyboard.TypeAsync(address);
            await Task.Delay(1000);
            await Page.Keyboard.PressAsync("Enter");
            await Task.Delay(1000);
            await Page.Keyboard.PressAsync("End");
            await Task.Delay(1000);
        }
    }

    /// <summary>Wrapper for Dexscreener site interactions.</summary>
    internal class DexBrowser : Browsing
    {
        /// <inheritdoc />
        override public async Task GoToAddress(string address)
        {
            await Navigate($"https://dexscreener.com/solana/{address}");
        }
    }

    /// <summary>Wrapper for Solscan site interactions.</summary>
    internal class SolscanBrowser : Browsing
    {
        /// <inheritdoc />
        override public async Task GoToAddress(string address)
        {
            await Navigate($"https://solscan.io/account/{address}");
            await Page.Locator("div:has-text('Signature')").Last.ScrollIntoViewIfNeededAsync();
        }
    }

    /// <summary>Wrapper for Pump.fun chat page.</summary>
    internal class PumpFunChatBrowser : Browsing
    {
        /// <inheritdoc />
        override public async Task GoToAddress(string address)
        {
            await Navigate($"https://pump.fun/coin/{address}");
        }

        /// <summary>Returns inner text of the visible chat panel.</summary>
        public async Task<string> GetLatestChatMessages()
        {
            try
            {
                var chatView = Page.Locator("div:has-text('live chat')").Last.Locator("xpath=../..");
                return await chatView.InnerTextAsync();
            }
            catch (Exception ex)
            {
                return string.Empty;
            }
        }
    }
}
