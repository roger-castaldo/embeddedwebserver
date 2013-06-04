using System;
using System.Collections.Generic;
using System.Text;
using System.Configuration;
using Org.Reddragonit.EmbeddedWebServer.Diagnostics;
using System.IO;

namespace Org.Reddragonit.EmbeddedWebServer
{
    /*
     * This class is used to access the application configurations that get 
     * set for the library.  If there are none set, default values are returned.
     */
    internal class Settings
    {

        //This is the path for the Messages properties file when overriding messages
        private const string MESSAGES_FILE_PATH_SETTING_ID = "Org.Reddragonit.EmbeddedWebServer.Settings.MessagesFilePath";
        public static string MessagesFilePath
        {
            get
            {
                if (ConfigurationSettings.AppSettings[MESSAGES_FILE_PATH_SETTING_ID] != null)
                    return ConfigurationSettings.AppSettings[MESSAGES_FILE_PATH_SETTING_ID];
                return null;
            }
        }

        //This is the default diagnostics level to use that can be overwridden by a site specific value
        private const string DIAGNOSTICS_LEVEL_SETTING_ID = "Org.Reddragonit.EmbeddedWebServer.Settings.DiagnosticsLevel";
        public static DiagnosticsLevels DiagnosticsLevel
        {
            get
            {
                if (ConfigurationSettings.AppSettings[DIAGNOSTICS_LEVEL_SETTING_ID] != null)
                    return (DiagnosticsLevels)Enum.Parse(typeof(DiagnosticsLevels), ConfigurationSettings.AppSettings[DIAGNOSTICS_LEVEL_SETTING_ID]);
                return DiagnosticsLevels.NONE;
            }
        }

        //This is the default output to use when running diagnostics, can be overwritten by a site specific value
        private const string DIAGNOSTICS_OUTPUT_SETTING_ID = "Org.Reddragonit.EmbeddedWebServer.Settings.DiagnosticsOutput";
        public static DiagnosticsOutputs DiagnosticsOutput
        {
            get
            {
                if (ConfigurationSettings.AppSettings[DIAGNOSTICS_OUTPUT_SETTING_ID] != null)
                    return (DiagnosticsOutputs)Enum.Parse(typeof(DiagnosticsOutputs), ConfigurationSettings.AppSettings[DIAGNOSTICS_OUTPUT_SETTING_ID]);
                return DiagnosticsOutputs.DEBUG;
            }
        }

        //This is the number of days to stored file system log files before deleting them.
        private const string LOG_AGE_DAYS_SETTING_ID = "Org.Reddragonit.EmbeddedWebServer.Settings.LogAgeDays";
        public static int LogAgeDays
        {
            get
            {
                if (ConfigurationSettings.AppSettings[LOG_AGE_DAYS_SETTING_ID] != null)
                    return int.Parse(ConfigurationSettings.AppSettings[LOG_AGE_DAYS_SETTING_ID]);
                return 30;
            }
        }

        //The file system path to write the log files to.
        private const string LOG_PATH_SETTING_ID = "Org.Reddragonit.EmbeddedWebServer.Settings.LogPath";
        public static string LogPath
        {
            get
            {
                if (ConfigurationSettings.AppSettings[LOG_PATH_SETTING_ID] != null)
                    return ConfigurationSettings.AppSettings[LOG_PATH_SETTING_ID];
                return "."+Path.DirectorySeparatorChar+"logs"+Path.DirectorySeparatorChar;
            }
        }

        //Indicates if the server name should be included in logging
        private const string USE_SERVER_NAME_IN_LOGGING_SETTING_ID = "Org.Reddragonit.EmbeddedWebServer.Settings.UseServerNameInLogging";
        public static bool UseServerNameInLogging
        {
            get
            {
                if (ConfigurationSettings.AppSettings[USE_SERVER_NAME_IN_LOGGING_SETTING_ID] != null)
                    return bool.Parse(ConfigurationSettings.AppSettings[USE_SERVER_NAME_IN_LOGGING_SETTING_ID]);
                return true;
            }
        }

        //The default timeout for a request to be handled
        private const string REQUEST_TIMEOUT_SETTING_ID = "Org.Reddragonit.EmbeddedWebServer.Settings.RequestTimeout";
        public static int RequestTimeout
        {
            get
            {
                if (ConfigurationSettings.AppSettings[REQUEST_TIMEOUT_SETTING_ID] != null)
                    return int.Parse(ConfigurationSettings.AppSettings[REQUEST_TIMEOUT_SETTING_ID]);
                return int.MaxValue;
            }
        }

        //the flag to allow backend compression of all js files
        private const string COMPRESS_ALL_JS_SETTING_ID = "Org.Reddragonit.EmbeddedWebServer.Settings.CompressAllJS";
        public static bool CompressAllJS
        {
            get
            {
                if (ConfigurationSettings.AppSettings[COMPRESS_ALL_JS_SETTING_ID] != null)
                    return bool.Parse(ConfigurationSettings.AppSettings[COMPRESS_ALL_JS_SETTING_ID]);
                return false;

            }
        }

        //the comma seperated list of js files (absolute urls) to ignore for compress all flag
        private const string COMPRESS_ALL_JS_IGNORE_PATHS_SETTING_ID = "Org.Reddragonit.EmbeddedWebServer.Settings.CompressAllJSIgnorePaths";
        public static List<string> CompressAllJSIgnorePaths
        {
            get
            {
                if (ConfigurationSettings.AppSettings[COMPRESS_ALL_JS_IGNORE_PATHS_SETTING_ID] != null)
                    return new List<string>(ConfigurationSettings.AppSettings[COMPRESS_ALL_JS_IGNORE_PATHS_SETTING_ID].Split(','));
                return new List<string>();
            }
        }

        //the flag to allow backend compression of all css files
        private const string COMPRESS_ALL_CSS_SETTING_ID = "Org.Reddragonit.EmbeddedWebServer.Settings.CompressAllCSS";
        public static bool CompressAllCSS
        {
            get
            {
                if (ConfigurationSettings.AppSettings[COMPRESS_ALL_CSS_SETTING_ID] != null)
                    return bool.Parse(ConfigurationSettings.AppSettings[COMPRESS_ALL_CSS_SETTING_ID]);
                return false;

            }
        }

        //the flag to allow gzip stream compression to occure
        private const string ALLOW_DEFLATE_COMPRESSION_SETTING_ID = "Org.Reddragonit.EmbeddedWebServer.Settings.AllowDeflateCompression";
        public static bool AllowDeflateCompression
        {
            get
            {
                if (ConfigurationSettings.AppSettings[ALLOW_DEFLATE_COMPRESSION_SETTING_ID] != null)
                    return bool.Parse(ConfigurationSettings.AppSettings[ALLOW_DEFLATE_COMPRESSION_SETTING_ID]);
                return true;
            }
        }

        //the comma seperated list of css files (absolute urls) to ignore for compress all flag
        private const string COMPRESS_ALL_CSS_IGNORE_PATHS_SETTING_ID = "Org.Reddragonit.EmbeddedWebServer.Settings.CompressAllCSSIgnorePaths";
        public static List<string> CompressAllCSSIgnorePaths
        {
            get
            {
                if (ConfigurationSettings.AppSettings[COMPRESS_ALL_CSS_IGNORE_PATHS_SETTING_ID] != null)
                    return new List<string>(ConfigurationSettings.AppSettings[COMPRESS_ALL_CSS_IGNORE_PATHS_SETTING_ID].Split(','));
                return new List<string>();
            }
        }
    }
}
