using System;
using System.Threading.Tasks;
using Blazored.LocalStorage;
using Microsoft.JSInterop;
using MudBlazor;

namespace AleaSim.Client.Services;

public class LayoutService
{
    private readonly ILocalStorageService _localStorage;
    private readonly IJSRuntime _jsRuntime;

    public bool IsDarkMode { get; private set; } = true;
    public string CurrentSkin { get; private set; } = "Default";
    public bool IsDrawerOpen { get; set; } = true;
    public bool IsBalanceUpdateSuppressed { get; private set; }

    public MudTheme CurrentTheme { get; private set; }

    public event Action? OnMajorUpdate;
    public event Action<string>? OnAvatarChanged;
    public event Action<bool>? OnBalanceUpdateSuppressionChanged;
    public event Action<decimal>? OnOptimisticDeduction;

    public LayoutService(ILocalStorageService localStorage, IJSRuntime jsRuntime)
    {
        _localStorage = localStorage;
        _jsRuntime = jsRuntime;
        CurrentTheme = CreateDefaultTheme();
    }

    public async Task InitializeAsync()
    {
        IsDarkMode = await _localStorage.ContainKeyAsync("darkMode") ? await _localStorage.GetItemAsync<bool>("darkMode") : true;
        CurrentSkin = await _localStorage.ContainKeyAsync("platformSkin") ? await _localStorage.GetItemAsync<string>("platformSkin") : "Default";
        
        ApplySkinInternal(CurrentSkin);
        await _jsRuntime.InvokeVoidAsync("setTheme", IsDarkMode ? "dark" : "light");
        OnMajorUpdate?.Invoke();
    }

    public void ApplySkin(string skin)
    {
        ApplySkinInternal(skin);
        _localStorage.SetItemAsync("platformSkin", skin);
        OnMajorUpdate?.Invoke();
    }

    private void ApplySkinInternal(string skin)
    {
        CurrentSkin = skin;
        var theme = CreateDefaultTheme();

        if (skin == "Vegas")
        {
            theme.PaletteDark.Primary = "#ffD700"; // Gold
            theme.PaletteDark.Secondary = "#b30000"; // Deep Red
            theme.PaletteDark.Background = "#1a0f00"; // Dark Chocolate
            theme.PaletteDark.Surface = "#2d1a00";
            theme.PaletteDark.AppbarBackground = "rgba(26, 15, 0, 0.8)";
            theme.PaletteDark.DrawerBackground = "rgba(26, 15, 0, 0.95)";
        }
        else if (skin == "Neon")
        {
            theme.PaletteDark.Primary = "#00f2ff"; // Cyan
            theme.PaletteDark.Secondary = "#ff00ff"; // Magenta
            theme.PaletteDark.Background = "#050505";
            theme.PaletteDark.Surface = "#111111";
            theme.PaletteDark.Success = "#39ff14";
            theme.PaletteDark.AppbarBackground = "rgba(5, 5, 5, 0.8)";
            theme.PaletteDark.DrawerBackground = "rgba(5, 5, 5, 0.95)";
        }
        else if (skin == "Minimal")
        {
            theme.PaletteDark.Primary = "#94a3b8";
            theme.PaletteDark.Secondary = "#64748b";
            theme.PaletteDark.Background = "#0f172a";
            theme.PaletteDark.Surface = "#1e293b";
        }

        CurrentTheme = theme;
    }

    private MudTheme CreateDefaultTheme() => new MudTheme()
    {
        PaletteLight = new PaletteLight()
        {
            Primary = "#7c3aed",
            Secondary = "#4338ca",
            Info = "#0ea5e9",
            Success = "#059669",
            Warning = "#b45309",
            Error = "#dc2626",
            AppbarBackground = "rgba(255, 255, 255, 0.8)",
            Background = "#f1f5f9",
            Surface = "#ffffff",
            DrawerBackground = "rgba(255, 255, 255, 0.95)",
            TextPrimary = "#0f172a",
            TextSecondary = "#475569",
            ActionDefault = "#7c3aed",
            Divider = "rgba(0,0,0,0.1)"
        },
        PaletteDark = new PaletteDark()
        {
            Primary = "#a855f7",
            Secondary = "#f472b6",
            Info = "#38bdf8",
            Success = "#22c55e",
            Warning = "#fbbf24",
            Error = "#f43f5e",
            Background = "#0f172a",
            Surface = "#1e293b",
            AppbarBackground = "rgba(15, 23, 42, 0.8)",
            DrawerBackground = "rgba(15, 23, 42, 0.95)",
            TextPrimary = "#f1f5f9",
            TextSecondary = "#94a3b8",
            ActionDefault = "#a855f7",
            Divider = "rgba(255,255,255,0.06)"
        },
        Typography = new Typography()
        {
            Default = new DefaultTypography()
            {
                FontFamily = new[] { "Montserrat", "Inter", "Helvetica", "Arial", "sans-serif" },
                FontSize = ".875rem",
                FontWeight = "400",
            },
            H5 = new H5Typography() { FontWeight = "700", LetterSpacing = ".05em" },
            Button = new ButtonTypography() { FontWeight = "600", TextTransform = "none" }
        },
        LayoutProperties = new LayoutProperties()
        {
            DefaultBorderRadius = "8px",
            DrawerWidthLeft = "260px"
        }
    };

    public void ToggleDrawer()
    {
        IsDrawerOpen = !IsDrawerOpen;
        OnMajorUpdate?.Invoke();
    }

    public async Task ToggleDarkMode()
    {
        IsDarkMode = !IsDarkMode;
        await _localStorage.SetItemAsync("darkMode", IsDarkMode);
        await _jsRuntime.InvokeVoidAsync("setTheme", IsDarkMode ? "dark" : "light");
        OnMajorUpdate?.Invoke();
    }

    public void NotifyMajorUpdate() => OnMajorUpdate?.Invoke();
    public void NotifyAvatarChanged(string url) => OnAvatarChanged?.Invoke(url);
    public void SetBalanceUpdateSuppression(bool suppressed)
    {
        IsBalanceUpdateSuppressed = suppressed;
        OnBalanceUpdateSuppressionChanged?.Invoke(suppressed);
    }
    public void DeductOptimisticBalance(decimal amount) => OnOptimisticDeduction?.Invoke(amount);
}
