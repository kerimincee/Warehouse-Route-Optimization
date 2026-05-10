using System;
using System.Collections.Generic;

namespace WarehouseSimulator.Models
{
    /// <summary>
    /// Depodaki bir raf konumunu temsil eder.
    /// Her raf, blok, koridor ve sıra numarasıyla tanımlanır.
    /// </summary>
    public class ShelfLocation
    {
        /// <summary>Blok numarası (0 tabanlı)</summary>
        public int BlockIndex { get; set; }

        /// <summary>Koridor numarası (0 tabanlı)</summary>
        public int AisleIndex { get; set; }

        /// <summary>Raf sırası numarası (0 tabanlı)</summary>
        public int ShelfRow { get; set; }

        /// <summary>Sol taraf mı sağ taraf mı (0=sol, 1=sağ)</summary>
        public int Side { get; set; }

        /// <summary>
        /// X koordinatı (piksel cinsinden, görselleştirme için)
        /// </summary>
        public double X { get; set; }

        /// <summary>
        /// Y koordinatı (piksel cinsinden, görselleştirme için)
        /// </summary>
        public double Y { get; set; }

        /// <summary>Raf seçili mi? (sipariş içeriyor mu)</summary>
        public bool IsSelected { get; set; }

        /// <summary>Raf kaç kez sipariş edildi (Isı haritası için)</summary>
        public int PickCount { get; set; }

        /// <summary>Raf etiketi (gösterim için)</summary>
        public string Label => $"B{BlockIndex + 1}-A{AisleIndex + 1}-R{ShelfRow + 1}-{(Side == 0 ? "L" : "R")}";

        /// <summary>
        /// Benzersiz kimlik oluşturur
        /// </summary>
        public string UniqueId => $"{BlockIndex}_{AisleIndex}_{ShelfRow}_{Side}";

        public override string ToString() => Label;
    }

    /// <summary>
    /// Depodaki bir koridoru temsil eder.
    /// Koridor, iki raf sırasından oluşur (sol ve sağ).
    /// </summary>
    public class Aisle
    {
        /// <summary>Koridorun ait olduğu blok indeksi</summary>
        public int BlockIndex { get; set; }

        /// <summary>Koridor indeksi (blok içinde)</summary>
        public int AisleIndex { get; set; }

        /// <summary>Koridordaki toplam raf sayısı (her iki taraf için)</summary>
        public int ShelfCount { get; set; }

        /// <summary>Bu koridordaki tüm raf konumları</summary>
        public List<ShelfLocation> Shelves { get; set; } = new();

        /// <summary>
        /// Koridorda seçili raf var mı?
        /// </summary>
        public bool HasSelectedShelves => Shelves.Exists(s => s.IsSelected);

        /// <summary>
        /// Seçili rafları döndürür
        /// </summary>
        public List<ShelfLocation> GetSelectedShelves() =>
            Shelves.FindAll(s => s.IsSelected);
    }

    /// <summary>
    /// Depodaki bir bloğu temsil eder.
    /// Bir blok, birden fazla koridordan oluşur.
    /// </summary>
    public class Block
    {
        /// <summary>Blok indeksi</summary>
        public int BlockIndex { get; set; }

        /// <summary>Bloktaki koridor listesi</summary>
        public List<Aisle> Aisles { get; set; } = new();

        /// <summary>
        /// Blokta seçili raf var mı?
        /// </summary>
        public bool HasSelectedShelves => Aisles.Exists(a => a.HasSelectedShelves);
    }

    /// <summary>
    /// Tüm depo yapısını temsil eden ana model.
    /// Roodbergen Interactive Warehouse sistemi temel alınmıştır.
    /// </summary>
    public class Warehouse
    {
        /// <summary>Blok sayısı</summary>
        public int BlockCount { get; set; }

        /// <summary>Her bloktaki koridor sayısı</summary>
        public int AislesPerBlock { get; set; }

        /// <summary>Her koridordaki raf sayısı</summary>
        public int ShelvesPerAisle { get; set; }

        /// <summary>Kapı lokasyonu (0=Sol, 1=Sağ, 2=Orta)</summary>
        public DoorLocation DoorLocation { get; set; }

        /// <summary>Koridor uzunluğu (birim cinsinden)</summary>
        public double AisleLength { get; set; } = 10.0;

        /// <summary>Geçiş mesafesi (bloklar arası)</summary>
        public double CrossAisleDistance { get; set; } = 3.0;

        /// <summary>Raf genişliği (birim cinsinden)</summary>
        public double ShelfWidth { get; set; } = 1.0;

        /// <summary>Tüm bloklar</summary>
        public List<Block> Blocks { get; set; } = new();

        /// <summary>Toplam koridor sayısı</summary>
        public int TotalAisles => BlockCount * AislesPerBlock;

        /// <summary>Toplam raf sayısı</summary>
        public int TotalShelves => TotalAisles * ShelvesPerAisle * 2; // iki taraf

        /// <summary>
        /// Tüm seçili rafları döndürür
        /// </summary>
        public List<ShelfLocation> GetSelectedShelves()
        {
            var selected = new List<ShelfLocation>();
            foreach (var block in Blocks)
                foreach (var aisle in block.Aisles)
                    selected.AddRange(aisle.GetSelectedShelves());
            return selected;
        }

        /// <summary>
        /// Tüm rafları döndürür
        /// </summary>
        public List<ShelfLocation> GetAllShelves()
        {
            var all = new List<ShelfLocation>();
            foreach (var block in Blocks)
                foreach (var aisle in block.Aisles)
                    all.AddRange(aisle.Shelves);
            return all;
        }

        /// <summary>
        /// Tüm seçimleri temizler
        /// </summary>
        public void ClearAllSelections()
        {
            foreach (var shelf in GetAllShelves())
                shelf.IsSelected = false;
        }
    }

    /// <summary>
    /// Kapı konumu seçenekleri
    /// </summary>
    public enum DoorLocation
    {
        /// <summary>Deponun sol tarafında (ilk koridor başında)</summary>
        Left = 0,
        /// <summary>Deponun sağ tarafında (son koridor sonunda)</summary>
        Right = 1,
        /// <summary>Deponun ortasında</summary>
        Center = 2
    }
}
