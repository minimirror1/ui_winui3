using System.Globalization;
using AnimatronicsControlCenter.Core.Interfaces;
using Microsoft.Windows.ApplicationModel.Resources;
using Windows.Globalization;

namespace AnimatronicsControlCenter.Infrastructure
{
    public class LocalizationService : ILocalizationService
    {
        private readonly ResourceManager _resourceManager;
        private readonly ResourceContext _resourceContext;
        private readonly ResourceMap _resourceMap;

        public LocalizationService()
        {
            _resourceManager = new ResourceManager();
            _resourceContext = _resourceManager.CreateResourceContext();
            _resourceMap = _resourceManager.MainResourceMap.GetSubtree("Resources");
        }

        public CultureInfo CurrentCulture
        {
            get => CultureInfo.CurrentUICulture;
            set
            {
                if (CultureInfo.CurrentUICulture.Name == value.Name) return;

                CultureInfo.CurrentUICulture = value;
                try
                {
                    ApplicationLanguages.PrimaryLanguageOverride = value.Name;
                }
                catch
                {
                    // Ignore error if PrimaryLanguageOverride cannot be set (e.g. invalid state)
                }
                _resourceContext.QualifierValues["Language"] = value.Name;
            }
        }

        public string GetString(string key)
        {
            try
            {
                var candidate = _resourceMap.GetValue(key, _resourceContext);
                return candidate?.ValueAsString ?? key;
            }
            catch
            {
                return key;
            }
        }

        public void SetLanguage(string languageCode)
        {
            CurrentCulture = new CultureInfo(languageCode);
        }
    }
}
