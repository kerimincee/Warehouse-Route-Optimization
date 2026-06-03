using System;
using System.Collections.Generic;

namespace WarehouseSimulator.Models
{
    /// <summary>
    /// Bir sipariş öğesini temsil eder.
    /// Her sipariş öğesi bir raf lokasyonuna karşılık gelir.
    /// </summary>
    public class OrderItem
    {
        /// <summary>Ürün adı/kodu</summary>
        public string ProductName { get; set; } = string.Empty;

        /// <summary>Ürünün bulunduğu raf konumu</summary>
        public ShelfLocation Location { get; set; } = null!;

        /// <summary>Toplama sırası (rota hesaplandıktan sonra)</summary>
        public int PickSequence { get; set; }

        public override string ToString() =>
            $"{ProductName} @ {Location.Label}";
    }

    /// <summary>
    /// Bir siparişi temsil eder.
    /// Sipariş, birden fazla ürün içerebilir.
    /// </summary>
    public class Order
    {
        /// <summary>Sipariş numarası</summary>
        public int OrderId { get; set; }

        /// <summary>Oluşturulma tarihi</summary>
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        /// <summary>Siparişteki ürünler</summary>
        public List<OrderItem> Items { get; set; } = new();

        /// <summary>Sipariş içindeki ürün sayısı</summary>
        public int ItemCount => Items.Count;

        /// <summary>
        /// Tüm sipariş öğelerinin raf lokasyonlarını döndürür
        /// </summary>
        public List<ShelfLocation> GetLocations()
        {
            var locations = new List<ShelfLocation>();
            foreach (var item in Items)
                locations.Add(item.Location);
            return locations;
        }
    }

    /// <summary>
    /// Toplayıcıyı (picker) temsil eder.
    /// Toplayıcı, depoda gezinerek ürünleri toplar.
    /// </summary>
    public class Picker
    {
        /// <summary>Toplayıcının adı</summary>
        public string Name { get; set; } = "";

        /// <summary>Başlangıç konumu (kapı)</summary>
        public WarehousePoint StartPosition { get; set; } = new();

        /// <summary>Şu anki konum</summary>
        public WarehousePoint CurrentPosition { get; set; } = new();

        /// <summary>Toplam kat edilen mesafe</summary>
        public double TotalDistance { get; set; }

        /// <summary>Toplanan ürün sayısı</summary>
        public int CollectedItems { get; set; }
    }

    /// <summary>
    /// Depo içindeki bir noktayı koordinat olarak temsil eder.
    /// Mesafe hesaplamaları için kullanılır.
    /// </summary>
    public class WarehousePoint
    {
        /// <summary>X koordinatı (birim cinsinden)</summary>
        public double X { get; set; }

        /// <summary>Y koordinatı (birim cinsinden)</summary>
        public double Y { get; set; }

        /// <summary>Noktanın etiketi (tanımlama için)</summary>
        public string Label { get; set; } = string.Empty;

        public WarehousePoint() { }

        public WarehousePoint(double x, double y, string label = "")
        {
            X = x;
            Y = y;
            Label = label;
        }

        /// <summary>
        /// Bu nokta ile başka bir nokta arasındaki Öklid mesafesini hesaplar
        /// </summary>
        public double DistanceTo(WarehousePoint other)
        {
            double dx = X - other.X;
            double dy = Y - other.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>
        /// Manhattan mesafesi (depo içi hareket için daha gerçekçi)
        /// </summary>
        public double ManhattanDistanceTo(WarehousePoint other)
        {
            return Math.Abs(X - other.X) + Math.Abs(Y - other.Y);
        }

        public override string ToString() => $"({X:F1}, {Y:F1})";
    }

    /// <summary>
    /// Rota hesaplaması sonucunu temsil eder.
    /// </summary>
    public class RouteResult
    {
        /// <summary>Algoritma adı</summary>
        public string AlgorithmName { get; set; } = string.Empty;

        /// <summary>Sıralı rota noktaları</summary>
        public List<WarehousePoint> RoutePoints { get; set; } = new();

        /// <summary>Sıralı sipariş öğeleri</summary>
        public List<OrderItem> PickOrder { get; set; } = new();

        /// <summary>Toplam mesafe (birim cinsinden)</summary>
        public double TotalDistance { get; set; }

        /// <summary>Hesaplama süresi (milisaniye)</summary>
        public double ComputationTimeMs { get; set; }

        /// <summary>Bu rota en iyisi mi?</summary>
        public bool IsBest { get; set; }

        /// <summary>Algoritma açıklaması</summary>
        public string Description { get; set; } = string.Empty;
    }
}
