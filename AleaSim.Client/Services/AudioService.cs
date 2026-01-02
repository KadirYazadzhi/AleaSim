using Microsoft.JSInterop;

namespace AleaSim.Client.Services;

public class AudioService {
    private readonly IJSRuntime _js;

    public AudioService(IJSRuntime js) {
        _js = js;
    }

    public async Task Init() => await _js.InvokeVoidAsync("aleaAudio.init");
    public async Task PlaySpin() => await _js.InvokeVoidAsync("aleaAudio.play", "spin");
    public async Task PlayWin() => await _js.InvokeVoidAsync("aleaAudio.play", "win");
    public async Task PlayBigWin() => await _js.InvokeVoidAsync("aleaAudio.play", "bigwin");
    public async Task PlayClick() => await _js.InvokeVoidAsync("aleaAudio.play", "click");
}
