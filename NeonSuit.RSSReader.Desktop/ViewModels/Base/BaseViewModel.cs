using CommunityToolkit.Mvvm.ComponentModel;

namespace NeonSuit.RSSReader.Desktop.ViewModels.Base // Cambiado para NeonSuit
{
    /// <summary>
    /// Base class for all ViewModels in the application.
    /// Isaac, aquí centralizamos IsBusy para que todas tus vistas 
    /// puedan mostrar el spinner de carga sin repetir código.
    /// </summary>
    public partial class BaseViewModel : ObservableObject
    {
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsNotBusy))] // El Toolkit genera IsNotBusy solito
        private bool _isBusy;

        [ObservableProperty]
        private string _title = string.Empty;

        // Propiedad calculada para facilitar los bindeos de "IsEnabled" en WPF
        public bool IsNotBusy => !IsBusy;
    }
}