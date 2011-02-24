using System;
using System.Collections.Generic;
using System.Text;
using System.Configuration;
using Org.Reddragonit.EmbeddedWebServer.Diagnostics;
using System.IO;

namespace Org.Reddragonit.EmbeddedWebServer
{
    internal class Settings
    {

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
    }
}
