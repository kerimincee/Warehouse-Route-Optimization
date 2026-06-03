using System.Linq;
using System.Windows;
using WarehouseSimulator.Helpers;
using WarehouseSimulator.Resources;

namespace WarehouseSimulator.Views
{
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
                    ID = h.Id,
                    ItemCount = h.ItemCount,
                    BestAlgo = h.BestAlgo,
                    DistanceText = LanguageResources.Format("DistanceUnitFormat", h.BestDist),
                    Date = h.Date
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
