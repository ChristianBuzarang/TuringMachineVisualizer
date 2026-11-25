namespace TuringMachineVisualizer.Core;

public enum Move
{
    Left,
    Right,
    Stay,
}

public enum RunStatus
{
    Ready,
    Running,
    Accepted,
    Rejected,
    StepLimitReached,
}

/// <summary>One row of the transition table: (State, Read) -> (NextState, Write, Move).</summary>
public sealed record TransitionRule(
    string State,
    char Read,
    string NextState,
    char Write,
    Move Move
);

/// <summary>
/// Serializable machine definition. Rules are a flat list (not a dictionary with tuple keys)
/// so it round-trips cleanly to JSON for the Supabase definition_json column.
/// </summary>
public sealed class MachineDefinition
{
    public string Title { get; init; } = "";
    public string Description { get; init; } = "";
    public char Blank { get; init; } = '_';
    public string InitialState { get; init; } = "q0";
    public string AcceptState { get; init; } = "qAccept";
    public string RejectState { get; init; } = "qReject";
    public string SampleInput { get; init; } = "";
    public List<TransitionRule> Rules { get; init; } = new();
}

public sealed class TapeEngine
{
    // Everything needed to undo one step.
    private readonly record struct StepRecord(TransitionRule Rule, int Head, char ReadSymbol);

    private readonly MachineDefinition _def;
    private readonly Dictionary<(string State, char Read), TransitionRule> _table;
    private readonly Dictionary<int, char> _tape = new(); // sparse: blanks are not stored
    private readonly Stack<StepRecord> _history = new();

    public TapeEngine(MachineDefinition definition, string input, int maxSteps = 10_000)
    {
        _def = definition;
        MaxSteps = maxSteps;

        _table = new();
        foreach (var r in definition.Rules)
        {
            if (!_table.TryAdd((r.State, r.Read), r))
                throw new ArgumentException(
                    $"Nondeterministic rule: duplicate transition for ({r.State}, '{r.Read}')."
                );
        }

        Load(input);
    }

    public int MaxSteps { get; set; }
    public string State { get; private set; } = "";
    public int Head { get; private set; }
    public RunStatus Status { get; private set; }
    public int StepCount => _history.Count;
    public bool CanStepBack => _history.Count > 0;
    public bool IsHalted =>
        Status is RunStatus.Accepted or RunStatus.Rejected or RunStatus.StepLimitReached;

    /// <summary>The rule applied by the most recent step (for highlighting in the UI).</summary>
    public TransitionRule? LastRule => _history.Count > 0 ? _history.Peek().Rule : null;

    /// <summary>The rule that will fire next, or null if none (implicit reject).</summary>
    public TransitionRule? NextRule =>
        _table.TryGetValue((State, ReadAt(Head)), out var r) ? r : null;

    public char ReadAt(int index) => _tape.TryGetValue(index, out var c) ? c : _def.Blank;

    // ---- Control -------------------------------------------------------

    /// <summary>Resets to the initial configuration for the given input (head on cell 0).</summary>
    public void Load(string input)
    {
        _tape.Clear();
        _history.Clear();
        for (int i = 0; i < input.Length; i++)
            WriteAt(i, input[i]);

        State = _def.InitialState;
        Head = 0;
        UpdateStatus();
    }

    /// <summary>Executes one transition. Returns false if the machine is already halted.</summary>
    public bool StepForward()
    {
        if (IsHalted)
            return false;

        var read = ReadAt(Head);
        var rule = _table[(State, read)]; // UpdateStatus guarantees this exists when not halted

        _history.Push(new StepRecord(rule, Head, read));

        WriteAt(Head, rule.Write);
        Head += rule.Move switch
        {
            Move.Left => -1,
            Move.Right => 1,
            _ => 0,
        };
        State = rule.NextState;

        UpdateStatus();
        return true;
    }

    /// <summary>Undoes the last step. Returns false if at the start.</summary>
    public bool StepBack()
    {
        if (_history.Count == 0)
            return false;

        var rec = _history.Pop();
        WriteAt(rec.Head, rec.ReadSymbol); // restore the overwritten symbol
        Head = rec.Head;
        State = rec.Rule.State;

        UpdateStatus();
        return true;
    }

    /// <summary>Runs until halt or the step limit. Returns the final status.</summary>
    public RunStatus RunToHalt()
    {
        while (StepForward()) { }
        return Status;
    }

    // ---- View helpers --------------------------------------------------

    /// <summary>Cells in [Head - radius, Head + radius] for rendering a fixed-width tape window.</summary>
    public IEnumerable<(int Index, char Symbol)> VisibleCells(int radius = 10)
    {
        for (int i = Head - radius; i <= Head + radius; i++)
            yield return (i, ReadAt(i));
    }

    /// <summary>Non-blank tape contents from the leftmost to the rightmost written cell.</summary>
    public string TapeContents()
    {
        if (_tape.Count == 0)
            return "";
        int min = _tape.Keys.Min(),
            max = _tape.Keys.Max();
        return new string(Enumerable.Range(min, max - min + 1).Select(ReadAt).ToArray());
    }

    // ---- Internals -----------------------------------------------------

    private void WriteAt(int index, char symbol)
    {
        if (symbol == _def.Blank)
            _tape.Remove(index);
        else
            _tape[index] = symbol;
    }

    // Status is derived from the current configuration, so StepBack needs no extra bookkeeping.
    private void UpdateStatus()
    {
        if (State == _def.AcceptState)
            Status = RunStatus.Accepted;
        else if (State == _def.RejectState)
            Status = RunStatus.Rejected;
        else if (!_table.ContainsKey((State, ReadAt(Head))))
            Status = RunStatus.Rejected; // implicit reject
        else if (_history.Count >= MaxSteps)
            Status = RunStatus.StepLimitReached;
        else
            Status = _history.Count == 0 ? RunStatus.Ready : RunStatus.Running;
    }
}

public static class Presets
{
    /// <summary>
    /// Flips a &lt;-&gt; b between the '#' delimiters. Input "#abb#" ends as "#baa#".
    /// </summary>
    public static MachineDefinition AbbInverter =>
        new()
        {
            Title = "a/b Inverter",
            Description = "Swaps every a with b and every b with a between # delimiters.",
            SampleInput = "#abb#",
            Rules =
            {
                new("q0", '#', "q1", '#', Move.Right), // skip left delimiter
                new("q1", 'a', "q1", 'b', Move.Right), // flip a -> b
                new("q1", 'b', "q1", 'a', Move.Right), // flip b -> a
                new("q1", '#', "qAccept", '#', Move.Stay), // right delimiter: done
            },
        };
}

/* Quick check (e.g. in a console app or xUnit test):

   var tm = new TapeEngine(Presets.AbbInverter, "#abb#");
   tm.RunToHalt();
   Console.WriteLine($"{tm.Status} {tm.TapeContents()} in {tm.StepCount} steps");
   // Accepted #baa# in 5 steps

   tm.StepBack();   // undo last step; State = q1, Head = 4
*/
