using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using NDesk.Options;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;

namespace InstagramBot
{
    class Program
    {
        const char SEPARATOR_CHAR = '\n';
        public static List<string> latestLogs = new List<string>();
        public static void Log(string text = "", int padding = 0)
        {
            string l_text = $"{Username ?? Environment.UserName}: {DateTime.Now.ToShortDateString()} {DateTime.Now.ToShortTimeString()} [{followed,4} / {liked,-4}] {new string(' ', padding * 3)}{text}";
            latestLogs.Add(l_text);
            Console.WriteLine(l_text);
            Append("log", l_text);
        }

        public static void Sleep(double amount) => Thread.Sleep((int)(amount + randomEngine.Next(2000, 10000)));

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        const int SW_HIDE = 0;
        const int SW_SHOW = 5;

        public static string Get(string name)
        {
            try
            {
                return System.IO.File.ReadAllText("data/" + Username + "/" + name);
            }
            catch (Exception)
            {
                return "";
            }
        }

        public static DateTime? GetVarDate(string name)
        {
            try
            {
                return System.IO.File.GetLastWriteTime("data/" + Username + "/" + name);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static void Set(string name, string data)
        {
            if (!Directory.Exists("data/" + Username))
            {
                Directory.CreateDirectory("data/" + Username);
            }
            System.IO.File.WriteAllText("data/" + Username + "/" + name, data);
        }

        public static void Append(string name, string data)
        {
            if (!Directory.Exists("data/" + Username))
            {
                Directory.CreateDirectory("data/" + Username);
            }
            string path = "data/" + Username + "/" + name;
            System.IO.File.AppendAllLines(path, new string[] { (Get(path) + SEPARATOR_CHAR + data).Trim() });
        }

        public static void ShowErrorAndQuit(string error)
        {
            Console.Write("Instasharp: ");
            Console.WriteLine(error);
            Console.WriteLine();
            Console.WriteLine("Try `Instasharp --help' for help.");
            Environment.Exit(1);
        }

        public static IntPtr windowHandle = IntPtr.Zero;
        public static OpenQA.Selenium.IWebDriver GetBrowserWebdriver()
        {
            try
            { // kill chrome
                foreach (Process p in Process.GetProcessesByName("chrome"))
                {
                    p.Kill();
                }
                foreach (Process p in Process.GetProcessesByName("chromedriver"))
                {
                    p.Kill();
                }
            }
            catch (Exception) { }
            var chromeDriverService = ChromeDriverService.CreateDefaultService();
            chromeDriverService.HideCommandPromptWindow = true;
            ChromeOptions options = new ChromeOptions();

            options.AddArguments("no-sandbox");
            //options.AddArguments("incognito");
            options.AddArguments("enable-features=NetworkServiceInProcess");

            var driver = new ChromeDriver(chromeDriverService, options);

            if (!Debug)
            {
                Process[] processesChrome = Process.GetProcessesByName("chrome");
                if (processesChrome.Length > 0)
                {
                    foreach (Process proc in processesChrome)
                    {
                        windowHandle = proc.MainWindowHandle;
                        if (windowHandle == IntPtr.Zero)
                            continue;
                        else
                        {
                            ShowWindow(windowHandle, SW_HIDE);
                            break;
                        }
                    }
                }
            }

            return driver;
        }

        const int MAX_LIKES = 350;

        static string Username = "";
        static string Password = "";
        static int LikesPerProfile = 5;
        static bool RandomLikes = false;
        static bool FollowUsers = false;
        static bool SkipFollowing = false;
        static bool Debug = false;
        static List<string> hashtags = new List<string>();

        static Stopwatch timeCounter = new Stopwatch();
        static int followed = 0;
        static int liked = 0;

        static bool show_help = false;
        static int timeouts = 3000;
        static Random randomEngine = new Random();

        static void Main(string[] args)
        {
            var p = new OptionSet() {
                { "u|user=", "Instagram username.",
                   v => Username = v },
                { "p|password=", "Instagram user password",
                   v => Password = v },
                { "s|hash=", "Add a Hashtag to follow in.",
                   v => hashtags.Add(v) },
                { "l|likesperpage=", "Number of Likes per profile",
                   v => LikesPerProfile = Int32.Parse(v) },
                { "r|random", "Makes the liking of photos be random.",
                   v => RandomLikes = true },
                { "f|followusers", "Follow users when entering in their page.",
                   v => FollowUsers = true },
                { "k|skipfollowing", "Skip following users.",
                   v => SkipFollowing = true },
                { "d|debug", "Enable debugging options.",
                   v => Debug = true },
                { "h|help",  "Shows this screen.",
                   v => show_help = v != null },
            };

            List<string> extra = new List<string>();
            try
            {
                extra = p.Parse(args);
            }
            catch (OptionException e)
            {
                ShowErrorAndQuit(e.Message);
                return;
            }

            if (show_help)
            {
                Console.WriteLine("Instasharp version 1.0");
                Console.WriteLine("by CypherPotato");
                Console.WriteLine();
                Console.WriteLine("Options:");
                p.WriteOptionDescriptions(Console.Out);
                Environment.Exit(1);
            }

            if (Username == "")
                ShowErrorAndQuit("Missing argument: username");
            if (Password == "")
                ShowErrorAndQuit("Missing argument: password");
            if (hashtags.Count == 0)
                ShowErrorAndQuit("Missing items for argument: hash");

            Username = Username.ToLower();

            restartEverything:
            Log("Starting INSTASHARP for @" + Username);
            try
            {
                DateTime? lastEdit = GetVarDate("liked");

                if (lastEdit != null && (DateTime.Now - lastEdit).Value.TotalDays >= 1)
                {
                    Log("You haven't been in Bot for a long time, have you? Resetting the date values.");
                    Set("liked", "0");
                    Set("followed", "0");
                }

                liked = Get("liked").Split(SEPARATOR_CHAR).Length;
                followed = Get("followed").Split(SEPARATOR_CHAR).Length;

                Log("Creating an new webdriver instance");

                IWebDriver driver = GetBrowserWebdriver();
                // try login
                driver.Navigate().GoToUrl("https://www.instagram.com/");

                if (Get("cookies") != "")
                {
                    Log("Logging-in using cookies...");
                    string[] C = Get("cookies").Split('\n');
                    foreach (string b in C)
                    {
                        if (string.IsNullOrWhiteSpace(b)) continue;
                        string[] c = b.Split('§');
                        if (long.TryParse(c[4], out var cookieTime))
                        {
                            driver.Manage().Cookies.AddCookie(new Cookie(c[0], c[1], c[2], c[3], DateTime.FromBinary(cookieTime)));
                        }
                        else
                        {
                            driver.Manage().Cookies.AddCookie(new Cookie(c[0], c[1], c[2], c[3], null));
                        }
                    }
                }
                else
                {
                    IWebElement userField = WaitForElement(driver, By.Name("username"), (int)(timeouts * 1.5));

                    if (userField != null)
                    {
                        Log("Logging-in using authentication...");
                        userField.SendKeys(Username);
                        driver.FindElement(By.Name("password")).SendKeys(Password + Keys.Enter);

                        if (WaitForElement(driver, By.Id("slfErrorAlert"), 3000) != null)
                        {
                            Log("Log-in failed");
                            Environment.Exit(1);
                        }
                        // if captcha
                        while (driver.PageSource.Contains("captcha"))
                        {
                            ShowWindow(windowHandle, SW_SHOW);
                            Log("Requested Captcha. Solve it to continue checking and press any key to continue. . .");
                            Console.ReadKey();
                            Thread.Sleep(timeouts);
                        }
                        if (WaitForElement(driver, By.XPath("/html/body/div[1]/section/div/div/div[3]/form/span/button"), 1500) != null)
                        {
                            ShowWindow(windowHandle, SW_SHOW);
                            Log("Requested 2FA. Solve it to continue checking and press any key to continue. . .");
                            Console.ReadKey();
                            Thread.Sleep(timeouts);
                        }

                        // save browser = no
                        WaitForElement(driver, By.XPath("/html/body/div[1]/section/main/div/div/div/section/div/button"), timeouts)?.Click();
                        // notifications = no
                        WaitForElement(driver, By.XPath("/html/body/div[4]/div/div/div/div[3]/button[2]"), timeouts)?.Click();
                    }

                    ShowWindow(windowHandle, SW_HIDE);

                    Set("cookies", "");
                    foreach (var cookie in driver.Manage().Cookies.AllCookies)
                    {
                        if (cookie.Expiry == null)
                        {
                            Append("cookies", $"{cookie.Name}§{cookie.Value}§{cookie.Domain}§{cookie.Path}§");
                        }
                        else
                        {
                            Append("cookies", $"{cookie.Name}§{cookie.Value}§{cookie.Domain}§{cookie.Path}§{cookie.Expiry.Value.ToBinary()}");
                        }
                    }
                }

                List<string> Following = Get("following").Split(SEPARATOR_CHAR).ToList();
                List<string> alreadyLiked = Get("alreadyLiked").Split(SEPARATOR_CHAR).ToList();

                Log("Total of liked on last session: " + liked);

                var hashtagsFormatted = Shuffle(randomEngine, hashtags.ToArray());

                int w = 0;
                while (liked < MAX_LIKES)
                {
                    var hashtag = hashtagsFormatted[w % hashtagsFormatted.Length];
                    w++;
                    if (w == Int32.MaxValue - 1)
                        w = 0;

                    Log("Hashtag: #" + hashtag.ToLower());

                    driver.Url = ("https://www.instagram.com/explore/tags/" + hashtag);
                    Sleep(timeouts);

                    var _posts = Regex.Matches(driver.PageSource, @"/p/[a-zA-Z0-9]*/");
                    var _postsL = new List<string>();
                    foreach (Match m in _posts)
                        _postsL.Add(m.Value);
                    var posts = _postsL.ToArray();

                    posts = Shuffle(randomEngine, posts);

                    // run into ten users
                    for (int i = 0; i <= 10; i++)
                    {
                        var post = posts[i % (posts.Length)];
                        driver.Url = "https://www.instagram.com" + post;

                        if (liked < MAX_LIKES)
                        {
                            // go to person profile
                            IWebElement followButton = WaitForElement(driver, By.XPath("/html/body/div[1]/section/main/div/div[1]/article/header/div[2]/div[1]/div[1]/span/a"), timeouts);
                        
                            if (followButton != null)
                            {
                                string username = followButton.Text.Trim();
                                driver.Url = "https://www.instagram.com/" + username;
                                Log("User @" + username + ":", 1);

                                if (FollowUsers)
                                {
                                    var x = WaitForElement(driver, By.XPath("/html/body/div[1]/section/main/div/header/section/div[1]/div[1]/div/div/div/span/span[1]/button"), 3000);
                                    if (x != null)
                                    {
                                        x.Click();

                                        if(WaitForElement(driver, By.XPath("/html/body/div[4]/div/div/div/div[2]/button[2]"), 1000) != null)
                                        {
                                            Log("Following was blocked. Turning off the bot during some time...");
                                            goto exit;
                                        } else
                                        {
                                            Log("Followed!", 2);
                                            followed++;
                                            Append("followed", username);
                                            Sleep(timeouts / 2);
                                        }

                                    } else
                                    {
                                        Log("User already following", 2);
                                        if(SkipFollowing)
                                        {
                                            Log("Skipping...", 3);
                                            i--;
                                            posts = Shuffle(randomEngine, posts);
                                            goto skipUser;
                                        }
                                    }
                                    if (Following.Contains(username))
                                    {
                                        Log("Already followed " + username + " - skipping", 2);
                                        continue;
                                    }
                                }

                                var _xposts = Regex.Matches(driver.PageSource, @"/p/[a-zA-Z0-9]*/");
                                var _xpostsL = new List<string>();
                                foreach (Match m in _xposts)
                                    _xpostsL.Add(m.Value);
                                var xposts = _xpostsL.ToArray();

                                xposts = Shuffle(randomEngine, xposts);
                                int number = randomEngine.Next(Math.Max(LikesPerProfile - 2, 1), Math.Min(LikesPerProfile + 2, 10));
                                for (int j = 0; j < number; j++)
                                {
                                    var xpost = xposts[RandomLikes ? randomEngine.Next(0, xposts.Length - 1) : j % xposts.Length];
                                    driver.Url = "https://www.instagram.com" + xpost;

                                    if (liked < MAX_LIKES)
                                    {
                                        if (driver.PageSource.Contains("#ed4956"))
                                        {
                                            Log("Already liked " + xpost + " - skipping", 3);
                                            continue;
                                        }
                                        var clickElement = WaitForElement(driver, By.XPath("/html/body/div[1]/section/main/div/div/article/div[3]/section[1]/span[1]/button"), 5000);

                                        if(clickElement == null)
                                        {
                                            Log("Cannot like " + xpost + " - skipping", 3);
                                        } else
                                        {
                                            clickElement.Click();
                                        }

                                        if (WaitForElement(driver, By.XPath("/html/body/div[4]/div/div/div/div[2]/button[1]"), timeouts / 2) != null)
                                        {
                                            Log("Liking was blocked. Turning off the bot during some time...");
                                            goto exit;
                                        }

                                        liked++;
                                        if (alreadyLiked.Contains(xpost))
                                        {
                                            Log("Already liked " + xpost + " - skipping", 3);
                                            continue;
                                        }
                                        Log("Liked " + $"({j + 1}/{number}): " + xpost, 3);
                                        Append("liked", xpost);
                                        Sleep(timeouts / 2);
                                    }
                                    else
                                    {
                                        goto exit;
                                    }
                                }
                                skipUser:
                                if (false) { };
                            }
                            else
                            {
                                goto exit;
                            }
                        }
                        else
                        {
                            goto exit;
                        }
                    }
                    SleepForMinutes(randomEngine.Next(5, 200));
                }
                exit:
                driver.Quit();
                SleepSession();
                Set("liked", "");
                goto restartEverything;
            } catch (Exception ex)
            {
               Log("Unhandled exception: " + ex.Message);
               Log("Restarting the application main module.");
               Log();
               Log();
               goto restartEverything;
            }
        }

        public static void SleepForMinutes(int minutes)
        {
            var time = TimeSpan.FromMinutes(minutes);
            Log($"Sleeping for {time.TotalMinutes} minutes");
            Sleep(time.TotalMilliseconds);
        }

        public static void SleepSession()
        {
            DateTime lastTime = System.IO.File.GetLastWriteTime("data/" + Username + "/liked");
            TimeSpan period = DateTime.Now - lastTime;

            Log($"Session ended. Sleeping for {period.Days}d {period.Hours}h {period.Minutes}m {period.Seconds}s");
            Sleep(period.TotalMilliseconds);
        }

        public static T[] Shuffle<T>(Random rng, T[] array)
        {
            return array.OrderBy(x => rng.Next()).ToArray();
        }
        public static IWebElement WaitForElement(IWebDriver driver, By byElement, int timeoutMs)
        {
            Stopwatch w = new Stopwatch();
            w.Start();
            while (w.ElapsedMilliseconds < timeoutMs)
            {
                var x = driver.FindElements(byElement);
                if (x.Count != 0)
                {
                    return x[0];
                }
                else
                {
                    System.Threading.Thread.Sleep(100);
                }
            }
            return null;
        }

        public static bool WaitForCondition(Func<bool> condition, int timeoutMs)
        {
            Stopwatch w = new Stopwatch();
            w.Start();
            while (w.ElapsedMilliseconds < timeoutMs)
            {
                if (condition())
                {
                    return true;
                }
                else
                {
                    System.Threading.Thread.Sleep(400);
                }
            }
            return false;
        }
    }
}
