using System.Windows;
using WarehouseSimulator.Helpers;

namespace WarehouseSimulator.Views
{
    /// <summary>
    /// Sipariş geçmişi penceresi (Veritabanı entegrasyonu bonus özelliği)
    /// </summary>
    public partial class HistoryWindow : Window
    {
        public HistoryWindow()
        {
            InitializeComponent();
            LoadHistory();
        }

        private void LoadHistory()
        {
            try
            {
                var history = DatabaseManager.Instance.GetOrderHistory();
                dgHistory.ItemsSource = history.Select(h => new
                {
                    ID       = h.Id,
                    ÜrünSay  = h.ItemCount,
                    EnİyiAlg = h.BestAlgo,
                    Mesafe   = $"{h.BestDist:F2} birim",
                    Tarih    = h.Date
                }).ToList();
            }
            catch
            {
                dgHistory.ItemsSource = null;
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
    }
}
