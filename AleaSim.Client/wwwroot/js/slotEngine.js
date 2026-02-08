
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
        
        // Convert Server (Row-Major) to Reel (Col-Major)
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
            
            setTimeout(() => {
                reel.stopping = true;
            }, 1000 + i * 400); // Staggered stops
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
                    
                    let nextId = Math.floor(Math.random() * 8) + 1; // Default random
                    
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
                            targetId = Math.floor(Math.random() * 8) + 1; // Buffer
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

        if (activeReels === 0) window.slotEngine.running = false;
    }
};
