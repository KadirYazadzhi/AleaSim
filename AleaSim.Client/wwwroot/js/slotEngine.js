
window.slotEngine = {
    app: null,
    reels: [],
    textures: {},
    symbolSize: 100,
    reelWidth: 160,
    rows: 4,
    cols: 5,
    running: false,

    init: (containerId) => {
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

        // Load Textures
        const loader = PIXI.Loader.shared;
        for (let i = 1; i <= 12; i++) {
            if (i === 9) continue; // Skip 9 if missing
            loader.add(`sym${i}`, `images/slots/${i}.png`);
        }

        loader.load((loader, resources) => {
            window.slotEngine.textures = resources;
            window.slotEngine.buildGrid();
        });
    },

    buildGrid: () => {
        const { app, cols, reelWidth, symbolSize } = window.slotEngine;
        const container = new PIXI.Container();
        container.x = 40; // Padding
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

            // Init Random Symbols
            for (let r = 0; r < 5; r++) { // 4 visible + 1 buffer
                const id = Math.floor(Math.random() * 8) + 1;
                const sprite = new PIXI.Sprite(window.slotEngine.textures[`sym${id}`].texture);
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
        
        // Render Loop
        app.ticker.add((delta) => window.slotEngine.update(delta));
    },

    spin: (resultJson) => {
        if (window.slotEngine.running) return;
        window.slotEngine.running = true;
        
        const data = JSON.parse(resultJson);
        const grid = data.Grid; // Expected [[r0c0, r0c1...], [r1c0...]] - Server is Row-Major
        
        // Convert Server Row-Major to Reel-Major for animation
        const finalSymbols = [];
        for(let c=0; c < 5; c++) {
            const colSyms = [];
            for(let r=0; r < 4; r++) {
                colSyms.push(grid[r][c]);
            }
            finalSymbols.push(colSyms);
        }

        // Start Spin
        window.slotEngine.reels.forEach((reel, i) => {
            reel.speed = 20 + i * 2;
            reel.targetSymbols = finalSymbols[i];
            reel.stopping = false;
            
            // Trigger stop sequence with delays
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

                // Wrap around
                const limit = 4 * 100;
                reel.symbols.forEach(s => {
                    if (s.sprite.y >= limit) {
                        s.sprite.y -= 500; // Move to top (-100)
                        // Swap texture
                        if (reel.stopping) {
                            // Inject target symbol
                            const targetIndex = 4 - Math.round(s.sprite.y / 100); // Rough logic
                            // Simplified: Just randomize until very last frame or implement strict stack
                            // For prototype: Just random while spinning
                            const rnd = Math.floor(Math.random() * 8) + 1;
                            s.id = rnd;
                            s.sprite.texture = window.slotEngine.textures[`sym${rnd}`].texture;
                        } else {
                            const rnd = Math.floor(Math.random() * 8) + 1;
                            s.sprite.texture = window.slotEngine.textures[`sym${rnd}`].texture;
                        }
                    }
                });
                
                if (reel.stopping) {
                    // Snap logic - Simplified for prototype
                    // In a real engine, we calculate exact distance. 
                    // Here we just stop and force set textures for instant visual fix (cheat)
                    if (reel.speed > 0) reel.speed -= 0.5 * delta;
                    if (reel.speed <= 0) {
                        reel.speed = 0;
                        // Force snap
                        reel.symbols.forEach((s, idx) => {
                            s.sprite.y = idx * 100;
                            // Set Final Texture
                            if (idx < 4 && reel.targetSymbols) {
                                const finalId = reel.targetSymbols[idx];
                                const tex = window.slotEngine.textures[`sym${finalId}`];
                                if (tex) s.sprite.texture = tex.texture;
                            }
                        });
                    }
                }
            }
        });

        if (allStopped) window.slotEngine.running = false;
    }
};
