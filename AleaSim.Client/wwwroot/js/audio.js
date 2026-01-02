window.aleaAudio = {
    sounds: {},
    init: function () {
        this.sounds['spin'] = new Audio('https://assets.mixkit.co/active_storage/sfx/2013/2013-preview.mp3');
        this.sounds['win'] = new Audio('https://assets.mixkit.co/active_storage/sfx/1435/1435-preview.mp3');
        this.sounds['bigwin'] = new Audio('https://assets.mixkit.co/active_storage/sfx/1431/1431-preview.mp3');
        this.sounds['click'] = new Audio('https://assets.mixkit.co/active_storage/sfx/2568/2568-preview.mp3');
    },
    play: function (name) {
        if (this.sounds[name]) {
            this.sounds[name].currentTime = 0;
            this.sounds[name].play().catch(e => console.log("Audio play blocked", e));
        }
    }
};
