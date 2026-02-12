using System.Text.Json;

namespace NeonSuit.RSSReader.Core.Helpers;

public static class JsonValidationHelper
{
    /// <summary>
    /// Valida que un string sea JSON válido y opcionalmente que sea un array de enteros.
    /// Lanza ArgumentException si es inválido.
    /// </summary>
    public static void EnsureValidJson(string? json, string fieldName, bool expectIntArray = false)
    {
        if (string.IsNullOrWhiteSpace(json))
            return; // null o vacío se considera válido (lista vacía)

        try
        {
            if (expectIntArray)
            {
                var array = JsonSerializer.Deserialize<int[]>(json);
                if (array == null)
                    throw new ArgumentException($"{fieldName} no puede ser null array", fieldName);
            }
            else
            {
                // Solo validar que es JSON válido, sin importar el tipo
                using var doc = JsonDocument.Parse(json);
            }
        }
        catch (JsonException ex)
        {
            throw new ArgumentException($"{fieldName} contiene JSON inválido", fieldName, ex);
        }
    }

    /// <summary>
    /// Versión genérica: valida que el JSON deserialice al tipo esperado.
    /// </summary>
    public static void EnsureValidJson<T>(string? json, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(json))
            return;

        try
        {
            var result = JsonSerializer.Deserialize<T>(json);
            if (result == null)
                throw new ArgumentException($"{fieldName} no puede ser null", fieldName);
        }
        catch (JsonException ex)
        {
            throw new ArgumentException($"{fieldName} contiene JSON inválido para tipo {typeof(T).Name}", fieldName, ex);
        }
    }
}