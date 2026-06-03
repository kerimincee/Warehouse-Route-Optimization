using System;
using System.Collections.Generic;
using System.Linq;
using WarehouseSimulator.Models;
using WarehouseSimulator.Resources;

namespace WarehouseSimulator.Helpers
{
    /// <summary>
    /// Depo yapısını oluşturan ve koordinatları hesaplayan yardımcı sınıf.
    /// Grid koordinat sistemi kullanılır.
    /// </summary>
    public static class WarehouseBuilder
    {
        // ---- Görselleştirme sabitleri ----
        /// <summary>Her raf kutucuğunun piksel genişliği</summary>
        public const double SHELF_PIXEL_WIDTH = 40;

        /// <summary>Her raf kutucuğunun piksel yüksekliği</summary>
        public const double SHELF_PIXEL_HEIGHT = 22;

        /// <summary>Koridor genişliği (piksel)</summary>
        public const double AISLE_PIXEL_WIDTH = 36;

        /// <summary>Bloklar arası geçiş genişliği (piksel)</summary>
        public const double CROSS_AISLE_PIXEL_HEIGHT = 30;

        /// <summary>Kenar boşlukları</summary>
        public const double MARGIN = 50;

        /// <summary>
        /// Verilen parametrelere göre depo yapısını oluşturur.
        /// Koordinatlar hem gerçek birim hem piksel olarak hesaplanır.
        /// </summary>
        /// <param name="blockCount">Blok sayısı</param>
        /// <param name="aislesPerBlock">Her bloktaki koridor sayısı</param>
        /// <param name="shelvesPerAisle">Her koridordaki raf sayısı</param>
        /// <param name="doorLocation">Kapı lokasyonu</param>
        /// <param name="aisleLength">Koridor uzunluğu (birim)</param>
        /// <param name="crossAisleDistance">Geçiş mesafesi (birim)</param>
        /// <returns>Tam yapılandırılmış depo nesnesi</returns>
        public static Warehouse Build(
            int blockCount,
            int aislesPerBlock,
            int shelvesPerAisle,
            DoorLocation doorLocation = DoorLocation.Left,
            double aisleLength = 10.0,
            double crossAisleDistance = 3.0)
        {
            // Parametre doğrulama
            if (blockCount < 1) throw new ArgumentException(LanguageResources.GetString("BlockCountMin"));
            if (aislesPerBlock < 1) throw new ArgumentException(LanguageResources.GetString("AisleCountMin"));
            if (shelvesPerAisle < 1) throw new ArgumentException(LanguageResources.GetString("ShelfCountMin"));

            var warehouse = new Warehouse
            {
                BlockCount = blockCount,
                AislesPerBlock = aislesPerBlock,
                ShelvesPerAisle = shelvesPerAisle,
                DoorLocation = doorLocation,
                AisleLength = aisleLength,
                CrossAisleDistance = crossAisleDistance
            };

            // Her blok için
            for (int b = 0; b < blockCount; b++)
            {
                var block = new Block { BlockIndex = b };

                // Her koridor için
                for (int a = 0; a < aislesPerBlock; a++)
                {
                    var aisle = new Aisle
                    {
                        BlockIndex = b,
                        AisleIndex = a,
                        ShelfCount = shelvesPerAisle
                    };

                    // Her raf sırası için (her iki taraf: sol=0, sağ=1)
                    for (int r = 0; r < shelvesPerAisle; r++)
                    {
                        for (int side = 0; side < 2; side++)
                        {
                            // Piksel koordinatlarını hesapla
                            double pixelX = CalculatePixelX(b, a, side, aislesPerBlock);
                            double pixelY = CalculatePixelY(b, r, blockCount, shelvesPerAisle);

                            // Gerçek birim koordinatları
                            double unitX = CalculateUnitX(b, a, side, aislesPerBlock, aisleLength);
                            double unitY = CalculateUnitY(b, r, shelvesPerAisle, crossAisleDistance);

                            var shelf = new ShelfLocation
                            {
                                BlockIndex = b,
                                AisleIndex = a,
                                ShelfRow = r,
                                Side = side,
                                X = pixelX,
                                Y = pixelY
                            };
                            aisle.Shelves.Add(shelf);
                        }
                    }
                    block.Aisles.Add(aisle);
                }
                warehouse.Blocks.Add(block);
            }

            return warehouse;
        }

        /// <summary>
        /// Bir rafın piksel X koordinatını hesaplar.
        /// Her blok yan yana yerleşir, her bloktaki koridorlar sıralıdır.
        /// </summary>
        private static double CalculatePixelX(int blockIndex, int aisleIndex, int side, int aislesPerBlock)
        {
            // Bir bloğun genişliği: (koridor sayısı) * (sol raf + koridor genişliği + sağ raf)
            double blockWidth = aislesPerBlock * (SHELF_PIXEL_WIDTH + AISLE_PIXEL_WIDTH + SHELF_PIXEL_WIDTH);
            double blockStartX = MARGIN + blockIndex * (blockWidth + CROSS_AISLE_PIXEL_HEIGHT * 2);

            // Koridor içindeki X pozisyonu
            double aisleGroupWidth = SHELF_PIXEL_WIDTH + AISLE_PIXEL_WIDTH + SHELF_PIXEL_WIDTH;
            double aisleStartX = blockStartX + aisleIndex * aisleGroupWidth;

            // Sol taraf (side=0) veya sağ taraf (side=1)
            if (side == 0)
                return aisleStartX; // Sol raf
            else
                return aisleStartX + SHELF_PIXEL_WIDTH + AISLE_PIXEL_WIDTH; // Sağ raf
        }

        /// <summary>
        /// Bir rafın piksel Y koordinatını hesaplar.
        /// Raflar yukarıdan aşağıya sıralanır.
        /// </summary>
        private static double CalculatePixelY(int blockIndex, int rowIndex, int blockCount, int shelvesPerAisle)
        {
            // Raflar, blok ne olursa olsun Y ekseninde aynı konumda
            // (Roodbergen modelinde bloklar X ekseninde sıralanır)
            return MARGIN + rowIndex * SHELF_PIXEL_HEIGHT;
        }

        /// <summary>
        /// Gerçek birim cinsinden X koordinatı
        /// </summary>
        private static double CalculateUnitX(int blockIndex, int aisleIndex, int side, int aislesPerBlock, double aisleLength)
        {
            double blockWidth = aislesPerBlock * 3.0; // Her koridor 3 birim genişlik
            double blockStartX = blockIndex * (blockWidth + 2.0);
            double aisleX = blockStartX + aisleIndex * 3.0;
            return side == 0 ? aisleX : aisleX + 2.0;
        }

        /// <summary>
        /// Gerçek birim cinsinden Y koordinatı
        /// </summary>
        private static double CalculateUnitY(int blockIndex, int rowIndex, int shelvesPerAisle, double crossAisleDistance)
        {
            return rowIndex * 1.0;
        }

        /// <summary>
        /// Kapı konumunun piksel koordinatlarını hesaplar
        /// </summary>
        public static (double X, double Y) GetDoorPixelPosition(Warehouse warehouse)
        {
            double totalWidth = CalculateTotalPixelWidth(warehouse);

            return warehouse.DoorLocation switch
            {
                DoorLocation.Left => (MARGIN, MARGIN - 25),
                DoorLocation.Right => (MARGIN + totalWidth, MARGIN - 25),
                DoorLocation.Center => (MARGIN + totalWidth / 2, MARGIN - 25),
                _ => (MARGIN, MARGIN - 25)
            };
        }

        /// <summary>
        /// Toplam depo piksel genişliğini hesaplar
        /// </summary>
        public static double CalculateTotalPixelWidth(Warehouse warehouse)
        {
            double blockWidth = warehouse.AislesPerBlock *
                                (SHELF_PIXEL_WIDTH + AISLE_PIXEL_WIDTH + SHELF_PIXEL_WIDTH);
            return warehouse.BlockCount * (blockWidth + CROSS_AISLE_PIXEL_HEIGHT * 2) - CROSS_AISLE_PIXEL_HEIGHT * 2;
        }

        /// <summary>
        /// Toplam depo piksel yüksekliğini hesaplar
        /// </summary>
        public static double CalculateTotalPixelHeight(Warehouse warehouse)
        {
            return warehouse.ShelvesPerAisle * SHELF_PIXEL_HEIGHT + MARGIN * 2;
        }

        /// <summary>
        /// Bir rafın depo koordinat sistemindeki merkez noktasını döndürür.
        /// Rota hesaplamalarında kullanılır.
        /// </summary>
        public static WarehousePoint GetShelfPickPoint(ShelfLocation shelf, Warehouse warehouse)
        {
            // Toplayıcı rafın önünden geçerken ürünü alır
            // X: koridorun orta çizgisi, Y: rafın konumu

            int globalAisleIndex = shelf.BlockIndex * warehouse.AislesPerBlock + shelf.AisleIndex;

            // Her koridor grubu 3 birim: 1 (sol raf) + 1 (koridor) + 1 (sağ raf)
            double aisleGroupSize = warehouse.ShelfWidth * 2 + 1.0;
            double blockOffset = shelf.BlockIndex * (warehouse.AislesPerBlock * aisleGroupSize + warehouse.CrossAisleDistance);
            double aisleOffset = shelf.AisleIndex * aisleGroupSize + warehouse.ShelfWidth;

            double x = blockOffset + aisleOffset; // Koridor orta noktası
            double y = shelf.ShelfRow * (warehouse.AisleLength / warehouse.ShelvesPerAisle);

            return new WarehousePoint(x, y, shelf.Label);
        }

        /// <summary>
        /// Kapı konumunun depo koordinatlarını döndürür
        /// </summary>
        public static WarehousePoint GetDoorPoint(Warehouse warehouse)
        {
            double totalWidth = warehouse.BlockCount *
                                (warehouse.AislesPerBlock * (warehouse.ShelfWidth * 2 + 1.0) + warehouse.CrossAisleDistance);

            double x = warehouse.DoorLocation switch
            {
                DoorLocation.Left => 0,
                DoorLocation.Right => totalWidth,
                DoorLocation.Center => totalWidth / 2,
                _ => 0
            };

            return new WarehousePoint(x, -warehouse.CrossAisleDistance, "Kapı");
        }
    }
}
