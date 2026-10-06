using CommunityToolkit.Mvvm.ComponentModel;

using Graviton.Settings;

using System.Text.Json.Serialization;

namespace Graviton.Models
{
    public partial class CustomHTTPHeader : ObservableObject
    {
        [ObservableProperty] private bool _enabled = false;
        [ObservableProperty] private string _name = "";
        private string _value = "";


        [JsonIgnore]
        public string Value
        {
            get => string.IsNullOrEmpty(_value) ? string.Empty : GravitonSecurity.UnProtect(_value);
            set
            {
                _value = string.IsNullOrEmpty(value) ? string.Empty : GravitonSecurity.Protect(value);
                OnPropertyChanged();
            }
        }

        public string ProtectedValue
        {
            get => _value;
            private set => _value = value ?? "";
        }
    }
}