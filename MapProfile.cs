using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;

namespace WPFToolGame
{
    public class MapProfile
    {
        public string MapName { get; set; }
        public string AnchorImage { get; set; }

        // 2 điểm ping-pong trên minimap
        public Point PointA { get; set; }
        public Point PointB { get; set; }

        // Bot tự học thời gian chạy thực tế (ms)
        // Ban đầu set 8000, sau mỗi lần chạy sẽ tự cập nhật
        public int EstimatedTravelMs { get; set; } = 8000;
    }
}
