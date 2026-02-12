using MaterialDesignThemes.Wpf;
using NeonSuit.RSSReader.Core.Models;
using NeonSuit.RSSReader.Desktop.ViewModels;
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace NeonSuit.RSSReader.Desktop.Views;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private GridLength _originalPreviewColumnWidth;
    private bool _isPreviewFullscreen = false;
    private bool _isWebViewInitialized = false;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        InitializeWebView();
        DataContext = viewModel;
        _viewModel = viewModel;

        // Al cargar, lanzamos el comando de refrescar
        this.Loaded += async (s, e) =>
        {
            if (DataContext is MainViewModel)
            {
                await viewModel.RefreshArticlesCommand.ExecuteAsync(null);
            }
        };
    }

    private async void InitializeWebView()
    {
        // Esto prepara el motor de Edge detrás del control
        await MyWebView.EnsureCoreWebView2Async(null);
    }


    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void FullScreenToggle_Checked(object sender, RoutedEventArgs e)
    {
        var mainGrid = (Grid)FindName("MainContentGrid");
        if (mainGrid == null) return;

        var columnDefinitions = mainGrid.ColumnDefinitions;

        // Store original width
        _originalPreviewColumnWidth = columnDefinitions[4].Width;

        // Hide other columns
        columnDefinitions[0].Width = new GridLength(0); // Left sidebar
        columnDefinitions[1].Width = new GridLength(0); // First splitter
        columnDefinitions[2].Width = new GridLength(0); // Center panel
        columnDefinitions[3].Width = new GridLength(0); // Second splitter

        // Expand preview to fill space
        columnDefinitions[4].Width = new GridLength(1, GridUnitType.Star);

        _isPreviewFullscreen = true;
    }

    private void FullScreenToggle_Unchecked(object sender, RoutedEventArgs e)
    {
        if (!_isPreviewFullscreen) return;

        var mainGrid = (Grid)FindName("MainContentGrid");
        if (mainGrid == null) return;

        var columnDefinitions = mainGrid.ColumnDefinitions;

        // Restore original column widths
        columnDefinitions[0].Width = new GridLength(320); // Left sidebar
        columnDefinitions[1].Width = new GridLength(8);   // First splitter
        columnDefinitions[2].Width = new GridLength(400); // Center panel
        columnDefinitions[3].Width = new GridLength(8);   // Second splitter
        columnDefinitions[4].Width = _originalPreviewColumnWidth; // Preview panel

        _isPreviewFullscreen = false;
    }

    private async void HandleArticleSelection(Article article)
    {
        if (MyWebView == null || article?.Link == null) return;

        if (article.Status != Core.Enums.ArticleStatus.Read)
        {
            article.Status = Core.Enums.ArticleStatus.Read;
            // Accedemos al repositorio a través del ViewModel
           // await _viewModel.UpdateArticleStatusAsync(article);
        }

        try
        {
            // Ensure WebView2 is initialized only once
            if (!_isWebViewInitialized)
            {
                await MyWebView.EnsureCoreWebView2Async();
                _isWebViewInitialized = true;
            }

            // Validate URI before navigating
            if (Uri.TryCreate(article.Link, UriKind.Absolute, out Uri uri))
            {
                MyWebView.Source = uri;
            }
            else
            {
                _viewModel.StatusMessage = "URL inválida";
                _viewModel.ShowStatus = true;
            }
        }
        catch (Exception ex)
        {
            _viewModel.StatusMessage = $"Error al cargar: {ex.Message}";
            _viewModel.ShowStatus = true;
        }
    }

    // This method handles article selection changes
    private void ArticlesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count > 0)
        {
            var article = e.AddedItems[0] as Article;
            HandleArticleSelection(article);
        }
    }

    // This method handles feed selection changes
    private void FeedsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // When feed changes, we don't want to clear the WebView
        // The ViewModel will handle refreshing articles for the selected feed
        // WebView should retain the currently displayed article until a new one is selected
    }

    private void FeedOptions_Click(object sender, RoutedEventArgs e)
    {
        var button = sender as Button;
        var feed = button?.DataContext as Feed;
        if (feed != null)
        {
            // Mostrar menú contextual para el feed
            var menu = new ContextMenu();

            menu.Items.Add(new MenuItem
            {
                Header = "Editar feed",
                Icon = new PackIcon { Kind = PackIconKind.Pencil }
            });

            menu.Items.Add(new MenuItem
            {
                Header = "Actualizar ahora",
                Icon = new PackIcon { Kind = PackIconKind.Refresh },
                Command = _viewModel.RefreshArticlesCommand,
                CommandParameter = feed
            });

            menu.Items.Add(new Separator());

            var deleteItem = new MenuItem
            {
                Header = "Eliminar feed",
                Icon = new PackIcon { Kind = PackIconKind.Delete },
                Foreground = Brushes.Red
            };
            deleteItem.Click += (s, args) => DeleteFeed_Click(s, args, feed);
            menu.Items.Add(deleteItem);

            menu.PlacementTarget = button;
            menu.IsOpen = true;
        }
    }

    private async void DeleteFeed_Click(object sender, RoutedEventArgs e, Feed feed)
    {
        if (feed != null)
        {
            // Mostrar diálogo de confirmación
            var result = await DialogHost.Show(new
            {
                Title = "Eliminar Feed",
                Message = $"¿Estás seguro de eliminar '{feed.Title}'?",
                OkButton = "Eliminar",
                CancelButton = "Cancelar"
            }, "DialogHost");

            if (result is bool confirmed && confirmed)
            {
                // Lógica para eliminar el feed
                await _viewModel.DeleteFeedCommand.ExecuteAsync(feed);
            }
        }
    }

    private void OpenInBrowser_Click(object sender, RoutedEventArgs e)
    {
        var button = sender as Button;
        var article = button?.DataContext as Article;
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
                _viewModel.StatusMessage = $"Error al abrir: {ex.Message}";
                _viewModel.ShowStatus = true;
            }
        }
    }

    private void CopyLink_Click(object sender, RoutedEventArgs e)
    {
        var button = sender as Button;
        var article = button?.DataContext as Article;
        if (article?.Link != null)
        {
            Clipboard.SetText(article.Link);
            _viewModel.StatusMessage = "Enlace copiado al portapapeles";
            _viewModel.ShowStatus = true;
        }
    }
}