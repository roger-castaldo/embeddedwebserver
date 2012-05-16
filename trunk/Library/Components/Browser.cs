using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Org.Reddragonit.EmbeddedWebServer.Components
{
    public class Browser
    {
        private static readonly Regex _REG_VERSION_NUMBER = new Regex("\\d+\\.\\d+(\\.\\d+)*", RegexOptions.Compiled | RegexOptions.ECMAScript);
        private static readonly Regex _REG_SAFARI_MOBILE = new Regex("Version/\\d+\\.\\d+\\s+Mobile(\\s+Safari)?/", RegexOptions.Compiled | RegexOptions.ECMAScript);
        private static readonly Regex _REG_IE_MOBILE = new Regex("(Windows CE;|IEMobile \\d+\\.\\d+;)+", RegexOptions.ECMAScript | RegexOptions.Compiled);
        private static readonly Regex _REG_HP_MOBILE = new Regex("(hp-tablet;|Linux;)+", RegexOptions.Compiled | RegexOptions.ECMAScript);

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

        private bool _isMobile=false;
        public bool IsMobile
        {
            get { return _isMobile; }
        }

        internal Browser(string userAgent)
        {
            _osVersion = new Version("0.0");
            _browserVersion = new Version("0.0");
            string[] tmp = UserAgentTools.getBotName(userAgent);
            if (tmp != null)
            {
                _osType = BrowserOSTypes.Bot;
                _botType = (BotTypes)Enum.Parse(typeof(BotTypes), tmp[0]);
                _browserFamily = BrowserFamilies.Bot;
                _name = tmp[0];
            }
            else
            {
                tmp = UserAgentTools.getOS(userAgent);
                switch (tmp[0])
                {
                    case "Win":
                        _osType = BrowserOSTypes.Windows;
                        break;
                    case "iOS":
                    case "Mac":
                        _osType = BrowserOSTypes.MAC;
                        break;
                    case "Linux":
                    case "*BSD":
                    case "Unix":
                        _osType = BrowserOSTypes.Linux;
                        break;
                    case "BlackBerry":
                        _osType = BrowserOSTypes.BlackBerry;
                        break;
                    default:
                        _osType = BrowserOSTypes.Other;
                        break;


                }
                if (_REG_VERSION_NUMBER.Matches(tmp[2]).Count > 0)
                    _osVersion = new Version(_REG_VERSION_NUMBER.Match(tmp[2]).Value);
                else
                    _osVersion = null;
                _osName = tmp[1];
                tmp = UserAgentTools.getBrowser(userAgent);
                switch (tmp[0])
                {
                    case "LotusNotes":
                        _browserFamily = BrowserFamilies.LotusNotes;
                        break;
                    case "Opera":
                        _browserFamily = BrowserFamilies.Opera;
                        break;
                    case "MSIE":
                        _browserFamily = BrowserFamilies.InternetExplorer;
                        break;
                    case "Gecko":
                        _browserFamily = BrowserFamilies.Gecko;
                        switch (tmp[1])
                        {
                            case "Gecko(Camino)":
                                _browserFamily = BrowserFamilies.Camino;
                                break;
                            case "Gecko(Chimera)":
                                _browserFamily = BrowserFamilies.Chimera;
                                break;
                            case "Gecko(Firebird)":
                                _browserFamily = BrowserFamilies.Firebird;
                                break;
                            case "Gecko(Phoenix)":
                                _browserFamily = BrowserFamilies.Phoenix;
                                break;
                            case "Gecko(Galeon)":
                                _browserFamily = BrowserFamilies.Galeon;
                                break;
                            case "Gecko(Firefox)":
                                _browserFamily = BrowserFamilies.Firefox;
                                break;
                            case "Gecko(NS6)":
                            case "Gecko(NS7)":
                            case "Gecko(NS8)":
                            case "Gecko(NS9)":
                            case "Gecko(NS?)":
                                _browserFamily = BrowserFamilies.Netscape;
                                break;
                        }
                        break;
                    case "NS":
                        _browserFamily = BrowserFamilies.Netscape;
                        break;
                    case "KHTML":
                        switch (tmp[1])
                        {
                            case "KHTML(Chrome)":
                                _browserFamily = BrowserFamilies.Chrome;
                                break;
                            case "KHTML(Safari)":
                                _browserFamily = BrowserFamilies.Safari;
                                break;
                            case "KHTML(Konqueror)":
                                _browserFamily = BrowserFamilies.Konqueror;
                                break;
                            default:
                                _browserFamily = BrowserFamilies.Other;
                                break;
                        }
                        break;
                    case "NetFront":
                        _browserFamily = BrowserFamilies.NetFront;
                        break;
                    case "BlackBerry":
                        _browserFamily = BrowserFamilies.BlackBerry;
                        break;
                    default:
                        _browserFamily = BrowserFamilies.Other;
                        break;
                        
                }
                if (_REG_VERSION_NUMBER.Matches(tmp[2]).Count > 0)
                    _browserVersion = new Version(_REG_VERSION_NUMBER.Match(tmp[2]).Value);
                else
                    _browserVersion = null;
                _name= tmp[1];
                switch (_browserFamily)
                {
                    case BrowserFamilies.BlackBerry:
                        _isMobile = true;
                        break;
                    case BrowserFamilies.Safari:
                        _isMobile = _REG_SAFARI_MOBILE.Matches(userAgent).Count > 0
                            || _REG_HP_MOBILE.Matches(userAgent).Count>=2;
                        break;
                    case BrowserFamilies.InternetExplorer:
                        _isMobile = _REG_IE_MOBILE.Matches(userAgent).Count >= 2;
                        break;
                    case BrowserFamilies.Firefox:
                        _isMobile = (_osName != null ? _osName : "").ToUpper() == "ANDROID";
                        break;
                    case BrowserFamilies.Opera:
                        _isMobile = userAgent.StartsWith("HTC-ST7377") || _name=="Opera Mobi";
                        break;
                }
                _isMobile |= userAgent.Contains("SymbianOS") | (OSType == BrowserOSTypes.BlackBerry);
            }
        }

    }
}
