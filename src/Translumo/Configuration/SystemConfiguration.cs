using System.Globalization;
using Translumo.Utils;

namespace Translumo.Configuration
{
    public class SystemConfiguration : BindableBase
    {
        public static SystemConfiguration Default => new SystemConfiguration()
        {
            ApplicationCulture = "hu-HU"
        };

        public string ApplicationCulture
        {
            get => _applicationCulture ?? "hu-HU";
            set
            {
                var culture = "hu-HU";
                SetProperty(ref _applicationCulture, culture);
                UpdateSelectedLanguage();
            }
        }

        private string _applicationCulture = "hu-HU";

        private void UpdateSelectedLanguage()
        {
            LocalizationManager.ChangeAppCulture(new CultureInfo(ApplicationCulture));
        }
    }
}
