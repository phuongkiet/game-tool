using System.Drawing;
using System.Windows;

namespace WPFToolGame
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public enum BotState { Unknown, InMap, Combat_UserTurn, Combat_PetTurn }

    public partial class MainWindow : Window
    {
        private VuaPhapThuatBot _bot;
        private CancellationTokenSource _cancelToken;
        private Navigator _navigator;
        private MapProfileManager _mapManager;

        // Thống kê & Biến toàn cục
        private int _petCaught = 0;
        private int _battleCount = 0;
        private bool _isCatchingMode = false; // Bật lên khi User ném bóng, để Pet biết đường phòng ngự

        // LỘ TRÌNH ĐIỂM CÀY QUÁI TRÊN BẢN ĐỒ LỚN (Thay bằng tọa độ thật của bạn)
        private System.Drawing.Point[] _routePoints = new System.Drawing.Point[]
        {
            new System.Drawing.Point(500, 350),
            new System.Drawing.Point(550, 400),
            new System.Drawing.Point(450, 400)
        };
        private int _currentRouteIndex = 0;

        public MainWindow()
        {
            InitializeComponent();
            Win32Api.SetProcessDPIAware();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                _bot = new VuaPhapThuatBot("Vua Pháp Thuật Game");
                _mapManager = new MapProfileManager();
                _mapManager.Load();
                _navigator = new Navigator(_bot, _mapManager, LogToUI);
                LogToUI("Đã gắn vào cửa sổ game thành công!");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                btnStart.IsEnabled = false;
            }
        }

        // --- HÀM UI HELPERS ---
        private void LogToUI(string message)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                lbLogs.Items.Insert(0, $"[{DateTime.Now:HH:mm:ss}] {message}");
            });
        }

        private void UpdateStatusToUI(string statusMsg, int? petCount = null, int? battleCount = null)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                txtStatus.Text = statusMsg;
                if (petCount.HasValue) txtPetCount.Text = petCount.Value.ToString();
                if (battleCount.HasValue) txtBattleCount.Text = battleCount.Value.ToString();
            });
        }

        // --- BUTTON EVENTS ---
        private async void btnStart_Click(object sender, RoutedEventArgs e)
        {
            btnStart.IsEnabled = false; btnStop.IsEnabled = true;
            _cancelToken = new CancellationTokenSource();
            LogToUI("Bot bắt đầu chạy...");

            try
            {
                await Task.Run(() => BotMainLoop(_cancelToken.Token));
            }
            catch (OperationCanceledException) { LogToUI("Đã dừng Bot an toàn."); }
            catch (Exception ex) { LogToUI($"Lỗi Bot: {ex.Message}"); }
            finally
            {
                btnStart.IsEnabled = true; btnStop.IsEnabled = false;
                UpdateStatusToUI("Đang nghỉ ngơi");
            }
        }

        private void btnStop_Click(object sender, RoutedEventArgs e)
        {
            _cancelToken?.Cancel();
        }

        // ==========================================
        // VÒNG LẶP CHÍNH (STATE MACHINE)
        // ==========================================
        private void BotMainLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                using Bitmap screen = _bot.CaptureGameScreen();
                BotState state = DetermineState(screen);

                switch (state)
                {
                    case BotState.InMap:
                        var result = _navigator.Execute(screen, token);
                        if (result == NavigateResult.Arrived)
                            UpdateStatusToUI("Di chuyển xong, đổi điểm.");
                        else if (result == NavigateResult.Timeout)
                            UpdateStatusToUI("Timeout, thử lại...");
                        break;

                    case BotState.Combat_UserTurn:
                        HandleUserCombatLogic(screen);
                        break;

                    case BotState.Combat_PetTurn:
                        HandlePetCombatLogic(screen);
                        break;

                    case BotState.Unknown:
                        UpdateStatusToUI("Đang dò tìm...");
                        Thread.Sleep(1000);
                        break;
                }

                Thread.Sleep(300);
            }
        }

        private BotState DetermineState(Bitmap screen)
        {
            // Kiểm tra các Dấu hiệu nhận biết (Anchor Images)
            if (_bot.FindTemplate(screen, "anchor_turn_user.png", 0.75).HasValue) return BotState.Combat_UserTurn;
            if (_bot.FindTemplate(screen, "anchor_turn_pet.png", 0.75).HasValue) return BotState.Combat_PetTurn;
            if (_bot.FindTemplate(screen, "anchor_map.png", 0.75).HasValue) return BotState.InMap;

            return BotState.Unknown;
        }

        // ==========================================
        // LOGIC 1: ĐI TUẦN TRA NGOÀI MAP
        // ==========================================
        private void HandleMapLogic(Bitmap screen)
        {
            UpdateStatusToUI("Đang ở ngoài Map...");

            // Tìm cái nút "Thế giới" (hoặc cái icon gì đó trên bản đồ lớn mà bạn đã cắt lọt lòng bằng ảnh của Bot)
            System.Drawing.Point? mapLonDaMo = _bot.FindTemplate(screen, "nut_the_gioi.png", 0.75);

            if (mapLonDaMo.HasValue)
            {
                // =============================================================
                // MAP ĐANG MỞ -> CHỈ CLICK TỌA ĐỘ VÀ ĐỂ ĐÓ CHO NHÂN VẬT CHẠY
                // =============================================================
                LogToUI($"[Auto-Route] Map đang mở. Click chạy tới điểm {(_currentRouteIndex + 1)}/{_routePoints.Length}");

                int targetMapX = _routePoints[_currentRouteIndex].X;
                int targetMapY = _routePoints[_currentRouteIndex].Y;

                _bot.HardClickAt(targetMapX, targetMapY);

                // Chuyển sang tọa độ tiếp theo sẵn cho lần chạy sau
                _currentRouteIndex++;
                if (_currentRouteIndex >= _routePoints.Length) _currentRouteIndex = 0;

                // Cho Bot ngủ một đoạn để nhân vật lon ton chạy trên map.
                // Giờ cứ ung dung để đó, gặp quái game tự tắt map thì bot nhảy sang State Combat thôi!
                Thread.Sleep(6000);
            }
            else
            {
                // =============================================================
                // MAP ĐANG ĐÓNG (Vừa bật tool, hoặc vừa đánh xong bị game đóng mất)
                // =============================================================
                LogToUI("Bản đồ đang đóng, gõ ~ để mở...");
                _bot.HardPressKey(Win32Api.VK_OEM_3);

                // Ngủ 1.5 giây chờ game vẽ cái khung bản đồ ra
                Thread.Sleep(1500);
            }
        }

        // ==========================================
        // LOGIC 2: LƯỢT ĐÁNH CỦA NHÂN VẬT (USER)
        // ==========================================
        private void HandleUserCombatLogic(Bitmap screen)
        {
            UpdateStatusToUI("Đang Combat (Lượt User)");

            System.Drawing.Point? baoBaoLoc = _bot.FindTemplate(screen, "baobao_icon.png", 0.85);

            if (baoBaoLoc.HasValue)
            {
                // --- TRƯỜNG HỢP CÓ PET ---
                LogToUI("Phát hiện BẢO BẢO! Đang ném lưới...");
                System.Drawing.Point? nutBatLoc = _bot.FindTemplate(screen, "nut_bat.png", 0.8);
                if (nutBatLoc.HasValue)
                {
                    _bot.HardClickAt(nutBatLoc.Value.X, nutBatLoc.Value.Y);
                    Thread.Sleep(800); // Chờ đổi con trỏ chuột

                    _bot.HardClickAt(baoBaoLoc.Value.X - 70, baoBaoLoc.Value.Y - 75);

                    _isCatchingMode = true; // Bật cờ để lát Pet biết đường phòng ngự
                    LogToUI("Đã chọn lệnh Bắt.");
                }
            }
            else
            {
                // --- TRƯỜNG HỢP KHÔNG CÓ PET (ĐÁNH QUÁI THƯỜNG) ---
                LogToUI("Đánh quái thường...");
                System.Drawing.Point? tanCongLoc = _bot.FindTemplate(screen, "nut_tan_cong.png", 0.8);
                if (tanCongLoc.HasValue)
                {
                    // Click nút Tấn công (nó sẽ tự đánh mục tiêu gần nhất, hoặc tự động kích hoạt Auto)
                    _bot.HardClickAt(tanCongLoc.Value.X, tanCongLoc.Value.Y);
                }
                _isCatchingMode = false;
            }

            Thread.Sleep(1500); // Ngủ chờ game giật menu lên cho Pet
        }

        // ==========================================
        // LOGIC 3: LƯỢT ĐÁNH CỦA PET
        // ==========================================
        private void HandlePetCombatLogic(Bitmap screen)
        {
            UpdateStatusToUI("Đang Combat (Lượt Pet)");

            if (_isCatchingMode)
            {
                // USER ĐANG BẮT PET -> PET PHẢI PHÒNG NGỰ
                LogToUI("Pet Phòng ngự để chờ bắt...");
                System.Drawing.Point? phongNguLoc = _bot.FindTemplate(screen, "nut_phong_ngu.png", 0.8);
                if (phongNguLoc.HasValue)
                {
                    _bot.HardClickAt(phongNguLoc.Value.X, phongNguLoc.Value.Y);
                    _petCaught++;
                    UpdateStatusToUI("Bắt thành công!", petCount: _petCaught);
                }
            }
            else
            {
                // USER ĐÁNH THƯỜNG -> PET CŨNG ĐÁNH THƯỜNG
                LogToUI("Pet tấn công...");
                System.Drawing.Point? tanCongLoc = _bot.FindTemplate(screen, "nut_tan_cong.png", 0.8);
                if (tanCongLoc.HasValue)
                {
                    _bot.HardClickAt(tanCongLoc.Value.X, tanCongLoc.Value.Y);
                }
                _battleCount++;
                UpdateStatusToUI("Trận kết thúc", battleCount: _battleCount);
            }

            // Chờ cả Turn diễn ra xong xuôi. (Hoạt hình ném bóng, hoạt hình chém quái...)
            LogToUI("Đang chờ hoàn tất Turn...");
            Thread.Sleep(8000);
        }
    }
}