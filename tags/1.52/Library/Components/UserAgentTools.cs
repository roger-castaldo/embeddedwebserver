﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Org.Reddragonit.EmbeddedWebServer.Components
{
    internal static class UserAgentTools
    {

        public static string getFirstVersionNumber(string a_userAgent, int a_position, int numDigits)
        {
            string ver = getVersionNumber(a_userAgent, a_position);
            if (ver == null) return "";
            int i = 0;
            string res = "";
            while (i < ver.Length && i < numDigits)
            {
                res += ver[i].ToString();
                i++;
            }
            return res;
        }

        public static string getVersionNumber(string a_userAgent, int a_position)
        {
            if (a_position < 0) return "";
            StringBuilder res = new StringBuilder();
            int status = 0;

            while (a_position < a_userAgent.Length)
            {
                char c = a_userAgent[a_position];
                switch (status)
                {
                    case 0: //<SPAN class="codecomment"> No valid digits encountered yet</span>
                        if (c == ' ' || c == '/') break;
                        if (c == ';' || c == ')') return "";
                        if (char.IsDigit(c))
                            res.Append(c);
                        status = 1;
                        break;
                    case 1: //<SPAN class="codecomment"> Version number in progress</span>
                        if (c == ';' || c == '/' || c == ')' || c == '(' || c == '[') return res.ToString().Trim();
                        if (c == ' ') status = 2;
                        res.Append(c);
                        break;
                    case 2: //<SPAN class="codecomment"> Space encountered - Might need to end the parsing</span>
                        if ((char.IsLetter(c) &&
                             (c == char.ToLower(c))) ||
                            char.IsDigit(c))
                        {
                            res.Append(c);
                            status = 1;
                        }
                        else
                            return res.ToString().Trim();
                        break;
                }
                a_position++;
            }
            return res.ToString().Trim();
        }

        public static string[] getArray(string a, string b, string c)
        {
            string[] res = new string[3];
            res[0] = a;
            res[1] = b;
            res[2] = c;
            return res;
        }

        public static string[] getBotName(string userAgent)
        {
            userAgent = userAgent.ToLower();
            int pos = 0;
            string res = null;
            if ((pos = userAgent.IndexOf("help.yahoo.com/")) > -1)
            {
                res = "Yahoo";
                pos += 7;
            }
            else
                if ((pos = userAgent.IndexOf("google/")) > -1)
                {
                    res = "Google";
                    pos += 7;
                }
                else
                    if ((pos = userAgent.IndexOf("msnbot/")) > -1)
                    {
                        res = "MSNBot";
                        pos += 7;
                    }
                    else
                        if ((pos = userAgent.IndexOf("googlebot/")) > -1)
                        {
                            res = "Google";
                            pos += 10;
                        }
                        else
                            if ((pos = userAgent.IndexOf("webcrawler/")) > -1)
                            {
                                res = "WebCrawler";
                                pos += 11;
                            }
                            else
                                //<SPAN class="codecomment"> The following two bots don't have any version number in their User-Agent strings.</span>
                                if ((pos = userAgent.IndexOf("inktomi")) > -1)
                                {
                                    res = "Inktomi";
                                    pos = -1;
                                }
                                else
                                    if ((pos = userAgent.IndexOf("teoma")) > -1)
                                    {
                                        res = "Teoma";
                                        pos = -1;
                                    }
            if (res == null) return null;
            return getArray(res, res, res + getVersionNumber(userAgent, pos));
        }


        public static string[] getOS(string userAgent)
        {
            if (getBotName(userAgent) != null) return getArray("Bot", "Bot", "Bot");
            string[] res = null;
            int pos;
            if ((pos = userAgent.IndexOf("Windows-NT")) > -1)
            {
                res = getArray("Win", "WinNT", "Win" + getVersionNumber(userAgent, pos + 8));
            }
            else
                if (userAgent.IndexOf("Windows NT") > -1)
                {
                    //<SPAN class="codecomment"> The different versions of Windows NT are decoded in the verbosity level 2</span>
                    //<SPAN class="codecomment"> ie: Windows NT 5.1 = Windows XP</span>
                    if ((pos = userAgent.IndexOf("Windows NT 5.1")) > -1)
                    {
                        res = getArray("Win", "WinXP", "Win" + getVersionNumber(userAgent, pos + 7));
                    }
                    else
                        if ((pos = userAgent.IndexOf("Windows NT 6.0")) > -1)
                        {
                            res = getArray("Win", "Vista", "Vista" + getVersionNumber(userAgent, pos + 7));
                        }
                        else
                            if ((pos = userAgent.IndexOf("Windows NT 6.1")) > -1)
                            {
                                res = getArray("Win", "Seven", "Seven " + getVersionNumber(userAgent, pos + 7));
                            }
                            else
                                if ((pos = userAgent.IndexOf("Windows NT 5.0")) > -1)
                                {
                                    res = getArray("Win", "Win2000", "Win" + getVersionNumber(userAgent, pos + 7));
                                }
                                else
                                    if ((pos = userAgent.IndexOf("Windows NT 5.2")) > -1)
                                    {
                                        res = getArray("Win", "Win2003", "Win" + getVersionNumber(userAgent, pos + 7));
                                    }
                                    else
                                        if ((pos = userAgent.IndexOf("Windows NT 4.0")) > -1)
                                        {
                                            res = getArray("Win", "WinNT4", "Win" + getVersionNumber(userAgent, pos + 7));
                                        }
                                        else
                                            if ((pos = userAgent.IndexOf("Windows NT)")) > -1)
                                            {
                                                res = getArray("Win", "WinNT", "WinNT");
                                            }
                                            else
                                                if ((pos = userAgent.IndexOf("Windows NT;")) > -1)
                                                {
                                                    res = getArray("Win", "WinNT", "WinNT");
                                                }
                                                else
                                                    res = getArray("Win", "<B>WinNT?</B>", "<B>WinNT?</B>");
                }
                else
                    if (userAgent.IndexOf("Win") > -1)
                    {
                        if (userAgent.IndexOf("Windows") > -1)
                        {
                            if ((pos = userAgent.IndexOf("Windows 98")) > -1)
                            {
                                res = getArray("Win", "Win98", "Win" + getVersionNumber(userAgent, pos + 7));
                            }
                            else
                                if ((pos = userAgent.IndexOf("Windows_98")) > -1)
                                {
                                    res = getArray("Win", "Win98", "Win" + getVersionNumber(userAgent, pos + 8));
                                }
                                else
                                    if ((pos = userAgent.IndexOf("Windows 2000")) > -1)
                                    {
                                        res = getArray("Win", "Win2000", "Win" + getVersionNumber(userAgent, pos + 7));
                                    }
                                    else
                                        if ((pos = userAgent.IndexOf("Windows 95")) > -1)
                                        {
                                            res = getArray("Win", "Win95", "Win" + getVersionNumber(userAgent, pos + 7));
                                        }
                                        else
                                            if ((pos = userAgent.IndexOf("Windows 9x")) > -1)
                                            {
                                                res = getArray("Win", "Win9x", "Win" + getVersionNumber(userAgent, pos + 7));
                                            }
                                            else
                                                if ((pos = userAgent.IndexOf("Windows ME")) > -1)
                                                {
                                                    res = getArray("Win", "WinME", "Win" + getVersionNumber(userAgent, pos + 7));
                                                }
                                                else
                                                    if ((pos = userAgent.IndexOf("Windows CE;")) > -1)
                                                    {
                                                        res = getArray("Win", "WinCE", "WinCE");
                                                    }
                                                    else
                                                        if ((pos = userAgent.IndexOf("Windows 3.1")) > -1)
                                                        {
                                                            res = getArray("Win", "Win31", "Win" + getVersionNumber(userAgent, pos + 7));
                                                        }
                            //<SPAN class="codecomment"> If no version was found, rely on the following code to detect "WinXX"</span>
                            //<SPAN class="codecomment"> As some User-Agents include two references to Windows</span>
                            //<SPAN class="codecomment"> Ex: Mozilla/5.0 (Windows; U; Win98; en-US; rv:1.5)</span>
                        }
                        if (res == null)
                        {
                            if ((pos = userAgent.IndexOf("Win98")) > -1)
                            {
                                res = getArray("Win", "Win98", "Win" + getVersionNumber(userAgent, pos + 3));
                            }
                            else
                                if ((pos = userAgent.IndexOf("Win31")) > -1)
                                {
                                    res = getArray("Win", "Win31", "Win" + getVersionNumber(userAgent, pos + 3));
                                }
                                else
                                    if ((pos = userAgent.IndexOf("Win95")) > -1)
                                    {
                                        res = getArray("Win", "Win95", "Win" + getVersionNumber(userAgent, pos + 3));
                                    }
                                    else
                                        if ((pos = userAgent.IndexOf("Win 9x")) > -1)
                                        {
                                            res = getArray("Win", "Win9x", "Win" + getVersionNumber(userAgent, pos + 3));
                                        }
                                        else
                                            if ((pos = userAgent.IndexOf("WinNT4.0")) > -1)
                                            {
                                                res = getArray("Win", "WinNT4", "Win" + getVersionNumber(userAgent, pos + 3));
                                            }
                                            else
                                                if ((pos = userAgent.IndexOf("WinNT")) > -1)
                                                {
                                                    res = getArray("Win", "WinNT", "Win" + getVersionNumber(userAgent, pos + 3));
                                                }
                        }
                        if (res == null)
                        {
                            if ((pos = userAgent.IndexOf("Windows")) > -1)
                            {
                                res = getArray("Win", "<B>Win?</B>", "<B>Win?" + getVersionNumber(userAgent, pos + 7) + "</B>");
                            }
                            else
                                if ((pos = userAgent.IndexOf("Win")) > -1)
                                {
                                    res = getArray("Win", "<B>Win?</B>", "<B>Win?" + getVersionNumber(userAgent, pos + 3) + "</B>");
                                }
                                else
                                    //<SPAN class="codecomment"> Should not happen at this point</span>
                                    res = getArray("Win", "<B>Win?</B>", "<B>Win?</B>");
                        }
                    }
                    else
                        if ((pos = userAgent.IndexOf("Mac OS X")) > -1)
                        {
                            if ((userAgent.IndexOf("iPhone")) > -1)
                            {
                                pos = userAgent.IndexOf("iPhone OS");
                                if ((userAgent.IndexOf("iPod")) > -1)
                                {
                                    res = getArray("iOS", "iOS-iPod", "iOS-iPod " + ((pos < 0) ? "" : getVersionNumber(userAgent, pos + 9)));
                                }
                                else
                                {
                                    res = getArray("iOS", "iOS-iPhone", "iOS-iPhone " + ((pos < 0) ? "" : getVersionNumber(userAgent, pos + 9)));
                                }
                            }
                            else
                                if ((userAgent.IndexOf("iPad")) > -1)
                                {
                                    pos = userAgent.IndexOf("CPU OS");
                                    res = getArray("iOS", "iOS-iPad", "iOS-iPad " + ((pos < 0) ? "" : getVersionNumber(userAgent, pos + 6)));
                                }
                                else
                                    res = getArray("Mac", "MacOSX", "MacOS " + getVersionNumber(userAgent, pos + 8));
                        }
                        else
                            if ((pos = userAgent.IndexOf("Android")) > -1)
                            {
                                res = getArray("Linux", "Android", "Android " + getVersionNumber(userAgent, pos + 8));
                            }
                            else
                                if ((pos = userAgent.IndexOf("Mac_PowerPC")) > -1)
                                {
                                    res = getArray("Mac", "MacPPC", "MacOS " + getVersionNumber(userAgent, pos + 3));
                                }
                                else
                                    if ((pos = userAgent.IndexOf("Macintosh")) > -1)
                                    {
                                        if (userAgent.IndexOf("PPC") > -1)
                                            res = getArray("Mac", "MacPPC", "Mac PPC");
                                        else
                                            res = getArray("Mac?", "Mac?", "MacOS?");
                                    }
                                    else
                                        if ((pos = userAgent.IndexOf("FreeBSD")) > -1)
                                        {
                                            res = getArray("*BSD", "*BSD FreeBSD", "FreeBSD " + getVersionNumber(userAgent, pos + 7));
                                        }
                                        else
                                            if ((pos = userAgent.IndexOf("OpenBSD")) > -1)
                                            {
                                                res = getArray("*BSD", "*BSD OpenBSD", "OpenBSD " + getVersionNumber(userAgent, pos + 7));
                                            }
                                            else
                                                if ((pos = userAgent.IndexOf("Linux")) > -1)
                                                {
                                                    string detail = "Linux " + getVersionNumber(userAgent, pos + 5);
                                                    string med = "Linux";
                                                    if ((pos = userAgent.IndexOf("Ubuntu/")) > -1)
                                                    {
                                                        detail = "Ubuntu " + getVersionNumber(userAgent, pos + 7);
                                                        med += " Ubuntu";
                                                    }
                                                    res = getArray("Linux", med, detail);
                                                }
                                                else
                                                    if ((pos = userAgent.IndexOf("CentOS")) > -1)
                                                    {
                                                        res = getArray("Linux", "Linux CentOS", "CentOS");
                                                    }
                                                    else
                                                        if ((pos = userAgent.IndexOf("NetBSD")) > -1)
                                                        {
                                                            res = getArray("*BSD", "*BSD NetBSD", "NetBSD " + getVersionNumber(userAgent, pos + 6));
                                                        }
                                                        else
                                                            if ((pos = userAgent.IndexOf("Unix")) > -1)
                                                            {
                                                                res = getArray("Linux", "Linux", "Linux " + getVersionNumber(userAgent, pos + 4));
                                                            }
                                                            else
                                                                if ((pos = userAgent.IndexOf("SunOS")) > -1)
                                                                {
                                                                    res = getArray("Unix", "SunOS", "SunOS" + getVersionNumber(userAgent, pos + 5));
                                                                }
                                                                else
                                                                    if ((pos = userAgent.IndexOf("IRIX")) > -1)
                                                                    {
                                                                        res = getArray("Unix", "IRIX", "IRIX" + getVersionNumber(userAgent, pos + 4));
                                                                    }
                                                                    else
                                                                        if ((pos = userAgent.IndexOf("SonyEricsson")) > -1)
                                                                        {
                                                                            res = getArray("SonyEricsson", "SonyEricsson", "SonyEricsson" + getVersionNumber(userAgent, pos + 12));
                                                                        }
                                                                        else
                                                                            if ((pos = userAgent.IndexOf("Nokia")) > -1)
                                                                            {
                                                                                res = getArray("Nokia", "Nokia", "Nokia" + getVersionNumber(userAgent, pos + 5));
                                                                            }
                                                                            else
                                                                                if ((pos = userAgent.IndexOf("BlackBerry")) > -1)
                                                                                {
                                                                                    res = getArray("BlackBerry", "BlackBerry", "BlackBerry" + getVersionNumber(userAgent, pos + 10));
                                                                                }
                                                                                else if ((pos = userAgent.IndexOf("RIM Tablet OS ")) > -1)
                                                                                {
                                                                                    res = getArray("BlackBerry", "PlayBook", "TabletOS" + getVersionNumber(userAgent, pos + 10));
                                                                                }
                                                                                else
                                                                                    if ((pos = userAgent.IndexOf("SymbianOS")) > -1)
                                                                                    {
                                                                                        res = getArray("SymbianOS", "SymbianOS", "SymbianOS" + getVersionNumber(userAgent, pos + 10));
                                                                                    }
                                                                                    else
                                                                                        if ((pos = userAgent.IndexOf("BeOS")) > -1)
                                                                                        {
                                                                                            res = getArray("BeOS", "BeOS", "BeOS");
                                                                                        }
                                                                                        else
                                                                                            if ((pos = userAgent.IndexOf("Nintendo Wii")) > -1)
                                                                                            {
                                                                                                res = getArray("Nintendo Wii", "Nintendo Wii", "Nintendo Wii" + getVersionNumber(userAgent, pos + 10));
                                                                                            }
                                                                                            else
                                                                                                if ((pos = userAgent.IndexOf("J2ME/MIDP")) > -1)
                                                                                                {
                                                                                                    res = getArray("Java", "J2ME", "J2ME/MIDP");
                                                                                                }
                                                                                                else
                                                                                                    res = getArray("<b>?</b>", "<b>?</b>", "<b>?</b>");
            return res;
        }


        public static string[] getBrowser(string userAgent)
        {
            string[] botName;
            if ((botName = getBotName(userAgent)) != null) return botName;
            string[] res = null;
            int pos;
            if ((pos = userAgent.IndexOf("Lotus-Notes/")) > -1)
            {
                res = getArray("LotusNotes", "LotusNotes", "LotusNotes" + getVersionNumber(userAgent, pos + 12));
            }
            else
                if ((pos = userAgent.IndexOf("Opera")) > -1)
                {
                    string ver = getVersionNumber(userAgent, pos + 5);
                    res = getArray("Opera", "Opera" + getFirstVersionNumber(userAgent, pos + 5, 1), "Opera" + ver);
                    if ((pos = userAgent.IndexOf("Opera Mini/")) > -1)
                    {
                        string ver2 = getVersionNumber(userAgent, pos + 11);
                        res = getArray("Opera", "Opera Mini", "Opera Mini " + ver2);
                    }
                    else
                        if ((pos = userAgent.IndexOf("Opera Mobi/")) > -1)
                        {
                            string ver2 = getVersionNumber(userAgent, pos + 11);
                            res = getArray("Opera", "Opera Mobi", "Opera Mobi " + ver2);
                        }
                }
                else
                    if (userAgent.IndexOf("MSIE") > -1)
                    {
                        if ((pos = userAgent.IndexOf("MSIE 6.0")) > -1)
                        {
                            res = getArray("MSIE", "MSIE6", "MSIE" + getVersionNumber(userAgent, pos + 4));
                        }
                        else
                            if ((pos = userAgent.IndexOf("MSIE 5.0")) > -1)
                            {
                                res = getArray("MSIE", "MSIE5", "MSIE" + getVersionNumber(userAgent, pos + 4));
                            }
                            else
                                if ((pos = userAgent.IndexOf("MSIE 5.5")) > -1)
                                {
                                    res = getArray("MSIE", "MSIE5.5", "MSIE" + getVersionNumber(userAgent, pos + 4));
                                }
                                else
                                    if ((pos = userAgent.IndexOf("MSIE 5.")) > -1)
                                    {
                                        res = getArray("MSIE", "MSIE5.x", "MSIE" + getVersionNumber(userAgent, pos + 4));
                                    }
                                    else
                                        if ((pos = userAgent.IndexOf("MSIE 4")) > -1)
                                        {
                                            res = getArray("MSIE", "MSIE4", "MSIE" + getVersionNumber(userAgent, pos + 4));
                                        }
                                        else
                                            if ((pos = userAgent.IndexOf("MSIE 7")) > -1 && userAgent.IndexOf("Trident/4.0") < 0)
                                            {
                                                res = getArray("MSIE", "MSIE7", "MSIE" + getVersionNumber(userAgent, pos + 4));
                                            }
                                            else
                                                if ((pos = userAgent.IndexOf("MSIE 8")) > -1 || userAgent.IndexOf("Trident/4.0") > -1)
                                                {
                                                    res = getArray("MSIE", "MSIE8", "MSIE" + getVersionNumber(userAgent, pos + 4));
                                                }
                                                else
                                                    if ((pos = userAgent.IndexOf("MSIE 9")) > -1 || userAgent.IndexOf("Trident/4.0") > -1)
                                                    {
                                                        res = getArray("MSIE", "MSIE9", "MSIE" + getVersionNumber(userAgent, pos + 4));
                                                    }
                                                    else
                                                        res = getArray("MSIE", "<B>MSIE?</B>", "<B>MSIE?" + getVersionNumber(userAgent, userAgent.IndexOf("MSIE") + 4) + "</B>");
                    }
                    else
                        if ((pos = userAgent.IndexOf("Gecko/")) > -1)
                        {
                            res = getArray("Gecko", "Gecko", "Gecko" + getFirstVersionNumber(userAgent, pos + 5, 4));
                            if ((pos = userAgent.IndexOf("Camino/")) > -1)
                            {
                                res[1] += "(Camino)";
                                res[2] += "(Camino" + getVersionNumber(userAgent, pos + 7) + ")";
                            }
                            else
                                if ((pos = userAgent.IndexOf("Chimera/")) > -1)
                                {
                                    res[1] += "(Chimera)";
                                    res[2] += "(Chimera" + getVersionNumber(userAgent, pos + 8) + ")";
                                }
                                else
                                    if ((pos = userAgent.IndexOf("Firebird/")) > -1)
                                    {
                                        res[1] += "(Firebird)";
                                        res[2] += "(Firebird" + getVersionNumber(userAgent, pos + 9) + ")";
                                    }
                                    else
                                        if ((pos = userAgent.IndexOf("Phoenix/")) > -1)
                                        {
                                            res[1] += "(Phoenix)";
                                            res[2] += "(Phoenix" + getVersionNumber(userAgent, pos + 8) + ")";
                                        }
                                        else
                                            if ((pos = userAgent.IndexOf("Galeon/")) > -1)
                                            {
                                                res[1] += "(Galeon)";
                                                res[2] += "(Galeon" + getVersionNumber(userAgent, pos + 7) + ")";
                                            }
                                            else
                                                if ((pos = userAgent.IndexOf("Firefox/")) > -1)
                                                {
                                                    res[1] += "(Firefox)";
                                                    res[2] += "(Firefox" + getVersionNumber(userAgent, pos + 8) + ")";
                                                }
                                                else
                                                    if ((pos = userAgent.IndexOf("Netscape/")) > -1)
                                                    {
                                                        if ((pos = userAgent.IndexOf("Netscape/6")) > -1)
                                                        {
                                                            res[1] += "(NS6)";
                                                            res[2] += "(NS" + getVersionNumber(userAgent, pos + 9) + ")";
                                                        }
                                                        else
                                                            if ((pos = userAgent.IndexOf("Netscape/7")) > -1)
                                                            {
                                                                res[1] += "(NS7)";
                                                                res[2] += "(NS" + getVersionNumber(userAgent, pos + 9) + ")";
                                                            }
                                                            else
                                                                if ((pos = userAgent.IndexOf("Netscape/8")) > -1)
                                                                {
                                                                    res[1] += "(NS8)";
                                                                    res[2] += "(NS" + getVersionNumber(userAgent, pos + 9) + ")";
                                                                }
                                                                else
                                                                    if ((pos = userAgent.IndexOf("Netscape/9")) > -1)
                                                                    {
                                                                        res[1] += "(NS9)";
                                                                        res[2] += "(NS" + getVersionNumber(userAgent, pos + 9) + ")";
                                                                    }
                                                                    else
                                                                    {
                                                                        res[1] += "(NS?)";
                                                                        res[2] += "(NS?" + getVersionNumber(userAgent, userAgent.IndexOf("Netscape/") + 9) + ")";
                                                                    }
                                                    }
                        }
                        else
                            if ((pos = userAgent.IndexOf("Netscape/")) > -1)
                            {
                                if ((pos = userAgent.IndexOf("Netscape/4")) > -1)
                                {
                                    res = getArray("NS", "NS4", "NS" + getVersionNumber(userAgent, pos + 9));
                                }
                                else
                                    res = getArray("NS", "NS?", "NS?" + getVersionNumber(userAgent, pos + 9));
                            }
                            else
                                if ((pos = userAgent.IndexOf("Chrome/")) > -1)
                                {
                                    res = getArray("KHTML", "KHTML(Chrome)", "KHTML(Chrome" + getVersionNumber(userAgent, pos + 6) + ")");
                                }
                                else
                                    if ((pos = userAgent.IndexOf("Safari/")) > -1)
                                    {
                                        res = getArray("KHTML", "KHTML(Safari)", "KHTML(Safari" + getVersionNumber(userAgent, pos + 6) + ")");
                                    }
                                    else
                                        if ((pos = userAgent.IndexOf("Konqueror/")) > -1)
                                        {
                                            res = getArray("KHTML", "KHTML(Konqueror)", "KHTML(Konqueror" + getVersionNumber(userAgent, pos + 9) + ")");
                                        }
                                        else
                                            if ((pos = userAgent.IndexOf("KHTML")) > -1)
                                            {
                                                res = getArray("KHTML", "KHTML?", "KHTML?(" + getVersionNumber(userAgent, pos + 5) + ")");
                                            }
                                            else
                                                if ((pos = userAgent.IndexOf("NetFront")) > -1)
                                                {
                                                    res = getArray("NetFront", "NetFront", "NetFront " + getVersionNumber(userAgent, pos + 8));
                                                }
                                                else
                                                    if ((pos = userAgent.IndexOf("BlackBerry")) > -1)
                                                    {
                                                        pos = userAgent.IndexOf("/", pos + 2);
                                                        res = getArray("BlackBerry", "BlackBerry", "BlackBerry" + getVersionNumber(userAgent, pos + 1));
                                                    }
                                                    else
                                                        //<SPAN class="codecomment"> We will interpret Mozilla/4.x as Netscape Communicator is and only if x</span>
                                                        //<SPAN class="codecomment"> is not 0 or 5</span>
                                                        if (userAgent.IndexOf("Mozilla/4.") == 0 &&
                                                            userAgent.IndexOf("Mozilla/4.0") < 0 &&
                                                            userAgent.IndexOf("Mozilla/4.5 ") < 0)
                                                        {
                                                            res = getArray("Communicator", "Communicator", "Communicator" + getVersionNumber(userAgent, pos + 8));
                                                        }
                                                        else
                                                            return getArray("<B>?</B>", "<B>?</B>", "<B>?</B>");
            return res;
        }
    }  
}
