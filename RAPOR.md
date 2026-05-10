# Depo Rota Optimizasyonu — Sistem Mimarisi ve Algoritma Raporu

## 1. Giriş ve Sistem Genel Bakışı

Bu proje, **Roodbergen Interactive Warehouse** sistemi temel alınarak geliştirilmiş bir **C# WPF masaüstü uygulamasıdır**. Sistem; depodaki raf yapısını, ürün yerleşimini ve sipariş toplama rotalarını simüle eder; birden fazla algoritmayı karşılaştırmalı olarak değerlendirir.

---

## 2. Sistem Mimarisi

Proje **MVVM (Model-View-ViewModel)** tasarım desenini kullanmaktadır.

```
WarehouseSimulator.sln        → Visual Studio çözüm dosyası
WarehouseSimulator/
├── Models/
│   ├── Warehouse.cs          → ShelfLocation, Aisle, Block, Warehouse sınıfları
│   └── Order.cs              → OrderItem, Order, Picker, WarehousePoint, RouteResult
├── Algorithms/
│   ├── RoutingAlgorithm.cs   → Soyut temel sınıf (Template Method deseni)
│   ├── SShapeAlgorithm.cs    → S-Shape serpentin algoritması
│   ├── LargestGapAlgorithm.cs→ Largest Gap algoritması
│   ├── AisleByAisleAlgorithm.cs → Koridor bazlı algoritma
│   └── OptimalAlgorithm.cs   → Nearest Neighbor + 2-Opt TSP
├── ViewModels/
│   └── MainViewModel.cs      → MVVM köprüsü, iş mantığı
├── Views/
│   ├── MainWindow.xaml       → Ana pencere UI
│   ├── MainWindow.xaml.cs    → Canvas çizim ve etkileşim
│   └── HistoryWindow.xaml    → SQLite geçmiş penceresi
└── Helpers/
    ├── WarehouseBuilder.cs   → Koordinat hesaplama
    └── DatabaseManager.cs    → SQLite CRUD işlemleri
```

### Nesne Yönelimli Tasarım

| Sınıf | Sorumluluk |
|-------|-----------|
| `Warehouse` | Tüm depo yapısı; blok, koridor ve raf hiyerarşisini yönetir |
| `Block` | Birden fazla koridoru içeren depo bölgesi |
| `Aisle` | Tek bir koridor; iki taraflı raf listesi içerir |
| `ShelfLocation` | Atomik raf konumu; blok/koridor/sıra/taraf indeksleri |
| `Order` | Sipariş; birden fazla `OrderItem` içerir |
| `Picker` | Toplayıcı; konumu ve kat ettiği mesafeyi takip eder |
| `WarehousePoint` | 2D koordinat noktası; Manhattan/Öklid mesafe metodları |
| `RouteResult` | Algoritma çıktısı; rota noktaları, mesafe, süre |
| `RoutingAlgorithm` | Soyut temel; Template Method ile alt sınıf kancaları |

---

## 3. Algoritma Açıklamaları

### 3.1 S-Shape (Serpentin) Algoritması

**Teori:** Toplayıcı, ürün içeren her koridora girer ve koridoru **başından sonuna** tamamen geçer. Çift koridorlar alt→üst, tek koridorlar üst→alt yönünde geçilir.

**Avantajlar:** Basit, öngörülebilir, az karar gerektiren.
**Dezavantajlar:** Koridorda yalnızca bir ürün olsa bile tüm koridoru geçer.

### 3.2 Largest Gap (En Büyük Boşluk) Algoritması

**Teori:** Her koridor için ürünler arasındaki boşluklar hesaplanır. **En büyük boşluk** nereye denk geliyorsa, toplayıcı o noktada geri döner.

- **Boşluk alt uçta** → Üstten gir, ürünlere kadar in, geri çık
- **Boşluk üst uçta** → Alttan gir, ürünlere kadar çık, geri in
- **Boşluk ortada** → Koridoru ikiye böl, her iki uçtan erişim

**Kaynak:** Roodbergen, K.J. & De Koster, R. (2001). IJPR, 39(9), 1865-1883.

### 3.3 Aisle-by-Aisle (Koridor Bazlı) Algoritması

**Teori:** Her koridor için bağımsız olarak **Return** veya **Traversal** stratejisi seçilir.

- Return maliyeti = 2 × (son ürünün Y konumu)
- Traversal maliyeti = koridor uzunluğu
- Daha küçük olan seçilir.

### 3.4 Nearest Neighbor TSP

**Teori:** Her adımda Manhattan mesafesine göre en yakın henüz ziyaret edilmemiş ürünü seçer. Karmaşıklık: O(n²).

### 3.5 2-Opt TSP (Bonus)

**Teori:** Nearest Neighbor başlangıç rotasına 2-Opt yerel arama uygulanır. İki kenarın yer değiştirmesi mesafeyi kısaltıyorsa değişiklik yapılır. Karmaşıklık: O(n³) iterasyon başına.

---

## 4. Koordinat Sistemi

| Sistem | Birim | Kullanım |
|--------|-------|---------|
| Birim koordinatları | Metre/birim | Mesafe hesaplama (Manhattan) |
| Piksel koordinatları | Px | WPF Canvas görselleştirme |

---

## 5. Veritabanı Entegrasyonu (Bonus)

SQLite (Microsoft.Data.Sqlite) kullanılmıştır:

| Tablo | Açıklama |
|-------|---------|
| `WarehouseConfigs` | Kaydedilen depo konfigürasyonları |
| `OrderHistory` | Sipariş geçmişi ve en iyi algoritma özeti |
| `OrderItems` | Siparişteki her raf konumu |
| `AlgorithmResults` | Her algoritmanın mesafe ve süre sonuçları |

---

## 6. Örnek Senaryo: 2 Blok, 3 Koridor, 8 Raf — 20 Ürünlü Sipariş

| Algoritma | Tahmini Mesafe | Açıklama |
|-----------|---------------|---------|
| S-Shape | ~85 birim | Tüm koridorları geçer |
| Largest Gap | ~68 birim | Boşluk optimizasyonu ile kısalır |
| Aisle-by-Aisle | ~72 birim | Her koridor bağımsız optimize |
| Nearest Neighbor TSP | ~61 birim | Sezgisel global optimizasyon |
| **2-Opt TSP** | **~58 birim** | **En iyi sonuç** |

---

## 7. Kurulum

### Gereksinimler
- Windows 10/11
- .NET 7.0 SDK (https://dotnet.microsoft.com/download/dotnet/7.0)
- Visual Studio 2022 veya VS Code + C# extension

### Çalıştırma
```bash
cd WarehouseSimulator
dotnet restore
dotnet run
```
Veya `build_and_run.bat` dosyasını çift tıklayın.

---

## 8. Değerlendirme Kriterleri

| Kriter | Uygulama |
|--------|---------|
| Arayüz (%20) | Koyu endüstriyel tema, animasyonlar, Canvas görselleştirme |
| OOP (%20) | Warehouse, Aisle, Block, Order, Picker, RoutingAlgorithm hiyerarşisi |
| Algoritma Doğruluğu (%40) | 5 algoritma, Manhattan mesafesi, Roodbergen modeli |
| Kod Kalitesi (%20) | XML doc, MVVM deseni, tek sorumluluk ilkesi |
| **Bonus: DB** | SQLite entegrasyonu |
| **Bonus: 2-Opt TSP** | Gelişmiş optimal algoritma |
| **Bonus: Isı Haritası** | Sık kullanılan rafları görselleştirme |
