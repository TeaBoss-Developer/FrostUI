using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using FrostUI.Designer.Models;

namespace FrostUI.Designer.Services;

public static class DesignSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters =
        {
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase),
        },
    };

    public static string Serialize(DesignDocument document) =>
        JsonSerializer.Serialize(document, Options);

    public static DesignDocument Deserialize(string json)
    {
        var root = JsonNode.Parse(json) as JsonObject
            ?? throw new InvalidDataException("设计文件为空或格式错误。");
        if (!root.ContainsKey("pages"))
        {
            var page = new JsonObject
            {
                ["id"] = Guid.NewGuid().ToString("N"),
                ["name"] = "主页面",
                ["controls"] = root["controls"]?.DeepClone() ?? new JsonArray(),
                ["guides"] = root["guides"]?.DeepClone() ?? new JsonArray(),
                ["scripts"] = root["windowScripts"]?.DeepClone() ?? new JsonObject(),
            };
            root.Remove("controls");
            root.Remove("guides");
            root.Remove("windowScripts");
            root["pages"] = new JsonArray(page);
            root["activePageIndex"] = 0;
        }

        return root.Deserialize<DesignDocument>(Options)
            ?? throw new InvalidDataException("设计文件为空或格式错误。");
    }

    public static void Save(DesignDocument document, string path)
    {
        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temporary = fullPath + ".tmp";
        File.WriteAllText(temporary, Serialize(document));
        File.Move(temporary, fullPath, overwrite: true);
    }

    public static DesignDocument Load(string path) =>
        Deserialize(File.ReadAllText(path));

    public static DesignDocument Clone(DesignDocument document) =>
        Deserialize(Serialize(document));
}
