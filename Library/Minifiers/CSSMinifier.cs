/*
 * Created by SharpDevelop.
 * User: Roger
 * Date: 23/04/2009
 * Time: 10:26 PM
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Org.Reddragonit.EmbeddedWebServer.Minifiers
{
	public static class CSSMinifier
	{
		private const string UnitRegex="(px|em|%|in|cm|mm|pc|pt|ex)";

        private static readonly Regex regBasicComment = new Regex("//.+\n", RegexOptions.Compiled | RegexOptions.ECMAScript);
        private static readonly Regex regComplexComment = new Regex("/\\*.+\\*/", RegexOptions.Compiled | RegexOptions.Multiline);
        private static readonly Regex regOpeningBracket = new Regex("\\s*\\{\\s+",RegexOptions.Compiled | RegexOptions.ECMAScript);
        private static readonly Regex regClosingBracket = new Regex("\\s*\\}\\s+",RegexOptions.Compiled | RegexOptions.ECMAScript);
        private static readonly Regex regJoining = new Regex("\\s*:\\s*",RegexOptions.Compiled | RegexOptions.ECMAScript);
        private static readonly Regex regEndSetting = new Regex("\\s*;\\s*",RegexOptions.Compiled | RegexOptions.ECMAScript);
        private static readonly Regex regClosingBracketSpacer = new Regex("}\\s{0}",RegexOptions.Compiled | RegexOptions.ECMAScript);
        //private static readonly Regex regUnit = new Regex("(px|em|%|in|cm|mm|pc|pt|ex)",RegexOptions.Compiled | RegexOptions.ECMAScript);
        private static readonly Regex regMarginPad = new Regex(":0"+UnitRegex+"?\\s+0"+UnitRegex+"?(\\s+0"+UnitRegex+"?(\\s+0"+UnitRegex+"?)?)?\\s*;",RegexOptions.Compiled | RegexOptions.ECMAScript);
        private static readonly Regex regImports = new Regex("@import\\s+(url\\()?(\"|\').+\\.css(\"|\')\\)?;",RegexOptions.Compiled | RegexOptions.ECMAScript);
        private static readonly Regex regDoubleSpace = new Regex("\\s{2}", RegexOptions.Compiled | RegexOptions.Multiline);

		
		public static string Minify(string css,out List<string> importUrls)
		{
			importUrls = new List<string>();
            Match m = regImports.Match(css);
			while (m.Success)
			{
				importUrls.Add(m.Value.Replace("@import","").Replace("url(","").Replace(");","").Replace("\"","").Replace("\'","").Replace(" ",""));
				css = css.Replace(m.Value,"");
				m=m.NextMatch();
			}
			css = StripComments(css);
            css = regBasicComment.Replace(css, string.Empty);
            css = regComplexComment.Replace(css, string.Empty);
            css = regOpeningBracket.Replace(css, "{");
            css = regClosingBracket.Replace(css, "}");
            css = regJoining.Replace(css, ":");
            css = regEndSetting.Replace(css, ";");
            css = regClosingBracketSpacer.Replace(css, "}\n");
            css = regMarginPad.Replace(css, ":0;");
			css = css.Replace("\n\n","\n");
            css = regDoubleSpace.Replace(css, " ");
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
