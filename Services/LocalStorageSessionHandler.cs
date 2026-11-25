using System.Text.Json;
using Microsoft.JSInterop;
using Supabase.Gotrue;
using Supabase.Gotrue.Interfaces;

namespace TuringMachineVisualizer.Services;

public sealed class LocalStorageSessionHandler : IGotrueSessionPersistence<Session>
{
    private const string StorageKey = "tm_visualizer_supabase_session";
    private readonly IJSRuntime _js;

    public LocalStorageSessionHandler(IJSRuntime js) => _js = js;

    public void SaveSession(Session session)
    {
        var json = JsonSerializer.Serialize(session);
        _ = _js.InvokeVoidAsync("localStorage.setItem", StorageKey, json);
    }

    public void DestroySession()
    {
        _ = _js.InvokeVoidAsync("localStorage.removeItem", StorageKey);
    }

    public Session? LoadSession()
    {
        // Initial async hydration is handled in SupabaseService.InitializeAsync()
        return null;
    }

    public async Task<Session?> LoadSessionAsync()
    {
        var json = await _js.InvokeAsync<string?>("localStorage.getItem", StorageKey);
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonSerializer.Deserialize<Session>(json);
        }
        catch
        {
            await _js.InvokeVoidAsync("localStorage.removeItem", StorageKey);
            return null;
        }
    }
}
