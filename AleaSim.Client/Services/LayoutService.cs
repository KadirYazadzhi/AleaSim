using System;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Blazored.LocalStorage;
using Microsoft.JSInterop;
using MudBlazor;

namespace AleaSim.Client.Services;

public class LayoutService
{
    private readonly ILocalStorageService _localStorage;
    private readonly IJSRuntime _jsRuntime;
    private readonly HttpClient _http;
    private readonly IBrowserViewportService _viewportService;

    public bool IsDarkMode { get; private set; } = true;
    public string CurrentSkin { get; private set; } = "Default";
    public string? AvatarUrl { get; private set; }
    public bool IsDrawerOpen { get; set; } = true;
    public bool IsBalanceUpdateSuppressed { get; private set; }
    public bool IsMobile { get; private set; }

    public MudTheme CurrentTheme { get; private set; }

    public event Action? OnMajorUpdate;
    public event Action<string>? OnAvatarChanged;
    public event Action<AleaSim.Shared.Models.UserDto>? OnProfileChanged;
    public event Action<bool>? OnBalanceUpdateSuppressionChanged;
    public event Action<decimal>? OnOptimisticDeduction;
    public event Action<decimal>? OnOptimisticAdd;

    public LayoutService(ILocalStorageService localStorage, IJSRuntime jsRuntime, HttpClient http, IBrowserViewportService viewportService)
    {
        _localStorage = localStorage;
        _jsRuntime = jsRuntime;
        _http = http;
        _viewportService = viewportService;
        CurrentTheme = CreateDefaultTheme();
    }

    public async Task InitializeAsync()
    {
        IsDarkMode = await _localStorage.ContainKeyAsync("darkMode") ? await _localStorage.GetItemAsync<bool>("darkMode") : true;
        
        // Try to get skin from LocalStorage first for speed, fallback to default
        CurrentSkin = await _localStorage.ContainKeyAsync("platformSkin") ? await _localStorage.GetItemAsync<string>("platformSkin") : "Default";
        
        ApplySkinInternal(CurrentSkin);
        await _jsRuntime.InvokeVoidAsync("setTheme", IsDarkMode ? "dark" : "light");
        UpdateGlobalBackground();

        // Initialize IsMobile using a simple width check
        try {
            var width = await _jsRuntime.InvokeAsync<int>("eval", "window.innerWidth");
            IsMobile = width < 960; // Standard MudBlazor MD breakpoint
        } catch {
            IsMobile = false;
        }

        OnMajorUpdate?.Invoke();
    }

    private void UpdateGlobalBackground() {
        var theme = CurrentTheme;
        var bg = IsDarkMode ? theme.PaletteDark.Background.ToString() : theme.PaletteLight.Background.ToString();
        _jsRuntime.InvokeVoidAsync("aleaUtils.setBackgroundColor", bg);
    }

    public void SetSkinFromProfile(string preferencesJson) {
        try {
            var prefs = System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.Dictionary<string, object>>(preferencesJson);
            if (prefs != null && prefs.TryGetValue("Skin", out var skinObj)) {
                string skin = skinObj.ToString() ?? "Default";
                if (skin != CurrentSkin) {
                    ApplySkinInternal(skin);
                    _localStorage.SetItemAsync("platformSkin", skin);
                    OnMajorUpdate?.Invoke();
                }
            }
            if (prefs != null && prefs.TryGetValue("DarkMode", out var darkObj)) {
                bool dark = darkObj.ToString()?.ToLower() == "true";
                if (dark != IsDarkMode) {
                    IsDarkMode = dark;
                    _localStorage.SetItemAsync("darkMode", dark);
                    _jsRuntime.InvokeVoidAsync("setTheme", dark ? "dark" : "light");
                    OnMajorUpdate?.Invoke();
                }
            }
        } catch {}
    }

    public void ApplySkin(string skin)
    {
        ApplySkinInternal(skin);
        _localStorage.SetItemAsync("platformSkin", skin);
        OnMajorUpdate?.Invoke();
        _ = SyncSettingsWithBackend();
    }

    private void ApplySkinInternal(string skin)
    {
        CurrentSkin = skin;
        var theme = CreateDefaultTheme();

        if (skin == "Vegas")
        {
            theme.PaletteDark.Primary = "#ffD700"; // Gold
            theme.PaletteDark.Secondary = "#b30000"; // Deep Red
            theme.PaletteDark.Background = "#0d0900"; // Very Dark Brown
            theme.PaletteDark.Surface = "#1a1200";
            theme.PaletteDark.AppbarBackground = "rgba(13, 9, 0, 0.9)";
            theme.PaletteDark.DrawerBackground = "rgba(13, 9, 0, 0.98)";
            theme.PaletteDark.TableStriped = "rgba(255, 215, 0, 0.05)";
        }
        else if (skin == "Neon")
        {
            theme.PaletteDark.Primary = "#00f2ff"; // Cyan
            theme.PaletteDark.Secondary = "#ff00ff"; // Magenta
            theme.PaletteDark.Background = "#000000"; // Pure Black
            theme.PaletteDark.Surface = "#0a0a0a";
            theme.PaletteDark.Success = "#39ff14";
            theme.PaletteDark.AppbarBackground = "rgba(0, 0, 0, 0.9)";
            theme.PaletteDark.DrawerBackground = "rgba(5, 5, 5, 0.98)";
            theme.PaletteDark.TableStriped = "rgba(0, 242, 255, 0.05)";
        }
        else if (skin == "Minimal")
        {
            theme.PaletteDark.Primary = "#94a3b8";
            theme.PaletteDark.Secondary = "#64748b";
            theme.PaletteDark.Background = "#0f172a"; 
            theme.PaletteDark.Surface = "#1e293b";
            theme.PaletteDark.AppbarBackground = "rgba(15, 23, 42, 0.85)";
            theme.PaletteDark.DrawerBackground = "rgba(15, 23, 42, 0.95)";
        }
        else // Default / Emerald
        {
            // Background is already #0f172a from CreateDefaultTheme
        }

        CurrentTheme = theme;
        
        // Explicitly update background
        var bg = IsDarkMode ? theme.PaletteDark.Background.ToString() : theme.PaletteLight.Background.ToString();
        _jsRuntime.InvokeVoidAsync("aleaUtils.setBackgroundColor", bg);
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
            Background = "#0f172a", // RESTORED ORIGINAL BLUE
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
        ApplySkinInternal(CurrentSkin); // Refresh theme colors
        OnMajorUpdate?.Invoke();
        await SyncSettingsWithBackend();
    }

    private async Task SyncSettingsWithBackend() {
        try {
            if (!await _localStorage.ContainKeyAsync("authToken")) return;

            var prefs = new System.Collections.Generic.Dictionary<string, string> {
                { "Skin", CurrentSkin },
                { "DarkMode", IsDarkMode.ToString() }
            };
            await _http.PostAsJsonAsync("api/Auth/settings", new { PreferencesJson = System.Text.Json.JsonSerializer.Serialize(prefs) });
        } catch {}
    }

    public void NotifyMajorUpdate() => OnMajorUpdate?.Invoke();
    public void NotifyAvatarChanged(string url) {
        AvatarUrl = url;
        OnAvatarChanged?.Invoke(url);
    }
    public void NotifyProfileChanged(AleaSim.Shared.Models.UserDto profile) => OnProfileChanged?.Invoke(profile);
    public void SetBalanceUpdateSuppression(bool suppressed)
    {
        IsBalanceUpdateSuppressed = suppressed;
        OnBalanceUpdateSuppressionChanged?.Invoke(suppressed);
    }
    public void DeductOptimisticBalance(decimal amount) => OnOptimisticDeduction?.Invoke(amount);
    public void AddOptimisticBalance(decimal amount) => OnOptimisticAdd?.Invoke(amount);
}
