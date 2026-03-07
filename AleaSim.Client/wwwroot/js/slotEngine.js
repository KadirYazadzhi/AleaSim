
window.slotEngine = {
    app: null,
    reels: [],
    textures: {},
    winGraphics: null, // Layer for win lines
    symbolSize: 100,
    reelWidth: 160,
    rows: 4,
    cols: 5,
    paylines: [
        [0,0,0,0,0], [1,1,1,1,1], [2,2,2,2,2], [3,3,3,3,3],
        [0,1,2,1,0], [1,2,3,2,1], [2,1,0,1,2], [3,2,1,2,3],
        [0,1,0,1,0], [1,0,1,0,1], [2,3,2,3,2], [3,2,3,2,3],
        [0,0,1,0,0], [1,1,2,1,1], [2,2,3,2,2], [1,1,0,1,1],
        [2,2,1,2,2], [1,0,0,0,1], [2,3,3,3,2], [0,1,1,1,0]
    ],
    running: false,
    performance: {
        turbo: false,
        lowGraphics: false
    },

    setPerformanceMode: (turbo, lowGraphics) => {
        window.slotEngine.performance.turbo = turbo;
        window.slotEngine.performance.lowGraphics = lowGraphics;
        
        if (window.slotEngine.app) {
            // Adjust antialias based on performance preference
            window.slotEngine.app.renderer.plugins.interaction.interactionFrequency = lowGraphics ? 10 : 30;
        }
    },

    init: async (containerId, turbo = false, lowGraphics = false) => {
        window.slotEngine.performance.turbo = turbo;
        window.slotEngine.performance.lowGraphics = lowGraphics;

        const el = document.getElementById(containerId);
        if (!el) return;
        el.innerHTML = '';

        window.slotEngine.app = new PIXI.Application({
            width: 800,
            height: 480,
            backgroundColor: 0x101010,
            antialias: !lowGraphics
        });
        el.appendChild(window.slotEngine.app.view);

        window.slotEngine.winGraphics = new PIXI.Graphics();
        window.slotEngine.app.stage.addChild(window.slotEngine.winGraphics);

        // Load Textures using PIXI.Assets (v7+)
        const symbolFiles = {
            1: 'cherries.png',
            2: 'lemon.png',
            3: 'orange.png',
            4: 'plum.png',
            5: 'grape.png',
            6: 'watermelon.png',
            7: 'apple.png',
            8: 'clover.png',
            9: 'bell.png',
            10: 'star.png',
            11: 'coin.png',
            12: 'seven.png'
        };

        for (const [id, file] of Object.entries(symbolFiles)) {
            PIXI.Assets.add(`sym${id}`, `images/slots/${file}`);
        }

        const keys = Object.keys(symbolFiles).map(id => `sym${id}`);
        
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
                const id = Math.floor(Math.random() * 12) + 1;
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
        window.slotEngine.clearWinLines(); // Clear previous lines
        
        const data = JSON.parse(resultJson);
        const grid = data.Grid; 
        window.slotEngine.lastWinningLines = data.WinningLines || [];
        // Grid[Row][Col] -> Reel[Col][Row]
        const finalSymbols = [];
        for(let c=0; c < 5; c++) {
            const colSyms = [];
            for(let r=0; r < 4; r++) {
                colSyms.push(grid[r][c]);
            }
            finalSymbols.push(colSyms);
        }

        window.slotEngine.reels.forEach((reel, i) => {
            reel.speed = 30 + i * 5; // Faster spin
            reel.targetSymbols = finalSymbols[i];
            reel.stopping = false;
            reel.isStopped = false; // Track state
            
            const stopDelay = window.slotEngine.performance.turbo ? (200 + i * 100) : (1000 + i * 400);
            
            setTimeout(() => {
                reel.stopping = true;
            }, stopDelay); // Staggered stops
        });
    },

    update: (delta) => {
        if (!window.slotEngine.running) return;
        
        let activeReels = 0;

        window.slotEngine.reels.forEach((reel, reelIdx) => {
            if (reel.isStopped) return;
            activeReels++;

            reel.symbols.forEach(s => {
                s.sprite.y += reel.speed * delta;
            });

            // Loop logic
            const limit = 4 * 100;
            const bufferY = -100; // Position of the top buffer symbol

            reel.symbols.forEach(s => {
                if (s.sprite.y >= limit) {
                    s.sprite.y = bufferY + (s.sprite.y - limit); // Wrap smoothly
                    
                    // Logic for mapping symbols
                    // Symbols are physically ordered by Y. We need to know which 'slot' this sprite occupies.
                    // Visual slots: 0 (top), 1, 2, 3. 
                    // However, sprites cycle. 
                    
                    let nextId = Math.floor(Math.random() * 12) + 1; // Default random
                    
                    if (reel.stopping) {
                        // When stopping, we want to start injecting the target symbols.
                        // But strictly mapping moving sprites to target indices is complex in a simple loop.
                        // SIMPLE RELIABLE HACK: 
                        // Just randomize during spin. When speed hits 0, FORCE replace textures at exact Y positions.
                    }
                    
                    const tex = window.slotEngine.textures[`sym${nextId}`];
                    if (tex && s.sprite instanceof PIXI.Sprite) s.sprite.texture = tex;
                }
            });
            
            if (reel.stopping) {
                // Decelerate
                if (reel.speed > 0) {
                    // Snap to grid
                    // If we are close to alignment (modulo symbolSize) AND speed is low enough, SNAP.
                    // But simpler: Just lerp speed to 0.
                    
                    // Instant Snap Logic for mapping accuracy:
                    // 1. Slow down
                    // 2. When speed is low, Hard Stop and Swap Textures.
                    
                    // Decay speed
                    // reel.speed -= 1 * delta;
                    
                    // Let's rely on time-based hard stop for visual accuracy in this prototype
                    // Actually, let's just stop immediately for precision if mapped.
                    reel.speed = 0;
                    reel.isStopped = true;
                    
                    // Force positioning and textures
                    reel.symbols.forEach((s, idx) => {
                        // We need to re-sort symbols by Y to know who is top
                        // But symbols array order might not match Y order due to cycling.
                        // Reset Y based on index is safest for a "hard stop" feel.
                        s.sprite.y = idx * 100;
                        
                        // Map Texture (0-3 visible, 4 is buffer)
                        let targetId = 1;
                        if (idx < 4 && reel.targetSymbols) {
                            targetId = reel.targetSymbols[idx];
                        } else {
                            targetId = Math.floor(Math.random() * 12) + 1; // Buffer
                        }
                        
                        const tex = window.slotEngine.textures[`sym${targetId}`];
                        if (tex && s.sprite instanceof PIXI.Sprite) s.sprite.texture = tex;
                    });

                    // Play Click Sound
                    if (window.aleaAudio && window.aleaAudio.play) {
                        window.aleaAudio.play('click');
                    }
                    
                    // Add bounce effect (visual polish)
                    const container = reel.container;
                    container.y = 20; // Push down
                    // Simple manual tween or rely on next frame
                    // We can use a simple decay variable on the container itself if we had a tween engine
                    // But we have GSAP loaded in index.html!
                    if (window.gsap) {
                        gsap.to(container, {y: 0, duration: 0.3, ease: "bounce.out"});
                    }
                }
            }
        });

        if (activeReels === 0) {
            window.slotEngine.running = false;
            if (window.slotEngine.lastWinningLines && window.slotEngine.lastWinningLines.length > 0) {
                window.slotEngine.drawWinLines(window.slotEngine.lastWinningLines);
            }
        }
    },

    clearWinLines: () => {
        if (window.slotEngine.winGraphics) {
            window.slotEngine.winGraphics.clear();
        }
    },

    drawWinLines: (winningLines) => {
        const { winGraphics, paylines, reelWidth, symbolSize } = window.slotEngine;
        winGraphics.clear();
        
        winningLines.forEach((win, index) => {
            const lineData = paylines[win.LineIndex];
            if (!lineData) return;

            winGraphics.lineStyle(6, 0x00f2ff, 0.8); // Cyan neon line
            
            // Starting point (center of first symbol)
            const startX = 40 + (reelWidth / 2);
            const startY = 40 + (lineData[0] * symbolSize) + (symbolSize / 2);
            winGraphics.moveTo(startX, startY);

            // Draw through matching columns
            for (let c = 1; c < win.Count; c++) {
                const x = 40 + (c * reelWidth) + (reelWidth / 2);
                const y = 40 + (lineData[c] * symbolSize) + (symbolSize / 2);
                winGraphics.lineTo(x, y);
            }

            // Optional: Glow effect (draw a wider faint line behind)
            winGraphics.lineStyle(12, 0x00f2ff, 0.2);
            winGraphics.moveTo(startX, startY);
            for (let c = 1; c < win.Count; c++) {
                const x = 40 + (c * reelWidth) + (reelWidth / 2);
                const y = 40 + (lineData[c] * symbolSize) + (symbolSize / 2);
                winGraphics.lineTo(x, y);
            }
        });
    }
};
