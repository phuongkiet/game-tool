using System.Drawing;
using System.IO;
using System.Text.Json;

namespace WPFToolGame
{
    public class MapProfileManager
    {
        private const string CONFIG_FILE = "map_profiles.json";
        public List<MapProfile> Profiles { get; private set; } = new();

        public void Load()
        {
            if (!File.Exists(CONFIG_FILE))
            {
                CreateDefaultProfiles();
                Save();
                return;
            }
            string json = File.ReadAllText(CONFIG_FILE);
            Profiles = JsonSerializer.Deserialize<List<MapProfile>>(json) ?? new();
        }

        public void Save()
        {
            string json = JsonSerializer.Serialize(Profiles, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(CONFIG_FILE, json);
        }

        public MapProfile? Detect(Bitmap screen, VuaPhapThuatBot bot)
        {
            foreach (var profile in Profiles)
            {
                if (bot.FindTemplate(screen, profile.AnchorImage, 0.80).HasValue)
                    return profile;
            }
            return null;
        }

        // Cập nhật thời gian chạy thực tế (moving average)
        public void UpdateTravelTime(MapProfile profile, long actualMs)
        {
            // Smooth: 70% cũ + 30% mới → tránh 1 lần bất thường làm sai hết
            profile.EstimatedTravelMs = (int)(profile.EstimatedTravelMs * 0.7 + actualMs * 0.3);
            Save();
        }

        private void CreateDefaultProfiles()
        {
            Profiles = new List<MapProfile>
        {
            new MapProfile {
                MapName = "Lạp Tuyết Địa",
                AnchorImage = "map_title_lap_tuyet_dia.png",
                PointA = new Point(420, 460), // góc trái dưới minimap
                PointB = new Point(820, 160), // góc phải trên minimap
                EstimatedTravelMs = 10000
            },
            // Thêm 9 map còn lại tương tự...
        };
        }
    }
}
