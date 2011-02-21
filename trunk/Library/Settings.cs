using System;
using System.Collections.Generic;
using System.Text;
using System.Configuration;

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

        private const string WRITE_GEN_PAGES_SETTING_ID = "Org.Reddragonit.EmbeddedWebServer.Settings.WriteGeneratedItemsToPhysicalDisk";
        public static bool WriteGeneratedItems
        {
            get
            {
                if (ConfigurationSettings.AppSettings[WRITE_GEN_PAGES_SETTING_ID] != null)
                    return bool.Parse(ConfigurationSettings.AppSettings[WRITE_GEN_PAGES_SETTING_ID]);
                return false;
            }
        }
    }
}
