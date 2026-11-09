using System.Text.Encodings.Web;
using System.Text.Json;

namespace GameProject.Core.Persistence;

/// <summary>
/// Configures high-performance System.Text.Json serialization options,
/// ensuring relaxed escaping for human-readable Vietnamese UTF-8 characters.
/// </summary>
public static class SaveGameJsonContext
{
    public static JsonSerializerOptions CreateOptions()
    {
        return new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping // Preserves Vietnamese UTF-8 without \uXXXX escaping
        };
    }
}
