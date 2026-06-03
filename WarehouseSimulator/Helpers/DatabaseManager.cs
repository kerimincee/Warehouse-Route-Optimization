using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;
using WarehouseSimulator.Models;
using WarehouseSimulator.Resources;

namespace WarehouseSimulator.Helpers
{
    /// <summary>
    /// SQLite veritabanı yöneticisi.
    /// Depo konfigürasyonları, sipariş geçmişi ve ürün lokasyonlarını saklar.
    /// Bonus: Veritabanı Entegrasyonu özelliği.
    /// </summary>
    public class DatabaseManager
    {
        private readonly string _connectionString;
        private static DatabaseManager? _instance;

        /// <summary>Singleton örneği</summary>
        public static DatabaseManager Instance => _instance ??= new DatabaseManager();

        private DatabaseManager()
        {
            // Veritabanı dosyasını uygulamanın çalıştığı klasörde oluştur
            string dbPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "warehouse_data.db");
            _connectionString = $"Data Source={dbPath}";
            InitializeDatabase();
        }

        /// <summary>
        /// Veritabanı tablolarını oluşturur (yoksa)
        /// </summary>
        private void InitializeDatabase()
        {
            using var conn = new SqliteConnection(_connectionString);
            conn.Open();

            // Depo konfigürasyonu tablosu
            ExecuteNonQuery(conn, @"
                CREATE TABLE IF NOT EXISTS WarehouseConfigs (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL,
                    BlockCount INTEGER NOT NULL,
                    AislesPerBlock INTEGER NOT NULL,
                    ShelvesPerAisle INTEGER NOT NULL,
                    DoorLocation INTEGER NOT NULL,
                    AisleLength REAL NOT NULL,
                    CrossAisleDistance REAL NOT NULL,
                    CreatedAt TEXT NOT NULL
                );");

            // Sipariş geçmişi tablosu
            ExecuteNonQuery(conn, @"
                CREATE TABLE IF NOT EXISTS OrderHistory (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    ConfigId INTEGER,
                    ItemCount INTEGER NOT NULL,
                    BestAlgorithm TEXT,
                    BestDistance REAL,
                    CreatedAt TEXT NOT NULL,
                    FOREIGN KEY(ConfigId) REFERENCES WarehouseConfigs(Id)
                );");

            // Sipariş öğeleri tablosu
            ExecuteNonQuery(conn, @"
                CREATE TABLE IF NOT EXISTS OrderItems (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    OrderId INTEGER NOT NULL,
                    BlockIndex INTEGER NOT NULL,
                    AisleIndex INTEGER NOT NULL,
                    ShelfRow INTEGER NOT NULL,
                    Side INTEGER NOT NULL,
                    ProductName TEXT,
                    FOREIGN KEY(OrderId) REFERENCES OrderHistory(Id)
                );");

            // Algoritma sonuçları tablosu
            ExecuteNonQuery(conn, @"
                CREATE TABLE IF NOT EXISTS AlgorithmResults (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    OrderId INTEGER NOT NULL,
                    AlgorithmName TEXT NOT NULL,
                    TotalDistance REAL NOT NULL,
                    ComputationTimeMs REAL,
                    IsBest INTEGER NOT NULL,
                    CreatedAt TEXT NOT NULL,
                    FOREIGN KEY(OrderId) REFERENCES OrderHistory(Id)
                );");
        }

        /// <summary>
        /// Depo konfigürasyonunu kayıt eder ve yeni ID döndürür
        /// </summary>
        public long SaveWarehouseConfig(Warehouse warehouse, string configName = "")
        {
            if (string.IsNullOrEmpty(configName))
                configName = LanguageResources.Format("DefaultConfigName", DateTime.Now);

            using var conn = new SqliteConnection(_connectionString);
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO WarehouseConfigs
                    (Name, BlockCount, AislesPerBlock, ShelvesPerAisle,
                     DoorLocation, AisleLength, CrossAisleDistance, CreatedAt)
                VALUES
                    (@name, @bc, @apc, @spa, @dl, @al, @cad, @ca);
                SELECT last_insert_rowid();";

            cmd.Parameters.AddWithValue("@name", configName);
            cmd.Parameters.AddWithValue("@bc", warehouse.BlockCount);
            cmd.Parameters.AddWithValue("@apc", warehouse.AislesPerBlock);
            cmd.Parameters.AddWithValue("@spa", warehouse.ShelvesPerAisle);
            cmd.Parameters.AddWithValue("@dl", (int)warehouse.DoorLocation);
            cmd.Parameters.AddWithValue("@al", warehouse.AisleLength);
            cmd.Parameters.AddWithValue("@cad", warehouse.CrossAisleDistance);
            cmd.Parameters.AddWithValue("@ca", DateTime.Now.ToString("o"));

            return (long)(cmd.ExecuteScalar() ?? 0L);
        }

        /// <summary>
        /// Siparişi ve öğelerini kayıt eder, yeni sipariş ID döndürür
        /// </summary>
        public long SaveOrder(Order order, long configId, List<RouteResult> results)
        {
            using var conn = new SqliteConnection(_connectionString);
            conn.Open();

            // En iyi sonucu bul
            RouteResult? best = null;
            foreach (var r in results)
                if (best == null || r.TotalDistance < best.TotalDistance)
                    best = r;

            // Sipariş kaydı
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO OrderHistory
                    (ConfigId, ItemCount, BestAlgorithm, BestDistance, CreatedAt)
                VALUES (@cid, @ic, @ba, @bd, @ca);
                SELECT last_insert_rowid();";

            cmd.Parameters.AddWithValue("@cid", configId);
            cmd.Parameters.AddWithValue("@ic", order.ItemCount);
            cmd.Parameters.AddWithValue("@ba", best?.AlgorithmName ?? "");
            cmd.Parameters.AddWithValue("@bd", best?.TotalDistance ?? 0.0);
            cmd.Parameters.AddWithValue("@ca", DateTime.Now.ToString("o"));

            long orderId = (long)(cmd.ExecuteScalar() ?? 0L);

            // Sipariş öğelerini kayıt et
            foreach (var item in order.Items)
            {
                using var itemCmd = conn.CreateCommand();
                itemCmd.CommandText = @"
                    INSERT INTO OrderItems
                        (OrderId, BlockIndex, AisleIndex, ShelfRow, Side, ProductName)
                    VALUES (@oid, @bi, @ai, @sr, @si, @pn);";

                itemCmd.Parameters.AddWithValue("@oid", orderId);
                itemCmd.Parameters.AddWithValue("@bi", item.Location.BlockIndex);
                itemCmd.Parameters.AddWithValue("@ai", item.Location.AisleIndex);
                itemCmd.Parameters.AddWithValue("@sr", item.Location.ShelfRow);
                itemCmd.Parameters.AddWithValue("@si", item.Location.Side);
                itemCmd.Parameters.AddWithValue("@pn", item.ProductName);
                itemCmd.ExecuteNonQuery();
            }

            // Algoritma sonuçlarını kayıt et
            foreach (var result in results)
            {
                using var resCmd = conn.CreateCommand();
                resCmd.CommandText = @"
                    INSERT INTO AlgorithmResults
                        (OrderId, AlgorithmName, TotalDistance, ComputationTimeMs, IsBest, CreatedAt)
                    VALUES (@oid, @an, @td, @ct, @ib, @ca);";

                resCmd.Parameters.AddWithValue("@oid", orderId);
                resCmd.Parameters.AddWithValue("@an", result.AlgorithmName);
                resCmd.Parameters.AddWithValue("@td", result.TotalDistance);
                resCmd.Parameters.AddWithValue("@ct", result.ComputationTimeMs);
                resCmd.Parameters.AddWithValue("@ib", result.IsBest ? 1 : 0);
                resCmd.Parameters.AddWithValue("@ca", DateTime.Now.ToString("o"));
                resCmd.ExecuteNonQuery();
            }

            return orderId;
        }

        /// <summary>
        /// Tüm kayıtlı depo konfigürasyonlarını listeler
        /// </summary>
        public List<(long Id, string Name, string Details)> GetSavedConfigs()
        {
            var list = new List<(long, string, string)>();
            using var conn = new SqliteConnection(_connectionString);
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT Id, Name, BlockCount, AislesPerBlock, ShelvesPerAisle, CreatedAt
                FROM WarehouseConfigs
                ORDER BY CreatedAt DESC;";

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                long id = reader.GetInt64(0);
                string name = reader.GetString(1);
                string details = LanguageResources.Format("ConfigDetailsFormat",
                    reader.GetInt32(2), reader.GetInt32(3), reader.GetInt32(4)) +
                    reader.GetString(5)[..16];
                list.Add((id, name, details));
            }
            return list;
        }

        /// <summary>
        /// Sipariş geçmişini listeler
        /// </summary>
        public List<(long Id, int ItemCount, string BestAlgo, double BestDist, string Date)> GetOrderHistory()
        {
            var list = new List<(long, int, string, double, string)>();
            using var conn = new SqliteConnection(_connectionString);
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT Id, ItemCount, BestAlgorithm, BestDistance, CreatedAt
                FROM OrderHistory
                ORDER BY CreatedAt DESC
                LIMIT 50;";

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add((
                    reader.GetInt64(0),
                    reader.GetInt32(1),
                    reader.GetString(2),
                    reader.GetDouble(3),
                    reader.GetString(4)[..16]
                ));
            }
            return list;
        }

        private void ExecuteNonQuery(SqliteConnection conn, string sql)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.ExecuteNonQuery();
        }
    }
}
