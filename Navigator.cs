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
        private bool _headingToA = true;
        private MapProfile? _lastMap = null;

        public Navigator(VuaPhapThuatBot bot, MapProfileManager mapManager, Action<string> log)
        {
            _bot = bot;
            _mapManager = mapManager;
            _log = log;
        }

        public NavigateResult Execute(Bitmap screen, CancellationToken token)
        {
            // 1. ĐẢM BẢO MINIMAP ĐÃ MỞ TRƯỚC TIÊN (Thay đổi thứ tự)
            if (!EnsureMinimapOpen(screen))
                return NavigateResult.Waiting; // Chờ vòng lặp sau chụp ảnh mới có minimap

            // 2. NHẬN DIỆN MAP (Lúc này chắc chắn 100% minimap đã mở)
            var profile = _mapManager.Detect(screen, _bot);
            if (profile == null)
            {
                _log("[Nav] Không nhận ra map! (Hãy check lại ảnh Title map)");
                return NavigateResult.UnknownMap;
            }

            // 3. Nếu qua map mới -> Reset về điểm A
            if (_lastMap?.MapName != profile.MapName)
            {
                _log($"[Nav] Phát hiện map mới: {profile.MapName}. Reset về điểm A.");
                _headingToA = true;
                _lastMap = profile;
            }

            // 4. Xác định điểm đích
            Point target = _headingToA ? profile.PointA : profile.PointB;
            string targetName = _headingToA ? "A" : "B";
            _log($"[Nav] {profile.MapName} → Chạy đến điểm {targetName} ({target.X},{target.Y})");

            // 5. Click + chờ pathfinding + đo thời gian
            _bot.HardClickAt(target.X, target.Y);

            // [FIX 1 TỪ BÀI TRƯỚC]: Bắt buộc chờ game vẽ đường màu xanh ra
            Thread.Sleep(1500);

            var sw = Stopwatch.StartNew();
            var result = WaitForArrival(profile, token);
            sw.Stop();

            // 6. Xử lý kết quả di chuyển
            if (result == ArrivalResult.Arrived)
            {
                _log($"[Nav] Đến điểm {targetName} sau {sw.ElapsedMilliseconds}ms. Đổi chiều.");
                _mapManager.UpdateTravelTime(profile, sw.ElapsedMilliseconds);
                _headingToA = !_headingToA; // Đổi điểm cho lần chạy tiếp
                return NavigateResult.Arrived;
            }
            else if (result == ArrivalResult.CombatDetected)
            {
                _log("[Nav] Vào combat giữa đường. Đổi hướng cho lần sau.");
                // [FIX 2 TỪ BÀI TRƯỚC]: Đảo chiều luôn để đánh xong không bị chạy lùi lại chỗ cũ
                _headingToA = !_headingToA;
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
            // Tận dụng nút Thế giới (đặc trưng của minimap) thay vì dùng ảnh mới
            // NHỚ SỬA ĐƯỜNG DẪN Ở ĐÂY NẾU BẠN CHIA FOLDER RỒI (Ví dụ: @"Images\Buttons\nut_the_gioi.png")
            bool isOpen = _bot.FindTemplate(screen, @"Images\Buttons\nut_the_gioi_ban_do.png", 0.75).HasValue;

            if (!isOpen)
            {
                _log("[Nav] Mở minimap...");
                _bot.HardPressKey(Win32Api.VK_OEM_3); // Bấm phím ~

                // Cực kỳ quan trọng: Phải ngủ để chờ game mở xong cái UI Minimap
                Thread.Sleep(1500);
                return false;
            }

            return true;
        }

        private ArrivalResult WaitForArrival(MapProfile profile, CancellationToken token)
        {
            int timeout = (int)(profile.EstimatedTravelMs * 1.5);
            var sw = Stopwatch.StartNew();

            while (sw.ElapsedMilliseconds < timeout)
            {
                token.ThrowIfCancellationRequested();
                Thread.Sleep(400);

                using Bitmap currentScreen = _bot.CaptureGameScreen();

                // Check Combat
                // NHỚ SỬA ĐƯỜNG DẪN ẢNH Ở ĐÂY NẾU ĐÃ CHIA FOLDER
                if (_bot.FindTemplate(currentScreen, @"anchor_turn_user.png", 0.75).HasValue ||
                    _bot.FindTemplate(currentScreen, @"anchor_turn_pet.png", 0.75).HasValue)
                    return ArrivalResult.CombatDetected;

                // Check đường xanh
                bool stillMoving = HasGreenPath(currentScreen);
                if (!stillMoving)
                {
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
            const int x1 = 370, x2 = 880;
            const int y1 = 65, y2 = 510;
            int count = 0;

            for (int x = x1; x < x2; x += 15)
                for (int y = y1; y < y2; y += 15)
                {
                    var c = screen.GetPixel(x, y);
                    if (c.G > 160 && c.R < 100 && c.B < 100)
                        count++;
                }
            return count >= 3;
        }
    }

    public enum NavigateResult { Arrived, CombatDetected, Timeout, UnknownMap, Waiting }
    public enum ArrivalResult { Arrived, CombatDetected, Timeout }
}
