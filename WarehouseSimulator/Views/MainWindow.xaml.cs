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
using WarehouseSimulator.Resources;
using WarehouseSimulator.ViewModels;

namespace WarehouseSimulator.Views
{
    public partial class MainWindow : Window
    {
        private MainViewModel ViewModel => (MainViewModel)DataContext;

        private static readonly SolidColorBrush SHELF_NORMAL      = new(Color.FromRgb(0x2E, 0x86, 0xAB));
        private static readonly SolidColorBrush SHELF_SELECTED    = new(Color.FromRgb(0xFF, 0x6B, 0x35));
        private static readonly SolidColorBrush SHELF_HOVER       = new(Color.FromRgb(0xFF, 0xD1, 0x66));
        private static readonly SolidColorBrush AISLE_BG          = new(Color.FromArgb(0x40, 0x0F, 0x11, 0x17));
        private static readonly SolidColorBrush ROUTE_COLOR       = new(Color.FromRgb(0xFF, 0x2D, 0x2D));
        private static readonly SolidColorBrush DOOR_COLOR        = new(Color.FromRgb(0x06, 0xD6, 0xA0));
        private static readonly SolidColorBrush BLOCK_HEADER_BG   = new(Color.FromArgb(0x60, 0x1A, 0x1D, 0x27));
        private static readonly SolidColorBrush GRID_LINE_COLOR   = new(Color.FromArgb(0x30, 0x2D, 0x32, 0x50));

        private readonly Dictionary<string, Rectangle> _shelfRects = new();
        private readonly List<UIElement> _routeElements = new();
        private Storyboard? _routeStoryboard;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void CmbLanguage_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var culture = cmbLanguage.SelectedIndex switch
            {
                0 => "tr-TR",
                1 => "en-US",
                2 => "de-DE",
                3 => "it-IT",
                _ => "es-ES"
            };
            TranslationSource.Instance.SetLanguage(culture);

            if (ViewModel.Warehouse != null)
                DrawWarehouse(ViewModel.Warehouse);
        }

        private void BtnBuildWarehouse_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.BuildWarehouse();
            if (ViewModel.Warehouse != null)
                DrawWarehouse(ViewModel.Warehouse);
        }

        private void BtnRandomOrder_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.GenerateRandomOrder();
            if (ViewModel.Warehouse != null)
                RefreshShelfColors(ViewModel.Warehouse);
        }

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

        private void BtnCalculateRoutes_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.CalculateAllRoutes();
        }

        private void BtnShowRoute_Click(object sender, RoutedEventArgs e)
        {
            var viewModel = DataContext as MainViewModel;
            if (viewModel?.SelectedResult != null && viewModel.Warehouse != null)
                DrawRoute(viewModel.SelectedResult, viewModel.Warehouse);
        }

        private void DgResults_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var viewModel = DataContext as MainViewModel;
            if (viewModel?.SelectedResult != null && viewModel.Warehouse != null)
                DrawRoute(viewModel.SelectedResult, viewModel.Warehouse);
        }

        private void DrawWarehouse(Warehouse warehouse)
        {
            warehouseCanvas.Children.Clear();
            warehouseCanvas.LayoutTransform = null;
            _shelfRects.Clear();
            _routeElements.Clear();

            double totalWidth  = WarehouseBuilder.CalculateTotalPixelWidth(warehouse);
            double totalHeight = WarehouseBuilder.CalculateTotalPixelHeight(warehouse);

            warehouseCanvas.Width  = totalWidth  + WarehouseBuilder.MARGIN * 2 + 100;
            warehouseCanvas.Height = totalHeight + WarehouseBuilder.MARGIN * 2 + 80;

            this.WindowState = WindowState.Maximized;

            DrawBackgroundGrid(warehouse);

            foreach (var block in warehouse.Blocks)
                DrawBlock(block, warehouse);

            DrawDoor(warehouse);
            DrawLabels(warehouse);

            this.Dispatcher.BeginInvoke(new Action(FitWarehouseToView),
                System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private void FitWarehouseToView()
        {
            double availableWidth  = canvasScrollViewer.ActualWidth;
            double availableHeight = canvasScrollViewer.ActualHeight;

            if (availableWidth <= 0 || availableHeight <= 0 ||
                warehouseCanvas.Width <= 0 || warehouseCanvas.Height <= 0)
                return;

            double scaleX = availableWidth / warehouseCanvas.Width;
            double scaleY = availableHeight / warehouseCanvas.Height;
            double scale = Math.Min(scaleX, scaleY) * 0.96;

            if (scale < 1.0)
            {
                warehouseCanvas.LayoutTransform = new ScaleTransform(scale, scale);
            }
        }

        private void DrawBackgroundGrid(Warehouse warehouse)
        {
            double cW = warehouseCanvas.Width;
            double cH = warehouseCanvas.Height;

            double spH = WarehouseBuilder.GetShelfPixelHeight(warehouse);
            for (double y = WarehouseBuilder.MARGIN; y < cH - 20; y += spH)
            {
                var line = new Line
                {
                    X1 = 0, Y1 = y, X2 = cW, Y2 = y,
                    Stroke = GRID_LINE_COLOR, StrokeThickness = 0.5
                };
                warehouseCanvas.Children.Add(line);
            }
        }

        private void DrawBlock(Block block, Warehouse warehouse)
        {
            double spH = WarehouseBuilder.GetShelfPixelHeight(warehouse);
            double cpH = WarehouseBuilder.GetCrossAislePixelHeight(warehouse);
            double blockGroupWidth = warehouse.AislesPerBlock *
                                     (WarehouseBuilder.SHELF_PIXEL_WIDTH * 2 + WarehouseBuilder.AISLE_PIXEL_WIDTH);

            double blockStartX = WarehouseBuilder.MARGIN +
                                 block.BlockIndex * (blockGroupWidth + cpH * 2);

            double blockHeight = warehouse.ShelvesPerAisle * spH;

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

            var blockLabel = CreateLabel(
                LanguageResources.Format("BlockLabel", block.BlockIndex + 1),
                Color.FromRgb(0x2E, 0x86, 0xAB), 11, FontWeights.Bold);
            Canvas.SetLeft(blockLabel, blockStartX + blockGroupWidth / 2 - 25);
            Canvas.SetTop(blockLabel, WarehouseBuilder.MARGIN - spH);
            warehouseCanvas.Children.Add(blockLabel);

            foreach (var aisle in block.Aisles)
                DrawAisle(aisle, blockStartX, warehouse);

            DrawCrossAisle(blockStartX, blockGroupWidth, warehouse, "bottom");
            DrawCrossAisle(blockStartX, blockGroupWidth, warehouse, "top");
        }

        private void DrawAisle(Aisle aisle, double blockStartX, Warehouse warehouse)
        {
            double spH = WarehouseBuilder.GetShelfPixelHeight(warehouse);
            double aisleGroupWidth = WarehouseBuilder.SHELF_PIXEL_WIDTH * 2 + WarehouseBuilder.AISLE_PIXEL_WIDTH;
            double aisleX = blockStartX + aisle.AisleIndex * aisleGroupWidth;

            var aisleBg = new Rectangle
            {
                Width  = WarehouseBuilder.AISLE_PIXEL_WIDTH,
                Height = warehouse.ShelvesPerAisle * spH,
                Fill   = AISLE_BG
            };
            Canvas.SetLeft(aisleBg, aisleX + WarehouseBuilder.SHELF_PIXEL_WIDTH);
            Canvas.SetTop(aisleBg, WarehouseBuilder.MARGIN);
            warehouseCanvas.Children.Add(aisleBg);

            var aisleLabel = CreateLabel(
                LanguageResources.Format("AisleLabel", aisle.AisleIndex + 1),
                Color.FromArgb(0x80, 0x8B, 0x92, 0xA8), 9, FontWeights.Normal);
            Canvas.SetLeft(aisleLabel,
                aisleX + WarehouseBuilder.SHELF_PIXEL_WIDTH + WarehouseBuilder.AISLE_PIXEL_WIDTH / 2 - 8);
            Canvas.SetTop(aisleLabel,
                WarehouseBuilder.MARGIN + warehouse.ShelvesPerAisle * spH + 3);
            warehouseCanvas.Children.Add(aisleLabel);

            foreach (var shelf in aisle.Shelves)
                DrawShelf(shelf, aisleX, warehouse);
        }

        private void DrawShelf(ShelfLocation shelf, double aisleX, Warehouse warehouse)
        {
            double spH = WarehouseBuilder.GetShelfPixelHeight(warehouse);
            double shelfX, shelfY;

            if (shelf.Side == 0)
                shelfX = aisleX;
            else
                shelfX = aisleX + WarehouseBuilder.SHELF_PIXEL_WIDTH + WarehouseBuilder.AISLE_PIXEL_WIDTH;

            shelfY = WarehouseBuilder.MARGIN + shelf.ShelfRow * spH;

            var rect = new Rectangle
            {
                Width  = WarehouseBuilder.SHELF_PIXEL_WIDTH - 2,
                Height = spH - 2,
                Fill   = shelf.IsSelected ? SHELF_SELECTED : SHELF_NORMAL,
                RadiusX = 3, RadiusY = 3,
                Cursor = Cursors.Hand,
                Tag = shelf
            };

            rect.ToolTip = new ToolTip
            {
                Content = LanguageResources.Format("ShelfTooltip", shelf.Label)
            };

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

            rect.MouseLeftButtonDown += (s, e) =>
            {
                ViewModel.ToggleShelf(shelf);
                rect.Fill = shelf.IsSelected ? SHELF_SELECTED : SHELF_NORMAL;
                ClearRouteVisualization();
            };

            Canvas.SetLeft(rect, shelfX + 1);
            Canvas.SetTop(rect, shelfY + 1);

            if (spH >= 18)
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
                Canvas.SetTop(label, shelfY + spH / 2 - 5);
                warehouseCanvas.Children.Add(label);
            }

            warehouseCanvas.Children.Add(rect);
            _shelfRects[shelf.UniqueId] = rect;
        }

        private void DrawCrossAisle(double blockStartX, double blockGroupWidth, Warehouse warehouse, string position)
        {
            double spH = WarehouseBuilder.GetShelfPixelHeight(warehouse);
            double cpH = WarehouseBuilder.GetCrossAislePixelHeight(warehouse);
            double y = position == "bottom"
                ? WarehouseBuilder.MARGIN + warehouse.ShelvesPerAisle * spH
                : WarehouseBuilder.MARGIN - cpH;

            var crossRect = new Rectangle
            {
                Width  = blockGroupWidth,
                Height = cpH,
                Fill   = new SolidColorBrush(Color.FromArgb(0x20, 0x06, 0xD6, 0xA0))
            };
            Canvas.SetLeft(crossRect, blockStartX);
            Canvas.SetTop(crossRect, y);
            warehouseCanvas.Children.Add(crossRect);

            var labelText = position == "bottom"
                ? LanguageResources.GetString("BottomCrossAisle")
                : LanguageResources.GetString("TopCrossAisle");
            var label = CreateLabel(labelText,
                Color.FromArgb(0x60, 0x06, 0xD6, 0xA0), 9, FontWeights.Normal);
            Canvas.SetLeft(label, blockStartX + 4);
            Canvas.SetTop(label, y + 8);
            warehouseCanvas.Children.Add(label);
        }

        private void DrawDoor(Warehouse warehouse)
        {
            var (doorX, doorY) = WarehouseBuilder.GetDoorPixelPosition(warehouse);

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

            var doorLabel = CreateLabel(
                LanguageResources.GetString("DoorLabel"),
                Color.FromRgb(0x06, 0xD6, 0xA0), 9, FontWeights.SemiBold);
            Canvas.SetLeft(doorLabel, doorX - 30);
            Canvas.SetTop(doorLabel, doorY + 16);
            warehouseCanvas.Children.Add(doorLabel);
        }

        private void DrawLabels(Warehouse warehouse)
        {
            double spH = WarehouseBuilder.GetShelfPixelHeight(warehouse);
            double cpH = WarehouseBuilder.GetCrossAislePixelHeight(warehouse);
            var summary = CreateLabel(
                LanguageResources.Format("SummaryFormat",
                    warehouse.TotalShelves, warehouse.TotalAisles, warehouse.BlockCount),
                Color.FromRgb(0x8B, 0x92, 0xA8), 10, FontWeights.Normal);
            Canvas.SetLeft(summary, WarehouseBuilder.MARGIN);
            Canvas.SetTop(summary,
                WarehouseBuilder.MARGIN +
                warehouse.ShelvesPerAisle * spH +
                cpH + 5);
            warehouseCanvas.Children.Add(summary);
        }

        private void DrawRoute(RouteResult result, Warehouse warehouse)
        {
            ClearRouteVisualization();

            if (result.RoutePoints.Count < 2) return;

            (double px, double py) ToPixel(WarehousePoint p)
            {
                double spH = WarehouseBuilder.GetShelfPixelHeight(warehouse);
                double cpH = WarehouseBuilder.GetCrossAislePixelHeight(warehouse);
                double aisleGroupUnit = warehouse.ShelfWidth * 2 + 1.0;
                double aisleGroupPixel = WarehouseBuilder.SHELF_PIXEL_WIDTH * 2 + WarehouseBuilder.AISLE_PIXEL_WIDTH;
                double blockUnitWidth = warehouse.AislesPerBlock * aisleGroupUnit;
                double blockPixelWidth = warehouse.AislesPerBlock * aisleGroupPixel;
                double totalBlockUnit = blockUnitWidth + warehouse.CrossAisleDistance;

                double calcX;
                double calcY;

                if (p.Y < 0)
                {
                    var (doorX, doorY) = WarehouseBuilder.GetDoorPixelPosition(warehouse);
                    calcX = doorX;
                    calcY = doorY;
                }
                else
                {
                    int blockIndex = (int)(p.X / totalBlockUnit);
                    double xInBlock = p.X - blockIndex * totalBlockUnit;
                    if (xInBlock < 0) xInBlock = 0;
                    if (xInBlock > blockUnitWidth) xInBlock = blockUnitWidth;

                    int aisleIndex = (int)(xInBlock / aisleGroupUnit);

                    double blockStartX = WarehouseBuilder.MARGIN
                        + blockIndex * (blockPixelWidth + cpH * 2);
                    double aisleStartX = blockStartX + aisleIndex * aisleGroupPixel;

                    calcX = aisleStartX + WarehouseBuilder.SHELF_PIXEL_WIDTH + WarehouseBuilder.AISLE_PIXEL_WIDTH / 2;

                    if (p.Y < 0.01)
                    {
                        calcY = WarehouseBuilder.MARGIN - cpH / 2;
                    }
                    else if (p.Y > warehouse.AisleLength - 0.01)
                    {
                        calcY = WarehouseBuilder.MARGIN
                              + warehouse.ShelvesPerAisle * spH
                              + cpH / 2;
                    }
                    else
                    {
                        double unitPerRow = warehouse.AisleLength / warehouse.ShelvesPerAisle;
                        calcY = WarehouseBuilder.MARGIN + (p.Y / unitPerRow) * spH;
                    }
                }

                return (calcX, calcY);
            }

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

                Canvas.SetZIndex(line, 10);
                warehouseCanvas.Children.Add(line);
                _routeElements.Add(line);
            }

            int seq = 1;
            foreach (var pt in result.RoutePoints.Skip(1).Take(result.RoutePoints.Count - 2))
            {
                var (px, py) = ToPixel(pt);

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

            AnimateRouteLines();
        }

        private void ClearRouteVisualization()
        {
            _routeStoryboard?.Stop();
            _routeStoryboard = null;
            foreach (var elem in _routeElements)
                warehouseCanvas.Children.Remove(elem);
            _routeElements.Clear();
        }

        private void AnimateRouteLines()
        {
            _routeStoryboard?.Stop();
            _routeStoryboard = new Storyboard();
            var duration = new Duration(TimeSpan.FromSeconds(1.2));

            foreach (var line in _routeElements.OfType<Line>())
            {
                var anim = new DoubleAnimation
                {
                    From = 0,
                    To = -9,
                    Duration = duration,
                    RepeatBehavior = RepeatBehavior.Forever
                };
                Storyboard.SetTarget(anim, line);
                Storyboard.SetTargetProperty(anim, new PropertyPath("StrokeDashOffset"));
                _routeStoryboard.Children.Add(anim);
            }

            _routeStoryboard.Begin();
        }

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
                            ? Color.FromRgb(0xFF, 0x6B, 0x35)
                            : Color.FromRgb(0x2E, 0x86, 0xAB);
                    }

                    rect.Fill = new SolidColorBrush(targetColor);
                }
            }
        }

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

    public class BooleanToVisibilityConverter : System.Windows.Data.IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            value is true ? Visibility.Visible : Visibility.Collapsed;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            value is Visibility.Visible;
    }

    public class NullToVisibilityConverter : System.Windows.Data.IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            value != null ? Visibility.Visible : Visibility.Collapsed;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotImplementedException();
    }
}
