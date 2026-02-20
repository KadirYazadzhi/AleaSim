using Microsoft.JSInterop;

namespace AleaSim.Client.Services;

public class AudioService {
    private readonly IJSRuntime _js;
    public bool IsMuted { get; set; } = false;
    public double MasterVolume { get; set; } = 0.5;
    public double MusicVolume { get; set; } = 0.3;
    public double EffectsVolume { get; set; } = 0.7;

    public AudioService(IJSRuntime js) {
        _js = js;
    }

    public async Task Init() => await _js.InvokeVoidAsync("aleaAudio.init");
    
    public async Task PlaySpin() { if (!IsMuted) await _js.InvokeVoidAsync("aleaAudio.play", "spin", EffectsVolume * MasterVolume); }
    public async Task PlayWin() { if (!IsMuted) await _js.InvokeVoidAsync("aleaAudio.play", "win", EffectsVolume * MasterVolume); }
    public async Task PlayBigWin() { if (!IsMuted) await _js.InvokeVoidAsync("aleaAudio.play", "bigwin", EffectsVolume * MasterVolume); }
    public async Task PlayBonus() { if (!IsMuted) await _js.InvokeVoidAsync("aleaAudio.play", "bigwin", EffectsVolume * MasterVolume); }
    public async Task PlayClick() { if (!IsMuted) await _js.InvokeVoidAsync("aleaAudio.play", "click", EffectsVolume * MasterVolume); }
}
