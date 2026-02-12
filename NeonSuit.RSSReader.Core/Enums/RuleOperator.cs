namespace NeonSuit.RSSReader.Core.Enums
{
    public enum RuleOperator
    {
        Contains = 0,           // Contiene texto
        Equals = 1,             // Igual exacto
        StartsWith = 2,         // Comienza con
        EndsWith = 3,           // Termina con
        NotContains = 4,        // No contiene
        NotEquals = 5,          // No es igual
        Regex = 6,              // Expresión regular
        GreaterThan = 7,        // Mayor que (para fechas)
        LessThan = 8,           // Menor que (para fechas)
        IsEmpty = 9,            // Está vacío
        IsNotEmpty = 10         // No está vacío
    }
}