using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using WebDriverManager;
using WebDriverManager.DriverConfigs.Impl;
using HtmlAgilityPack;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Diagnostics;

namespace FaceBookAdLibraryCrawler
{
    public class AdCard
    {
        public string LibraryID { get; set; }
    }

    public class Program
    {
        private static IWebDriver _driver;

        static async Task Main(string[] args)
        {
            //string whatToSearch = "Nike";
           // int takeEntity = 500;
            Console.WriteLine("Please enter keyword to search . Exmaple : Nike");
            var whatToSearch = Console.ReadLine();

            Console.WriteLine("How many items would you like to select?");
            if (!int.TryParse(Console.ReadLine(), out int takeEntity)) takeEntity = 5;

            if (string.IsNullOrEmpty(whatToSearch)) return;

            Stopwatch totalOpTimer = Stopwatch.StartNew();
            Stopwatch setupTimer = Stopwatch.StartNew();

            string searchUrl = $"https://www.facebook.com/ads/library/?active_status=active&ad_type=all&country=ALL&is_targeted_country=false&media_type=all&q={whatToSearch}&search_type=keyword_unordered";

            try
            {
                Console.WriteLine("Going to set up the driver");
                new DriverManager().SetUpDriver(new ChromeConfig());
                var options = new ChromeOptions();
                options.AddArgument("--headless=new");
                options.AddArgument("--disable-gpu");
                options.AddArgument("--window-size=1280,720");

                _driver = new ChromeDriver(options);
                Console.WriteLine("Going to manage the driver");
                _driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(10);

                Console.WriteLine("Driver launched. Navigating to the Ads Library.");
                _driver.Navigate().GoToUrl(searchUrl);

                const string stableXPathSelector = "//div[@role='heading' and @aria-level='3']";

                Console.WriteLine("Going to open web driver");
                WebDriverWait wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(15));
                Console.WriteLine("Waiting to find the starting element");

                int remainingAttempts = 5;
                while (remainingAttempts > 0)
                {
                    try
                    {
                        wait.Until(driver => driver.FindElement(By.XPath(stableXPathSelector)));
                        break;
                    }
                    catch (Exception)
                    {
                        remainingAttempts--;
                    }
                }

                setupTimer.Stop();
                PrintFormattedTime("Base Setup", setupTimer.Elapsed);

                int totalResults = GetTotalResults();
                Console.WriteLine($"Found {totalResults:N0} active items for '{whatToSearch}'.");
                Console.WriteLine("We assume that there are 15 items per scroll");
                await ScrollUntilLoaded(takeEntity);

                HtmlDocument doc = GetHtmlDocument();

                if (doc != null)
                {
                    Console.WriteLine("HTML content fetched and ready for parsing with HtmlAgilityPack.");
                }

                Stopwatch extractionTimer = Stopwatch.StartNew();
                var cards = await ParseAdCards(doc, takeEntity);
                await WriteInFile(cards);
                extractionTimer.Stop();

                totalOpTimer.Stop();

                Console.WriteLine("\n--- Performance Metrics ---");
                PrintFormattedTime("Full Operation", totalOpTimer.Elapsed);

                if (cards.Count > 0)
                {
                    double avgTicks = (double)extractionTimer.Elapsed.Ticks / cards.Count;
                    TimeSpan avgTime = TimeSpan.FromTicks((long)avgTicks);
                    Console.WriteLine($"Average time per extraction: {avgTime.TotalSeconds:F2} seconds");
                }
            }
            catch (WebDriverTimeoutException)
            {
                Console.WriteLine("Error: The results element did not load within the timeout.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An unexpected error occurred: {ex.Message}");
            }
            finally
            {
                if (_driver != null)
                {
                    _driver.Quit();
                    Console.WriteLine("Driver session closed.");
                    Console.ReadKey();
                }
            }
        }

        static void PrintFormattedTime(string label, TimeSpan ts)
        {
            if (ts.TotalSeconds > 60)
            {
                Console.WriteLine($"{label} Time: {ts.Minutes}m {ts.Seconds}s");
            }
            else
            {
                Console.WriteLine($"{label} Time: {ts.TotalSeconds:F2}s");
            }
        }

        static int GetTotalResults()
        {
            const string stableXPathSelector = "//div[@role='heading' and @aria-level='3']";
            IWebElement resultElement = _driver.FindElement(By.XPath(stableXPathSelector));
            string rawResultText = resultElement.Text;
            if (string.IsNullOrEmpty(rawResultText)) return 0;
            string numericText = Regex.Replace(rawResultText, @"[^\d,]", "").Replace(",", "");
            return int.TryParse(numericText, out int resultCount) ? resultCount : 0;
        }

        static async Task WriteInFile(List<AdCard> cards)
        {
            try
            {
                var lines = new List<string>
        {
            "index,libraryId"
        };

                lines.AddRange(
                    cards.Select((card, index) => $"{index + 1},{card.LibraryID}")
                );

                await File.WriteAllLinesAsync("output.txt", lines);
                Console.WriteLine($"file saved at {Path.Combine(Directory.GetCurrentDirectory(), "output.txt")}");
            }
            catch (Exception e)
            {
                Console.WriteLine($"failed to write file : {e.Message}");
            }
        }

        static async Task<List<AdCard>> ParseAdCards(HtmlDocument doc, int takeEntity)
        {
            var adList = new List<AdCard>();
            const string parentClasses = "xrvj5dj x18m771g x1p5oq8j xp48ta0 x18d9i69 xtssl2i xtqikln x1na6gtj xjewof7 x1l48g3s x1vql8b3 x1m5622i";
            var parentNode = doc.DocumentNode.SelectSingleNode($"//div[contains(@class, '{parentClasses}')]");

            if (parentNode == null)
            {
                Console.WriteLine("DEBUG: Parent results container not found.");
                return adList;
            }
            var adNodes = parentNode.SelectNodes(".//div[@class='xh8yej3'][div[1][contains(@class, 'x1plvlek')]]"); if (adNodes == null)
            {
                Console.WriteLine("DEBUG: No ad card nodes found inside parent.");
                return adList;
            }
            if (adNodes.Count < takeEntity)
            {
                Console.WriteLine($"there are currently {adNodes.Count} ads on the page . Going to scroll again.");
                await ScrollUntilLoaded(takeEntity);
                doc = GetHtmlDocument();
                return await ParseAdCards(doc, takeEntity);
            }

            var nodesToProcess = adNodes.Take(takeEntity).ToList();

            for (int i = 0; i < nodesToProcess.Count; i++)
            {
                var node = nodesToProcess[i];
                string libraryID = "N/A";

                string nodeHtml = node.InnerHtml;

                if (!string.IsNullOrEmpty(nodeHtml))
                {

                    var match = Regex.Match(nodeHtml, @"Library ID:\s*(\d+)", RegexOptions.IgnoreCase);

                    if (match.Success)
                    {
                        libraryID = match.Groups[1].Value;
                    }
                    else
                    {
                        Console.WriteLine("Direct 'Library ID' text not found in string. Checking for long numeric strings...");
                        var longDigitMatch = Regex.Match(nodeHtml, @"\d{14,16}");
                        if (longDigitMatch.Success)
                        {
                            libraryID = longDigitMatch.Value;
                        }
                    }
                }

                Console.WriteLine($"{i+1}. {libraryID}");
                adList.Add(new AdCard { LibraryID = libraryID });
            }
            return adList;
        }

        static async Task<int> ScrollUntilLoaded(int targetCount, int timeoutSeconds = 300)
        {
            IJavaScriptExecutor js = (IJavaScriptExecutor)_driver;
            int currentDetected = 0;
            int interval = 600;
            var totalStopwatch = Stopwatch.StartNew();
            int index = 0;
            int remainingScrolls = targetCount / 15;
            if (remainingScrolls < 1)
                remainingScrolls = 1;
            Console.WriteLine($"Calculated Scroll count : {remainingScrolls}");
            while (totalStopwatch.Elapsed.TotalSeconds < timeoutSeconds)
            {
                var lapStopwatch = Stopwatch.StartNew();
                js.ExecuteScript("window.scrollTo(0, document.body.scrollHeight);");

                await Task.Delay(interval);
                Console.WriteLine($"Scroll {index + 1} took: {lapStopwatch.Elapsed.TotalMilliseconds:F0}ms | Total: {totalStopwatch.Elapsed.TotalSeconds:F1}s");
                index++;
                if (remainingScrolls == 0)
                {
                    var doc = GetHtmlDocument();
                    const string parentClasses = "xrvj5dj x18m771g x1p5oq8j xp48ta0 x18d9i69 xtssl2i xtqikln x1na6gtj xjewof7 x1l48g3s x1vql8b3 x1m5622i";
                    var parentNode = doc.DocumentNode.SelectSingleNode($"//div[contains(@class, '{parentClasses}')]");

                    if (parentNode != null)
                    {
                        var adNodes = parentNode.SelectNodes(".//div[@class='xh8yej3'][div[1][contains(@class, 'x1plvlek')]]");
                        currentDetected = adNodes?.Count ?? 0;
                    }

                    Console.WriteLine($"Dynamic Scroll: Found {currentDetected}/{targetCount} ads... ");

                    if (currentDetected >= targetCount)
                    {
                        Console.WriteLine("Target reached. Proceeding to extraction.");
                        break;
                    }
                    else
                    {
                        remainingScrolls = (targetCount - currentDetected) / 15;
                        if (remainingScrolls < 1)
                            remainingScrolls = 1;
                    }
                }
                remainingScrolls--;

            }
            Console.WriteLine("Time out reached.Exiting loop");
            return currentDetected;
        }
        static HtmlDocument GetHtmlDocument()
        {
            string htmlContent = _driver.PageSource;
            var doc = new HtmlDocument();
            doc.LoadHtml(htmlContent);
            return doc;
        }
    }
}