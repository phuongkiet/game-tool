using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Text;

namespace WPFToolGame
{
    public class Navigator
    {
        private readonly VuaPhapThuatBot _bot;
        private readonly MapProfileManager _mapManager;
        private readonly Action<string> _log;

        // State di chuyển
        private bool _headingToA = true; // true=đang đi về A, false=đang đi về B
        private MapProfile? _lastMap = null;

        public Navigator(VuaPhapThuatBot bot, MapProfileManager mapManager, Action<string> log)
        {
            _bot = bot;
            _mapManager = mapManager;
            _log = log;
        }

        /// <summary>
        /// Gọi khi State = InMap. Return true nếu đã di chuyển xong 1 leg.
        /// </summary>
        public NavigateResult Execute(Bitmap screen, CancellationToken token)
        {
            // 1. Nhận diện map
            var profile = _mapManager.Detect(screen, _bot);
            if (profile == null)
            {
                _log("[Nav] Không nhận ra map!");
                return NavigateResult.UnknownMap;
            }

            // Nếu vừa chuyển map → reset về điểm A
            if (_lastMap?.MapName != profile.MapName)
            {
                _log($"[Nav] Phát hiện map mới: {profile.MapName}. Reset về điểm A.");
                _headingToA = true;
                _lastMap = profile;
            }

            // 2. Mở minimap nếu chưa mở
            if (!EnsureMinimapOpen(screen)) return NavigateResult.Waiting;

            // 3. Xác định điểm đích
            Point target = _headingToA ? profile.PointA : profile.PointB;
            string targetName = _headingToA ? "A" : "B";
            _log($"[Nav] {profile.MapName} → Chạy đến điểm {targetName} ({target.X},{target.Y})");

            // 4. Click + đo thời gian thực tế
            _bot.HardClickAt(target.X, target.Y);
            var sw = Stopwatch.StartNew();

            // 5. Chờ đến nơi
            var result = WaitForArrival(profile, token);
            sw.Stop();

            if (result == ArrivalResult.Arrived)
            {
                _log($"[Nav] Đến điểm {targetName} sau {sw.ElapsedMilliseconds}ms. Đổi chiều.");
                _mapManager.UpdateTravelTime(profile, sw.ElapsedMilliseconds);
                _headingToA = !_headingToA; // Đổi điểm
                return NavigateResult.Arrived;
            }
            else if (result == ArrivalResult.CombatDetected)
            {
                _log("[Nav] Vào combat giữa đường.");
                return NavigateResult.CombatDetected;
            }
            else // Timeout
            {
                _log($"[Nav] Timeout sau {profile.EstimatedTravelMs}ms. Thử lại.");
                return NavigateResult.Timeout;
            }
        }

        private bool EnsureMinimapOpen(Bitmap screen)
        {
            bool isOpen = _bot.FindTemplate(screen, "anchor_minimap_open.png", 0.80).HasValue;
            if (!isOpen)
            {
                _log("[Nav] Mở minimap...");
                _bot.HardPressKey(Win32Api.VK_OEM_3);
                Thread.Sleep(1000);
                return false; // Để vòng lặp capture screen mới
            }
            return true;
        }

        private ArrivalResult WaitForArrival(MapProfile profile, CancellationToken token)
        {
            // Timeout = thời gian ước tính * 1.5 (buffer an toàn)
            int timeout = (int)(profile.EstimatedTravelMs * 1.5);
            var sw = Stopwatch.StartNew();

            while (sw.ElapsedMilliseconds < timeout)
            {
                token.ThrowIfCancellationRequested();
                Thread.Sleep(400);

                using Bitmap screen = _bot.CaptureGameScreen();

                // Ưu tiên check combat trước
                if (_bot.FindTemplate(screen, "anchor_turn_user.png", 0.75).HasValue ||
                    _bot.FindTemplate(screen, "anchor_turn_pet.png", 0.75).HasValue)
                    return ArrivalResult.CombatDetected;

                // Check đường xanh đã biến mất chưa
                bool stillMoving = HasGreenPath(screen);
                if (!stillMoving)
                {
                    // Chờ thêm 300ms confirm (tránh false positive khi mới bắt đầu chạy)
                    Thread.Sleep(300);
                    using Bitmap confirm = _bot.CaptureGameScreen();
                    if (!HasGreenPath(confirm))
                        return ArrivalResult.Arrived;
                }
            }
            return ArrivalResult.Timeout;
        }

        private bool HasGreenPath(Bitmap screen)
        {
            // Vùng minimap theo ảnh của bạn (điều chỉnh theo resolution thực)
            const int x1 = 370, x2 = 880;
            const int y1 = 65, y2 = 510;
            int count = 0;

            for (int x = x1; x < x2; x += 15)
                for (int y = y1; y < y2; y += 15)
                {
                    var c = screen.GetPixel(x, y);
                    // Xanh lá thuần (đường pathfinding)
                    if (c.G > 160 && c.R < 100 && c.B < 100)
                        count++;
                }
            return count >= 3;
        }
    }

    // Enums kết quả
    public enum NavigateResult { Arrived, CombatDetected, Timeout, UnknownMap, Waiting }
    public enum ArrivalResult { Arrived, CombatDetected, Timeout }
}
