using System.Collections.Generic;
using System.Linq;
using WarehouseSimulator.Models;
using WarehouseSimulator.Helpers;

namespace WarehouseSimulator.Algorithms
{
    /// <summary>
    /// Aisle-by-Aisle (Koridor Bazlı) Rota Algoritması
    /// =================================================
    /// Mantık:
    ///   Her koridor için en kısa geri dönüş stratejisi bağımsız olarak seçilir.
    ///   Toplayıcı her koridora en verimli taraftan girer ve aynı taraftan çıkar
    ///   ya da karşı taraftan geçer. Karar, koridordaki ürünlerin konumuna göre
    ///   verilir (Return veya Traversal).
    ///
    ///   Çalışma adımları:
    ///   1. Her koridor için: Ürün varsa işlem yap.
    ///   2. Geçiş (traversal) maliyeti vs. geri dönüş (return) maliyeti karşılaştır.
    ///   3. Daha kısa olan stratejiyi seç.
    ///   4. Bir sonraki koridora geç.
    ///
    /// Bu algoritma Roodbergen'in "Return" ve "Traversal" stratejilerini
    /// her koridor için dinamik olarak seçer.
    /// </summary>
    public class AisleByAisleAlgorithm : RoutingAlgorithm
    {
        public override string Name => "Aisle-by-Aisle (Koridor Bazlı)";
        public override string Description =>
            "Her koridor için Return (geri dön) veya Traversal (geç) " +
            "stratejisini bağımsız olarak karşılaştırır ve en kısayı seçer.";

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

            // Mevcut konum: kapı (alt geçiş seviyesinde)
            double currentY = 0; // Alt geçiş Y=0

            foreach (var aisleGroup in byAisle)
            {
                int blockIdx = aisleGroup.Key.BlockIndex;
                int aisleIdx = aisleGroup.Key.AisleIndex;

                var bottomPt = GetAisleBottomPoint(blockIdx, aisleIdx, warehouse);
                var topPt = GetAisleTopPoint(blockIdx, aisleIdx, warehouse);

                var shelvesOrdered = aisleGroup.OrderBy(s => s.ShelfRow).ToList();
                var pickPoints = shelvesOrdered
                    .Select(s => WarehouseBuilder.GetShelfPickPoint(s, warehouse))
                    .ToList();

                if (pickPoints.Count == 0) continue;

                double firstY = pickPoints[0].Y;
                double lastY = pickPoints[^1].Y;
                double aisleLen = warehouse.AisleLength;

                // Return maliyeti: alt→ilk ürün→geri alt = 2 * firstY + (lastY - firstY) * 2 değil
                // Doğru Return: alt→son ürüne kadar git ve geri dön
                double returnCost = 2 * lastY;

                // Traversal maliyeti: alt→üst = aisleLen (tam geçiş)
                double traversalCost = aisleLen;

                if (returnCost <= traversalCost)
                {
                    // Return stratejisi: alttan gir, son ürüne kadar git, geri dön
                    routePoints.Add(bottomPt);
                    foreach (var (shelf, pt) in shelvesOrdered.Zip(pickPoints))
                    {
                        routePoints.Add(pt);
                        pickOrder.Add(new OrderItem
                        {
                            Location = shelf,
                            ProductName = $"Ürün@{shelf.Label}",
                            PickSequence = pickOrder.Count + 1
                        });
                    }
                    routePoints.Add(bottomPt); // Geri dön
                    currentY = 0;
                }
                else
                {
                    // Traversal stratejisi: alttan gir, tüm koridoru geç, üstten çık
                    routePoints.Add(bottomPt);
                    foreach (var (shelf, pt) in shelvesOrdered.Zip(pickPoints))
                    {
                        routePoints.Add(pt);
                        pickOrder.Add(new OrderItem
                        {
                            Location = shelf,
                            ProductName = $"Ürün@{shelf.Label}",
                            PickSequence = pickOrder.Count + 1
                        });
                    }
                    routePoints.Add(topPt); // Üstten çık
                    currentY = aisleLen;
                }
            }

            // Kapıya geri dön (alt geçiş üzerinden)
            if (routePoints.Count > 0 && routePoints[^1] != door)
            {
                // Eğer üst geçişteyse alt geçişe in
                var lastPt = routePoints[^1];
                if (lastPt.Y > 0)
                {
                    // Mevcut koridorun alt geçişine git
                    var bottomReturn = new WarehousePoint(lastPt.X, 0, "Alt-Geçiş");
                    routePoints.Add(bottomReturn);
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
    }
}
