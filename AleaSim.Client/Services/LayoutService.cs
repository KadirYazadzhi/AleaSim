using System;
using System.Threading.Tasks;
using Blazored.LocalStorage;
using Microsoft.JSInterop;

namespace AleaSim.Client.Services;

public class LayoutService
{
    private readonly ILocalStorageService _localStorage;
    private readonly IJSRuntime _jsRuntime;

    public LayoutService(ILocalStorageService localStorage, IJSRuntime jsRuntime)
    {
        _localStorage = localStorage;
        _jsRuntime = jsRuntime;
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
        await _jsRuntime.InvokeVoidAsync("setTheme", IsDarkMode ? "dark" : "light");
    }

    public async Task SetDarkMode(bool value)
    {
        IsDarkMode = value;
        await _localStorage.SetItemAsync("darkMode", IsDarkMode);
        await _jsRuntime.InvokeVoidAsync("setTheme", IsDarkMode ? "dark" : "light");
        OnMajorUpdate?.Invoke();
    }

    public async Task ToggleDarkMode()
    {
        IsDarkMode = !IsDarkMode;
        await _localStorage.SetItemAsync("darkMode", IsDarkMode);
        await _jsRuntime.InvokeVoidAsync("setTheme", IsDarkMode ? "dark" : "light");
        OnMajorUpdate?.Invoke();
    }
}
