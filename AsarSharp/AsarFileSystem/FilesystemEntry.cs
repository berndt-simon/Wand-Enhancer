using System.Collections.Generic;
using System.Text.Json.Serialization;
using AsarSharp.Integrity;

namespace AsarSharp.AsarFileSystem;

public class FilesystemEntry
{
    [JsonPropertyName("files")]
    public Dictionary<string, FilesystemEntry>? Files { get; set; }


    [JsonPropertyName("executable")]
    public bool? Executable { get; set; }

    [JsonPropertyName("size")]
    public long? Size { get; set; }

    [JsonPropertyName("offset")]
    public string? Offset { get; set; }

    // Serialization surrogate: emit "unpacked" only when true (replaces Newtonsoft ShouldSerializeUnpacked).
    // With JsonIgnoreCondition.WhenWritingDefault, a null value is omitted and true is written.
    [JsonPropertyName("unpacked")]
    public bool? UnpackedSerialized
    {
        get => Unpacked == true ? true : (bool?)null;
        set => Unpacked = value;
    }

    [JsonPropertyName("integrity")]
    public IntegrityHelper.FileIntegrity? Integrity { get; set; }

    [JsonPropertyName("link")]
    public string? Link { get; set; }

    [JsonIgnore]
    public bool? Unpacked { get; set; }

    [JsonIgnore]
    public bool IsDirectory => Files != null;
    [JsonIgnore]
    public bool IsFile => Size.HasValue;

    [JsonIgnore]
    public bool IsLink => Link != null;

    public override string ToString()
    {
        return $"Offset: {Offset}, Size: {Size} Unpacked: {Unpacked}";
    }
}