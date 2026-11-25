using Supabase.Gotrue;
using TuringMachineVisualizer.Core;
using TuringMachineVisualizer.Models;

namespace TuringMachineVisualizer.Services;

public sealed class SupabaseService
{
    private readonly Supabase.Client _client;
    private readonly LocalStorageSessionHandler _sessionHandler;
    private bool _initialized;

    public SupabaseService(Supabase.Client client, LocalStorageSessionHandler sessionHandler)
    {
        _client = client;
        _sessionHandler = sessionHandler;
    }

    public event Action? AuthStateChanged;

    public User? CurrentUser => _client.Auth.CurrentUser;
    public bool IsAuthenticated => CurrentUser is not null;

    public async Task InitializeAsync()
    {
        if (_initialized)
            return;
        _initialized = true;

        await _client.InitializeAsync();

        var savedSession = await _sessionHandler.LoadSessionAsync();
        if (savedSession?.AccessToken is not null && savedSession.RefreshToken is not null)
        {
            try
            {
                await _client.Auth.SetSession(savedSession.AccessToken, savedSession.RefreshToken);
            }
            catch
            {
                _sessionHandler.DestroySession();
            }
        }

        AuthStateChanged?.Invoke();
    }

    public async Task<(bool Success, string? Error)> SignInAsync(string email, string password)
    {
        try
        {
            var session = await _client.Auth.SignIn(email, password);
            if (session?.User is null)
                return (false, "Invalid credentials.");
            _sessionHandler.SaveSession(session);
            AuthStateChanged?.Invoke();
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public async Task<(bool Success, string? Error)> SignUpAsync(
        string email,
        string password,
        string displayName
    )
    {
        try
        {
            var options = new SignUpOptions
            {
                Data = new Dictionary<string, object> { ["display_name"] = displayName },
            };
            var session = await _client.Auth.SignUp(email, password, options);
            if (session?.User is not null)
            {
                _sessionHandler.SaveSession(session);
                AuthStateChanged?.Invoke();
            }
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public async Task SignOutAsync()
    {
        await _client.Auth.SignOut();
        _sessionHandler.DestroySession();
        AuthStateChanged?.Invoke();
    }

    public async Task<List<SavedMachineRecord>> GetMyMachinesAsync()
    {
        if (!IsAuthenticated)
            return new();

        var response = await _client
            .From<SavedMachineRecord>()
            .Filter("deleted_at", Supabase.Postgrest.Constants.Operator.Is, "null")
            .Order("created_at", Supabase.Postgrest.Constants.Ordering.Descending)
            .Get();

        return response.Models;
    }

    public async Task<SavedMachineRecord?> SaveMachineAsync(MachineDefinition definition)
    {
        if (CurrentUser?.Id is null)
            return null;

        var record = SavedMachineRecord.FromDefinition(CurrentUser.Id, definition);
        var response = await _client.From<SavedMachineRecord>().Insert(record);
        return response.Models.FirstOrDefault();
    }

    public async Task DeleteMachineAsync(long id)
    {
        await _client
            .From<SavedMachineRecord>()
            .Where(m => m.Id == id)
            .Set(m => m.DeletedAt, DateTime.UtcNow)
            .Update();
    }
}
