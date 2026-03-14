window.aleaAudio = {
    sounds: {},
    init: function () {
        console.log("Initializing Alea Audio System...");
        const soundFiles = {
            'spin': 'sounds/spin.mp3',
            'win': 'sounds/win.mp3',
            'bigwin': 'sounds/bigwin.mp3',
            'click': 'sounds/click.mp3',
            'explosion': 'sounds/bigwin.mp3' // Fallback to bigwin for now
        };

        for (const [name, path] of Object.entries(soundFiles)) {
            const audio = new Audio(path);
            audio.preload = 'auto';
            audio.load();
            this.sounds[name] = audio;
        }
    },
    play: function (name, volume) {
        const sound = this.sounds[name];
        if (sound) {
            sound.currentTime = 0;
            if (volume !== undefined) {
                sound.volume = Math.max(0, Math.min(1, volume));
            }
            sound.play().catch(e => {
                console.warn(`Audio play blocked for ${name}:`, e.message);
            });
        } else {
            console.error(`Sound not found: ${name}`);
        }
    }
};
