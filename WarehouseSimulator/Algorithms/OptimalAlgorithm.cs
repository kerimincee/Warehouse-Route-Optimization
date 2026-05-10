using System;
using System.Collections.Generic;
using System.Linq;
using WarehouseSimulator.Models;
using WarehouseSimulator.Helpers;

namespace WarehouseSimulator.Algorithms
{
    /// <summary>
    /// Optimal (Nearest Neighbor TSP) Rota Algoritması
    /// =================================================
    /// Mantık:
    ///   Gezgin Satıcı Problemi'nin (TSP) en yakın komşu sezgisel
    ///   çözümü uygulanır. Toplayıcı her adımda en yakın henüz
    ///   ziyaret edilmemiş ürüne gider.
    ///
    ///   Bu, küçük-orta ölçekli siparişler için makul sonuçlar
    ///   üretir ancak global optimalin garantisi yoktur (NP-zor problem).
    ///
    ///   Çalışma adımları:
    ///   1. Kapıdan başla.
    ///   2. Henüz ziyaret edilmemiş en yakın ürünü bul (Manhattan mesafesi).
    ///   3. O ürüne git ve ziyaret edildi olarak işaretle.
    ///   4. Tüm ürünler ziyaret edilene kadar devam et.
    ///   5. Kapıya dön.
    ///
    /// Zaman karmaşıklığı: O(n²) — n ürün sayısı.
    /// </summary>
    public class OptimalNearestNeighborAlgorithm : RoutingAlgorithm
    {
        public override string Name => "Optimal - Nearest Neighbor (TSP)";
        public override string Description =>
            "En Yakın Komşu sezgiseliyle TSP çözümü; her adımda " +
            "Manhattan mesafesine göre en yakın ürünü hedefler.";

        protected override RouteResult ComputeRoute(Warehouse warehouse, List<ShelfLocation> selectedShelves)
        {
            var routePoints = new List<WarehousePoint>();
            var pickOrder = new List<OrderItem>();

            var door = GetDoor(warehouse);
            routePoints.Add(door);

            // Tüm seçili rafların pick noktalarını hazırla
            var remaining = selectedShelves
                .Select(s => (shelf: s, point: WarehouseBuilder.GetShelfPickPoint(s, warehouse)))
                .ToList();

            var current = door;

            // Ziyaret döngüsü
            while (remaining.Count > 0)
            {
                // Manhattan mesafesine göre en yakın noktayı bul
                int nearestIndex = 0;
                double nearestDist = current.ManhattanDistanceTo(remaining[0].point);

                for (int i = 1; i < remaining.Count; i++)
                {
                    double dist = current.ManhattanDistanceTo(remaining[i].point);
                    if (dist < nearestDist)
                    {
                        nearestDist = dist;
                        nearestIndex = i;
                    }
                }

                var nearest = remaining[nearestIndex];
                remaining.RemoveAt(nearestIndex);

                // Depo içi gerçekçi rota ekle (sadece Manhattan yönlü)
                AddWarehousePath(routePoints, current, nearest.point, warehouse);
                current = nearest.point;

                pickOrder.Add(new OrderItem
                {
                    Location = nearest.shelf,
                    ProductName = $"Ürün@{nearest.shelf.Label}",
                    PickSequence = pickOrder.Count + 1
                });
            }

            // Kapıya dön
            AddWarehousePath(routePoints, current, door, warehouse);

            return new RouteResult
            {
                RoutePoints = routePoints,
                PickOrder = pickOrder,
                TotalDistance = CalculateRouteTotalDistance(routePoints)
            };
        }

        /// <summary>
        /// Depo içinde iki nokta arasındaki gerçekçi yolu rota noktalarına ekler.
        /// Önce Y ekseni boyunca hareket, sonra X ekseni boyunca (veya tersi).
        /// Bu, koridorlar arası geçişi simüle eder.
        /// </summary>
        private void AddWarehousePath(List<WarehousePoint> route,
            WarehousePoint from, WarehousePoint to, Warehouse warehouse)
        {
            // Eğer aynı X koridorundaysak: direkt Y hareketi
            if (Math.Abs(from.X - to.X) < 0.01)
            {
                route.Add(to);
                return;
            }

            // Farklı koridor: önce alt geçişe in (Y=0), sonra X'i değiştir, sonra hedefe çık
            double crossY = from.Y > warehouse.AisleLength / 2
                ? warehouse.AisleLength   // Üst geçiş
                : 0;                       // Alt geçiş

            var crossFrom = new WarehousePoint(from.X, crossY, "Geçiş-Ara");
            var crossTo = new WarehousePoint(to.X, crossY, "Geçiş-Hedef");

            route.Add(crossFrom);
            route.Add(crossTo);
            route.Add(to);
        }
    }

    /// <summary>
    /// 2-Opt İyileştirmeli TSP Algoritması (Bonus Geliştirme)
    /// =======================================================
    /// Mantık:
    ///   Nearest Neighbor ile başlangıç rotası oluşturulur, ardından
    ///   2-opt yerel arama ile iyileştirilir. İki kenarın yer değiştirmesi
    ///   mesafeyi kısaltıyorsa değişiklik yapılır.
    ///
    ///   Zaman karmaşıklığı: O(n³) iterasyon başına, pratikte çok hızlı.
    /// </summary>
    public class TwoOptTSPAlgorithm : RoutingAlgorithm
    {
        public override string Name => "2-Opt TSP (Gelişmiş Optimal)";
        public override string Description =>
            "Nearest Neighbor başlangıcından 2-Opt yerel arama ile " +
            "iyileştirme yapan gelişmiş TSP çözümü.";

        protected override RouteResult ComputeRoute(Warehouse warehouse, List<ShelfLocation> selectedShelves)
        {
            if (selectedShelves.Count <= 2)
            {
                // Küçük sipariş için NN yeterli
                var nn = new OptimalNearestNeighborAlgorithm();
                return nn.Calculate(warehouse, selectedShelves);
            }

            var door = GetDoor(warehouse);

            // 1. Başlangıç rotası: Nearest Neighbor
            var pickPoints = selectedShelves
                .Select(s => WarehouseBuilder.GetShelfPickPoint(s, warehouse))
                .ToList();

            var tour = BuildNNTour(door, pickPoints);

            // 2. 2-Opt iyileştirme
            tour = Apply2Opt(tour);

            // 3. Rota noktalarını oluştur
            var routePoints = new List<WarehousePoint> { door };
            var pickOrder = new List<OrderItem>();

            for (int i = 1; i < tour.Count - 1; i++)
            {
                routePoints.Add(tour[i]);
                // Hangi rafa karşılık geldiğini bul
                var matchingShelf = selectedShelves
                    .OrderBy(s => WarehouseBuilder.GetShelfPickPoint(s, warehouse)
                        .ManhattanDistanceTo(tour[i]))
                    .First();

                pickOrder.Add(new OrderItem
                {
                    Location = matchingShelf,
                    ProductName = $"Ürün@{matchingShelf.Label}",
                    PickSequence = i
                });
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
        /// Nearest Neighbor tur oluşturur: kapı → ürünler → kapı
        /// </summary>
        private List<WarehousePoint> BuildNNTour(WarehousePoint door, List<WarehousePoint> points)
        {
            var tour = new List<WarehousePoint> { door };
            var remaining = new List<WarehousePoint>(points);
            var current = door;

            while (remaining.Count > 0)
            {
                var nearest = remaining.OrderBy(p => current.ManhattanDistanceTo(p)).First();
                remaining.Remove(nearest);
                tour.Add(nearest);
                current = nearest;
            }

            tour.Add(door);
            return tour;
        }

        /// <summary>
        /// 2-Opt yerel arama: tur üzerinde iki kenarı yer değiştirerek kısaltma dener.
        /// Gelişme olmayıncaya kadar iterasyon yapar (en fazla 1000 iterasyon).
        /// </summary>
        private List<WarehousePoint> Apply2Opt(List<WarehousePoint> tour)
        {
            bool improved = true;
            int iteration = 0;
            const int MAX_ITER = 1000;

            while (improved && iteration < MAX_ITER)
            {
                improved = false;
                iteration++;

                for (int i = 1; i < tour.Count - 2; i++)
                {
                    for (int j = i + 1; j < tour.Count - 1; j++)
                    {
                        // Mevcut mesafe: (i-1→i) + (j→j+1)
                        double d1 = tour[i - 1].ManhattanDistanceTo(tour[i]) +
                                    tour[j].ManhattanDistanceTo(tour[j + 1]);

                        // Yeni mesafe: (i-1→j) + (i→j+1) — kenarları değiştir
                        double d2 = tour[i - 1].ManhattanDistanceTo(tour[j]) +
                                    tour[i].ManhattanDistanceTo(tour[j + 1]);

                        if (d2 < d1 - 1e-10)
                        {
                            // Segmenti ters çevir
                            tour.Reverse(i, j - i + 1);
                            improved = true;
                        }
                    }
                }
            }

            return tour;
        }
    }
}
