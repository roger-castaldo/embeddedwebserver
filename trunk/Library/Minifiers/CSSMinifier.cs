/*
 * Created by SharpDevelop.
 * User: Roger
 * Date: 23/04/2009
 * Time: 10:26 PM
 * 
 * This class is designed to minify CSS code.  It does this by first
 * stripping out comments, then using a complexe set of regular rexpressions it 
 * replaces unnecessary portions of a css file to provide a faster transfer 
 * by transfer a smaller amount of data.
 */
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Org.Reddragonit.EmbeddedWebServer.Minifiers
{
	public static class CSSMinifier
	{
		private const string UnitRegex="(px|em|%|in|cm|mm|pc|pt|ex)";
        private static readonly Dictionary<string, string> ReplaceColors = new Dictionary<string, string>()
        {
            {"aliceblue","F0F8FF"},
	        {"antiquewhite","FAEBD7"},
	        {"aquamarine","7FFFD4"},
	        {"azure","F0FFFF"},
	        {"beige","F5F5DC"},
	        {"bisque","FFE4C4"},
	        {"blanchedalmond","FFEBCD"},
	        {"blueviolet","8A2BE2"},
	        {"brown","A52A2A"},
	        {"burlywood","DEB887"},
	        {"cadetblue","5F9EA0"},
	        {"chartreuse","7FFF00"},
	        {"chocolate","D2691E"},
	        {"coral","FF7F50"},
	        {"cornflowerblue","6495ED"},
	        {"cornsilk","FFF8DC"},
	        {"crimson","DC143C"},
	        {"cyan","00FFFF"},
	        {"darkblue","00008B"},
	        {"darkcyan","008B8B"},
	        {"darkgoldenrod","B8860B"},
	        {"darkgray","A9A9A9"},
	        {"darkgreen","006400"},
	        {"darkkhaki","BDB76B"},
	        {"darkmagenta","8B008B"},
	        {"darkolivegreen","556B2F"},
	        {"darkorange","FF8C00"},
	        {"darkorchid","9932CC"},
	        {"darkred","8B0000"},
	        {"darksalmon","E9967A"},
	        {"darkseagreen","8FBC8F"},
	        {"darkslateblue","483D8B"},
	        {"darkslategray","2F4F4F"},
	        {"darkturquoise","00CED1"},
	        {"darkviolet","9400D3"},
	        {"deeppink","FF1493"},
	        {"deepskyblue","00BFFF"},
	        {"dimgray","696969"},
	        {"dodgerblue","1E90FF"},
	        {"feldspar","D19275"},
	        {"firebrick","B22222"},
	        {"floralwhite","FFFAF0"},
	        {"forestgreen","228B22"},
	        {"gainsboro","DCDCDC"},
	        {"ghostwhite","F8F8FF"},
	        {"gold","FFD700"},
	        {"goldenrod","DAA520"},
	        {"greenyellow","ADFF2F"},
	        {"honeydew","F0FFF0"},
	        {"hotpink","FF69B4"},
	        {"indianred","CD5C5C"},
	        {"indigo","4B0082"},
	        {"ivory","FFFFF0"},
	        {"khaki","F0E68C"},
	        {"lavender","E6E6FA"},
	        {"lavenderblush","FFF0F5"},
	        {"lawngreen","7CFC00"},
	        {"lemonchiffon","FFFACD"},
	        {"lightblue","ADD8E6"},
	        {"lightcoral","F08080"},
	        {"lightcyan","E0FFFF"},
	        {"lightgoldenrodyellow","FAFAD2"},
	        {"lightgrey","D3D3D3"},
	        {"lightgreen","90EE90"},
	        {"lightpink","FFB6C1"},
	        {"lightsalmon","FFA07A"},
	        {"lightseagreen","20B2AA"},
	        {"lightskyblue","87CEFA"},
	        {"lightslateblue","8470FF"},
	        {"lightslategray","778899"},
	        {"lightsteelblue","B0C4DE"},
	        {"lightyellow","FFFFE0"},
	        {"limegreen","32CD32"},
	        {"linen","FAF0E6"},
	        {"magenta","FF00FF"},
	        {"mediumaquamarine","66CDAA"},
	        {"mediumblue","0000CD"},
	        {"mediumorchid","BA55D3"},
	        {"mediumpurple","9370D8"},
	        {"mediumseagreen","3CB371"},
	        {"mediumslateblue","7B68EE"},
	        {"mediumspringgreen","00FA9A"},
	        {"mediumturquoise","48D1CC"},
	        {"mediumvioletred","C71585"},
	        {"midnightblue","191970"},
	        {"mintcream","F5FFFA"},
	        {"mistyrose","FFE4E1"},
	        {"moccasin","FFE4B5"},
	        {"navajowhite","FFDEAD"},
	        {"oldlace","FDF5E6"},
	        {"olivedrab","6B8E23"},
	        {"orangered","FF4500"},
	        {"orchid","DA70D6"},
	        {"palegoldenrod","EEE8AA"},
	        {"palegreen","98FB98"},
	        {"paleturquoise","AFEEEE"},
	        {"palevioletred","D87093"},
	        {"papayawhip","FFEFD5"},
	        {"peachpuff","FFDAB9"},
	        {"peru","CD853F"},
	        {"pink","FFC0CB"},
	        {"plum","DDA0DD"},
	        {"powderblue","B0E0E6"},
	        {"rosybrown","BC8F8F"},
	        {"royalblue","4169E1"},
	        {"saddlebrown","8B4513"},
	        {"salmon","FA8072"},
	        {"sandybrown","F4A460"},
	        {"seagreen","2E8B57"},
	        {"seashell","FFF5EE"},
	        {"sienna","A0522D"},
	        {"skyblue","87CEEB"},
	        {"slateblue","6A5ACD"},
	        {"slategray","708090"},
	        {"snow","FFFAFA"},
	        {"springgreen","00FF7F"},
	        {"steelblue","4682B4"},
	        {"tan","D2B48C"},
	        {"thistle","D8BFD8"},
	        {"tomato","FF6347"},
	        {"turquoise","40E0D0"},
	        {"violet","EE82EE"},
	        {"violetred","D02090"},
	        {"wheat","F5DEB3"},
	        {"whitesmoke","F5F5F5"},
	        {"yellowgreen","9ACD32"},
            {"white","FFFFFF"},
            {"black","000000"}
        };
        private static readonly string[] ColorValues = new string[]{
            "color",
	        "background-color",
	        "border-color",
	        "border-top-color",
	        "border-right-color",
	        "border-bottom-color",
	        "border-left-color",
	        "border-color",
	        "color",
	        "outline-color"
        };

        private static readonly Regex regBasicComment = new Regex("//.+\n", RegexOptions.Compiled | RegexOptions.ECMAScript);
        private static readonly Regex regComplexComment = new Regex("/\\*.+\\*/", RegexOptions.Compiled | RegexOptions.Multiline);
        private static readonly Regex regOpeningBracket = new Regex("\\s*\\{\\s+",RegexOptions.Compiled | RegexOptions.ECMAScript);
        private static readonly Regex regClosingBracket = new Regex("\\s*\\}\\s+",RegexOptions.Compiled | RegexOptions.ECMAScript);
        private static readonly Regex regJoining = new Regex("\\s*:\\s*",RegexOptions.Compiled | RegexOptions.ECMAScript);
        private static readonly Regex regEndSetting = new Regex("\\s*;\\s*",RegexOptions.Compiled | RegexOptions.ECMAScript);
        private static readonly Regex regClosingBracketSpacer = new Regex("}\\s{0}",RegexOptions.Compiled | RegexOptions.ECMAScript);
        private static readonly Regex regMarginPad = new Regex(":0"+UnitRegex+"?\\s+0"+UnitRegex+"?(\\s+0"+UnitRegex+"?(\\s+0"+UnitRegex+"?)?)?\\s*;",RegexOptions.Compiled | RegexOptions.ECMAScript);
        private static readonly Regex regDoubleSpace = new Regex("(\\s+|\t+)", RegexOptions.Compiled | RegexOptions.Multiline);
        private static readonly Regex regLines = new Regex("[\r\n]", RegexOptions.Compiled | RegexOptions.Multiline);

        private static Regex regColors;

        static CSSMinifier()
        {
            string reg = "(";
            foreach (string str in ColorValues)
                reg += str + "|";
            reg = reg.Substring(0, reg.Length - 1) + "):(";
            foreach (string str in ReplaceColors.Keys)
                reg += str + "|";
            regColors = new Regex(reg.Substring(0, reg.Length - 1) + ")", RegexOptions.Compiled | RegexOptions.ECMAScript);
        }


		
		public static string Minify(string css)
		{
			css = StripComments(css);
            css = regBasicComment.Replace(css, string.Empty);
            css = regComplexComment.Replace(css, string.Empty);
            css = regOpeningBracket.Replace(css, "{");
            css = regClosingBracket.Replace(css, "}");
            css = regJoining.Replace(css, ":");
            css = regEndSetting.Replace(css, ";");
            css = regClosingBracketSpacer.Replace(css, "}\n");
            css = regMarginPad.Replace(css, ":0;");
            css = regLines.Replace(css, "");
            css = regDoubleSpace.Replace(css, " ");
            int lastIndex=0;
            while (regColors.Matches(css.ToLower(),lastIndex).Count > 0)
            {
                Match m = regColors.Matches(css.ToLower(),lastIndex)[0];
                string start = css.Substring(0, m.Index);
                string end = css.Substring(m.Index + m.Length);
                string code = string.Format("{0}:#{1}",
                    m.Groups[1].Value,
                    ReplaceColors[m.Groups[2].Value]);
                css = start + code + end;
                lastIndex = m.Index + code.Length;
            }
			return css;
		}
		
		internal static string StripComments(string originalString)
		{
			string ret = "";
            for (int x = 0; x < originalString.Length; x++)
            {
                if ((originalString[x] == '/') && ret.EndsWith("/"))
                {
                    ret = ret.Substring(0, ret.Length - 1);
                    while (x < originalString.Length)
                    {
                        if (originalString[x] == '\n')
                            break;
                        x++;
                    }
                }
                else if ((originalString[x] == '*') && ret.EndsWith("/"))
                {
                    ret = ret.Substring(0, ret.Length - 1);
                    x++;
                    if (x < originalString.Length)
                    {
                        string tmp = originalString[x].ToString();
                        while (x < originalString.Length)
                        {
                            if (originalString[x] == '/' && tmp.EndsWith("*"))
                            {
                                x++;
                                break;
                            }
                            tmp += originalString[x];
                            x++;
                        }
                    }
                }
                else if (originalString[x] == '\"')
                {
                    ret += originalString[x];
                    x++;
                    while (x < originalString.Length)
                    {
                        if ((originalString[x] == '\"') && !ret.EndsWith("\\"))
                            break;
                        ret += originalString[x];
                        x++;
                    }
                }
                else if (originalString[x] == '\'')
                {
                    ret += originalString[x];
                    x++;
                    while (x < originalString.Length)
                    {
                        if ((originalString[x] == '\'') && !ret.EndsWith("\\"))
                            break;
                        ret += originalString[x];
                        x++;
                    }
                }
                if (x<originalString.Length)
                    ret += originalString[x];
            }
			return ret;
		}
	}
}
