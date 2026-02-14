using System;

namespace AleaSim.Client.Services;

public class LayoutService
{
    public bool IsDarkMode { get; private set; } = true;

    public event Action? OnMajorUpdate;

    public void SetDarkMode(bool value)
    {
        IsDarkMode = value;
        OnMajorUpdate?.Invoke();
    }

    public void ToggleDarkMode()
    {
        IsDarkMode = !IsDarkMode;
        OnMajorUpdate?.Invoke();
    }
}
