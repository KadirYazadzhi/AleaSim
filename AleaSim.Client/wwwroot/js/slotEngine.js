window.slotEngine = {
    app: null,
    reels: [],
    textures: {},
    winGraphics: null,
    stickyLayer: null,
    reelLayer: null,
    mask: null,
    
    internalWidth: 1000,
    internalHeight: 540,
    symbolSize: 115,
    reelWidth: 180,
    rows: 4,
    cols: 5,
    
    running: false,
    isBonusActive: false,
    stickyBells: [],
    dotNetRef: null,
    performance: { speed: 1, lowGraphics: false },

    paylines: [
        [0,0,0,0,0], [1,1,1,1,1], [2,2,2,2,2], [3,3,3,3,3],
        [0,1,2,1,0], [1,2,3,2,1], [2,1,0,1,2], [3,2,1,2,3],
        [0,1,0,1,0], [1,0,1,0,1], [2,3,2,3,2], [3,2,3,2,3],
        [0,0,1,0,0], [1,1,2,1,1], [2,2,3,2,2], [1,1,0,1,1],
        [2,2,1,2,2], [1,0,0,0,1], [2,3,3,3,2], [0,1,1,1,0]
    ],

    init: async (containerId, speed = 1, lowGraphics = false, dotNetRef) => {
        window.slotEngine.performance.speed = speed;
        window.slotEngine.performance.lowGraphics = lowGraphics;
        window.slotEngine.dotNetRef = dotNetRef;

        const el = document.getElementById(containerId);
        if (!el) return;
        el.innerHTML = '';

        window.slotEngine.app = new PIXI.Application({
            width: window.slotEngine.internalWidth,
            height: window.slotEngine.internalHeight,
            backgroundAlpha: 0,
            antialias: !lowGraphics,
            resolution: window.devicePixelRatio || 1,
            autoDensity: true
        });
        el.appendChild(window.slotEngine.app.view);

        const gridW = window.slotEngine.reelWidth * window.slotEngine.cols;
        const gridH = window.slotEngine.symbolSize * window.slotEngine.rows;
        const gridX = (window.slotEngine.internalWidth - gridW) / 2;
        const gridY = (window.slotEngine.internalHeight - gridH) / 2;

        window.slotEngine.reelLayer = new PIXI.Container();
        window.slotEngine.reelLayer.x = gridX;
        window.slotEngine.reelLayer.y = gridY;

        window.slotEngine.stickyLayer = new PIXI.Container();
        window.slotEngine.stickyLayer.x = gridX;
        window.slotEngine.stickyLayer.y = gridY;

        window.slotEngine.winGraphics = new PIXI.Graphics();
        window.slotEngine.winGraphics.x = gridX;
        window.slotEngine.winGraphics.y = gridY;
        
        window.slotEngine.app.stage.addChild(window.slotEngine.reelLayer);
        window.slotEngine.app.stage.addChild(window.slotEngine.stickyLayer);
        window.slotEngine.app.stage.addChild(window.slotEngine.winGraphics);

        window.slotEngine.mask = new PIXI.Graphics();
        window.slotEngine.mask.beginFill(0xffffff);
        window.slotEngine.mask.drawRect(0, 0, gridW, gridH);
        window.slotEngine.mask.endFill();
        window.slotEngine.reelLayer.mask = window.slotEngine.mask;
        window.slotEngine.reelLayer.addChild(window.slotEngine.mask);

        const symbolFiles = {
            1: 'cherries.png', 2: 'lemon.png', 3: 'orange.png', 4: 'plum.png',
            5: 'grape.png', 6: 'watermelon.png', 7: 'apple.png', 8: 'clover.png',
            9: 'bell.png', 10: 'star.png', 11: 'coin.png', 12: 'seven.png'
        };

        for (const [id, file] of Object.entries(symbolFiles)) {
            PIXI.Assets.add(`sym${id}`, `images/slots/${file}`);
        }

        try {
            window.slotEngine.textures = await PIXI.Assets.load(Object.keys(symbolFiles).map(id => `sym${id}`));
            window.slotEngine.buildGrid();
            window.slotEngine.resize();
            window.addEventListener('resize', () => window.slotEngine.resize());
        } catch (e) { console.error("Asset Load Fail", e); }
    },

    resize: () => {
        if (!window.slotEngine.app) return;
        const canvas = window.slotEngine.app.view;
        const parent = canvas.parentElement;
        if (!parent) return;

        const pW = parent.clientWidth;
        const pH = parent.clientHeight;
        const ratio = window.slotEngine.internalWidth / window.slotEngine.internalHeight;

        let newW = pW;
        let newH = pW / ratio;

        if (newH > pH) {
            newH = pH;
            newW = pH * ratio;
        }

        canvas.style.width = `${newW}px`;
        canvas.style.height = `${newH}px`;
    },

    setPerformanceMode: (speed, lowGraphics) => {
        window.slotEngine.performance.speed = speed;
        window.slotEngine.performance.lowGraphics = lowGraphics;
    },

    buildGrid: () => {
        const { reelLayer, cols, rows, reelWidth, symbolSize } = window.slotEngine;
        window.slotEngine.reels = [];
        
        for (let c = 0; c < cols; c++) {
            const rc = new PIXI.Container();
            rc.x = c * reelWidth;
            const reel = { container: rc, symbols: [], speed: 0, isStopped: true };

            for (let r = 0; r < rows + 1; r++) { 
                const id = Math.floor(Math.random() * 12) + 1;
                const sprite = new PIXI.Sprite(window.slotEngine.textures[`sym${id}`]);
                sprite.width = sprite.height = symbolSize;
                sprite.x = (reelWidth - symbolSize) / 2;
                sprite.y = r * symbolSize;
                rc.addChild(sprite);
                reel.symbols.push({ sprite, id });
            }
            reelLayer.addChild(rc);
            window.slotEngine.reels.push(reel);
        }
        window.slotEngine.app.ticker.add((delta) => window.slotEngine.update(delta));
    },

    spin: (resultJson) => {
        if (window.slotEngine.running) return;
        window.slotEngine.running = true;
        window.slotEngine.clearWinLines();
        
        const data = JSON.parse(resultJson);
        const grid = data.Grid;
        window.slotEngine.lastWinningLines = data.WinningLines || [];
        
        // PRESERVE STICKY BELLS IF BONUS IS ENDING
        const wasInBonus = window.slotEngine.isBonusActive;
        window.slotEngine.isBonusActive = data.IsBonusActive;
        
        if (data.IsBonusActive || data.IsRespinActive) {
            window.slotEngine.syncStickyBells(data.BonusBells || [], data.StickyClovers || []);
        } else if (!wasInBonus) {
            // Only clear if we weren't just in a bonus (normal game)
            window.slotEngine.stickyBells = [];
            window.slotEngine.stickyLayer.removeChildren();
        }

        const finalSymbols = [];
        for(let c=0; c < 5; c++) {
            const colSyms = [];
            for(let r=0; r < 4; r++) colSyms.push(grid[r][c]);
            finalSymbols.push(colSyms);
        }

        const baseSpeed = 25 * window.slotEngine.performance.speed;

        window.slotEngine.reels.forEach((reel, i) => {
            reel.speed = baseSpeed + (i * 2);
            reel.targetSymbols = finalSymbols[i];
            reel.stopping = false;
            reel.isStopped = false;
            const stopDelay = window.slotEngine.performance.speed === 3 ? (100 + i * 50) : 
                             window.slotEngine.performance.speed === 2 ? (400 + i * 150) : (1000 + i * 300);
            setTimeout(() => reel.stopping = true, stopDelay);
        });
    },

    syncStickyBells: (bells, clovers) => {
        const newSticky = [];
        bells.forEach(b => { newSticky.push({ r: b.Pos.R, c: b.Pos.C, id: 9, value: b.Value, type: b.Type }); });
        clovers.forEach(p => {
            if (!newSticky.find(s => s.r === p.R && s.c === p.C)) {
                newSticky.push({ r: p.R, c: p.C, id: 8 });
            }
        });
        window.slotEngine.stickyBells = newSticky;
        window.slotEngine.updateStickyBellsVisuals();
    },

    updateStickyBellsVisuals: () => {
        const { stickyLayer, stickyBells, symbolSize, reelWidth, textures } = window.slotEngine;
        stickyLayer.removeChildren();

        stickyBells.forEach(sb => {
            const tex = textures[`sym${sb.id}`];
            const sprite = new PIXI.Sprite(tex);
            sprite.width = sprite.height = symbolSize;
            sprite.x = sb.c * reelWidth + (reelWidth - symbolSize) / 2;
            sprite.y = sb.r * symbolSize;

            if (sb.type !== undefined) {
                // Initial hide value text, reveal later in animation
                if (sb.type === 1) { sprite.tint = 0x00ff00; window.slotEngine.addLabel(sprite, "MINI", false); }
                else if (sb.type === 2) { sprite.tint = 0x00ffff; window.slotEngine.addLabel(sprite, "MINOR", false); }
                else if (sb.type === 3) { sprite.tint = 0xff00ff; window.slotEngine.addLabel(sprite, "MAJOR", false); }
                else if (sb.type === 4) { 
                    sprite.tint = 0xffd700; 
                    window.slotEngine.addLabel(sprite, "MEGA", false); 
                    if (window.gsap) gsap.to(sprite, { alpha: 0.7, duration: 0.5, repeat: -1, yoyo: true });
                } else {
                    // Regular cash bell
                    window.slotEngine.addLabel(sprite, `$${sb.value.toFixed(2)}`, false);
                }
            }
            stickyLayer.addChild(sprite);
        });
    },

    addLabel: (parent, text, visible = true) => {
        const style = new PIXI.TextStyle({
            fontFamily: 'Rajdhani, Arial', fontSize: 22, fontWeight: 'bold', fill: '#ffffff',
            stroke: '#000000', strokeThickness: 5, align: 'center'
        });
        const richText = new PIXI.Text(text, style);
        richText.anchor.set(0.5);
        richText.x = parent.width / 2;
        richText.y = parent.height / 2 + 10;
        richText.visible = visible;
        richText.name = "valueLabel";
        parent.addChild(richText);
    },

    update: (delta) => {
        if (!window.slotEngine.running) return;
        let activeReels = 0;
        const { reels, symbolSize, textures } = window.slotEngine;

        reels.forEach((reel, i) => {
            if (reel.isStopped) return;
            activeReels++;

            reel.symbols.forEach(s => { s.sprite.y += reel.speed * delta; });

            const limit = 4 * symbolSize;
            const bufferY = -symbolSize;

            reel.symbols.forEach(s => {
                if (s.sprite.y >= limit) {
                    s.sprite.y = bufferY + (s.sprite.y - limit);
                    let nextId = Math.floor(Math.random() * 12) + 1;
                    const tex = textures[`sym${nextId}`];
                    if (tex) s.sprite.texture = tex;
                }
            });

            if (reel.stopping) {
                reel.speed = 0;
                reel.isStopped = true;
                reel.symbols.forEach((s, idx) => {
                    s.sprite.y = idx * symbolSize;
                    let tid = (idx < 4 && reel.targetSymbols) ? reel.targetSymbols[idx] : 0;
                    
                    if (tid === 0) {
                        // Empty slot in bonus: show a dimmed ghost symbol or just a placeholder
                        s.sprite.texture = textures[`sym${Math.floor(Math.random()*7)+1}`];
                        s.sprite.alpha = 0.15; // Ghostly look
                    } else {
                        s.sprite.texture = textures[`sym${tid}`];
                        const isSticky = window.slotEngine.stickyBells.find(sb => sb.c === i && sb.r === idx);
                        s.sprite.alpha = isSticky ? 0 : 1;
                    }
                });

                if (window.aleaAudio?.play) window.aleaAudio.play('click');
                if (window.gsap) gsap.to(reel.container, { y: 10, duration: 0.05, yoyo: true, repeat: 1 });
            }
        });

        if (activeReels === 0) {
            window.slotEngine.running = false;
            if (window.slotEngine.lastWinningLines?.length > 0) window.slotEngine.drawWinLines(window.slotEngine.lastWinningLines);
            
            // Check if we just finished a bonus round
            if (window.slotEngine.isBonusActive === false && window.slotEngine.stickyBells.length > 0) {
                window.slotEngine.revealBonusValues();
            } else {
                if (window.slotEngine.dotNetRef) {
                    window.slotEngine.dotNetRef.invokeMethodAsync('OnAnimationFinished');
                }
            }
        }
    },

    revealBonusValues: async () => {
        const { stickyLayer } = window.slotEngine;
        
        // Sequence reveal animation
        for (let i = 0; i < stickyLayer.children.length; i++) {
            const bell = stickyLayer.children[i];
            const label = bell.getChildByName("valueLabel");
            
            if (window.gsap) {
                // Play sound for each reveal if available
                if (window.aleaAudio?.play) window.aleaAudio.play('click');
                
                label.visible = true;
                gsap.fromTo(label.scale, { x: 0, y: 0 }, { x: 1, y: 1, duration: 0.3, ease: "back.out" });
                gsap.to(bell.scale, { x: 1.2, y: 1.2, duration: 0.15, yoyo: true, repeat: 1 });
                
                await new Promise(r => setTimeout(r, 200));
            }
        }
        
        // Final delay before notifying Blazor
        setTimeout(() => {
            if (window.slotEngine.dotNetRef) {
                window.slotEngine.dotNetRef.invokeMethodAsync('OnAnimationFinished');
            }
            // Fade out sticky layer
            if (window.gsap) {
                gsap.to(stickyLayer, { alpha: 0, duration: 0.5, onComplete: () => {
                    window.slotEngine.stickyBells = [];
                    stickyLayer.removeChildren();
                    stickyLayer.alpha = 1;
                }});
            }
        }, 1000);
    },

    clearWinLines: () => window.slotEngine.winGraphics?.clear(),

    drawWinLines: (winningLines) => {
        const { winGraphics, paylines, reelWidth, symbolSize } = window.slotEngine;
        winGraphics.clear();
        winningLines.forEach(win => {
            const lineData = paylines[win.LineIndex];
            if (!lineData) return;
            winGraphics.lineStyle(4, 0xffeb3b, 0.8);
            const startX = (reelWidth / 2);
            const startY = (lineData[0] * symbolSize) + (symbolSize / 2);
            winGraphics.moveTo(startX, startY);
            for (let c = 1; c < win.Count; c++) {
                winGraphics.lineTo((c * reelWidth) + (reelWidth / 2), (lineData[c] * symbolSize) + (symbolSize / 2));
            }
        });
    },

    restoreState: (json) => {
        const data = JSON.parse(json);
        if (data.Grid) {
            window.slotEngine.reels.forEach((reel, c) => {
                reel.symbols.forEach((s, r) => {
                    if (r < 4) {
                        const sid = data.Grid[r][c];
                        if (sid > 0) {
                            s.sprite.texture = window.slotEngine.textures[`sym${sid}`];
                            s.sprite.alpha = 1;
                        } else {
                            s.sprite.alpha = 0.15;
                        }
                    }
                });
            });
        }
        if (data.BonusBells || data.StickyClovers) {
            window.slotEngine.syncStickyBells(data.BonusBells || [], data.StickyClovers || []);
            // Make labels visible on restore
            window.slotEngine.stickyLayer.children.forEach(c => {
                const l = c.getChildByName("valueLabel");
                if (l) l.visible = true;
            });
        }
    }
};
