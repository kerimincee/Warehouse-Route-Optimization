using System;
using System.Collections.Generic;
using System.Linq;
using WarehouseSimulator.Models;
using WarehouseSimulator.Helpers;

namespace WarehouseSimulator.Algorithms
{
    /// <summary>
    /// Tüm rota algoritmalarının türetileceği soyut temel sınıf.
    /// Template Method tasarım deseni uygulanmıştır.
    /// </summary>
    public abstract class RoutingAlgorithm
    {
        /// <summary>Algoritmanın adı (görüntülemek için)</summary>
        public abstract string Name { get; }

        /// <summary>Algoritmanın kısa açıklaması</summary>
        public abstract string Description { get; }

        /// <summary>
        /// Ana rota hesaplama metodu.
        /// Süre ölçümü ve sonuç paketleme bu metotta yapılır.
        /// </summary>
        /// <param name="warehouse">Depo yapısı</param>
        /// <param name="selectedShelves">Seçili raf konumları</param>
        /// <returns>Hesaplanan rota sonucu</returns>
        public RouteResult Calculate(Warehouse warehouse, List<ShelfLocation> selectedShelves)
        {
            if (selectedShelves == null || selectedShelves.Count == 0)
                return new RouteResult
                {
                    AlgorithmName = Name,
                    Description = Description,
                    TotalDistance = 0,
                    RoutePoints = new List<WarehousePoint>()
                };

            var startTime = DateTime.Now;

            // Alt sınıf algoritmasını çalıştır
            var result = ComputeRoute(warehouse, selectedShelves);

            result.AlgorithmName = Name;
            result.Description = Description;
            result.ComputationTimeMs = (DateTime.Now - startTime).TotalMilliseconds;

            // Toplam mesafeyi hesapla (rota noktaları üzerinden)
            if (result.TotalDistance == 0 && result.RoutePoints.Count > 1)
            {
                result.TotalDistance = CalculateRouteTotalDistance(result.RoutePoints);
            }

            return result;
        }

        /// <summary>
        /// Alt sınıflar bu metodu implement eder.
        /// </summary>
        protected abstract RouteResult ComputeRoute(Warehouse warehouse, List<ShelfLocation> selectedShelves);

        /// <summary>
        /// Rota noktaları arasındaki toplam Manhattan mesafesini hesaplar.
        /// Depo içi hareket Manhattan mesafesi ile modellenir.
        /// </summary>
        protected double CalculateRouteTotalDistance(List<WarehousePoint> points)
        {
            double total = 0;
            for (int i = 0; i < points.Count - 1; i++)
                total += points[i].ManhattanDistanceTo(points[i + 1]);
            return Math.Round(total, 2);
        }

        /// <summary>
        /// Bir koridordaki seçili rafların Y aralığını hesaplar.
        /// Largest Gap algoritması için kullanılır.
        /// </summary>
        protected (double minY, double maxY) GetAislePickRange(
            List<ShelfLocation> shelvesInAisle, Warehouse warehouse)
        {
            if (!shelvesInAisle.Any())
                return (0, 0);

            var points = shelvesInAisle.Select(s => WarehouseBuilder.GetShelfPickPoint(s, warehouse));
            return (points.Min(p => p.Y), points.Max(p => p.Y));
        }

        /// <summary>
        /// Kapı noktasını döndürür
        /// </summary>
        protected WarehousePoint GetDoor(Warehouse warehouse)
            => WarehouseBuilder.GetDoorPoint(warehouse);

        /// <summary>
        /// Koridorun alt geçiş (cross-aisle başlangıç) noktasını döndürür
        /// </summary>
        protected WarehousePoint GetAisleBottomPoint(int blockIndex, int aisleIndex, Warehouse warehouse)
        {
            double aisleGroupSize = warehouse.ShelfWidth * 2 + 1.0;
            double blockOffset = blockIndex * (warehouse.AislesPerBlock * aisleGroupSize + warehouse.CrossAisleDistance);
            double x = blockOffset + aisleIndex * aisleGroupSize + warehouse.ShelfWidth;
            return new WarehousePoint(x, 0, $"Alt-B{blockIndex + 1}-A{aisleIndex + 1}");
        }

        /// <summary>
        /// Koridorun üst geçiş (cross-aisle son) noktasını döndürür
        /// </summary>
        protected WarehousePoint GetAisleTopPoint(int blockIndex, int aisleIndex, Warehouse warehouse)
        {
            double aisleGroupSize = warehouse.ShelfWidth * 2 + 1.0;
            double blockOffset = blockIndex * (warehouse.AislesPerBlock * aisleGroupSize + warehouse.CrossAisleDistance);
            double x = blockOffset + aisleIndex * aisleGroupSize + warehouse.ShelfWidth;
            return new WarehousePoint(x, warehouse.AisleLength, $"Üst-B{blockIndex + 1}-A{aisleIndex + 1}");
        }
    }
}
