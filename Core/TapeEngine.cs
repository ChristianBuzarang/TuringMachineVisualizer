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

public enum StartHeadMode
{
    Leftmost,
    RightmostDelimiter,
}

public sealed record TransitionRule(
    string State,
    char Read,
    string NextState,
    char Write,
    Move Move
);

public sealed class MachineDefinition
{
    public string Title { get; init; } = "";
    public string Description { get; init; } = "";
    public char Blank { get; init; } = '_';
    public char Delimiter { get; init; } = '#';
    public StartHeadMode StartHead { get; init; } = StartHeadMode.Leftmost;
    public string InitialState { get; init; } = "q0";
    public string AcceptState { get; init; } = "qAccept";
    public string RejectState { get; init; } = "qReject";
    public string SampleInput { get; init; } = "";
    public List<TransitionRule> Rules { get; init; } = new();
}

public sealed class TapeEngine
{
    private readonly record struct StepRecord(TransitionRule Rule, int Head, char ReadSymbol);

    private readonly MachineDefinition _def;
    private readonly Dictionary<(string State, char Read), TransitionRule> _table;
    private readonly Dictionary<int, char> _tape = new(); // blanks are not stored
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

    public TransitionRule? LastRule => _history.Count > 0 ? _history.Peek().Rule : null;

    public TransitionRule? NextRule =>
        _table.TryGetValue((State, ReadAt(Head)), out var r) ? r : null;

    public char ReadAt(int index) => _tape.TryGetValue(index, out var c) ? c : _def.Blank;

    public void Load(string input)
    {
        _tape.Clear();
        _history.Clear();
        for (int i = 0; i < input.Length; i++)
            WriteAt(i, input[i]);

        State = _def.InitialState;
        Head = ResolveInitialHead(input);
        UpdateStatus();
    }

    private int ResolveInitialHead(string input)
    {
        if (_def.StartHead == StartHeadMode.RightmostDelimiter)
        {
            int lastDelim = input.LastIndexOf(_def.Delimiter);
            if (lastDelim >= 0)
                return lastDelim;
        }
        return 0;
    }

    public bool StepForward()
    {
        if (IsHalted)
            return false;

        var read = ReadAt(Head);
        var rule = _table[(State, read)];

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

    public bool StepBack()
    {
        if (_history.Count == 0)
            return false;

        var rec = _history.Pop();
        WriteAt(rec.Head, rec.ReadSymbol);
        Head = rec.Head;
        State = rec.Rule.State;

        UpdateStatus();
        return true;
    }

    public RunStatus RunToHalt()
    {
        while (StepForward()) { }
        return Status;
    }

    public IEnumerable<(int Index, char Symbol)> VisibleCells(int radius = 10)
    {
        for (int i = Head - radius; i <= Head + radius; i++)
            yield return (i, ReadAt(i));
    }

    public string TapeContents()
    {
        if (_tape.Count == 0)
            return "";
        int min = _tape.Keys.Min(),
            max = _tape.Keys.Max();
        return new string(Enumerable.Range(min, max - min + 1).Select(ReadAt).ToArray());
    }

    private void WriteAt(int index, char symbol)
    {
        if (symbol == _def.Blank)
            _tape.Remove(index);
        else
            _tape[index] = symbol;
    }

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
    public static MachineDefinition AbbInverter =>
        new()
        {
            Title = "a/b Inverter (Right-to-Left)",
            Description =
                "Starts at the rightmost #, flips a <-> b moving left, and accepts at the leftmost #.",
            StartHead = StartHeadMode.RightmostDelimiter,
            Delimiter = '#',
            SampleInput = "#abb#",
            Rules =
            {
                new("q0", '#', "q1", '#', Move.Left), // start at rightmost #, step left
                new("q1", 'a', "q1", 'b', Move.Left), // flip a -> b going left
                new("q1", 'b', "q1", 'a', Move.Left), // flip b -> a going left
                new("q1", '#', "qAccept", '#', Move.Stay), // leftmost #: done
            },
        };
}
