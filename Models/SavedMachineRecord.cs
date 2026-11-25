using System.Text.Json;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using TuringMachineVisualizer.Core;

namespace TuringMachineVisualizer.Models;

[Table("saved_machines")]
public class SavedMachineRecord : BaseModel
{
    [PrimaryKey("id", false)]
    public long Id { get; set; }

    [Column("user_id")]
    public string UserId { get; set; } = "";

    [Column("title")]
    public string Title { get; set; } = "";

    [Column("description")]
    public string Description { get; set; } = "";

    [Column("blank_symbol")]
    public string BlankSymbol { get; set; } = "_";

    [Column("initial_state")]
    public string InitialState { get; set; } = "q0";

    [Column("accept_state")]
    public string AcceptState { get; set; } = "qAccept";

    [Column("reject_state")]
    public string RejectState { get; set; } = "qReject";

    [Column("sample_input")]
    public string SampleInput { get; set; } = "";

    [Column("definition_json")]
    public JsonElement DefinitionJson { get; set; }

    [Column("deleted_at")]
    public DateTime? DeletedAt { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    public MachineDefinition ToDefinition()
    {
        var def =
            DefinitionJson.ValueKind == JsonValueKind.Object
                ? JsonSerializer.Deserialize<MachineDefinition>(DefinitionJson.GetRawText())
                : null;

        return def
            ?? new MachineDefinition
            {
                Title = Title,
                Description = Description,
                Blank = string.IsNullOrEmpty(BlankSymbol) ? '_' : BlankSymbol[0],
                InitialState = InitialState,
                AcceptState = AcceptState,
                RejectState = RejectState,
                SampleInput = SampleInput,
            };
    }

    public static SavedMachineRecord FromDefinition(string userId, MachineDefinition def)
    {
        var jsonElement = JsonSerializer.SerializeToElement(def);
        return new SavedMachineRecord
        {
            UserId = userId,
            Title = def.Title,
            Description = def.Description,
            BlankSymbol = def.Blank.ToString(),
            InitialState = def.InitialState,
            AcceptState = def.AcceptState,
            RejectState = def.RejectState,
            SampleInput = def.SampleInput,
            DefinitionJson = jsonElement,
        };
    }
}
