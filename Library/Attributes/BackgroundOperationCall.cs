using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Org.Reddragonit.EmbeddedWebServer.Attributes
{
    /*
     * Attribute used to tag Background Methods and indicate when to run them.
     * Only should be attached to public static void methods.
     * Use -1 for any interger value to indicate any minute
     */
    [AttributeUsage(AttributeTargets.Method,AllowMultiple=true)]
    public class BackgroundOperationCall : Attribute
    {
        private Regex _regMatch;

        public bool CanRunNow(DateTime date)
        {
            return _regMatch.IsMatch(date.ToString("mm HH dd MM ddd"));
        }

        public BackgroundOperationCall(int minute, int hour, int day, int month, BackgroundOperationDaysOfWeek weekDay)
        {
            string reg = (minute == -1 ? ".{1,2}" : minute.ToString("00")) + " " +
                (hour == -1 ? ".{1,2}" : hour.ToString("00")) + " " +
                (day == -1 ? ".{1,2}" : day.ToString("00")) + " " +
                (month == -1 ? ".{1,2}" : month.ToString("00")) + " ";
            if (weekDay == BackgroundOperationDaysOfWeek.All)
                reg += ".{3}";
            else
                reg += weekDay.ToString().Substring(0, 3);
            _regMatch = new Regex(reg,RegexOptions.Compiled | RegexOptions.ECMAScript);
        }
    }
}
