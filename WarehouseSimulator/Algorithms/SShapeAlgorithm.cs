using System.Collections.Generic;
using System.Linq;
using WarehouseSimulator.Models;
using WarehouseSimulator.Helpers;

namespace WarehouseSimulator.Algorithms
{
    /// <summary>
    /// S-Shape (Serpentin) Rota Algoritması
    /// =====================================
    /// Mantık:
    ///   Toplayıcı koridorları sırayla dolaşır. Bir koridora girdiğinde
    ///   o koridoru tamamen (başından sonuna) geçer ve bir sonraki koridora
    ///   karşı uçtan girer. Bu yöntem özellikle ürünlerin koridorlara
    ///   eşit dağıldığı durumlarda etkilidir.
    ///
    ///   Çalışma adımları:
    ///   1. Seçili ürün içeren koridorları sol→sağ sıralı listele.
    ///   2. Tek numaralı koridorları alt→üst, çift numaralıları üst→alt dolaş.
    ///   3. Her koridor sonunda arka geçiş koridoruna geç.
    ///   4. Son koridordan kapıya dön.
    ///
    /// Kaynak: Roodbergen, K.J. (2001) Layout and Routing Methods for Warehouses.
    /// </summary>
    public class SShapeAlgorithm : RoutingAlgorithm
    {
        public override string Name => "S-Shape (Serpentin)";
        public override string Description =>
            "Toplayıcı her koridoru başından sonuna kadar tam geçer, " +
            "koridorlar arasında dönüşümlü yön değiştirir.";

        protected override RouteResult ComputeRoute(Warehouse warehouse, List<ShelfLocation> selectedShelves)
        {
            var routePoints = new List<WarehousePoint>();
            var pickOrder = new List<OrderItem>();

            // Kapı başlangıç noktası
            var door = GetDoor(warehouse);
            routePoints.Add(door);

            // Seçili rafları blok ve koridor bazında grupla
            var byAisle = selectedShelves
                .GroupBy(s => new { s.BlockIndex, s.AisleIndex })
                .OrderBy(g => g.Key.BlockIndex)
                .ThenBy(g => g.Key.AisleIndex)
                .ToList();

            bool goingUp = true; // İlk koridor alt→üst yönünde

            foreach (var aisleGroup in byAisle)
            {
                int blockIdx = aisleGroup.Key.BlockIndex;
                int aisleIdx = aisleGroup.Key.AisleIndex;

                // Bu koridorun alt ve üst noktaları
                var bottomPt = GetAisleBottomPoint(blockIdx, aisleIdx, warehouse);
                var topPt = GetAisleTopPoint(blockIdx, aisleIdx, warehouse);

                // Koridordaki ürünleri Y'ye göre sırala
                var shelvesInAisle = aisleGroup
                    .OrderBy(s => s.ShelfRow)
                    .ToList();

                if (goingUp)
                {
                    // Alt→Üst geçiş: koridora alttan gir
                    routePoints.Add(bottomPt);
                    foreach (var shelf in shelvesInAisle)
                    {
                        var pickPt = WarehouseBuilder.GetShelfPickPoint(shelf, warehouse);
                        routePoints.Add(pickPt);
                        pickOrder.Add(new OrderItem
                        {
                            Location = shelf,
                            ProductName = $"Ürün@{shelf.Label}",
                            PickSequence = pickOrder.Count + 1
                        });
                    }
                    routePoints.Add(topPt); // Koridoru tamamen geç
                }
                else
                {
                    // Üst→Alt geçiş: koridora üstten gir
                    routePoints.Add(topPt);
                    foreach (var shelf in shelvesInAisle.AsEnumerable().Reverse())
                    {
                        var pickPt = WarehouseBuilder.GetShelfPickPoint(shelf, warehouse);
                        routePoints.Add(pickPt);
                        pickOrder.Add(new OrderItem
                        {
                            Location = shelf,
                            ProductName = $"Ürün@{shelf.Label}",
                            PickSequence = pickOrder.Count + 1
                        });
                    }
                    routePoints.Add(bottomPt);
                }

                goingUp = !goingUp; // Yönü değiştir
            }

            // Kapıya geri dön
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
