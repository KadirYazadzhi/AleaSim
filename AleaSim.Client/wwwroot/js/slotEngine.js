
window.slotEngine = {
    app: null,
    reels: [],
    textures: {},
    symbolSize: 100,
    reelWidth: 160,
    rows: 4,
    cols: 5,
    running: false,

    init: async (containerId) => {
        const el = document.getElementById(containerId);
        if (!el) return;
        el.innerHTML = '';

        window.slotEngine.app = new PIXI.Application({
            width: 800,
            height: 480,
            backgroundColor: 0x101010,
            antialias: true
        });
        el.appendChild(window.slotEngine.app.view);

        // Load Textures using PIXI.Assets (v7+)
        for (let i = 1; i <= 12; i++) {
            if (i === 9) continue; 
            PIXI.Assets.add(`sym${i}`, `images/slots/${i}.png`);
        }

        const keys = [];
        for(let i=1; i<=12; i++) { if(i!==9) keys.push(`sym${i}`); }
        
        try {
            window.slotEngine.textures = await PIXI.Assets.load(keys);
            window.slotEngine.buildGrid();
        } catch (e) {
            console.error("Failed to load assets", e);
        }
    },

    buildGrid: () => {
        const { app, cols, reelWidth, symbolSize } = window.slotEngine;
        const container = new PIXI.Container();
        container.x = 40; 
        container.y = 40;
        app.stage.addChild(container);

        for (let c = 0; c < cols; c++) {
            const reelContainer = new PIXI.Container();
            reelContainer.x = c * reelWidth;
            
            const reel = {
                container: reelContainer,
                symbols: [],
                position: 0,
                targetPosition: 0,
                speed: 0
            };

            for (let r = 0; r < 5; r++) { 
                const id = Math.floor(Math.random() * 8) + 1;
                const tex = window.slotEngine.textures[`sym${id}`];
                
                // Fallback graphic if texture fails
                let sprite;
                if (tex) {
                    sprite = new PIXI.Sprite(tex);
                } else {
                    sprite = new PIXI.Graphics();
                    sprite.beginFill(0xFF0000);
                    sprite.drawRect(0,0,symbolSize, symbolSize);
                }

                sprite.width = symbolSize;
                sprite.height = symbolSize;
                sprite.x = (reelWidth - symbolSize) / 2;
                sprite.y = r * symbolSize;
                reelContainer.addChild(sprite);
                reel.symbols.push({ sprite, id });
            }
            
            container.addChild(reelContainer);
            window.slotEngine.reels.push(reel);
        }
        
        app.ticker.add((delta) => window.slotEngine.update(delta));
    },

    spin: (resultJson) => {
        if (window.slotEngine.running) return;
        window.slotEngine.running = true;
        
        const data = JSON.parse(resultJson);
        const grid = data.Grid; 
        
        const finalSymbols = [];
        for(let c=0; c < 5; c++) {
            const colSyms = [];
            for(let r=0; r < 4; r++) {
                colSyms.push(grid[r][c]);
            }
            finalSymbols.push(colSyms);
        }

        window.slotEngine.reels.forEach((reel, i) => {
            reel.speed = 20 + i * 2;
            reel.targetSymbols = finalSymbols[i];
            reel.stopping = false;
            
            setTimeout(() => {
                reel.stopping = true;
            }, 1500 + i * 300);
        });
    },

    update: (delta) => {
        if (!window.slotEngine.running) return;
        
        let allStopped = true;

        window.slotEngine.reels.forEach(reel => {
            if (reel.speed > 0) {
                allStopped = false;
                reel.symbols.forEach(s => {
                    s.sprite.y += reel.speed * delta;
                });

                const limit = 4 * 100;
                reel.symbols.forEach(s => {
                    if (s.sprite.y >= limit) {
                        s.sprite.y -= 500; 
                        
                        let nextId;
                        if (reel.stopping) {
                            const targetIndex = 4 - Math.round(s.sprite.y / 100); 
                            // Simplified random fill until exact snap
                            nextId = Math.floor(Math.random() * 8) + 1;
                        } else {
                            nextId = Math.floor(Math.random() * 8) + 1;
                        }
                        
                        s.id = nextId;
                        const tex = window.slotEngine.textures[`sym${nextId}`];
                        if (tex && s.sprite instanceof PIXI.Sprite) s.sprite.texture = tex;
                    }
                });
                
                if (reel.stopping) {
                    if (reel.speed > 0) reel.speed -= 0.5 * delta;
                    if (reel.speed <= 0) {
                        reel.speed = 0;
                        reel.symbols.forEach((s, idx) => {
                            s.sprite.y = idx * 100;
                            if (idx < 4 && reel.targetSymbols) {
                                const finalId = reel.targetSymbols[idx];
                                const tex = window.slotEngine.textures[`sym${finalId}`];
                                if (tex && s.sprite instanceof PIXI.Sprite) s.sprite.texture = tex;
                            }
                        });
                    }
                }
            }
        });

        if (allStopped) window.slotEngine.running = false;
    }
};
