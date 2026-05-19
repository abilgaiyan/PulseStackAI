// using Microsoft.Playwright;
// using PulseStack.Abstractions.Tools;
// using System.Text.Json;
// using System.Text.Json.Serialization;

// namespace PulseStack.Tools.BuiltIn;

// public sealed class BrowserTool : ITool, IAsyncDisposable
// {
//     private IPlaywright? _playwright;
//     private IBrowser? _browser;
//     private IPage? _currentPage;
//     private readonly BrowserToolOptions _options;

//     public string Name => "browser";
//     public string Description => "Advanced browser control: navigate, observe, auto-detect & fill forms, click, screenshot. Supports localhost and already open browsers.";
//     public string Category => "Web";
//     public bool IsEnabled => true;
//     public IReadOnlyCollection<string> Tags => ["browser", "web", "form", "automation", "localhost"];

//     public BrowserTool(BrowserToolOptions? options = null)
//     {
//         _options = options ?? new BrowserToolOptions();
//     }

//     public async Task<ToolExecutionResult> ExecuteAsync(string input, CancellationToken ct = default)
//     {
//         try
//         {
//             await EnsureBrowserAsync(ct);

//             var command = JsonSerializer.Deserialize<BrowserCommand>(input, JsonOptions);
//             if (command == null)
//                 return new ToolExecutionResult(false, "", "Failed to parse command JSON.");

//             return command.Action?.ToLowerInvariant() switch
//             {
//                 "navigate" => await NavigateAsync(command, ct),
//                 "get_content" or "observe" => await GetContentAsync(ct),
//                 "detect_forms" => await DetectFormsAsync(ct),
//                 "fill_form" => await FillFormAsync(command, ct),
//                 "click" or "submit" => await ClickAsync(command, ct),
//                 "screenshot" => await ScreenshotAsync(ct),
//                 _ => new ToolExecutionResult(false, "", $"Unknown action: {command.Action}")
//             };
//         }
//         catch (Exception ex)
//         {
//             return new ToolExecutionResult(false, "", $"BrowserTool error: {ex.Message}");
//         }
//     }

//     private static readonly JsonSerializerOptions JsonOptions = new()
//     {
//         PropertyNameCaseInsensitive = true,
//         Converters = { new JsonStringEnumConverter() }
//     };

//     private async Task EnsureBrowserAsync(CancellationToken ct)
//     {
//         if (_playwright == null)
//         {
//             _playwright = await Playwright.CreateAsync();
//         }

//         if (_browser == null)
//         {
//             if (!string.IsNullOrWhiteSpace(_options.ConnectToExistingBrowserUrl))
//             {
//                 // Connect to already running browser (Chrome/Edge with --remote-debugging-port=9222)
//                 _browser = await _playwright.Chromium.ConnectOverCDPAsync(_options.ConnectToExistingBrowserUrl);
//             }
//             else
//             {
//                 _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
//                 {
//                     Headless = _options.Headless,
//                     SlowMo = _options.SlowMo,
//                     Args = ["--no-sandbox", "--disable-blink-features=AutomationControlled"]
//                 });
//             }
//         }

//         if (_currentPage == null)
//         {
//             _currentPage = await _browser.NewPageAsync();
//         }
//     }

//     private async Task<ToolExecutionResult> NavigateAsync(BrowserCommand cmd, CancellationToken ct)
//     {
//         if (string.IsNullOrWhiteSpace(cmd.Url))
//             return new ToolExecutionResult(false, "", "URL is required.");

//         await _currentPage!.GotoAsync(cmd.Url, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle, Timeout = 30000 });
//         return new ToolExecutionResult(true, $"Navigated to: {cmd.Url}", "");
//     }

//     private async Task<ToolExecutionResult> GetContentAsync(CancellationToken ct)
//     {
//         var title = await _currentPage!.TitleAsync();
//         var url = _currentPage.Url;

//         var textContent = await _currentPage.EvaluateAsync<string>("() => document.body.innerText");

//         var output = $"""
//             Title: {title}
//             URL: {url}

//             {textContent}
//             """;

//         return new ToolExecutionResult(true, output, "");
//     }

//     private async Task<ToolExecutionResult> DetectFormsAsync(CancellationToken ct)
//     {
//         var forms = await _currentPage!.EvaluateAsync<string>(@"
//             () => {
//                 const forms = Array.from(document.querySelectorAll('form, input, textarea, select'));
//                 return JSON.stringify(forms.map(el => ({
//                     tag: el.tagName.toLowerCase(),
//                     id: el.id || '',
//                     name: el.name || '',
//                     placeholder: el.placeholder || '',
//                     label: el.labels?.[0]?.innerText.trim() || '',
//                     type: el.type || ''
//                 })));
//             }");

//         return new ToolExecutionResult(true, $"Detected form fields:\n{forms}", "");
//     }

//     private async Task<ToolExecutionResult> FillFormAsync(BrowserCommand cmd, CancellationToken ct)
//     {
//         if (cmd.Fields == null || cmd.Fields.Count == 0)
//             return new ToolExecutionResult(false, "", "No fields provided for filling.");

//         int filled = 0;
//         foreach (var kvp in cmd.Fields)
//         {
//             try
//             {
//                 var locator = _currentPage!.Locator(
//                     $"input[name='{kvp.Key}'], textarea[name='{kvp.Key}'], select[name='{kvp.Key}'], " +
//                     $"#{kvp.Key}, input[placeholder*='{kvp.Key}'], [id*='{kvp.Key}']");

//                 await locator.First.FillAsync(kvp.Value);
//                 filled++;
//             }
//             catch { /* Best effort */ }
//         }

//         return new ToolExecutionResult(true, $"Successfully filled {filled} fields.", "");
//     }

//     private async Task<ToolExecutionResult> ClickAsync(BrowserCommand cmd, CancellationToken ct)
//     {
//         if (string.IsNullOrWhiteSpace(cmd.Selector))
//             return new ToolExecutionResult(false, "", "Selector is required for click.");

//         await _currentPage!.ClickAsync(cmd.Selector);
//         return new ToolExecutionResult(true, $"Clicked: {cmd.Selector}", "");
//     }

//     private async Task<ToolExecutionResult> ScreenshotAsync(CancellationToken ct)
//     {
//         var bytes = await _currentPage!.ScreenshotAsync();
//         var base64 = Convert.ToBase64String(bytes);
//         return new ToolExecutionResult(true, $"Screenshot (base64): {base64[..200]}...", "");
//     }

//     public async ValueTask DisposeAsync()
//     {
//         await _browser?.CloseAsync()!;
//         _playwright?.Dispose();
//     }
// }

// public class BrowserToolOptions
// {
//     public bool Headless { get; set; } = true;
//     public float? SlowMo { get; set; } = null;

//     // Connect to already open browser (CDP)
//     public string? ConnectToExistingBrowserUrl { get; set; }

//     // Media / Audio-Video Support
//     public bool UseFakeMediaDevices { get; set; } = true;
//     public string? FakeAudioCaptureFile { get; set; }
// }
// public class BrowserCommand
// {
//     public string Action { get; set; } = string.Empty;           // navigate, get_content, detect_forms, fill_form, click, screenshot
//     public string? Url { get; set; }
//     public Dictionary<string, string>? Fields { get; set; }
//     public string? Selector { get; set; }
// }