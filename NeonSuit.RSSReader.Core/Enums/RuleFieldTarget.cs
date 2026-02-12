namespace NeonSuit.RSSReader.Core.Enums
{
    public enum RuleFieldTarget
    {
        Title = 0,
        Content = 1,
        Author = 2,
        Categories = 3,
        AllFields = 4,      // Busca en título Y contenido
        AnyField = 5        // Busca en título O contenido
    }
}