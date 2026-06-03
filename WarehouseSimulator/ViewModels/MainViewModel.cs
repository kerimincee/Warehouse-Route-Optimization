using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using WarehouseSimulator.Models;
using WarehouseSimulator.Algorithms;
using WarehouseSimulator.Helpers;
using WarehouseSimulator.Resources;

namespace WarehouseSimulator.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        // ===== Depo Parametreleri =====
        private int _blockCount = 2;
        private int _aislesPerBlock = 3;
        private int _shelvesPerAisle = 8;
        private DoorLocation _doorLocation = DoorLocation.Left;
        private double _aisleLength = 10.0;
        private double _crossAisleDistance = 3.0;

        public int BlockCount
        {
            get => _blockCount;
            set { _blockCount = Math.Max(1, Math.Min(10, value)); OnPropertyChanged(); }
        }

        public int AislesPerBlock
        {
            get => _aislesPerBlock;
            set { _aislesPerBlock = Math.Max(1, Math.Min(12, value)); OnPropertyChanged(); }
        }

        public int ShelvesPerAisle
        {
            get => _shelvesPerAisle;
            set { _shelvesPerAisle = Math.Max(2, Math.Min(20, value)); OnPropertyChanged(); }
        }

        public DoorLocation DoorLocation
        {
            get => _doorLocation;
            set { _doorLocation = value; OnPropertyChanged(); }
        }

        public double AisleLength
        {
            get => _aisleLength;
            set { _aisleLength = Math.Max(1, value); OnPropertyChanged(); }
        }

        public double CrossAisleDistance
        {
            get => _crossAisleDistance;
            set { _crossAisleDistance = Math.Max(0.5, value); OnPropertyChanged(); }
        }

        // ===== Sipariş Parametreleri =====
        private int _randomOrderCount = 7;

        public int RandomOrderCount
        {
            get => _randomOrderCount;
            set { _randomOrderCount = Math.Max(1, value); OnPropertyChanged(); }
        }

        // ===== Durum =====
        private Warehouse? _warehouse;
        private Order? _currentOrder;
        private string _statusMessage = "";
        private bool _isWarehouseBuilt;
        private bool _hasOrder;

        public Warehouse? Warehouse
        {
            get => _warehouse;
            set { _warehouse = value; OnPropertyChanged(); IsWarehouseBuilt = value != null; }
        }

        public Order? CurrentOrder
        {
            get => _currentOrder;
            set { _currentOrder = value; OnPropertyChanged(); HasOrder = value?.ItemCount > 0; }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set { _statusMessage = value; OnPropertyChanged(); }
        }

        public bool IsWarehouseBuilt
        {
            get => _isWarehouseBuilt;
            set { _isWarehouseBuilt = value; OnPropertyChanged(); }
        }

        public bool HasOrder
        {
            get => _hasOrder;
            set { _hasOrder = value; OnPropertyChanged(); }
        }

        private bool _isHeatmapVisible;
        public bool IsHeatmapVisible
        {
            get => _isHeatmapVisible;
            set { _isHeatmapVisible = value; OnPropertyChanged(); }
        }

        // ===== Algoritmalar =====
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

        public ObservableCollection<RouteResult> AlgorithmResults
        {
            get => _algorithmResults;
            set { _algorithmResults = value; OnPropertyChanged(); }
        }

        private RouteResult? _selectedResult;

        public RouteResult? SelectedResult
        {
            get => _selectedResult;
            set { _selectedResult = value; OnPropertyChanged(); }
        }

        // ===== Veritabanı İstatistikleri =====
        private long _currentConfigId = -1;
        public long CurrentConfigId => _currentConfigId;

        public MainViewModel()
        {
            StatusMessage = LanguageResources.GetString("StatusEnterParams");
        }

        public void BuildWarehouse()
        {
            try
            {
                Warehouse = WarehouseBuilder.Build(
                    BlockCount, AislesPerBlock, ShelvesPerAisle,
                    DoorLocation, AisleLength, CrossAisleDistance);

                _currentConfigId = DatabaseManager.Instance.SaveWarehouseConfig(Warehouse);

                AlgorithmResults.Clear();
                SelectedResult = null;
                CurrentOrder = new Order { OrderId = 1 };

                StatusMessage = LanguageResources.Format("StatusWarehouseBuilt",
                    Warehouse.TotalShelves, Warehouse.TotalAisles);
            }
            catch (Exception ex)
            {
                StatusMessage = LanguageResources.Format("StatusError", ex.Message);
            }
        }

        public void GenerateRandomOrder()
        {
            if (Warehouse == null) return;

            var allShelves = Warehouse.GetAllShelves();
            if (allShelves.Count == 0) return;

            Warehouse.ClearAllSelections();

            var rng = new Random();
            int count = Math.Min(RandomOrderCount, allShelves.Count);
            var selected = new HashSet<int>();

            CurrentOrder = new Order { OrderId = (CurrentOrder?.OrderId ?? 0) + 1, CreatedAt = DateTime.Now };

            while (selected.Count < count)
                selected.Add(rng.Next(allShelves.Count));

            int seq = 1;
            foreach (int idx in selected)
            {
                allShelves[idx].IsSelected = true;
                CurrentOrder.Items.Add(new OrderItem
                {
                    Location = allShelves[idx],
                    ProductName = LanguageResources.Format("ProductNameSeq", seq),
                    PickSequence = seq++
                });
            }

            HasOrder = CurrentOrder.ItemCount > 0;

            AlgorithmResults.Clear();
            SelectedResult = null;
            StatusMessage = LanguageResources.Format("StatusOrderCreated", count);
        }

        public void ClearOrder()
        {
            Warehouse?.ClearAllSelections();
            CurrentOrder = new Order { OrderId = (CurrentOrder?.OrderId ?? 0) + 1 };
            AlgorithmResults.Clear();
            SelectedResult = null;
            StatusMessage = LanguageResources.GetString("StatusOrderCleared");
        }

        public void ToggleShelf(ShelfLocation shelf)
        {
            if (Warehouse == null || CurrentOrder == null) return;

            shelf.IsSelected = !shelf.IsSelected;

            if (shelf.IsSelected)
            {
                shelf.PickCount++;
                CurrentOrder.Items.Add(new OrderItem
                {
                    Location = shelf,
                    ProductName = LanguageResources.Format("ProductNameAt", shelf.Label),
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
            StatusMessage = LanguageResources.Format("StatusSelectedCount", CurrentOrder.ItemCount);
        }

        public void CalculateAllRoutes()
        {
            if (Warehouse == null || CurrentOrder == null || CurrentOrder.ItemCount == 0)
            {
                StatusMessage = LanguageResources.GetString("StatusCreateOrderFirst");
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

            foreach (var r in results)
            {
                r.IsBest = Math.Abs(r.TotalDistance - bestDist) < 0.01;
                AlgorithmResults.Add(r);
            }

            if (CurrentOrder != null && _currentConfigId > 0)
            {
                try
                {
                    DatabaseManager.Instance.SaveOrder(CurrentOrder, _currentConfigId, results);
                }
                catch { }
            }

            if (AlgorithmResults.Count > 0)
                SelectedResult = AlgorithmResults[0];

            StatusMessage = LanguageResources.Format("StatusAlgorithmsComputed", results.Count, bestDist);
        }

        // ===== INotifyPropertyChanged =====
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
