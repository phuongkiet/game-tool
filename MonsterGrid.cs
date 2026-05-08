using System.Drawing;

namespace WPFToolGame
{
    /// <summary>
    /// Grid vị trí quái dựa trên cơ chế spawn của game.
    /// Đo từ screenshot thật (window 1456×816):
    ///   Slot 0 center = (500, 390) — Bạch Tuột Xanh reference
    ///   Mỗi slot cách nhau ~55px ngang, ~82px dọc (isometric offset)
    /// </summary>
    public static class MonsterGrid
    {
        // ── Tọa độ 10 slot, đo từ screenshot thật ──────────────────
        //
        //   HÀNG SAU (Row 2, y≈308):
        //   [9:390,308] [7:445,308] [5:500,308] [6:555,308] [8:610,308]
        //
        //   HÀNG TRƯỚC (Row 1, y≈390):
        //   [4:335,390] [2:390,390] [0:500,390] [1:555,390] [3:610,390]
        //
        //   Thứ tự spawn: 0 → 1(R) → 2(L) → 3(RR) → 4(LL) → 5(back)...

        public static readonly Point[] Slots = new[]
        {
            // --- Hàng trước (front row) ---
            new Point(500, 390),   // Slot 0: CENTER ← LUÔN CÓ QUÁI NẾU CÒN QUÁI
            new Point(555, 390),   // Slot 1: right
            new Point(445, 390),   // Slot 2: left
            new Point(610, 390),   // Slot 3: far-right
            new Point(390, 390),   // Slot 4: far-left

            // --- Hàng sau (back row, isometric = lên trên ~82px) ---
            new Point(500, 308),   // Slot 5: back-center (spawn 6th)
            new Point(555, 308),   // Slot 6: back-right
            new Point(445, 308),   // Slot 7: back-left
            new Point(610, 308),   // Slot 8: back-far-right
            new Point(390, 308),   // Slot 9: back-far-left
        };

        // Thứ tự click khi muốn đánh hết tất cả quái (slot 0 luôn là ưu tiên)
        public static readonly int[] AttackOrder = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };

        // Vùng quét HP bar quái (tránh vùng pet x>820 và char info y>450)
        // Dựa trên screenshot thật: game content x:192–1135, y:88–730
        public static readonly Rectangle HpScanZone =
            new Rectangle(x: 192, y: 240, width: 628, height: 190);
        //                          ↑ game content start   ↑ tránh pet ở x:820+
        //                                  ↑ HP bars bắt đầu từ y≈240
        //                                              ↑ HP bars kết thúc ở y≈430
    }
}