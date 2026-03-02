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
    public bool IsDrawerOpen { get; set; } = true;
    public bool IsBalanceUpdateSuppressed { get; private set; }

    public event Action? OnMajorUpdate;
    public event Action<string>? OnAvatarChanged; // Added
    public event Action<bool>? OnBalanceUpdateSuppressionChanged;
    public event Action<decimal>? OnOptimisticDeduction;

    public void ToggleDrawer()
    {
        IsDrawerOpen = !IsDrawerOpen;
        OnMajorUpdate?.Invoke();
    }

    public void NotifyAvatarChanged(string newUrl)
    {
        OnAvatarChanged?.Invoke(newUrl);
    }

    public void NotifyMajorUpdate()
    {
        OnMajorUpdate?.Invoke();
    }

    public void SetBalanceUpdateSuppression(bool suppressed)
    {
        IsBalanceUpdateSuppressed = suppressed;
        OnBalanceUpdateSuppressionChanged?.Invoke(suppressed);
    }

    public void DeductOptimisticBalance(decimal amount)
    {
        OnOptimisticDeduction?.Invoke(amount);
    }

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
