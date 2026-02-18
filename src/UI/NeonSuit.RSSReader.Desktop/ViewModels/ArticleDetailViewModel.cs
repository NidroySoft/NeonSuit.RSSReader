using CommunityToolkit.Mvvm.ComponentModel;
using NeonSuit.RSSReader.Core.Models;
using NeonSuit.RSSReader.Desktop.ViewModels.Base;

namespace NeonSuit.RSSReader.Desktop.ViewModels
{
    public partial class ArticleDetailViewModel : BaseViewModel
    {
        [ObservableProperty]
        private Article _article = new();

        // En WPF usamos un string simple para el contenido 
        // o un formato que el control WebView2 pueda entender
        [ObservableProperty]
        private string _htmlContent = string.Empty;

        public ArticleDetailViewModel()
        {
            // Constructor vacío para el diseñador si hace falta
        }

        // Método para cargar el artículo (lo llamarás desde el MainViewModel)
        public void LoadArticle(Article article)
        {
            if (article == null) return;

            Article = article;
            Title = article.Title;

            // Aquí generamos el HTML igual que hacías en MAUI
            // Pero adaptado a un string para el WebView2 de WPF
            string textColor = "#FFFFFF"; // Neon Style por defecto
            string bgColor = "#121212";

            var style = $"<style>body {{ background-color: {bgColor}; color: {textColor}; font-family: 'Segoe UI', sans-serif; padding: 20px; line-height: 1.6; }} img {{ max-width: 100%; height: auto; border-radius: 8px; }} a {{ color: #00ffcc; }}</style>";

            HtmlContent = $"<html><head>{style}</head><body><h1>{article.Title}</h1>{article.Content ?? article.Summary}</body></html>";
        }
    }
}