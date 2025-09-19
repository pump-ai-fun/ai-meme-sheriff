# AI Meme Sheriff

Live, animated console streamer that roleplays an anime-styled sheriff while scanning Pump.fun for freshly created coins. It automates multiple browsers (Dexscreener, Axiom, Solscan, Pump.fun chat), evaluates coins via a local LLM (Microsoft AI Foundry Local), and renders big ASCII-art UI/animations.

Not financial advice. Just a dev having fun.

---

## Highlights

- ASCII dashboard with animated character loops
- Multi-site screening pipeline:
  - Pump.fun board (new coins)
  - Dexscreener (price/liquidity/volume)
  - Axiom (holders/dev/snipers metrics)
  - Solscan (developer wallet activity)
- Local LLM integration via Microsoft AI Foundry Local
  - In-character chat replies to live chat logs
  - One-line trader-style coin evaluations
- Playwright-driven automation targeting Microsoft Edge
- Highly tweakable thresholds and layout

---

## Project layout

- ConsoleApp1/Program.cs
  - App entry point. Wires up browsers, ASCII UI, and AI. Runs the main loop that alternates animations, chat, and screening tasks.
- ConsoleApp1/Browsing.cs
  - Base Playwright wrapper + site-specific wrappers:
    - AxiomBrowser
    - DexBrowser
    - SolscanBrowser
    - PumpFunChatBrowser
- ConsoleApp1/Screener.cs
  - Orchestrates Pump.fun board navigation, applies filters, checks ATH/traders, then fetches data from Dexscreener, Axiom, and Solscan.
- ConsoleApp1/AI.cs
  - Thin wrapper over Microsoft.AI.Foundry.Local + OpenAI .NET client (chat). Streams responses and trims system/think tags.
- ConsoleApp1/UX.cs
  - Console UI helpers for titles, sections, replies, and evaluations with FIGlet fonts and scaling.
- ConsoleApp1/Animations.cs
  - Loads ASCII animation frames from disk and exposes them by character/type.

---

## Requirements

- .NET 8 SDK
- Microsoft Edge (the automation launches the "msedge" channel)
- Microsoft Playwright for .NET (browsers are provided by Edge, but Playwright is required)
- Microsoft AI Foundry Local (service + model)
  - Recommended catalog alias: `gpt-oss-20b` (default in code)
- A storage state file for Playwright (cookies/session) and an environment variable:
  - `PLAYWRIGHT_CACHE_PATH` pointing to a valid JSON storage state file (see Setup)
- Optional: animations folder with `.ascii` files (see Animations)

NuGet packages used (already referenced in the project):
- Microsoft.Playwright
- OpenAI (Azure/Non-Azure OpenAI client)
- Microsoft.AI.Foundry.Local
- Figgle (FIGlet fonts)
- TextCopy (clipboard helper)

---

## Setup

1) Environment variable
- Create or choose a storage state JSON file path, e.g.: `C:\playwright\storage_state.json`
- Ensure it exists and contains a valid JSON object, e.g. `{}` initially
- Set the environment variable `PLAYWRIGHT_CACHE_PATH` to that path

2) First run to populate storage state (optional but recommended)
- Some sites may work without being signed-in; others might show popups until you accept cookies, etc.
- After the app opens the sites, sign-in/accept cookies manually in Edge windows.
- You can later persist the state yourself by calling `BrowserContext.StorageStateAsync` to the same path if you modify code, or keep reusing the same session file.

3) Microsoft AI Foundry Local
- Install and start the service (default local endpoint is `http://localhost:1234/v1`)
- Ensure the model alias in code is available in the catalog (default: `gpt-oss-20b`)
- The app attempts to start the Foundry Local service if it is not running

4) Animations (optional)
- Configure your animations folder in Program.cs:
  - `new Animations(@"C:\\Dev\\Automate\\ConsoleApp1\\animations")`
- Files should be exported `.ascii` sequences (e.g., from https://www.ascii-animator.com/)

5) Multi-monitor coordinates
- Program.cs uses explicit window positions/sizes per browser
- Adjust `Position` and `Size` blocks for your monitor layout

---

## Running

- From the repository root:
  - `dotnet run --project .\ConsoleApp1\ConsoleApp1.csproj`
- Edge windows will appear, and the console dashboard will render
- The main loop alternates between:
  - Playing a random animation
  - Handling background chat and screener tasks

---

## Configuration knobs

In Program.cs:
- Screener thresholds
  - `MinHolders` (default 50 in Program.cs override)
  - `MinAthPct` (default 70 in Program.cs override)
  - `MinMarketCapLimitInK` (default 22 in Program.cs override)
  - `MinVolumeInK` (default 3 in Program.cs override)
- Window layout per site
- Animations folder path

In Screener.cs:
- `OrderBy5MinRate` (disabled by default)
- `MaxCoinsToReturn`, `MinHolders`, `MinAthPct`, `MinMarketCapLimitInK`, `MinVolumeInK`

In AI.cs:
- `AliasOrModelId` (default `gpt-oss-20b`)
- Two distinct system prompts for roleplay chat and coin evaluation

In UX.cs:
- Layout constants for sections, fonts, and scaling

---

## How it works (high level)

1) Program wires up Browsing wrappers and creates a Screener instance
2) AI is initialized against Foundry Local via the OpenAI-compatible client
3) The console UI is drawn (title, subtitles, sections)
4) Two async tasks run in the loop:
   - Screener task (when complete):
     - Navigates Pump.fun board, filters, checks ATH bar and holders
     - If promising, pulls Dexscreener, Axiom, and Dev wallet data via Solscan
     - The merged data is passed to AI for a one-line trader-style evaluation
   - Chat task (when complete):
     - Scrapes the on-page chat log and asks AI for a single-line, in-character reply
5) ASCII animations keep the console lively in between

---

## Troubleshooting

- Storage state / env var
  - If you see `Environment variable 'PLAYWRIGHT_CACHE_PATH' is not set.` set it to a valid JSON path
  - If some sites show popups or overlays, accept cookies/log in as needed and reuse the same storage

- Foundry Local
  - Make sure the service can start and the model alias exists in the catalog
  - If you changed ports, adjust `FoundryMgr.Endpoint`

- Playwright / Edge
  - The code launches the `msedge` channel (not bundled Chromium). Ensure Edge is installed
  - Increase timeouts if pages are slow to load

- Window coordinates
  - Negative Y or large X values are common on multi-monitor setups; adjust to your layout

---

## Roadmap

- Better AI model selection and prompt tuning
- Smarter animation selection driven by AI “mood”
- Relative/anchored UI coordinates
- Persisted snipe wallets DB and analytics
- Optional web dashboard in addition to console UI

---

## Contributing

PRs welcome. Please keep:
- Code documented and consistent (XML doc summaries kept concise)
- Async style consistent (prefer `Task.Delay` over `Thread.Sleep` in async flows)
- No secrets or private keys in code or commits

---

## Disclaimer

This project is for educational/entertainment purposes only. It is not financial advice, nor an investment service.
