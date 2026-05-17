using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using WarehouseSimulator.Helpers;
using WarehouseSimulator.Models;
using WarehouseSimulator.ViewModels;

namespace WarehouseSimulator.Views
{
    /// <summary>
    /// MainWindow.xaml için code-behind.
    /// Depo görselleştirmesi ve kullanıcı etkileşimlerini yönetir.
    /// </summary>
    public partial class MainWindow : Window
    {
        // ViewModel referansı
        private MainViewModel ViewModel => (MainViewModel)DataContext;

        // Renk sabitleri (görselleştirme için)
        private static readonly SolidColorBrush SHELF_NORMAL      = new(Color.FromRgb(0x2E, 0x86, 0xAB));
        private static readonly SolidColorBrush SHELF_SELECTED    = new(Color.FromRgb(0xFF, 0x6B, 0x35));
        private static readonly SolidColorBrush SHELF_HOVER       = new(Color.FromRgb(0xFF, 0xD1, 0x66));
        private static readonly SolidColorBrush AISLE_BG          = new(Color.FromArgb(0x40, 0x0F, 0x11, 0x17));
        private static readonly SolidColorBrush ROUTE_COLOR       = new(Color.FromRgb(0xFF, 0x2D, 0x2D));
        private static readonly SolidColorBrush DOOR_COLOR        = new(Color.FromRgb(0x06, 0xD6, 0xA0));
        private static readonly SolidColorBrush BLOCK_HEADER_BG   = new(Color.FromArgb(0x60, 0x1A, 0x1D, 0x27));
        private static readonly SolidColorBrush GRID_LINE_COLOR   = new(Color.FromArgb(0x30, 0x2D, 0x32, 0x50));

        // Canvas üzerindeki raf Rectangle'larının haritası
        private readonly Dictionary<string, Rectangle> _shelfRects = new();

        // Son çizilen rota çizgileri
        private readonly List<UIElement> _routeElements = new();

        public MainWindow()
        {
            InitializeComponent();
        }

        // =========================================================
        //  BUTON OLAYLARI
        // =========================================================

        /// <summary>Depoyu Göster butonu</summary>
        private void BtnBuildWarehouse_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.BuildWarehouse();
            if (ViewModel.Warehouse != null)
                DrawWarehouse(ViewModel.Warehouse);
        }

        /// <summary>Rastgele Sipariş butonu</summary>
        private void BtnRandomOrder_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.GenerateRandomOrder();
            if (ViewModel.Warehouse != null)
                RefreshShelfColors(ViewModel.Warehouse);
        }

        /// <summary>Siparişi Temizle butonu</summary>
        private void BtnClearOrder_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.ClearOrder();
            ClearRouteVisualization();
            if (ViewModel.Warehouse != null)
                RefreshShelfColors(ViewModel.Warehouse);
        }

        private void Heatmap_Changed(object sender, RoutedEventArgs e)
        {
            if (ViewModel.Warehouse != null)
                RefreshShelfColors(ViewModel.Warehouse);
        }

        /// <summary>Rotaları Hesapla butonu</summary>
        private void BtnCalculateRoutes_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.CalculateAllRoutes();
        }

        /// <summary>Bu Rotayı Göster butonu</summary>
        private void BtnShowRoute_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.SelectedResult != null && ViewModel.Warehouse != null)
                DrawRoute(ViewModel.SelectedResult, ViewModel.Warehouse);
        }

        /// <summary>DataGrid seçim değiştiğinde rotayı güncelle</summary>
        private void DgResults_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ViewModel.SelectedResult != null && ViewModel.Warehouse != null)
                DrawRoute(ViewModel.SelectedResult, ViewModel.Warehouse);
        }

        // =========================================================
        //  DEPO ÇİZİM METODLARI
        // =========================================================

        /// <summary>
        /// Depoyu canvas üzerine çizer.
        /// Grid tabanlı görselleştirme: Bloklar → Koridorlar → Raflar
        /// </summary>
        private void DrawWarehouse(Warehouse warehouse)
        {
            warehouseCanvas.Children.Clear();
            _shelfRects.Clear();
            _routeElements.Clear();

            double totalWidth  = WarehouseBuilder.CalculateTotalPixelWidth(warehouse);
            double totalHeight = WarehouseBuilder.CalculateTotalPixelHeight(warehouse);

            // Canvas boyutunu güncelle
            warehouseCanvas.Width  = totalWidth  + WarehouseBuilder.MARGIN * 2 + 100;
            warehouseCanvas.Height = totalHeight + WarehouseBuilder.MARGIN * 2 + 80;

            // Arka plan ızgara çizgileri
            DrawBackgroundGrid(warehouse);

            // Her blok
            foreach (var block in warehouse.Blocks)
                DrawBlock(block, warehouse);

            // Kapıyı çiz
            DrawDoor(warehouse);

            // Başlık etiketleri
            DrawLabels(warehouse);
        }

        /// <summary>Arka plan ızgara çizgileri</summary>
        private void DrawBackgroundGrid(Warehouse warehouse)
        {
            double cW = warehouseCanvas.Width;
            double cH = warehouseCanvas.Height;

            // Yatay çizgiler
            for (double y = WarehouseBuilder.MARGIN; y < cH - 20; y += WarehouseBuilder.SHELF_PIXEL_HEIGHT)
            {
                var line = new Line
                {
                    X1 = 0, Y1 = y, X2 = cW, Y2 = y,
                    Stroke = GRID_LINE_COLOR, StrokeThickness = 0.5
                };
                warehouseCanvas.Children.Add(line);
            }
        }

        /// <summary>Tek bir bloğu çizer</summary>
        private void DrawBlock(Block block, Warehouse warehouse)
        {
            double blockGroupWidth = warehouse.AislesPerBlock *
                                     (WarehouseBuilder.SHELF_PIXEL_WIDTH * 2 + WarehouseBuilder.AISLE_PIXEL_WIDTH);

            double blockStartX = WarehouseBuilder.MARGIN +
                                 block.BlockIndex * (blockGroupWidth + WarehouseBuilder.CROSS_AISLE_PIXEL_HEIGHT * 2);

            double blockHeight = warehouse.ShelvesPerAisle * WarehouseBuilder.SHELF_PIXEL_HEIGHT;

            // Blok çerçevesi
            var blockRect = new Rectangle
            {
                Width  = blockGroupWidth,
                Height = blockHeight,
                Fill   = Brushes.Transparent,
                Stroke = new SolidColorBrush(Color.FromArgb(0x60, 0x2E, 0x86, 0xAB)),
                StrokeThickness = 1,
                RadiusX = 4, RadiusY = 4,
                StrokeDashArray = new DoubleCollection { 4, 3 }
            };
            Canvas.SetLeft(blockRect, blockStartX);
            Canvas.SetTop(blockRect, WarehouseBuilder.MARGIN);
            warehouseCanvas.Children.Add(blockRect);

            // Blok başlığı
            var blockLabel = CreateLabel($"BLOK {block.BlockIndex + 1}",
                Color.FromRgb(0x2E, 0x86, 0xAB), 11, FontWeights.Bold);
            Canvas.SetLeft(blockLabel, blockStartX + blockGroupWidth / 2 - 25);
            Canvas.SetTop(blockLabel, WarehouseBuilder.MARGIN - 22);
            warehouseCanvas.Children.Add(blockLabel);

            // Her koridor
            foreach (var aisle in block.Aisles)
                DrawAisle(aisle, blockStartX, warehouse);

            // Arka plan (alt ve üst geçiş yolları)
            DrawCrossAisle(blockStartX, blockGroupWidth, warehouse, "alt");
            DrawCrossAisle(blockStartX, blockGroupWidth, warehouse, "üst");
        }

        /// <summary>Tek bir koridoru çizer</summary>
        private void DrawAisle(Aisle aisle, double blockStartX, Warehouse warehouse)
        {
            double aisleGroupWidth = WarehouseBuilder.SHELF_PIXEL_WIDTH * 2 + WarehouseBuilder.AISLE_PIXEL_WIDTH;
            double aisleX = blockStartX + aisle.AisleIndex * aisleGroupWidth;

            // Koridor geçiş alanı (koridorun kendi boşluğu)
            var aisleBg = new Rectangle
            {
                Width  = WarehouseBuilder.AISLE_PIXEL_WIDTH,
                Height = warehouse.ShelvesPerAisle * WarehouseBuilder.SHELF_PIXEL_HEIGHT,
                Fill   = AISLE_BG
            };
            Canvas.SetLeft(aisleBg, aisleX + WarehouseBuilder.SHELF_PIXEL_WIDTH);
            Canvas.SetTop(aisleBg, WarehouseBuilder.MARGIN);
            warehouseCanvas.Children.Add(aisleBg);

            // Koridor numarası
            var aisleLabel = CreateLabel($"K{aisle.AisleIndex + 1}",
                Color.FromArgb(0x80, 0x8B, 0x92, 0xA8), 9, FontWeights.Normal);
            Canvas.SetLeft(aisleLabel,
                aisleX + WarehouseBuilder.SHELF_PIXEL_WIDTH + WarehouseBuilder.AISLE_PIXEL_WIDTH / 2 - 8);
            Canvas.SetTop(aisleLabel,
                WarehouseBuilder.MARGIN + warehouse.ShelvesPerAisle * WarehouseBuilder.SHELF_PIXEL_HEIGHT + 3);
            warehouseCanvas.Children.Add(aisleLabel);

            // Her raf
            foreach (var shelf in aisle.Shelves)
                DrawShelf(shelf, aisleX, warehouse);
        }

        /// <summary>Tek bir raf kutucuğunu çizer ve tıklama olayını bağlar</summary>
        private void DrawShelf(ShelfLocation shelf, double aisleX, Warehouse warehouse)
        {
            double shelfX, shelfY;

            if (shelf.Side == 0)
                shelfX = aisleX; // Sol raf
            else
                shelfX = aisleX + WarehouseBuilder.SHELF_PIXEL_WIDTH + WarehouseBuilder.AISLE_PIXEL_WIDTH;

            shelfY = WarehouseBuilder.MARGIN + shelf.ShelfRow * WarehouseBuilder.SHELF_PIXEL_HEIGHT;

            var rect = new Rectangle
            {
                Width  = WarehouseBuilder.SHELF_PIXEL_WIDTH - 2,
                Height = WarehouseBuilder.SHELF_PIXEL_HEIGHT - 2,
                Fill   = shelf.IsSelected ? SHELF_SELECTED : SHELF_NORMAL,
                RadiusX = 3, RadiusY = 3,
                Cursor = Cursors.Hand,
                Tag = shelf
            };

            // Tooltip
            rect.ToolTip = new ToolTip
            {
                Content = $"📦 {shelf.Label}\nTıklayın: Seç/Kaldır"
            };

            // Hover animasyonu
            rect.MouseEnter += (s, e) =>
            {
                if (!shelf.IsSelected)
                    ((Rectangle)s!).Fill = SHELF_HOVER;
                ((Rectangle)s!).RenderTransform = new ScaleTransform(1.05, 1.05, rect.Width / 2, rect.Height / 2);
            };

            rect.MouseLeave += (s, e) =>
            {
                if (!shelf.IsSelected)
                    ((Rectangle)s!).Fill = SHELF_NORMAL;
                ((Rectangle)s!).RenderTransform = null;
            };

            // Tıklama: Manuel seçim
            rect.MouseLeftButtonDown += (s, e) =>
            {
                ViewModel.ToggleShelf(shelf);
                rect.Fill = shelf.IsSelected ? SHELF_SELECTED : SHELF_NORMAL;
                ClearRouteVisualization();
            };

            Canvas.SetLeft(rect, shelfX + 1);
            Canvas.SetTop(rect, shelfY + 1);

            // Çok küçük boyutlarda raf numarası gösterme
            if (WarehouseBuilder.SHELF_PIXEL_HEIGHT >= 18)
            {
                var label = new TextBlock
                {
                    Text = (shelf.ShelfRow + 1).ToString(),
                    Foreground = Brushes.White,
                    FontSize = 8,
                    FontFamily = new FontFamily("Segoe UI"),
                    IsHitTestVisible = false
                };
                Canvas.SetLeft(label, shelfX + WarehouseBuilder.SHELF_PIXEL_WIDTH / 2 - 4);
                Canvas.SetTop(label, shelfY + WarehouseBuilder.SHELF_PIXEL_HEIGHT / 2 - 5);
                warehouseCanvas.Children.Add(label);
            }

            warehouseCanvas.Children.Add(rect);
            _shelfRects[shelf.UniqueId] = rect;
        }

        /// <summary>Alt/üst geçiş koridorunu çizer</summary>
        private void DrawCrossAisle(double blockStartX, double blockGroupWidth, Warehouse warehouse, string position)
        {
            double y = position == "alt"
                ? WarehouseBuilder.MARGIN + warehouse.ShelvesPerAisle * WarehouseBuilder.SHELF_PIXEL_HEIGHT
                : WarehouseBuilder.MARGIN - WarehouseBuilder.CROSS_AISLE_PIXEL_HEIGHT;

            var crossRect = new Rectangle
            {
                Width  = blockGroupWidth,
                Height = WarehouseBuilder.CROSS_AISLE_PIXEL_HEIGHT,
                Fill   = new SolidColorBrush(Color.FromArgb(0x20, 0x06, 0xD6, 0xA0))
            };
            Canvas.SetLeft(crossRect, blockStartX);
            Canvas.SetTop(crossRect, y);
            warehouseCanvas.Children.Add(crossRect);

            var label = CreateLabel(
                position == "alt" ? "Alt Geçiş" : "Üst Geçiş",
                Color.FromArgb(0x60, 0x06, 0xD6, 0xA0), 9, FontWeights.Normal);
            Canvas.SetLeft(label, blockStartX + 4);
            Canvas.SetTop(label, y + 8);
            warehouseCanvas.Children.Add(label);
        }

        /// <summary>Kapı sembolünü çizer</summary>
        private void DrawDoor(Warehouse warehouse)
        {
            var (doorX, doorY) = WarehouseBuilder.GetDoorPixelPosition(warehouse);

            // Kapı ikonu
            var doorEllipse = new Ellipse
            {
                Width = 28, Height = 28,
                Fill = DOOR_COLOR,
                Stroke = Brushes.White,
                StrokeThickness = 2
            };
            Canvas.SetLeft(doorEllipse, doorX - 14);
            Canvas.SetTop(doorEllipse, doorY - 14);
            warehouseCanvas.Children.Add(doorEllipse);

            var doorIcon = new TextBlock
            {
                Text = "🚪", FontSize = 13,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(doorIcon, doorX - 10);
            Canvas.SetTop(doorIcon, doorY - 11);
            warehouseCanvas.Children.Add(doorIcon);

            // Etiket
            var doorLabel = CreateLabel("KAPI / Başlangıç",
                Color.FromRgb(0x06, 0xD6, 0xA0), 9, FontWeights.SemiBold);
            Canvas.SetLeft(doorLabel, doorX - 30);
            Canvas.SetTop(doorLabel, doorY + 16);
            warehouseCanvas.Children.Add(doorLabel);
        }

        /// <summary>Genel etiket (ölçek/başlık) çizer</summary>
        private void DrawLabels(Warehouse warehouse)
        {
            // Depo özet etiketi
            var summary = CreateLabel(
                $"Toplam: {warehouse.TotalShelves} raf | " +
                $"{warehouse.TotalAisles} koridor | " +
                $"{warehouse.BlockCount} blok",
                Color.FromRgb(0x8B, 0x92, 0xA8), 10, FontWeights.Normal);
            Canvas.SetLeft(summary, WarehouseBuilder.MARGIN);
            Canvas.SetTop(summary,
                WarehouseBuilder.MARGIN +
                warehouse.ShelvesPerAisle * WarehouseBuilder.SHELF_PIXEL_HEIGHT +
                WarehouseBuilder.CROSS_AISLE_PIXEL_HEIGHT + 5);
            warehouseCanvas.Children.Add(summary);
        }

        // =========================================================
        //  ROTA ÇİZİM METODLARI
        // =========================================================

        /// <summary>
        /// Seçili algoritma sonucunun rotasını canvas üzerine çizer.
        /// Rota noktaları piksel koordinatlarına dönüştürülür ve
        /// animasyonlu kırmızı çizgilerle bağlanır.
        /// </summary>
        private void DrawRoute(RouteResult result, Warehouse warehouse)
        {
            ClearRouteVisualization();

            if (result.RoutePoints.Count < 2) return;

            // Birim koordinatlarını piksel koordinatlarına çeviren fonksiyon
            (double px, double py) ToPixel(WarehousePoint p)
            {
                // X: birim değerini piksel ölçeğine çevir
                // Bir blok grubu: aislesPerBlock * (sol_raf + koridor + sağ_raf)
                double unitToPixelX = WarehouseBuilder.SHELF_PIXEL_WIDTH + WarehouseBuilder.AISLE_PIXEL_WIDTH / 2;

                double aisleGroupUnit = warehouse.ShelfWidth * 2 + 1.0;
                double aisleGroupPixel = WarehouseBuilder.SHELF_PIXEL_WIDTH * 2 + WarehouseBuilder.AISLE_PIXEL_WIDTH;
                double blockUnitWidth = warehouse.AislesPerBlock * aisleGroupUnit;
                double blockPixelWidth = warehouse.AislesPerBlock * aisleGroupPixel;
                double crossPixel = WarehouseBuilder.CROSS_AISLE_PIXEL_HEIGHT * 2;

                double scale = blockUnitWidth > 0 ? blockPixelWidth / blockUnitWidth : 1.0;

                double px = WarehouseBuilder.MARGIN + p.X * scale;
                double py;

                if (p.Y < 0)
                {
                    // Kapı: rafların üstünde
                    py = WarehouseBuilder.MARGIN - WarehouseBuilder.CROSS_AISLE_PIXEL_HEIGHT / 2;
                }
                else if (p.Y > warehouse.AisleLength - 0.01)
                {
                    // Üst geçiş
                    py = WarehouseBuilder.MARGIN +
                         warehouse.ShelvesPerAisle * WarehouseBuilder.SHELF_PIXEL_HEIGHT +
                         WarehouseBuilder.CROSS_AISLE_PIXEL_HEIGHT / 2;
                }
                else
                {
                    // Raf içi: birim Y'yi piksel Y'ye çevir
                    double rowScale = WarehouseBuilder.SHELF_PIXEL_HEIGHT;
                    double unitPerRow = warehouse.AisleLength / warehouse.ShelvesPerAisle;
                    py = WarehouseBuilder.MARGIN + (p.Y / unitPerRow) * rowScale + rowScale / 2;
                }

                return (px, py);
            }

            // Rota çizgilerini çiz
            for (int i = 0; i < result.RoutePoints.Count - 1; i++)
            {
                var (x1, y1) = ToPixel(result.RoutePoints[i]);
                var (x2, y2) = ToPixel(result.RoutePoints[i + 1]);

                var line = new Line
                {
                    X1 = x1, Y1 = y1,
                    X2 = x2, Y2 = y2,
                    Stroke = ROUTE_COLOR,
                    StrokeThickness = 2.5,
                    StrokeDashArray = new DoubleCollection { 6, 3 },
                    Opacity = 0.85,
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round
                };

                // Solma animasyonu (fade-in)
                var anim = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300 + i * 30))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                line.BeginAnimation(OpacityProperty, anim);

                Canvas.SetZIndex(line, 10);
                warehouseCanvas.Children.Add(line);
                _routeElements.Add(line);
            }

            // Rota noktalarını işaretle (toplama noktaları)
            int seq = 1;
            foreach (var pt in result.RoutePoints.Skip(1).Take(result.RoutePoints.Count - 2))
            {
                var (px, py) = ToPixel(pt);

                // Küçük daire
                var circle = new Ellipse
                {
                    Width = 10, Height = 10,
                    Fill = new SolidColorBrush(Color.FromRgb(0xFF, 0xD1, 0x66)),
                    Stroke = Brushes.White,
                    StrokeThickness = 1
                };
                Canvas.SetLeft(circle, px - 5);
                Canvas.SetTop(circle, py - 5);
                Canvas.SetZIndex(circle, 11);
                warehouseCanvas.Children.Add(circle);
                _routeElements.Add(circle);

                // Sıra numarası
                var numLabel = new TextBlock
                {
                    Text = seq.ToString(),
                    Foreground = Brushes.White,
                    FontSize = 7,
                    FontFamily = new FontFamily("Segoe UI"),
                    FontWeight = FontWeights.Bold
                };
                Canvas.SetLeft(numLabel, px - 4);
                Canvas.SetTop(numLabel, py - 5);
                Canvas.SetZIndex(numLabel, 12);
                warehouseCanvas.Children.Add(numLabel);
                _routeElements.Add(numLabel);

                seq++;
            }
        }

        /// <summary>Rota görselleştirmesini temizler (raf renkleri korunur)</summary>
        private void ClearRouteVisualization()
        {
            foreach (var elem in _routeElements)
                warehouseCanvas.Children.Remove(elem);
            _routeElements.Clear();
        }

        /// <summary>Tüm raf renklerini güncel IsSelected değerine göre yeniler</summary>
        private void RefreshShelfColors(Warehouse warehouse)
        {
            var allShelves = warehouse.GetAllShelves();
            int maxPicks = allShelves.Count > 0 ? allShelves.Max(s => s.PickCount) : 0;
            if (maxPicks == 0) maxPicks = 1;

            foreach (var shelf in allShelves)
            {
                if (_shelfRects.TryGetValue(shelf.UniqueId, out var rect))
                {
                    Color targetColor;

                    if (ViewModel.IsHeatmapVisible)
                    {
                        // Isı haritası: Mavi (soğuk) -> Kırmızı (sıcak) geçişi
                        double intensity = (double)shelf.PickCount / maxPicks;
                        targetColor = Color.FromRgb(
                            (byte)(46 + (255 - 46) * intensity),
                            (byte)(134 * (1 - intensity)),
                            (byte)(171 * (1 - intensity))
                        );
                    }
                    else
                    {
                        targetColor = shelf.IsSelected
                            ? Color.FromRgb(0xFF, 0x6B, 0x35) // Turuncu (Seçili)
                            : Color.FromRgb(0x2E, 0x86, 0xAB); // Mavi (Normal)
                    }

                    var anim = new ColorAnimation(targetColor, TimeSpan.FromMilliseconds(200));
                    ((SolidColorBrush)rect.Fill).BeginAnimation(SolidColorBrush.ColorProperty, anim);
                }
            }
        }

        // =========================================================
        //  YARDIMCI METODLAR
        // =========================================================

        /// <summary>Styled TextBlock etiketi oluşturur</summary>
        private TextBlock CreateLabel(string text, Color color, double fontSize, FontWeight weight) =>
            new()
            {
                Text = text,
                Foreground = new SolidColorBrush(color),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = fontSize,
                FontWeight = weight,
                IsHitTestVisible = false
            };
    }

    // =========================================================
    //  DÖNÜŞTÜRÜCÜLER (Value Converters)
    // =========================================================

    /// <summary>
    /// Boolean → Visibility dönüştürücü (XAML için)
    /// </summary>
    public class BooleanToVisibilityConverter : System.Windows.Data.IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            value is true ? Visibility.Visible : Visibility.Collapsed;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            value is Visibility.Visible;
    }

    /// <summary>
    /// Null kontrolü → Visibility (null ise Collapsed, değilse Visible)
    /// </summary>
    public class NullToVisibilityConverter : System.Windows.Data.IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            value != null ? Visibility.Visible : Visibility.Collapsed;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotImplementedException();
    }
}
