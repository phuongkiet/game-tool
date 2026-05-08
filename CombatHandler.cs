using System.Drawing;
using System.Diagnostics;

namespace WPFToolGame
{
    public class CombatHandler
    {
        private readonly VuaPhapThuatBot _bot;
        private readonly Action<string> _log;

        public bool IsCatchingMode { get; set; } = false;
        public int BattleCount { get; private set; } = 0;

        public CombatHandler(VuaPhapThuatBot bot, Action<string> log)
        {
            _bot = bot;
            _log = log;
        }

        // ══════════════════════════════════════════
        // LƯỢT NHÂN VẬT
        // ══════════════════════════════════════════
        public void HandleUserTurn(Bitmap screen)
        {
            // Ưu tiên: Bắt pet nếu có BẢO BẢO
            var baoBao = _bot.FindTemplate(screen, @"Images\Anchors\baobao_icon.png", 0.85);
            if (baoBao.HasValue)
            {
                TryCatchPet(screen, baoBao.Value);
                return;
            }

            ClickAttackButton(screen, isUserTurn: true);
        }

        // ══════════════════════════════════════════
        // LƯỢT PET
        // ══════════════════════════════════════════
        public void HandlePetTurn(Bitmap screen)
        {
            if (IsCatchingMode)
            {
                var phongNgu = _bot.FindTemplate(screen, @"Images\Buttons\nut_phong_ngu.png", 0.8);
                if (phongNgu.HasValue)
                {
                    _bot.HardClickAt(phongNgu.Value.X, phongNgu.Value.Y);
                    _log("[Pet] Phòng ngự chờ bắt.");
                }
            }
            else
            {
                ClickAttackButton(screen, isUserTurn: false);
            }

            BattleCount++;
            _log($"[Combat] Turn #{BattleCount} — chờ animation...");
            Thread.Sleep(7000);
        }

        // ══════════════════════════════════════════
        // CORE: Click Tấn Công → chọn target
        // ══════════════════════════════════════════
        private void ClickAttackButton(Bitmap screen, bool isUserTurn)
        {
            string who = isUserTurn ? "User" : "Pet";

            // Step 1: Tìm và click nút Tấn Công
            var tanCong = _bot.FindTemplate(screen, @"Images\Buttons\nut_tan_cong.png", 0.80);
            if (!tanCong.HasValue)
            {
                _log($"[{who}] Không thấy nút Tấn Công!");
                return;
            }
            _bot.HardClickAt(tanCong.Value.X, tanCong.Value.Y);

            // Step 2: Chờ game vào mode "chọn target"
            Thread.Sleep(650);

            // Step 3: Chọn target
            using Bitmap fresh = _bot.CaptureGameScreen();
            SelectTarget(fresh, who);

            IsCatchingMode = false;
        }

        // ══════════════════════════════════════════
        // CHỌN TARGET: Scan HP bar xác định slot nào
        // có quái, rồi click slot đó
        // ══════════════════════════════════════════
        private void SelectTarget(Bitmap screen, string who)
        {
            // Quét HP bar để biết slot nào đang có quái
            var occupiedSlots = FindOccupiedSlots(screen);

            if (occupiedSlots.Count > 0)
            {
                // Click slot đầu tiên có quái (theo AttackOrder → slot 0 luôn ưu tiên)
                var target = MonsterGrid.Slots[occupiedSlots[0]];
                _bot.HardClickAt(target.X, target.Y);
                _log($"[{who}] Click Slot {occupiedSlots[0]} ({target.X},{target.Y}) — {occupiedSlots.Count} quái còn lại");
            }
            else
            {
                // Fallback: Slot 0 luôn là vị trí đầu tiên có quái khi combat bắt đầu
                var fallback = MonsterGrid.Slots[0];
                _bot.HardClickAt(fallback.X, fallback.Y);
                _log($"[{who}] Scan miss — Fallback Slot 0 ({fallback.X},{fallback.Y})");
            }
        }

        // ══════════════════════════════════════════
        // SCAN HP BAR → trả về list slot index có quái
        // ══════════════════════════════════════════
        public List<int> FindOccupiedSlots(Bitmap screen)
        {
            var zone = MonsterGrid.HpScanZone;

            // Bước 1: Thu thập tất cả pixel HP bar đỏ
            var redPixels = new List<(int x, int y)>();
            for (int x = zone.Left; x < zone.Right; x += 5)
                for (int y = zone.Top; y < zone.Bottom; y += 5)
                {
                    if (x >= screen.Width || y >= screen.Height) continue;
                    Color c = screen.GetPixel(x, y);
                    // Màu HP bar đỏ đo từ screenshot: R≈218, G≈42, B≈42
                    if (c.R > 160 && c.G < 80 && c.B < 80)
                        redPixels.Add((x, y));
                }

            if (redPixels.Count < 5) return new List<int>(); // Quá ít pixel → không có quái

            // Bước 2: Cluster pixels thành từng HP bar riêng biệt
            var barCenters = ClusterToBarCenters(redPixels, clusterRadius: 60);
            Debug.WriteLine($"[Scan] {redPixels.Count} px đỏ → {barCenters.Count} HP bar");

            // Bước 3: Lọc bar của PET (có thanh thứ 2 bên dưới)
            var monsterBars = barCenters.Where(b => !HasSecondBarBelow(screen, b.x, b.y)).ToList();
            Debug.WriteLine($"[Scan] Sau lọc pet: {monsterBars.Count} HP bar quái");

            // Bước 4: Map mỗi HP bar → slot gần nhất
            var result = new List<int>();
            foreach (var bar in monsterBars)
            {
                // HP bar nằm ~50-80px TRÊN thân quái
                // → thân quái ≈ (bar.x, bar.y + 60)
                int bodyX = bar.x;
                int bodyY = bar.y + 60;

                // Tìm slot gần nhất với body position
                int closestSlot = FindNearestSlot(bodyX, bodyY, maxDistance: 100);
                if (closestSlot >= 0 && !result.Contains(closestSlot))
                    result.Add(closestSlot);
            }

            // Sắp xếp theo AttackOrder (slot 0 lên đầu)
            result.Sort((a, b) =>
                Array.IndexOf(MonsterGrid.AttackOrder, a)
                    .CompareTo(Array.IndexOf(MonsterGrid.AttackOrder, b)));

            return result;
        }

        // ── Tìm slot gần nhất với tọa độ body ──────────────────────────
        private static int FindNearestSlot(int x, int y, int maxDistance)
        {
            int best = -1;
            double bestDist = maxDistance;

            for (int i = 0; i < MonsterGrid.Slots.Length; i++)
            {
                double d = Math.Sqrt(
                    Math.Pow(MonsterGrid.Slots[i].X - x, 2) +
                    Math.Pow(MonsterGrid.Slots[i].Y - y, 2));
                if (d < bestDist) { bestDist = d; best = i; }
            }
            return best;
        }

        // ── Kiểm tra có thanh HP/MP thứ 2 bên dưới không (dấu hiệu Pet/Char) ──
        private static bool HasSecondBarBelow(Bitmap screen, int bx, int by)
        {
            for (int dy = 8; dy <= 22; dy++)
            {
                int scanY = by + dy;
                if (scanY >= screen.Height) break;

                int hits = 0;
                for (int dx = -20; dx <= 20; dx += 3)
                {
                    int px = bx + dx;
                    if (px < 0 || px >= screen.Width) continue;
                    Color c = screen.GetPixel(px, scanY);
                    // Thanh 2 có thể là đỏ (HP), vàng (MP pet), hoặc xanh (MP char)
                    bool isBar = (c.R > 160 && c.G < 80 && c.B < 80)   // đỏ
                              || (c.R > 180 && c.G > 120 && c.B < 80)  // vàng
                              || (c.B > 150 && c.R < 100);              // xanh
                    if (isBar) hits++;
                }
                if (hits >= 3) return true;
            }
            return false;
        }

        // ── Gom pixels gần nhau thành cluster ──────────────────────────
        private static List<(int x, int y)> ClusterToBarCenters(
            List<(int x, int y)> pts, int clusterRadius)
        {
            var clusters = new List<(int x, int y)>();
            var used = new bool[pts.Count];

            for (int i = 0; i < pts.Count; i++)
            {
                if (used[i]) continue;
                var grp = new List<(int x, int y)> { pts[i] };
                used[i] = true;

                for (int j = i + 1; j < pts.Count; j++)
                {
                    if (used[j]) continue;
                    int dx = pts[i].x - pts[j].x, dy = pts[i].y - pts[j].y;
                    if (dx * dx + dy * dy < clusterRadius * clusterRadius)
                    {
                        grp.Add(pts[j]); used[j] = true;
                    }
                }
                clusters.Add(((int)grp.Average(p => p.x), (int)grp.Average(p => p.y)));
            }
            return clusters;
        }

        // ══════════════════════════════════════════
        // BẮT PET
        // ══════════════════════════════════════════
        private void TryCatchPet(Bitmap screen, Point baoBaoPos)
        {
            var nutBat = _bot.FindTemplate(screen, @"Images\Buttons\nut_bat.png", 0.8);
            if (!nutBat.HasValue) return;

            _bot.HardClickAt(nutBat.Value.X, nutBat.Value.Y);
            Thread.Sleep(800);
            _bot.HardClickAt(baoBaoPos.X - 70, baoBaoPos.Y - 75);
            IsCatchingMode = true;
            _log("[Combat] Ném lưới bắt pet.");
        }

        // ══════════════════════════════════════════
        // DEBUG HELPER — Chạy 1 lần để xác nhận màu HP bar
        // ══════════════════════════════════════════
        public void DumpHpBarColors(Bitmap screen)
        {
            var zone = MonsterGrid.HpScanZone;
            Debug.WriteLine($"=== HP BAR COLOR DUMP (zone {zone}) ===");
            // Quét dọc theo từng cột trong vùng scan, in ra pixel nào có R>150
            for (int x = zone.Left; x < zone.Right; x += 20)
                for (int y = zone.Top; y < zone.Bottom; y += 5)
                {
                    Color c = screen.GetPixel(x, y);
                    if (c.R > 150 && c.G < 100 && c.B < 100)
                        Debug.WriteLine($"  RedPixel [{x},{y}]: R={c.R} G={c.G} B={c.B}");
                }
        }
    }
}