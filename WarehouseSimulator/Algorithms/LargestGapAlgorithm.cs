using System;
using System.Collections.Generic;
using System.Linq;
using WarehouseSimulator.Models;
using WarehouseSimulator.Helpers;

namespace WarehouseSimulator.Algorithms
{
    /// <summary>
    /// Largest Gap (En Büyük Boşluk) Rota Algoritması
    /// ================================================
    /// Mantık:
    ///   Her koridor için ürünler arasındaki boşluklar hesaplanır.
    ///   En büyük boşluğun altında veya üstünden geri dönme kararı verilir.
    ///   Bu yöntem, koridorun tamamını geçmek yerine daha kısa yol seçerek
    ///   gereksiz yürüyüşü azaltır.
    ///
    ///   Çalışma adımları:
    ///   1. Her koridorda seçili rafları Y'ye göre sırala.
    ///   2. Ürünler arasındaki boşlukları hesapla (kapı→ilk ürün ve son ürün→üst dahil).
    ///   3. En büyük boşluğu bul.
    ///   4. Eğer en büyük boşluk koridorun üstündeyse → sadece üste çık ve geri dön.
    ///      Eğer en büyük boşluk altındaysa → altta kal ve geri dön.
    ///      Aksi halde koridoru ikiye böl: alt bölümü alttan, üst bölümü üstten al.
    ///
    /// Kaynak: Roodbergen, K.J. & De Koster, R. (2001). Routing methods for warehouses
    ///         with multiple cross aisles. IJPR, 39(9), 1865-1883.
    /// </summary>
    public class LargestGapAlgorithm : RoutingAlgorithm
    {
        public override string Name => "Largest Gap (En Büyük Boşluk)";
        public override string Description =>
            "Koridor içindeki en büyük boşluğu bularak geri dönme noktasını " +
            "optimize eder; tüm koridoru geçmek yerine kısa yolu seçer.";

        protected override RouteResult ComputeRoute(Warehouse warehouse, List<ShelfLocation> selectedShelves)
        {
            var routePoints = new List<WarehousePoint>();
            var pickOrder = new List<OrderItem>();

            var door = GetDoor(warehouse);
            routePoints.Add(door);

            // Seçili rafları blok ve koridor bazında grupla
            var byAisle = selectedShelves
                .GroupBy(s => new { s.BlockIndex, s.AisleIndex })
                .OrderBy(g => g.Key.BlockIndex)
                .ThenBy(g => g.Key.AisleIndex)
                .ToList();

            // Her iki gidişte mevcut Y konumu takip edilir
            double currentY = door.Y; // Kapı Y konumu (negatif)

            foreach (var aisleGroup in byAisle)
            {
                int blockIdx = aisleGroup.Key.BlockIndex;
                int aisleIdx = aisleGroup.Key.AisleIndex;

                var bottomPt = GetAisleBottomPoint(blockIdx, aisleIdx, warehouse);
                var topPt = GetAisleTopPoint(blockIdx, aisleIdx, warehouse);

                // Koridordaki ürünleri Y'ye göre sırala
                var shelvesOrdered = aisleGroup
                    .OrderBy(s => s.ShelfRow)
                    .ToList();

                // Ürün noktalarını hesapla
                var pickPoints = shelvesOrdered
                    .Select(s => WarehouseBuilder.GetShelfPickPoint(s, warehouse))
                    .ToList();

                // Boşlukları hesapla
                // Boşluklar: [kapı→ilk_ürün, ürünler_arası, son_ürün→üst_geçiş]
                var gaps = ComputeGaps(pickPoints, bottomPt.Y, topPt.Y, warehouse.AisleLength);

                // En büyük boşluğu bul
                int largestGapIndex = FindLargestGapIndex(gaps);

                // Stratejiye göre rota oluştur
                if (largestGapIndex == gaps.Count - 1)
                {
                    // En büyük boşluk koridorun üstünde → sadece ürünlere kadar git, geri dön
                    routePoints.Add(bottomPt);
                    foreach (var (shelf, pt) in shelvesOrdered.Zip(pickPoints))
                    {
                        routePoints.Add(pt);
                        AddPickItem(pickOrder, shelf);
                    }
                    // Son üründen geri alt geçişe dön
                    routePoints.Add(bottomPt);
                }
                else if (largestGapIndex == 0)
                {
                    // En büyük boşluk koridorun altında → üstten gir, ürünlere kadar in, geri dön
                    routePoints.Add(topPt);
                    foreach (var (shelf, pt) in shelvesOrdered.AsEnumerable().Reverse()
                        .Zip(pickPoints.AsEnumerable().Reverse()))
                    {
                        routePoints.Add(pt);
                        AddPickItem(pickOrder, shelf);
                    }
                    routePoints.Add(topPt);
                }
                else
                {
                    // Boşluk ortada → koridoru ikiye böl
                    // Alt kısım: bottomPt → largestGapIndex'e kadar olan ürünler
                    routePoints.Add(bottomPt);
                    for (int i = 0; i < largestGapIndex; i++)
                    {
                        routePoints.Add(pickPoints[i]);
                        AddPickItem(pickOrder, shelvesOrdered[i]);
                    }
                    // Geri alt geçişe dön
                    routePoints.Add(bottomPt);

                    // Üst kısım: topPt → largestGapIndex'ten sonraki ürünler
                    routePoints.Add(topPt);
                    for (int i = pickPoints.Count - 1; i >= largestGapIndex; i--)
                    {
                        routePoints.Add(pickPoints[i]);
                        AddPickItem(pickOrder, shelvesOrdered[i]);
                    }
                    routePoints.Add(topPt);
                }
            }

            routePoints.Add(door);

            return new RouteResult
            {
                RoutePoints = routePoints,
                PickOrder = pickOrder,
                TotalDistance = CalculateRouteTotalDistance(routePoints)
            };
        }

        /// <summary>
        /// Koridordaki boşlukları hesaplar.
        /// [0]: Alt geçiş → ilk ürün arası boşluk
        /// [i]: i. ürün → (i+1). ürün arası boşluk
        /// [last]: Son ürün → üst geçiş arası boşluk
        /// </summary>
        private List<double> ComputeGaps(List<WarehousePoint> pickPoints, double bottomY, double topY, double aisleLength)
        {
            var gaps = new List<double>();

            if (pickPoints.Count == 0) return gaps;

            // İlk boşluk: alt geçişten ilk ürüne
            gaps.Add(pickPoints[0].Y - bottomY);

            // Ürünler arası boşluklar
            for (int i = 0; i < pickPoints.Count - 1; i++)
                gaps.Add(pickPoints[i + 1].Y - pickPoints[i].Y);

            // Son boşluk: son üründen üst geçişe
            gaps.Add(topY - pickPoints[^1].Y);

            return gaps;
        }

        /// <summary>
        /// En büyük boşluğun indeksini döndürür
        /// </summary>
        private int FindLargestGapIndex(List<double> gaps)
        {
            if (gaps.Count == 0) return 0;
            int maxIndex = 0;
            double maxVal = gaps[0];
            for (int i = 1; i < gaps.Count; i++)
            {
                if (gaps[i] > maxVal)
                {
                    maxVal = gaps[i];
                    maxIndex = i;
                }
            }
            return maxIndex;
        }

        /// <summary>
        /// Sipariş listesine yeni bir toplama öğesi ekler
        /// </summary>
        private void AddPickItem(List<OrderItem> pickOrder, ShelfLocation shelf)
        {
            pickOrder.Add(new OrderItem
            {
                Location = shelf,
                ProductName = $"Ürün@{shelf.Label}",
                PickSequence = pickOrder.Count + 1
            });
        }
    }
}
