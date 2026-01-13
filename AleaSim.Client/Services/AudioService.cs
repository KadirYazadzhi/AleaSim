using Microsoft.JSInterop;

namespace AleaSim.Client.Services;

public class AudioService {
    private readonly IJSRuntime _js;
    public bool IsMuted { get; set; } = false;

    public AudioService(IJSRuntime js) {
        _js = js;
    }

    public async Task Init() => await _js.InvokeVoidAsync("aleaAudio.init");
    
    public async Task PlaySpin() { if (!IsMuted) await _js.InvokeVoidAsync("aleaAudio.play", "spin"); }
    public async Task PlayWin() { if (!IsMuted) await _js.InvokeVoidAsync("aleaAudio.play", "win"); }
    public async Task PlayBigWin() { if (!IsMuted) await _js.InvokeVoidAsync("aleaAudio.play", "bigwin"); }
    public async Task PlayBonus() { if (!IsMuted) await _js.InvokeVoidAsync("aleaAudio.play", "bigwin"); } // Added
    public async Task PlayClick() { if (!IsMuted) await _js.InvokeVoidAsync("aleaAudio.play", "click"); }
}
