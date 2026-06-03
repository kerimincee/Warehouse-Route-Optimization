using System;
using System.Collections.Generic;
using System.Linq;
using WarehouseSimulator.Models;
using WarehouseSimulator.Helpers;
using WarehouseSimulator.Resources;

namespace WarehouseSimulator.Algorithms
{
    public class LargestGapAlgorithm : RoutingAlgorithm
    {
        public override string Name => LanguageResources.GetString("LargestGapName");
        public override string Description => LanguageResources.GetString("LargestGapDesc");

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

            double currentY = door.Y;

            foreach (var aisleGroup in byAisle)
            {
                int blockIdx = aisleGroup.Key.BlockIndex;
                int aisleIdx = aisleGroup.Key.AisleIndex;

                var bottomPt = GetAisleBottomPoint(blockIdx, aisleIdx, warehouse);
                var topPt = GetAisleTopPoint(blockIdx, aisleIdx, warehouse);

                var shelvesOrdered = aisleGroup
                    .OrderBy(s => s.ShelfRow)
                    .ToList();

                var pickPoints = shelvesOrdered
                    .Select(s => WarehouseBuilder.GetShelfPickPoint(s, warehouse))
                    .ToList();

                var gaps = ComputeGaps(pickPoints, bottomPt.Y, topPt.Y, warehouse.AisleLength);

                int largestGapIndex = FindLargestGapIndex(gaps);

                if (largestGapIndex == gaps.Count - 1)
                {
                    routePoints.Add(bottomPt);
                    foreach (var (shelf, pt) in shelvesOrdered.Zip(pickPoints))
                    {
                        routePoints.Add(pt);
                        AddPickItem(pickOrder, shelf);
                    }
                    routePoints.Add(bottomPt);
                }
                else if (largestGapIndex == 0)
                {
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
                    routePoints.Add(bottomPt);
                    for (int i = 0; i < largestGapIndex; i++)
                    {
                        routePoints.Add(pickPoints[i]);
                        AddPickItem(pickOrder, shelvesOrdered[i]);
                    }
                    routePoints.Add(bottomPt);

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

        private List<double> ComputeGaps(List<WarehousePoint> pickPoints, double bottomY, double topY, double aisleLength)
        {
            var gaps = new List<double>();

            if (pickPoints.Count == 0) return gaps;

            gaps.Add(pickPoints[0].Y - bottomY);

            for (int i = 0; i < pickPoints.Count - 1; i++)
                gaps.Add(pickPoints[i + 1].Y - pickPoints[i].Y);

            gaps.Add(topY - pickPoints[^1].Y);

            return gaps;
        }

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

        private void AddPickItem(List<OrderItem> pickOrder, ShelfLocation shelf)
        {
            pickOrder.Add(new OrderItem
            {
                Location = shelf,
                ProductName = LanguageResources.Format("ProductNameAt", shelf.Label),
                PickSequence = pickOrder.Count + 1
            });
        }
    }
}
