using System.ComponentModel;
using AnimatronicsControlCenter.Core.Interfaces;

namespace AnimatronicsControlCenter.UI.Helpers
{
    public class LocalizedStrings : INotifyPropertyChanged
    {
        private readonly ILocalizationService _localizationService;

        public event PropertyChangedEventHandler? PropertyChanged;

        public LocalizedStrings(ILocalizationService localizationService)
        {
            _localizationService = localizationService;
            _localizationService.LanguageChanged += (s, e) =>
            {
                // Notify that the indexer property has changed
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
                // Notify that the code has changed to trigger function bindings
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Code)));
            };
        }

        public string this[string key] => _localizationService.GetString(key);

        public string Code => _localizationService.CurrentCulture.Name;

        // Helper for x:Bind to trigger updates when Code changes
        public string Get(string key, string trigger) => this[key];
    }
}
