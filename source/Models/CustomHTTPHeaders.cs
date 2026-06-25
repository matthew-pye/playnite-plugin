using CommunityToolkit.Mvvm.ComponentModel;

namespace Graviton.Models
{
    public partial class CustomHTTPHeader : ObservableObject
    {
        [ObservableProperty] private bool _enabled = false;
        [ObservableProperty] private string _name = "";
        [ObservableProperty] private string _value = "";
    }
}