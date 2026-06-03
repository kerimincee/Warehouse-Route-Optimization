using System.Collections.Generic;
using System.Linq;
using WarehouseSimulator.Models;
using WarehouseSimulator.Helpers;
using WarehouseSimulator.Resources;

namespace WarehouseSimulator.Algorithms
{
    public class AisleByAisleAlgorithm : RoutingAlgorithm
    {
        public override string Name => LanguageResources.GetString("AisleByName");
        public override string Description => LanguageResources.GetString("AisleByDesc");

        protected override RouteResult ComputeRoute(Warehouse warehouse, List<ShelfLocation> selectedShelves)
        {
            var routePoints = new List<WarehousePoint>();
            var pickOrder = new List<OrderItem>();

            var door = GetDoor(warehouse);
            routePoints.Add(door);

            var byAisle = selectedShelves
                .GroupBy(s => new { s.BlockIndex, s.AisleIndex })
                .OrderBy(g => g.Key.BlockIndex)
                .ThenBy(g => g.Key.AisleIndex)
                .ToList();

            double currentY = 0;

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

                double returnCost = 2 * lastY;
                double traversalCost = aisleLen;

                if (returnCost <= traversalCost)
                {
                    routePoints.Add(bottomPt);
                    foreach (var (shelf, pt) in shelvesOrdered.Zip(pickPoints))
                    {
                        routePoints.Add(pt);
                        pickOrder.Add(new OrderItem
                        {
                            Location = shelf,
                            ProductName = LanguageResources.Format("ProductNameAt", shelf.Label),
                            PickSequence = pickOrder.Count + 1
                        });
                    }
                    routePoints.Add(bottomPt);
                    currentY = 0;
                }
                else
                {
                    routePoints.Add(bottomPt);
                    foreach (var (shelf, pt) in shelvesOrdered.Zip(pickPoints))
                    {
                        routePoints.Add(pt);
                        pickOrder.Add(new OrderItem
                        {
                            Location = shelf,
                            ProductName = LanguageResources.Format("ProductNameAt", shelf.Label),
                            PickSequence = pickOrder.Count + 1
                        });
                    }
                    routePoints.Add(topPt);
                    currentY = aisleLen;
                }
            }

            if (routePoints.Count > 0 && routePoints[^1] != door)
            {
                var lastPt = routePoints[^1];
                if (lastPt.Y > 0)
                {
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
