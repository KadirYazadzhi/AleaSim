window.aleaAudio = {
    sounds: {},
    init: function () {
        this.sounds['spin'] = new Audio('sounds/spin.mp3');
        this.sounds['win'] = new Audio('sounds/win.mp3');
        this.sounds['bigwin'] = new Audio('sounds/bigwin.mp3');
        this.sounds['click'] = new Audio('sounds/click.mp3');
    },
    play: function (name) {
        if (this.sounds[name]) {
            this.sounds[name].currentTime = 0;
            this.sounds[name].play().catch(e => console.log("Audio play blocked", e));
        }
    }
};
