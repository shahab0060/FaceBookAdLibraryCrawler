using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using WebDriverManager;
using WebDriverManager.DriverConfigs.Impl;
using HtmlAgilityPack;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

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
            Console.WriteLine("Please enter keyword to search . Exmaple : Nike");
            var whatToSearch = Console.ReadLine();
            Console.WriteLine("The Crawler will select 5 items for you.");
            if (string.IsNullOrEmpty(whatToSearch)) return;
            string searchUrl = $"https://www.facebook.com/ads/library/?active_status=active&ad_type=all&country=ALL&is_targeted_country=false&media_type=all&q={whatToSearch}&search_type=keyword_unordered";
            // searchUrl = "https://google.com";
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
                while (remainingAttempts>0)
                {
                    try
                    {
                        wait.Until(driver => driver.FindElement(By.XPath(stableXPathSelector)));
                        break;
                    }
                    catch (Exception e)
                    {
                        remainingAttempts--;
                    }
                }
                int totalResults = GetTotalResults();
                Console.WriteLine($"Found {totalResults:N0} active items for '{whatToSearch}'.");

                await Scroll();

                HtmlDocument? doc = GetHtmlDocument();

                if (doc != null)
                {
                    Console.WriteLine("HTML content fetched and ready for parsing with HtmlAgilityPack.");
                }
                var cards =  await ParseAdCards(doc, 5);
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

        static int GetTotalResults()
        {
            const string stableXPathSelector = "//div[@role='heading' and @aria-level='3']";

            IWebElement resultElement = _driver.FindElement(By.XPath(stableXPathSelector));
            string rawResultText = resultElement.Text;

            if (string.IsNullOrEmpty(rawResultText))
            {
                return 0;
            }

            string numericText = Regex.Replace(rawResultText, @"[^\d,]", "").Replace(",", "");

            if (int.TryParse(numericText, out int resultCount))
            {
                return resultCount;
            }
            else
            {
                Console.WriteLine($"Warning: Could not parse result count text: '{rawResultText}'");
                return 0;
            }
        }

        static async Task<List<AdCard>> ParseAdCards(HtmlDocument doc, int takeEntity,int remainingTries=5)
        {
            var adList = new List<AdCard>();

            const string adCardXPath = "//div[contains(@class, 'xh8yej3')]";

            var adNodes = doc.DocumentNode.SelectNodes(adCardXPath);

            if (adNodes == null)
            {
                Console.WriteLine("No ad cards found with the primary selector.");
                return adList;
            }
            if (adNodes.Count < takeEntity)
            {
                Console.WriteLine($"there are currently {adNodes.Count} ads on the page . Going to scroll again.");
                await Scroll();
                return await ParseAdCards(doc, takeEntity, remainingTries--);
            }
            Console.WriteLine($"Found {adNodes.Count} ad cards. Starting extraction...");

            foreach (var node in adNodes.Take(takeEntity))
            {
                var libraryIdParentNode = node.SelectSingleNode("//div[contains(@class, 'x1rg5ohu x67bb7w')]");
                var libraryIdNode = libraryIdParentNode.FirstChild;
                string libraryIDText = libraryIdNode?.InnerText.Trim() ?? "N/A";
                string libraryID = libraryIDText.Replace("Library ID:", "").Trim();

                Console.WriteLine($"--- Scanning Ad : {libraryIDText} ---");

                
                adList.Add(new AdCard
                {
                    LibraryID = libraryID,
                   
                });
            }

            return adList;
        }

        static async Task Scroll()
        {
            const int scrollAttempts = 5;
            IJavaScriptExecutor js = (IJavaScriptExecutor)_driver;

            Console.WriteLine($"Starting scroll loop ({scrollAttempts} times)...");

            for (int i = 0; i < scrollAttempts; i++)
            {
                js.ExecuteScript("window.scrollTo(0, document.body.scrollHeight)");

                await Task.Delay(2000);
                Console.WriteLine($"Scroll attempt {i + 1} complete.");
            }
        }

        static HtmlDocument? GetHtmlDocument()
        {
            string htmlContent = _driver.PageSource;
            var doc = new HtmlDocument();
            doc.LoadHtml(htmlContent);
            return doc;
        }
    }
}