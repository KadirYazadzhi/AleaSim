using System;
using System.Threading.Tasks;
using Blazored.LocalStorage;

namespace AleaSim.Client.Services;

public class LayoutService
{
    private readonly ILocalStorageService _localStorage;

    public LayoutService(ILocalStorageService localStorage)
    {
        _localStorage = localStorage;
    }

    public bool IsDarkMode { get; private set; } = true;

    public event Action? OnMajorUpdate;

    public async Task InitializeAsync()
    {
        if (await _localStorage.ContainKeyAsync("darkMode"))
        {
            IsDarkMode = await _localStorage.GetItemAsync<bool>("darkMode");
            OnMajorUpdate?.Invoke();
        }
    }

    public async Task SetDarkMode(bool value)
    {
        IsDarkMode = value;
        await _localStorage.SetItemAsync("darkMode", IsDarkMode);
        OnMajorUpdate?.Invoke();
    }

    public async Task ToggleDarkMode()
    {
        IsDarkMode = !IsDarkMode;
        await _localStorage.SetItemAsync("darkMode", IsDarkMode);
        OnMajorUpdate?.Invoke();
    }
}
