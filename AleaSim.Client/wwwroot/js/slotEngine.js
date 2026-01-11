
window.slotEngine = {
    app: null,
    container: null,
    initialized: false,
    
    init: (containerId) => {
        const el = document.getElementById(containerId);
        if (!el) return;
        
        // Изчистваме контейнера преди старт
        el.innerHTML = '';
        
        try {
            this.app = new PIXI.Application({
                width: el.offsetWidth || 800,
                height: 450,
                backgroundColor: 0x050505,
                antialias: true
            });
            el.appendChild(this.app.view);
            
            // Създаваме временна визуализация, за да се вижда че работи
            const graphics = new PIXI.Graphics();
            graphics.beginFill(0x1a1a1a);
            graphics.drawRect(0, 0, 800, 450);
            this.app.stage.addChild(graphics);
            
            const text = new PIXI.Text('READY TO SPIN', {fill: 0xffffff, fontSize: 32});
            text.anchor.set(0.5);
            text.x = 400; text.y = 225;
            this.app.stage.addChild(text);
            
            console.log('PixiJS Slot Engine Initialized');
            window.slotEngine.initialized = true;
        } catch (e) {
            console.error('PixiJS failed, using CSS fallback', e);
            el.innerHTML = '<div style="color:white; padding:20px;">Graphics Engine Loading...</div>';
        }
    },

    spin: (results) => {
        if (!window.slotEngine.initialized) return;
        // Проста GSAP анимация за целия Canvas
        gsap.to(this.app.stage, {
            alpha: 0.5,
            duration: 0.1,
            repeat: 20,
            yoyo: true,
            onComplete: () => this.app.stage.alpha = 1
        });
    }
};
