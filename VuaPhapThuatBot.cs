using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using System;
using System.Diagnostics;
using System.Drawing;

public class VuaPhapThuatBot
{
    private IntPtr _gameHandle;

    public VuaPhapThuatBot(string windowTitleKeyword)
    {
        var process = Process.GetProcesses()
                         .FirstOrDefault(p => p.MainWindowTitle.Equals(windowTitleKeyword, StringComparison.OrdinalIgnoreCase));

        if (process != null && process.MainWindowHandle != IntPtr.Zero)
        {
            _gameHandle = process.MainWindowHandle;
        }
        else
        {
            throw new Exception($"Không tìm thấy game chứa từ khóa: '{windowTitleKeyword}'");
        }
    }

    // --- MẮT (VISION) ---
    public Bitmap CaptureGameScreen()
    {
        Win32Api.GetWindowRect(_gameHandle, out Win32Api.RECT rect);
        Bitmap bmp = new Bitmap(rect.Width, rect.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

        using (Graphics gfxBmp = Graphics.FromImage(bmp))
        {
            IntPtr hdcBitmap = gfxBmp.GetHdc();
            Win32Api.PrintWindow(_gameHandle, hdcBitmap, 2);
            gfxBmp.ReleaseHdc(hdcBitmap);
        }
        return bmp;
    }

    public Point? FindTemplate(Bitmap gameScreen, string templatePath, double threshold = 0.75)
    {
        try
        {
            // 1. Load ảnh màu
            using (Image<Bgr, byte> sourceColor = gameScreen.ToImage<Bgr, byte>())
            using (Image<Bgr, byte> templateColor = new Image<Bgr, byte>(templatePath))

            // 2. ÉP SANG ẢNH XÁM (GRAYSCALE) ĐỂ CHỐNG NHIỄU MÀU NỀN
            using (Image<Gray, byte> sourceGray = sourceColor.Convert<Gray, byte>())
            using (Image<Gray, byte> templateGray = templateColor.Convert<Gray, byte>())

            // 3. So sánh trên ảnh xám
            using (Image<Gray, float> resultImage = sourceGray.MatchTemplate(templateGray, TemplateMatchingType.CcoeffNormed))
            {
                resultImage.MinMax(out _, out double[] maxValues, out _, out Point[] maxLocations);

                // Bật dòng này lên để xem điểm số thực tế OpenCV chấm là bao nhiêu (xem ở Output của Visual Studio)
                System.Diagnostics.Debug.WriteLine($"[OpenCV] Quét {templatePath} - Điểm khớp: {maxValues[0]:0.00}");

                if (maxValues[0] >= threshold)
                {
                    return new Point(
                        maxLocations[0].X + (templateGray.Width / 2),
                        maxLocations[0].Y + (templateGray.Height / 2)
                    );
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LỖI FindTemplate] {templatePath}: {ex.Message}");
        }
        return null;
    }

    // --- TAY (ACTION) ---
    public void HardClickAt(int x, int y)
    {
        Win32Api.SetForegroundWindow(_gameHandle);
        Thread.Sleep(50);
        Win32Api.GetWindowRect(_gameHandle, out Win32Api.RECT rect);

        int screenX = rect.Left + x;
        int screenY = rect.Top + y;

        Win32Api.SetCursorPos(screenX, screenY);
        Thread.Sleep(100); // Hover chờ UI sáng lên

        Win32Api.mouse_event(Win32Api.MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0);
        Thread.Sleep(80); // Độ trễ giữ chuột
        Win32Api.mouse_event(Win32Api.MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);
    }

    public void HardPressKey(byte keyCode)
    {
        Win32Api.SetForegroundWindow(_gameHandle);
        Thread.Sleep(50);

        Win32Api.keybd_event(keyCode, 0, 0, UIntPtr.Zero);
        Thread.Sleep(60); // Độ trễ giữ phím
        Win32Api.keybd_event(keyCode, 0, Win32Api.KEYEVENTF_KEYUP, UIntPtr.Zero);
    }
}