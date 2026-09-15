using System.IO;
using System.Text.Json;

namespace ImageEditor
{
    public class AppConfig
    {
        public bool IsSidebarCollapsed { get; set; } = false;
        public string PenColorHex { get; set; } = "#0078D4";
        public double PenThickness { get; set; } = 3.0;
        public string PenShape { get; set; } = "Freehand";
        public double LastCompressSliderValue { get; set; } = 100.0;

        public static string ConfigFilePath
        {
            get
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string folder = Path.Combine(appData, "ImageEditor");
                return Path.Combine(folder, "config.json");
            }
        }

        public static AppConfig Load()
        {
            try
            {
                string path = ConfigFilePath;
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
                    var cfg = JsonSerializer.Deserialize<AppConfig>(json);
                    if (cfg != null) return cfg;
                }
            }
            catch
            {
                // Fallback to defaults
            }
            return new AppConfig();
        }

        public void Save()
        {
            try
            {
                string path = ConfigFilePath;
                string dir = Path.GetDirectoryName(path)!;
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                string json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(path, json);
            }
            catch
            {
                // Silently ignore if cannot write
            }
        }
    }
}
