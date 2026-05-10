using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using WarehouseSimulator.Models;
using WarehouseSimulator.Algorithms;
using WarehouseSimulator.Helpers;

namespace WarehouseSimulator.ViewModels
{
    /// <summary>
    /// Ana pencere için ViewModel.
    /// MVVM mimarisi: Model ve View arasında köprü görevi görür.
    /// INotifyPropertyChanged implementasyonu ile UI güncellemeleri otomatik yapılır.
    /// </summary>
    public class MainViewModel : INotifyPropertyChanged
    {
        // ===== Depo Parametreleri =====
        private int _blockCount = 2;
        private int _aislesPerBlock = 3;
        private int _shelvesPerAisle = 8;
        private DoorLocation _doorLocation = DoorLocation.Left;
        private double _aisleLength = 10.0;
        private double _crossAisleDistance = 3.0;

        /// <summary>Blok sayısı (1-10)</summary>
        public int BlockCount
        {
            get => _blockCount;
            set { _blockCount = Math.Max(1, Math.Min(10, value)); OnPropertyChanged(); }
        }

        /// <summary>Koridor sayısı (1-12)</summary>
        public int AislesPerBlock
        {
            get => _aislesPerBlock;
            set { _aislesPerBlock = Math.Max(1, Math.Min(12, value)); OnPropertyChanged(); }
        }

        /// <summary>Raf sayısı (2-20)</summary>
        public int ShelvesPerAisle
        {
            get => _shelvesPerAisle;
            set { _shelvesPerAisle = Math.Max(2, Math.Min(20, value)); OnPropertyChanged(); }
        }

        /// <summary>Kapı lokasyonu</summary>
        public DoorLocation DoorLocation
        {
            get => _doorLocation;
            set { _doorLocation = value; OnPropertyChanged(); }
        }

        /// <summary>Koridor uzunluğu (birim)</summary>
        public double AisleLength
        {
            get => _aisleLength;
            set { _aisleLength = Math.Max(1, value); OnPropertyChanged(); }
        }

        /// <summary>Geçiş mesafesi (birim)</summary>
        public double CrossAisleDistance
        {
            get => _crossAisleDistance;
            set { _crossAisleDistance = Math.Max(0.5, value); OnPropertyChanged(); }
        }

        // ===== Sipariş Parametreleri =====
        private int _randomOrderCount = 7;

        /// <summary>Rastgele sipariş sayısı</summary>
        public int RandomOrderCount
        {
            get => _randomOrderCount;
            set { _randomOrderCount = Math.Max(1, value); OnPropertyChanged(); }
        }

        // ===== Durum =====
        private Warehouse? _warehouse;
        private Order? _currentOrder;
        private string _statusMessage = "Depo parametrelerini girin ve 'Depoyu Göster' butonuna basın.";
        private bool _isWarehouseBuilt;
        private bool _hasOrder;

        /// <summary>Oluşturulmuş depo nesnesi</summary>
        public Warehouse? Warehouse
        {
            get => _warehouse;
            set { _warehouse = value; OnPropertyChanged(); IsWarehouseBuilt = value != null; }
        }

        /// <summary>Mevcut sipariş</summary>
        public Order? CurrentOrder
        {
            get => _currentOrder;
            set { _currentOrder = value; OnPropertyChanged(); HasOrder = value?.ItemCount > 0; }
        }

        /// <summary>Durum mesajı (alt barı için)</summary>
        public string StatusMessage
        {
            get => _statusMessage;
            set { _statusMessage = value; OnPropertyChanged(); }
        }

        /// <summary>Depo oluşturuldu mu?</summary>
        public bool IsWarehouseBuilt
        {
            get => _isWarehouseBuilt;
            set { _isWarehouseBuilt = value; OnPropertyChanged(); }
        }

        /// <summary>Sipariş var mı?</summary>
        public bool HasOrder
        {
            get => _hasOrder;
            set { _hasOrder = value; OnPropertyChanged(); }
        }

        private bool _isHeatmapVisible;
        /// <summary>Isı haritası görünür mü?</summary>
        public bool IsHeatmapVisible
        {
            get => _isHeatmapVisible;
            set { _isHeatmapVisible = value; OnPropertyChanged(); }
        }

        // ===== Algoritmalar =====
        /// <summary>Kullanılabilir algoritmalar listesi</summary>
        public List<RoutingAlgorithm> AvailableAlgorithms { get; } = new()
        {
            new SShapeAlgorithm(),
            new LargestGapAlgorithm(),
            new AisleByAisleAlgorithm(),
            new OptimalNearestNeighborAlgorithm(),
            new TwoOptTSPAlgorithm()
        };

        // ===== Sonuçlar =====
        private ObservableCollection<RouteResult> _algorithmResults = new();

        /// <summary>Hesaplanan algoritma sonuçları</summary>
        public ObservableCollection<RouteResult> AlgorithmResults
        {
            get => _algorithmResults;
            set { _algorithmResults = value; OnPropertyChanged(); }
        }

        private RouteResult? _selectedResult;

        /// <summary>Görselleştirme için seçili sonuç</summary>
        public RouteResult? SelectedResult
        {
            get => _selectedResult;
            set { _selectedResult = value; OnPropertyChanged(); }
        }

        // ===== Veritabanı İstatistikleri =====
        private long _currentConfigId = -1;
        public long CurrentConfigId => _currentConfigId;

        /// <summary>
        /// Depoyu verilen parametrelerle oluşturur
        /// </summary>
        public void BuildWarehouse()
        {
            try
            {
                Warehouse = WarehouseBuilder.Build(
                    BlockCount, AislesPerBlock, ShelvesPerAisle,
                    DoorLocation, AisleLength, CrossAisleDistance);

                // Veritabanına kayıt et
                _currentConfigId = DatabaseManager.Instance.SaveWarehouseConfig(Warehouse);

                AlgorithmResults.Clear();
                SelectedResult = null;
                CurrentOrder = new Order { OrderId = 1 };

                StatusMessage = $"✓ Depo oluşturuldu: {Warehouse.TotalShelves} raf, " +
                               $"{Warehouse.TotalAisles} koridor. " +
                               $"Şimdi sipariş oluşturun.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Hata: {ex.Message}";
            }
        }

        /// <summary>
        /// Rastgele sipariş oluşturur
        /// </summary>
        public void GenerateRandomOrder()
        {
            if (Warehouse == null) return;

            var allShelves = Warehouse.GetAllShelves();
            if (allShelves.Count == 0) return;

            // Önce tüm seçimleri temizle
            Warehouse.ClearAllSelections();

            var rng = new Random();
            int count = Math.Min(RandomOrderCount, allShelves.Count);
            var selected = new HashSet<int>();

            CurrentOrder = new Order { OrderId = (CurrentOrder?.OrderId ?? 0) + 1, CreatedAt = DateTime.Now };

            // Benzersiz rastgele indeksler seç
            while (selected.Count < count)
                selected.Add(rng.Next(allShelves.Count));

            int seq = 1;
            foreach (int idx in selected)
            {
                allShelves[idx].IsSelected = true;
                CurrentOrder.Items.Add(new OrderItem
                {
                    Location = allShelves[idx],
                    ProductName = $"Ürün-{seq:D3}",
                    PickSequence = seq++
                });
            }

            AlgorithmResults.Clear();
            SelectedResult = null;
            StatusMessage = $"✓ {count} ürünlük sipariş oluşturuldu. " +
                           $"Algoritma seçerek rotayı hesaplayın.";
        }

        /// <summary>
        /// Tüm sipariş seçimlerini temizler
        /// </summary>
        public void ClearOrder()
        {
            Warehouse?.ClearAllSelections();
            CurrentOrder = new Order { OrderId = (CurrentOrder?.OrderId ?? 0) + 1 };
            AlgorithmResults.Clear();
            SelectedResult = null;
            StatusMessage = "Sipariş temizlendi. Yeni sipariş oluşturabilirsiniz.";
        }

        /// <summary>
        /// Seçili raf durumunu değiştirir (toggle)
        /// </summary>
        public void ToggleShelf(ShelfLocation shelf)
        {
            if (Warehouse == null || CurrentOrder == null) return;

            shelf.IsSelected = !shelf.IsSelected;

            if (shelf.IsSelected)
            {
                shelf.PickCount++; // Isı haritası için artır
                CurrentOrder.Items.Add(new OrderItem
                {
                    Location = shelf,
                    ProductName = $"Ürün@{shelf.Label}",
                    PickSequence = CurrentOrder.ItemCount
                });
            }
            else
            {
                shelf.PickCount = Math.Max(0, shelf.PickCount - 1);
                CurrentOrder.Items.RemoveAll(i => i.Location.UniqueId == shelf.UniqueId);
            }

            HasOrder = CurrentOrder.ItemCount > 0;
            AlgorithmResults.Clear();
            SelectedResult = null;
            StatusMessage = $"Seçili raf sayısı: {CurrentOrder.ItemCount}";
        }

        /// <summary>
        /// Tüm seçili algoritmalar için rota hesaplar
        /// </summary>
        public void CalculateAllRoutes()
        {
            if (Warehouse == null || CurrentOrder == null || CurrentOrder.ItemCount == 0)
            {
                StatusMessage = "Önce sipariş oluşturun!";
                return;
            }

            var selectedShelves = Warehouse.GetSelectedShelves();
            AlgorithmResults.Clear();

            double bestDist = double.MaxValue;
            var results = new List<RouteResult>();

            foreach (var algorithm in AvailableAlgorithms)
            {
                var result = algorithm.Calculate(Warehouse, selectedShelves);
                results.Add(result);

                if (result.TotalDistance < bestDist)
                    bestDist = result.TotalDistance;
            }

            // En iyi algoritmayı işaretle
            foreach (var r in results)
            {
                r.IsBest = Math.Abs(r.TotalDistance - bestDist) < 0.01;
                AlgorithmResults.Add(r);
            }

            // Veritabanına kayıt et
            if (CurrentOrder != null && _currentConfigId > 0)
            {
                try
                {
                    DatabaseManager.Instance.SaveOrder(CurrentOrder, _currentConfigId, results);
                }
                catch { /* DB hatası sessizce geç */ }
            }

            // İlk sonucu göster
            if (AlgorithmResults.Count > 0)
                SelectedResult = AlgorithmResults[0];

            StatusMessage = $"✓ {results.Count} algoritma hesaplandı. " +
                           $"En kısa mesafe: {bestDist:F2} birim.";
        }

        // ===== INotifyPropertyChanged =====
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
