using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Org.Reddragonit.EmbeddedWebServer.Diagnostics;

namespace Org.Reddragonit.EmbeddedWebServer.Components.Message
{
    public class Browser
    {
        private static readonly Regex _REG_BOT = new Regex("(help.yahoo.com/|msnbot/|google/|googlebot/|webcrawler/|inktomi|teoma)((\\d+\\.)+\\d+)?", RegexOptions.ECMAScript | RegexOptions.Compiled);
        private static readonly Regex _REG_CRACKBERRY_10S = new Regex("^Mozilla/(\\d+\\.\\d+)\\s+\\(Windows NT (\\d+\\.\\d+); WOW\\d+\\)\\s+AppleWebKit/(\\d+\\.\\d+)\\s+\\(KHTML,\\s+like\\s+Gecko\\)\\s+Chrome/(\\d+\\.\\d+\\.\\d+\\.\\d+)\\s+Safari/(\\d+\\.\\d+)$", RegexOptions.Compiled | RegexOptions.ExplicitCapture);
        private static readonly Regex _REG_OS = new Regex("((Windows[- ]NT;?|Windows[ _](9x|ME|CE;)?|Win( 9x|NT)?|Windows|((iPhone|iPod|iPad).+CPU iPhone OS)|Mac OS X|Android|Mac_PowerPC|Macintosh( PPC)?|FreeBSD|OpenBSD|Ubuntu/|Linux|CentOS|NetBSD|Unix|SunOS|IRIX|SonyEricsson|Nokia|BlackBerry|RIM Table OS|SymbianOS|BeOS|Nintendo Wii|J2ME/MIDP)\\s*((\\d+[\\._])*\\d+)?)", RegexOptions.ECMAScript | RegexOptions.Compiled);
        private static readonly Regex _REG_BROWSER = new Regex("(Lotus-Notes|Opera|OPR|Opera Mini|Opera Mobi|MSIE|Trident|Camino|Chimera|Firebird|Phoenix|Galeon|Firefox|Netscape|Gecko|Chrome|Safari|Konqueror|KHTML|NetFront|BlackBerry|Mozilla/4\\.)(/?|\\s*)((\\d+\\.)*\\d+)", RegexOptions.ECMAScript | RegexOptions.Compiled);
        private static readonly Regex _REG_VERSION_NUMBER = new Regex("\\d+\\.\\d+(\\.\\d+)*", RegexOptions.Compiled | RegexOptions.ECMAScript);
        private static readonly Regex _REG_SAFARI_MOBILE = new Regex("Version/\\d+\\.\\d+\\s+Mobile(\\s+Safari)?/", RegexOptions.Compiled | RegexOptions.ECMAScript);
        private static readonly Regex _REG_IE_MOBILE = new Regex("(Windows CE;|IEMobile \\d+\\.\\d+;)+", RegexOptions.ECMAScript | RegexOptions.Compiled);
        private static readonly Regex _REG_HP_MOBILE = new Regex("(hp-tablet;|Linux;)+", RegexOptions.Compiled | RegexOptions.ECMAScript);
        private const string _USER_AGENT_HEADER = "User-Agent";

        private BrowserOSTypes _osType;
        public BrowserOSTypes OSType
        {
            get { return _osType; }
        }

        private Version _osVersion;
        public Version OSVersion
        {
            get { return _osVersion; }
        }

        private string _osName;
        public string OSName
        {
            get { return _osName; }
        }

        private BrowserFamilies _browserFamily;
        public BrowserFamilies BrowserFamily
        {
            get { return _browserFamily; }
        }

        private string _name;
        public string Name
        {
            get { return _name; }
        }

        private Version _browserVersion;
        public Version BrowserVersion
        {
            get { return _browserVersion; }
        }

        private BotTypes? _botType;
        public BotTypes? BotType
        {
            get { return _botType; }
        }

        private bool _isMobile = false;
        public bool IsMobile
        {
            get { return _isMobile; }
        }

        private bool _isTablet = false;
        public bool IsTablet
        {
            get { return _isTablet; }
        }

        internal Browser(string userAgent)
        {
            _osVersion = new Version("0.0");
            _browserVersion = new Version("0.0");
            Match m = _REG_BOT.Match(userAgent);
            if (m.Value != "")
            {
                _osType = BrowserOSTypes.Bot;
                switch (m.Groups[1].Value)
                {
                    case "help.yahoo.com/":
                        _botType = BotTypes.Yahoo;
                        break;
                    case "msnbot/":
                        _botType = BotTypes.MSNBot;
                        break;
                    case "google/":
                    case "googlebot/":
                        _botType = BotTypes.Google;
                        break;
                    case "webcrawler/":
                        _botType = BotTypes.WebCrawler;
                        break;
                    case "inktomi":
                        _botType = BotTypes.Inktomi;
                        break;
                    case "teoma":
                        _botType = BotTypes.Teoma;
                        break;
                }
                _browserFamily = BrowserFamilies.Bot;
                _name = m.Groups[1].Value;
            }
            else
            {
                DateTime start = DateTime.Now;
                if (_REG_CRACKBERRY_10S.IsMatch(userAgent))
                {
                    _osType = BrowserOSTypes.BlackBerry;
                    _osVersion = new Version("10.0");
                    _isMobile = true;
                    _browserFamily = BrowserFamilies.BlackBerry;
                    _browserVersion = new Version("10.0");
                }
                else
                {
                    m = _REG_OS.Match(userAgent);
                    if (!m.Success)
                    {
                        _osName = "Unknown";
                        _osType = BrowserOSTypes.Other;
                        _osVersion = new Version("0.0");
                    }
                    else
                    {
                        if (m.Groups[8].Value != "")
                            _osVersion = new Version(m.Groups[8].Value.Replace("_", "."));
                        switch (m.Groups[2].Value)
                        {
                            case "Windows-NT":
                            case "Windows NT;":
                            case "Windows NT":
                            case "Windows":
                            case "Windows_98":
                            case "Windows 9x":
                            case "Windows ME":
                            case "Windows CE;":
                            case "Win":
                            case "Win 9x":
                            case "WinNT":
                                _osType = BrowserOSTypes.Windows;
                                switch (m.Groups[2].Value)
                                {
                                    case "Windows-NT":
                                    case "Windows NT;":
                                        _osName = "WinNT";
                                        break;
                                    case "Windows NT":
                                        switch (m.Groups[8].Value)
                                        {
                                            case "5.1":
                                                _osName = "WinXP";
                                                break;
                                            case "6.0":
                                                _osName = "Vista";
                                                break;
                                            case "6.1":
                                                _osName = "Seven";
                                                break;
                                            case "6.2":
                                                _osName = "Eight";
                                                break;
                                            case "5.0":
                                                _osName = "Win2000";
                                                break;
                                            case "5.2":
                                                _osName = "Win2003";
                                                break;
                                            case "4.0":
                                                _osName = "WinNT4";
                                                break;
                                            default:
                                                _osName = "WinNT";
                                                break;
                                        }
                                        break;
                                    case "Windows":
                                    case "Win":
                                        switch (m.Groups[8].Value)
                                        {
                                            case "98":
                                                _osName = "Win98";
                                                break;
                                            case "2000":
                                                _osName = "Win2000";
                                                break;
                                            case "95":
                                                _osName = "Win95";
                                                break;
                                            case "3.1":
                                                _osName = "Win31";
                                                break;
                                            default:
                                                _osName = "Win?";
                                                break;
                                        }
                                        break;
                                    case "Windows_98":
                                        _osName = "Win98";
                                        break;
                                    case "Windows 9x":
                                    case "Win 9x":
                                        _osName = "Win9x";
                                        break;
                                    case "Windows ME":
                                        _osName = "WinME";
                                        break;
                                    case "Windows CE;":
                                        _osName = "WinCE";
                                        break;
                                    case "WinNT":
                                        switch (m.Groups[8].Value)
                                        {
                                            case "4.0":
                                                _osName = "WinNT4";
                                                break;
                                            default:
                                                _osName = "WinNT";
                                                break;
                                        }
                                        break;
                                }
                                break;
                            case "Android":
                                _osName = "Android";
                                _osType = BrowserOSTypes.Linux;
                                break;
                            case "FreeBSD":
                            case "OpenBSD":
                            case "Ubuntu/":
                            case "Linux":
                            case "CentOS":
                            case "NetBSD":
                            case "Unix":
                            case "SunOS":
                            case "IRIX":
                                _osType = BrowserOSTypes.Linux;
                                _osName = m.Groups[2].Value;
                                break;
                            case "Nokia":
                            case "SonyEricsson":
                            case "SymbianOS":
                            case "BeOS":
                            case "Nintendo Wii":
                            case "J2ME/MIDP":
                                _osName = m.Groups[2].Value;
                                _osType = BrowserOSTypes.Other;
                                break;
                            case "BlackBerry":
                            case "RIM Tablet OS":
                                _osName = "BlackBerry";
                                _osType = BrowserOSTypes.BlackBerry;
                                if (m.Groups[2].Value == "RIM Tablet OS")
                                    _isTablet = true;
                                else
                                    _isMobile = true;
                                break;
                            case "Mac OS X":
                            case "Macintosh":
                                _osType = BrowserOSTypes.MAC;
                                _osName = m.Groups[8].Value;
                                break;
                            case "MAC_PowerPC":
                            case "Macintosh PPC":
                                _osType = BrowserOSTypes.MAC;
                                _osName = "MacPPC";
                                break;
                            default:
                                switch (m.Groups[6].Value)
                                {
                                    case "iPhone":
                                    case "iPod":
                                    case "iPad":
                                        _osType = BrowserOSTypes.MAC;
                                        _osName = "iOS-" + m.Groups[6].Value;
                                        switch (m.Groups[6].Value)
                                        {
                                            case "iPad":
                                                _isMobile = false;
                                                _isTablet = true;
                                                break;
                                            default:
                                                _isMobile = true;
                                                break;

                                        }
                                        break;
                                }
                                break;
                        }
                        Logger.Debug("Time to process OS from User Agent: " + DateTime.Now.Subtract(start).TotalMilliseconds.ToString() + "ms");
                    }
                    start = DateTime.Now;
                    MatchCollection mc = _REG_BROWSER.Matches(userAgent);
                    if (mc.Count > 0)
                    {
                        m = mc[mc.Count - 1];
                        _browserVersion = (m.Groups[4].Value == "" ? null : new Version(m.Groups[3].Value));
                        switch (m.Groups[1].Value)
                        {
                            case "Lotus-Notes":
                                _browserFamily = BrowserFamilies.LotusNotes;
                                break;
                            case "Opera":
                            case "OPR":
                            case "Opera Mini":
                            case "Opera Mobi":
                                _browserFamily = BrowserFamilies.Opera;
                                switch (m.Groups[1].Value)
                                {
                                    case "Opera Mini":
                                    case "Opera Mobi":
                                        _isMobile = true;
                                        break;
                                }
                                break;
                            case "MSIE":
                            case "Trident":
                                _browserFamily = BrowserFamilies.InternetExplorer;
                                if (userAgent.Contains("rv:"))
                                    _browserVersion = new Version(_REG_VERSION_NUMBER.Match(userAgent, userAgent.IndexOf("rv:")).Value);
                                else if (userAgent.Contains("compatible;"))
                                    _browserVersion = new Version("7.0");
                                break;
                            case "Mozilla/4.":
                                _browserFamily = BrowserFamilies.Other;
                                break;
                            default:
                                if (new List<string>(Enum.GetNames(typeof(BrowserFamilies))).Contains(m.Groups[1].Value))
                                    _browserFamily = (BrowserFamilies)Enum.Parse(typeof(BrowserFamilies), m.Groups[1].Value);
                                else
                                    _browserFamily = BrowserFamilies.Other;
                                break;
                        }
                    }
                    else
                    {
                        _browserFamily = BrowserFamilies.Other;
                        _browserVersion = new Version("0.0");
                    }
                    Logger.Debug("Time to process Browser from User Agent: " + DateTime.Now.Subtract(start).TotalMilliseconds.ToString() + "ms");
                }
                switch (_browserFamily)
                {
                    case BrowserFamilies.Safari:
                        _isMobile = _REG_SAFARI_MOBILE.Matches(userAgent).Count > 0
                            || _REG_HP_MOBILE.Matches(userAgent).Count >= 2;
                        break;
                    case BrowserFamilies.InternetExplorer:
                        _isMobile = _REG_IE_MOBILE.Matches(userAgent).Count >= 2;
                        break;
                    case BrowserFamilies.Firefox:
                        _isMobile = (_osName != null ? _osName : "").ToUpper() == "ANDROID";
                        break;
                    case BrowserFamilies.Opera:
                        _isMobile |= userAgent.StartsWith("HTC-ST7377");
                        break;
                }
                _isMobile |= userAgent.Contains("SymbianOS");
            }
        }
    }
}
