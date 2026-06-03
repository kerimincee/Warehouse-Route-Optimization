using System;
using System.Collections.Generic;
using System.Linq;
using WarehouseSimulator.Models;
using WarehouseSimulator.Helpers;
using WarehouseSimulator.Resources;

namespace WarehouseSimulator.Algorithms
{
    public class OptimalNearestNeighborAlgorithm : RoutingAlgorithm
    {
        public override string Name => LanguageResources.GetString("OptimalNNName");
        public override string Description => LanguageResources.GetString("OptimalNNDesc");

        protected override RouteResult ComputeRoute(Warehouse warehouse, List<ShelfLocation> selectedShelves)
        {
            var routePoints = new List<WarehousePoint>();
            var pickOrder = new List<OrderItem>();

            var door = GetDoor(warehouse);
            routePoints.Add(door);

            var remaining = selectedShelves
                .Select(s => (shelf: s, point: WarehouseBuilder.GetShelfPickPoint(s, warehouse)))
                .ToList();

            var current = door;

            while (remaining.Count > 0)
            {
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

                AddWarehousePath(routePoints, current, nearest.point, warehouse);
                current = nearest.point;

                pickOrder.Add(new OrderItem
                {
                    Location = nearest.shelf,
                    ProductName = LanguageResources.Format("ProductNameAt", nearest.shelf.Label),
                    PickSequence = pickOrder.Count + 1
                });
            }

            AddWarehousePath(routePoints, current, door, warehouse);

            return new RouteResult
            {
                RoutePoints = routePoints,
                PickOrder = pickOrder,
                TotalDistance = CalculateRouteTotalDistance(routePoints)
            };
        }

    }

    public class TwoOptTSPAlgorithm : RoutingAlgorithm
    {
        public override string Name => LanguageResources.GetString("TwoOptName");
        public override string Description => LanguageResources.GetString("TwoOptDesc");

        protected override RouteResult ComputeRoute(Warehouse warehouse, List<ShelfLocation> selectedShelves)
        {
            if (selectedShelves.Count <= 2)
            {
                var nn = new OptimalNearestNeighborAlgorithm();
                return nn.Calculate(warehouse, selectedShelves);
            }

            var door = GetDoor(warehouse);

            var pickPoints = selectedShelves
                .Select(s => WarehouseBuilder.GetShelfPickPoint(s, warehouse))
                .ToList();

            var tour = BuildNNTour(door, pickPoints);

            tour = Apply2Opt(tour);

            var routePoints = new List<WarehousePoint> { door };
            var pickOrder = new List<OrderItem>();
            var current = door;

            for (int i = 1; i < tour.Count - 1; i++)
            {
                AddWarehousePath(routePoints, current, tour[i], warehouse);
                current = tour[i];

                var matchingShelf = selectedShelves
                    .OrderBy(s => WarehouseBuilder.GetShelfPickPoint(s, warehouse)
                        .ManhattanDistanceTo(tour[i]))
                    .First();

                pickOrder.Add(new OrderItem
                {
                    Location = matchingShelf,
                    ProductName = LanguageResources.Format("ProductNameAt", matchingShelf.Label),
                    PickSequence = i
                });
            }

            AddWarehousePath(routePoints, current, door, warehouse);

            return new RouteResult
            {
                RoutePoints = routePoints,
                PickOrder = pickOrder,
                TotalDistance = CalculateRouteTotalDistance(routePoints)
            };
        }

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
                        double d1 = tour[i - 1].ManhattanDistanceTo(tour[i]) +
                                    tour[j].ManhattanDistanceTo(tour[j + 1]);

                        double d2 = tour[i - 1].ManhattanDistanceTo(tour[j]) +
                                    tour[i].ManhattanDistanceTo(tour[j + 1]);

                        if (d2 < d1 - 1e-10)
                        {
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
