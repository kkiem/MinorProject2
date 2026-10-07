#nullable disable   // turning off null warnings so the code stays simple
using System;
using System.IO;
using System.Text.Json;

namespace MinorProject2_GUI
{
    // holds the user's settings and saves them to a small file so they stick around
    public class AppSettings
    {
        public bool DarkMode { get; set; } = false;
        public bool AskBeforeDelete { get; set; } = true;

        // settings file lives in the user's AppData folder
        private static string FilePath
        {
            get
            {
                string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PremierLeagueManager");
                return Path.Combine(folder, "settings.json");
            }
        }

        // load settings from the file (or use the defaults if there isn't one yet)
        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    AppSettings loaded = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath));
                    if (loaded != null) return loaded;
                }
            }
            catch
            {
                // file is messed up? no biggie, just use the defaults
            }
            return new AppSettings();
        }

        // save settings to the file
        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                File.WriteAllText(FilePath, JsonSerializer.Serialize(this));
            }
            catch
            {
                // if saving fails the app still works, so don't bug the user about it
            }
        }
    }
}