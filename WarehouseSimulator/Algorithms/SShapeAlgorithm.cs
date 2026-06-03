using System.Collections.Generic;
using System.Linq;
using WarehouseSimulator.Models;
using WarehouseSimulator.Helpers;
using WarehouseSimulator.Resources;

namespace WarehouseSimulator.Algorithms
{
    public class SShapeAlgorithm : RoutingAlgorithm
    {
        public override string Name => LanguageResources.GetString("SShapeName");
        public override string Description => LanguageResources.GetString("SShapeDesc");

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

            bool goingUp = true;

            foreach (var aisleGroup in byAisle)
            {
                int blockIdx = aisleGroup.Key.BlockIndex;
                int aisleIdx = aisleGroup.Key.AisleIndex;

                var bottomPt = GetAisleBottomPoint(blockIdx, aisleIdx, warehouse);
                var topPt = GetAisleTopPoint(blockIdx, aisleIdx, warehouse);

                var shelvesInAisle = aisleGroup
                    .OrderBy(s => s.ShelfRow)
                    .ToList();

                if (goingUp)
                {
                    routePoints.Add(bottomPt);
                    foreach (var shelf in shelvesInAisle)
                    {
                        var pickPt = WarehouseBuilder.GetShelfPickPoint(shelf, warehouse);
                        routePoints.Add(pickPt);
                        pickOrder.Add(new OrderItem
                        {
                            Location = shelf,
                            ProductName = LanguageResources.Format("ProductNameAt", shelf.Label),
                            PickSequence = pickOrder.Count + 1
                        });
                    }
                    routePoints.Add(topPt);
                }
                else
                {
                    routePoints.Add(topPt);
                    foreach (var shelf in shelvesInAisle.AsEnumerable().Reverse())
                    {
                        var pickPt = WarehouseBuilder.GetShelfPickPoint(shelf, warehouse);
                        routePoints.Add(pickPt);
                        pickOrder.Add(new OrderItem
                        {
                            Location = shelf,
                            ProductName = LanguageResources.Format("ProductNameAt", shelf.Label),
                            PickSequence = pickOrder.Count + 1
                        });
                    }
                    routePoints.Add(bottomPt);
                }

                goingUp = !goingUp;
            }

            AddWarehousePath(routePoints, routePoints[^1], door, warehouse);

            return new RouteResult
            {
                RoutePoints = routePoints,
                PickOrder = pickOrder,
                TotalDistance = CalculateRouteTotalDistance(routePoints)
            };
        }
    }
}
