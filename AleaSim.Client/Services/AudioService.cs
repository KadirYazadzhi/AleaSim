using Microsoft.JSInterop;
using Blazored.LocalStorage;

namespace AleaSim.Client.Services;

public class AudioService {
    private readonly IJSRuntime _js;
    private readonly ILocalStorageService _localStorage;
    
    public bool IsMuted { get; set; } = false;
    public double MasterVolume { get; set; } = 0.5;
    public double MusicVolume { get; set; } = 0.3;
    public double EffectsVolume { get; set; } = 0.7;

    public AudioService(IJSRuntime js, ILocalStorageService localStorage) {
        _js = js;
        _localStorage = localStorage;
    }

    public async Task Init() {
        await _js.InvokeVoidAsync("aleaAudio.init");
        
        // Load persisted settings
        IsMuted = await _localStorage.GetItemAsync<bool>("audio_muted");
        MasterVolume = await _localStorage.ContainKeyAsync("audio_master") ? await _localStorage.GetItemAsync<double>("audio_master") : 0.5;
        EffectsVolume = await _localStorage.ContainKeyAsync("audio_effects") ? await _localStorage.GetItemAsync<double>("audio_effects") : 0.7;
    }

    public async Task SaveSettings() {
        await _localStorage.SetItemAsync("audio_muted", IsMuted);
        await _localStorage.SetItemAsync("audio_master", MasterVolume);
        await _localStorage.SetItemAsync("audio_effects", EffectsVolume);
    }
    
    public async Task PlaySpin() { if (!IsMuted) await _js.InvokeVoidAsync("aleaAudio.play", "spin", EffectsVolume * MasterVolume); }
    public async Task PlayWin() { if (!IsMuted) await _js.InvokeVoidAsync("aleaAudio.play", "win", EffectsVolume * MasterVolume); }
    public async Task PlayBigWin() { if (!IsMuted) await _js.InvokeVoidAsync("aleaAudio.play", "bigwin", EffectsVolume * MasterVolume); }
    public async Task PlayBonus() { if (!IsMuted) await _js.InvokeVoidAsync("aleaAudio.play", "bigwin", EffectsVolume * MasterVolume); }
    public async Task PlayClick() { if (!IsMuted) await _js.InvokeVoidAsync("aleaAudio.play", "click", EffectsVolume * MasterVolume); }
}
