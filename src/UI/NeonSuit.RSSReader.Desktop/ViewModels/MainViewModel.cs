using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.Logging;
using NeonSuit.RSSReader.Core.Interfaces.Services;
using NeonSuit.RSSReader.Core.Models;
using NeonSuit.RSSReader.Desktop.ViewModels.Base;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace NeonSuit.RSSReader.Desktop.ViewModels
{
    /// <summary>
    /// ViewModel principal de NeonSuit - Refactorizado para mostrar Fuentes directamente.
    /// </summary>
    public partial class MainViewModel : BaseViewModel
    {
        private readonly IArticleService _articleService;
        private readonly IFeedService _feedService;
        private readonly ILogger<MainViewModel> _logger;

        public ISnackbarMessageQueue MessageQueue { get; }

        // Colecciones que bindean a la UI
        public ObservableCollection<Article> Articles { get; } = new();

        // CAMBIO CLAVE: Ahora bindeamos a Feeds para ver los nombres de los periódicos
        public ObservableCollection<Feed> Feeds { get; } = new();

        [ObservableProperty]
        private string _newFeedUrl = string.Empty;

        [ObservableProperty]
        private string _searchQuery = string.Empty; // NUEVO: Para búsqueda

        // Propiedad para saber qué periódico tiene seleccionado el usuario
        [ObservableProperty]
        private Feed? _selectedFeed;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        [ObservableProperty]
        private bool _showStatus;

        [ObservableProperty]
        private string _statusColor = "#00FFCC"; // Verde neón por defecto

        [ObservableProperty]
        private Article? _selectedArticle;

        public MainViewModel(
            IArticleService articleService,
            IFeedService feedService,
            ILogger<MainViewModel> logger)
        {
            Title = "NeonSuit RSS - Estación de Prensa";
            _articleService = articleService;
            _feedService = feedService;
            _logger = logger;
            MessageQueue = new SnackbarMessageQueue(TimeSpan.FromSeconds(3));
            // Inicialización al arrancar
            _ = InitializeDataAsync();
        }

        private async Task InitializeDataAsync()
        {
            await RefreshFeedsAsync();
            await RefreshArticlesAsync();
        }

        /// <summary>
        /// Carga los nombres de los periódicos en la columna izquierda.
        /// </summary>
        [RelayCommand]
        private async Task RefreshFeedsAsync()
        {
            try
            {
                var items = await _feedService.GetAllFeedsAsync();

                Feeds.Clear();
                foreach (var feed in items)
                {
                    Feeds.Add(feed);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading feeds.");
            }
        }

        /// <summary>
        /// Actualizar todos los feeds - Versión compatible con XAML
        /// </summary>
        [RelayCommand]
        private async Task RefreshAllFeedsAsync()
        {
            try
            {
                IsBusy = true;
                await _feedService.RefreshAllFeedsAsync(); // Necesitas este método en IFeedService
                await RefreshFeedsAsync();
                await RefreshArticlesAsync();
                await DisplayStatus("¡Todos los feeds actualizados!", false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar feeds.");
                await DisplayStatus($"Error: {ex.Message}", true);
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// Carga las noticias. Si hay un feed seleccionado, filtra por ese.
        /// </summary>
        [RelayCommand]
        private async Task RefreshArticlesAsync(object? parameter = null)
        {
            if (IsBusy) return;
            try
            {
                IsBusy = true;
                var allArticles = await _articleService.GetAllArticlesAsync();

                Articles.Clear();

                // Si el usuario seleccionó un periódico a la izquierda, filtramos
                var filtered = SelectedFeed == null
                    ? allArticles
                    : allArticles.Where(a => a.FeedId == SelectedFeed.Id);

                // Si hay búsqueda, filtrar por texto
                if (!string.IsNullOrWhiteSpace(SearchQuery))
                {
                    filtered = filtered.Where(a =>
                        (a.Title?.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) ?? false) ||
                        (a.Summary?.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) ?? false));
                }

                foreach (var article in filtered)
                {
                    Articles.Add(article);
                }

                await DisplayStatus($"Se cargaron {Articles.Count} artículos", false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading articles.");
                await DisplayStatus("Facho cargando noticias.", true);
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        public async Task AddNewFeedAsync()
        {
            if (string.IsNullOrWhiteSpace(NewFeedUrl))
            {
                await DisplayStatus("Oye Isaac, pega una URL primero, asere.", true);
                return;
            }

            IsBusy = true;
            try
            {
                // 1. Agregamos el feed (esto ya limpia el XML y guarda en DB)
                await _feedService.AddFeedAsync(NewFeedUrl);

                var urlAgregada = NewFeedUrl;
                NewFeedUrl = string.Empty;

                // 2. REFUERZO: Refrescamos la lista de periódicos y noticias
                await RefreshFeedsAsync();
                await RefreshArticlesAsync();

                await DisplayStatus($"¡Fuente agregada: {urlAgregada}!");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Facho al añadir el feed");
                string errorDetallado = ex.InnerException?.Message ?? ex.Message;
                await DisplayStatus($"Facho: {errorDetallado}", true);
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// Este método se dispara automáticamente cuando cambias de periódico en la lista.
        /// </summary>
        partial void OnSelectedFeedChanged(Feed? value)
        {
            // Cuando Isaac toca un periódico, refrescamos el centro para ver solo esas noticias
            _ = RefreshArticlesAsync();
        }

        /// <summary>
        /// Cuando cambia el texto de búsqueda
        /// </summary>
        partial void OnSearchQueryChanged(string value)
        {
            _ = RefreshArticlesAsync();
        }

        [RelayCommand]
        private async Task GoToDetailsAsync(Article article)
        {
            if (article == null) return;
            _logger.LogInformation($"Navegando al artículo: {article.Title}");
        }

        public async Task DisplayStatus(string message, bool isError = false)
        {
            StatusMessage = message;
            StatusColor = isError ? "#FF5555" : "#00FFCC";
            ShowStatus = true;
            MessageQueue?.Enqueue(message, isError ? "ERROR" : "OK", () => { });
            await Task.Delay(4000);
            ShowStatus = false;
        }

        [RelayCommand]
        public void ToggleTheme()
        {
            var paletteHelper = new MaterialDesignThemes.Wpf.PaletteHelper();
            var theme = paletteHelper.GetTheme();

            // Cambiamos el "Base Theme" (Light o Dark)
            if (theme.GetBaseTheme() == BaseTheme.Dark)
            {
                theme.SetBaseTheme(BaseTheme.Light);
            }
            else
            {
                theme.SetBaseTheme(BaseTheme.Dark);
            }

            paletteHelper.SetTheme(theme);
        }

        // Este método se dispara solo gracias a CommunityToolkit.Mvvm
        partial void OnSelectedArticleChanged(Article? value)
        {
            if (value == null) return;

            // Aquí es donde mandamos a abrir la noticia
            _logger.LogInformation($"Abriendo: {value.Title}");

            // Por ahora, para probar, podrías disparar tu comando de detalles
            _ = GoToDetailsAsync(value);
        }

        [RelayCommand]
        private async Task DeleteFeedAsync(Feed feed)
        {
            try
            {
                IsBusy = true;
                await _feedService.DeleteFeedAsync(feed.Id);
                await RefreshFeedsAsync();
                StatusMessage = $"Feed '{feed.Title}' eliminado";
                ShowStatus = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar feed");
                StatusMessage = $"Error: {ex.Message}";
                ShowStatus = true;
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private void OpenInBrowser(Article article)
        {
            if (article?.Link != null)
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = article.Link,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error al abrir en navegador");
                    _ = DisplayStatus($"Error al abrir: {ex.Message}", true);
                }
            }
        }

        [RelayCommand]
        private void CopyLink(Article article)
        {
            if (article?.Link != null)
            {
                try
                {
                    Clipboard.SetText(article.Link);
                    StatusMessage = "Enlace copiado al portapapeles";
                    ShowStatus = true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error al copiar enlace");
                    StatusMessage = $"Error: {ex.Message}";
                    ShowStatus = true;
                }
            }
        }

        /// <summary>
        /// Marcar todos como leídos - NUEVO para cumplir con XAML
        /// </summary>
        [RelayCommand]
        private async Task MarkAllReadAsync()
        {
            try
            {
                IsBusy = true;
                // Necesitas implementar este método en IArticleService
                await _articleService.MarkAllAsReadAsync();
                await RefreshArticlesAsync();
                await DisplayStatus("Todos los artículos marcados como leídos", false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al marcar como leídos");
                await DisplayStatus($"Error: {ex.Message}", true);
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// Propiedad computada para saber si hay artículos
        /// </summary>
        public bool HasArticles => Articles.Count > 0;

        /// <summary>
        /// Propiedad computada para saber si hay feeds
        /// </summary>
        public bool HasFeeds => Feeds.Count > 0;
    }
}