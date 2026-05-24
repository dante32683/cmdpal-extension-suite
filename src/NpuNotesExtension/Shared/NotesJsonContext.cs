using System.Collections.Generic;
using System.Text.Json.Serialization;
using NpuTools.Notes.Models;

namespace NpuTools.Notes.Shared;

[JsonSerializable(typeof(NotesAppSettings))]
[JsonSerializable(typeof(NotesIndex))]
[JsonSerializable(typeof(List<NoteEntry>))]
[JsonSourceGenerationOptions(WriteIndented = false)]
internal sealed partial class NotesJsonContext : JsonSerializerContext
{
}
