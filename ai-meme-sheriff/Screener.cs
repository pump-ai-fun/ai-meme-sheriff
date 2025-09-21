using Microsoft.Playwright;
using System.Text.RegularExpressions;
using TextCopy;

namespace AIMemeSherif
{
    /// <summary>
    /// High-level orchestration for screening newly created Pump.fun coins across Axiom, Dexscreener and Solscan.
    /// </summary>
    internal class Screener : Browsing
    {
        public Browsing Axiom { get; set; }
        public Browsing Dex { get; set; }

        /// <summary>Maximum number of coins to inspect from the list.</summary>
        public int MaxCoinsToReturn { get; set; } = 3;

        /// <summary>Minimum trader count to consider a coin.</summary>
        public int MinHolders { get; set; } = 40;

        /// <summary>Minimum percentage of progress toward ATH to consider good.</summary>
        public int MinAthPct { get; set; } = 60;

        /// <summary>Whether to order the table by 5 minute rate.</summary>
        public bool OrderBy5MinRate { get; set; } = false; // fixed typo

        /// <summary>Minimum market cap filter (in thousands).</summary>
        public int MinMarketCapLimitInK { get; set; } = 10;

        /// <summary>Minimum volume filter (in thousands).</summary>
        public int MinVolumeInK { get; set; } = 2;

        /// <summary>Last evaluated coin name/address for UI.</summary>
        public string LatestCoinName { get; set; } = string.Empty;
        public string LatestCoinAddress { get; set; } = string.Empty;

        Dictionary<string, DateTime> LastScanned { get; set; } = new Dictionary<string, DateTime>();

        /// <inheritdoc />
        public override async Task Navigate(string url)
        {
            await base.Navigate(url);
            await Task.Delay(1500); // overhead but reduce risk of partially loaded data with '-'

            // Ensure "Newly Created" is selected
            var isNewlyCreatedAlreadySelected = await Page.Locator("button:has-text('Newly Created')").Last.IsVisibleAsync();
            if (!isNewlyCreatedAlreadySelected)
            {
                await Page.Locator("button[aria-label=\"Sort\"]").Last.ClickAsync();
                await Task.Delay(800);
                await Page.Locator("span:has-text('Newly Created')").Last.ClickAsync();
                await Task.Delay(1000);
            }

            // Apply filters
            await Page.Locator("span:has-text('Filter')").Last.ClickAsync();
            if (MinMarketCapLimitInK > 0)
            {
                await Page.Locator("input[placeholder=\"e.g., 10k, 1m\"]").First.FillAsync($"{MinMarketCapLimitInK}k");
            }
            if (MinVolumeInK > 0)
            {
                await Page.Locator("input[placeholder=\"e.g., 5k, 100k\"]").First.FillAsync($"{MinVolumeInK}k");
            }
            if (MinMarketCapLimitInK > 0 || MinVolumeInK > 0)
            {
                await Task.Delay(500);
                if (!await Page.Locator("button:has-text('Apply')").Last.IsDisabledAsync())
                {
                    await Page.Locator("button:has-text('Apply')").Last.ClickAsync();
                    await Task.Delay(1000);
                }
            }

            // Enter table mode so we fetch data from all tokens
            await Page.Locator("svg.lucide-table-of-contents").Last.ClickAsync();
            await Task.Delay(1500);

            // Order by 5min rate if required
            if (OrderBy5MinRate)
            {
                await Page.Locator("th:has-text('5M')").Last.ClickAsync();
            }
        }

        /// <summary>
        /// Navigates Pump.fun board, inspects top N coins meeting ATH and traders thresholds, returns merged data blobs.
        /// </summary>
        public async Task<string> Screen()
        {
            // Go to Pump.fun board and set filters
            await Navigate("https://pump.fun/board?coins_sort=created_timestamp&show_animations=false");
            await Page.Locator("span:has-text('Filter')").WaitForAsync(new LocatorWaitForOptions { Timeout = 45_000 });
            await Task.Delay(1500); // ensure all data is loaded
            if (await Page.Locator("div:has-text('Wallet connection unsuccessful')").Last.IsVisibleAsync())
            {   // avoid wallet error overlay
                await Page.Mouse.ClickAsync(100, 100);
            }

            var allNewCoins = Page.Locator("a").Filter(new() { Has = Page.Locator("tr") });
            var allNewCoinsCount = Math.Min(await allNewCoins.CountAsync(), 5); // limit to 5 for UI
            string coinAddress = string.Empty;

            for (int newCoinIndex = 0; newCoinIndex < allNewCoinsCount; newCoinIndex++)
            {
                try
                {
                    var newCoin = allNewCoins.Nth(newCoinIndex);

                    // If ATH progress bar is high we want to look at it
                    var progressBarElement = newCoin.Locator("div[data-state='indeterminate']").Last;
                    string? progressBarStyle = await progressBarElement.GetAttributeAsync("style");
                    var match = Regex.Match(progressBarStyle ?? string.Empty, @"translateX\((-?\d+(\.\d+)?)%\)");
                    double progress = 100 + double.Parse(match.Groups[1].Value);
                    bool athBarIsGood = progress > MinAthPct || await newCoin.Locator("div.Sparkler_spark___h4dk").Last.IsVisibleAsync();

                    // Ensure enough traders
                    var tradersStr = newCoin.Locator("td#traders").InnerTextAsync(new LocatorInnerTextOptions { Timeout = 800 }).Result;
                    if (string.IsNullOrEmpty(tradersStr) || tradersStr == "-")
                    {
                        tradersStr = "0";
                    }
                    int traders = int.Parse(tradersStr.Replace(",", ""));
                    bool tradersIsGood = traders >= MinHolders;

                    if (athBarIsGood && tradersIsGood)
                    {
                        string coinName = await newCoin.Locator("td#name").Locator("span").Nth(1).InnerTextAsync(new LocatorInnerTextOptions { Timeout = 800 });
                        string coinSymbol = await newCoin.Locator("td#name").Locator("span").Nth(2).InnerTextAsync(new LocatorInnerTextOptions { Timeout = 800 });

                        string? coinHref = await newCoin.GetAttributeAsync("href");
                        string coinUrl = Uri.UnescapeDataString($"https://pump.fun/advanced{coinHref}");

                        coinAddress = coinUrl.Substring(coinUrl.LastIndexOf("/") + 1);

                        // Extract data from all sources
                        var dexData = await GetDexData(coinAddress);
                        var axiomData = await GetAxiomData(coinAddress);

                        if(!LastScanned.ContainsKey(coinAddress))
                        {
                            LastScanned.Add(coinAddress, DateTime.Now);
                        }
                        else
                        {
                            var lastScanTime = LastScanned[coinAddress];
                            if ((DateTime.Now - lastScanTime).TotalMinutes < 3)
                            {
                                // recently scanned, skip
                                continue;
                            }
                        }

                        LastScanned[coinAddress] = DateTime.Now;
                        LatestCoinName = coinName;
                        LatestCoinAddress = coinAddress;
                        return $"Data for `{LatestCoinName}`, address `{LatestCoinAddress}`, is:\nDexScreener coin trading information:\n" +
                               $"{dexData}\nAxion coin metrics:\n{axiomData}\n";
                    }
                }
                catch
                {
                    return string.Empty;
                }
            }
            return string.Empty;
        }

        private async Task<string> GetDexData(string address)
        {
            await Dex.GoToAddress(address);
            await Task.Delay(500);
            var dexCoinInfo = Dex.Page.Locator("div", new PageLocatorOptions
            {
                HasTextRegex = new System.Text.RegularExpressions.Regex(
                                @"(?=.*Price)(?=.*Cap)(?=.*Txns)(?=.*Volume)",
                                System.Text.RegularExpressions.RegexOptions.IgnoreCase
                            ),
            }).Last;
            var dexInfo = await dexCoinInfo.InnerTextAsync();
            int startIndex = Math.Min(dexInfo.IndexOf("Buy"), dexInfo.IndexOf("Ad\n"));
            int endIndex = dexInfo.IndexOf("5M");
            return dexInfo.Remove(startIndex, (endIndex + 2) - startIndex);
        }

        private async Task<string> GetAxiomData(string address)
        {
            await Axiom.GoToAddress(address);
            await Task.Delay(500);
            var axiomCoinInfo = Axiom.Page.Locator("div", new PageLocatorOptions
            {
                HasTextRegex = new System.Text.RegularExpressions.Regex(
                                @"(?=.*Top 10 H)(?=.*Dev H)(?=.*Snipers H)(?=.*Dex Paid)",
                                System.Text.RegularExpressions.RegexOptions.IgnoreCase
                            ),
            }).Last;
            var axiomInfo = await axiomCoinInfo.InnerTextAsync();
            axiomInfo = axiomInfo.Remove(axiomInfo.IndexOf("CA"));
            var lines = axiomInfo.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).ToList();
            var pairs = lines.Select((line, i) => new { line, i })
                .GroupBy(x => x.i / 2)
                .Select(g => $"{g.Skip(1).First().line} : {g.First().line}");
            return string.Join(Environment.NewLine, pairs);
        }
    }
}
