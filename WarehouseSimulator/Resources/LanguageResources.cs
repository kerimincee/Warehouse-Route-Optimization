using System.Globalization;
using System.Reflection;
using System.Resources;
using System.ComponentModel;

namespace WarehouseSimulator.Resources
{
    public sealed class TranslationSource : INotifyPropertyChanged
    {
        public static TranslationSource Instance { get; } = new();

        private static readonly ResourceManager ResMgr = new(
            "WarehouseSimulator.Resources.Strings",
            Assembly.GetExecutingAssembly());

        private CultureInfo _culture = new("tr-TR");

        public CultureInfo Culture
        {
            get => _culture;
            set
            {
                if (_culture.Equals(value)) return;
                _culture = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
            }
        }

        public string this[string key] => ResMgr.GetString(key, _culture) ?? key;

        public event PropertyChangedEventHandler? PropertyChanged;

        public void SetLanguage(string cultureCode)
        {
            Culture = new CultureInfo(cultureCode);
        }
    }

    public static class LanguageResources
    {
        public static string GetString(string key)
        {
            return TranslationSource.Instance[key];
        }

        public static string Format(string key, params object?[] args)
        {
            return string.Format(TranslationSource.Instance[key], args);
        }
    }
}
