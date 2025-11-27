using System;
using System.Globalization;

namespace AnimatronicsControlCenter.Core.Interfaces
{
    public interface ILocalizationService
    {
        event EventHandler LanguageChanged;
        CultureInfo CurrentCulture { get; set; }
        string GetString(string key);
        void SetLanguage(string languageCode);
    }
}

