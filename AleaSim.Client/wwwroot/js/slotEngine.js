
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
    isRespinActive: false,
    wasInBonus: false, 
    isRevealing: false, 
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

        // Optimization: Limit resolution on mobile to prevent GPU bottleneck
        const isMobile = /Android|webOS|iPhone|iPad|iPod|BlackBerry|IEMobile|Opera Mini/i.test(navigator.userAgent);
        const maxResolution = isMobile ? Math.min(window.devicePixelRatio, 2) : window.devicePixelRatio;

        window.slotEngine.app = new PIXI.Application({
            width: window.slotEngine.internalWidth,
            height: window.slotEngine.internalHeight,
            backgroundAlpha: 0,
            antialias: !isMobile && !lowGraphics, // Disable antialias on mobile for speed
            resolution: maxResolution || 1,
            autoDensity: true,
            powerPreference: 'high-performance'
        });
        el.appendChild(window.slotEngine.app.view);

        const gridW = window.slotEngine.reelWidth * window.slotEngine.cols;
        const gridH = window.slotEngine.symbolSize * window.slotEngine.rows;
        const gridX = (window.slotEngine.internalWidth - gridW) / 2;
        const gridY = (window.slotEngine.internalHeight - gridH) / 2;

        window.slotEngine.reelLayer = new PIXI.Container();
        window.slotEngine.reelLayer.x = gridX; window.slotEngine.reelLayer.y = gridY;

        window.slotEngine.stickyLayer = new PIXI.Container();
        window.slotEngine.stickyLayer.x = gridX; window.slotEngine.stickyLayer.y = gridY;

        window.slotEngine.winGraphics = new PIXI.Graphics();
        window.slotEngine.winGraphics.x = gridX; window.slotEngine.winGraphics.y = gridY;
        
        window.slotEngine.app.stage.addChild(window.slotEngine.reelLayer);
        window.slotEngine.app.stage.addChild(window.slotEngine.stickyLayer);
        window.slotEngine.app.stage.addChild(window.slotEngine.winGraphics);

        window.slotEngine.mask = new PIXI.Graphics();
        window.slotEngine.mask.beginFill(0xffffff);
        window.slotEngine.mask.drawRect(0, 0, gridW, gridH);
        window.slotEngine.mask.endFill();
        window.slotEngine.reelLayer.mask = window.slotEngine.mask;
        window.slotEngine.reelLayer.addChild(window.slotEngine.mask);

        try {
            // LOAD ATLAS INSTEAD OF INDIVIDUAL FILES
            const sheet = await PIXI.Assets.load('images/slots/sprites.json');
            
            // Map frame names to our ID system
            const mapping = {
                1: 'cherries', 2: 'lemon', 3: 'orange', 4: 'plum',
                5: 'grape', 6: 'watermelon', 7: 'apple', 8: 'clover',
                9: 'bell', 10: 'star', 11: 'coin', 12: 'seven'
            };

            for (const [id, name] of Object.entries(mapping)) {
                window.slotEngine.textures[`sym${id}`] = sheet.textures[name];
            }

            window.slotEngine.buildGrid();
            window.slotEngine.resize();
            window.addEventListener('resize', () => window.slotEngine.resize());
        } catch (e) { console.error("Atlas Load Fail", e); }
    },

    resize: () => {
        if (!window.slotEngine.app) return;
        const canvas = window.slotEngine.app.view;
        const parent = canvas.parentElement;
        if (!parent) return;
        const pW = parent.clientWidth; const pH = parent.clientHeight;
        const ratio = window.slotEngine.internalWidth / window.slotEngine.internalHeight;
        let newW = pW; let newH = pW / ratio;
        if (newH > pH) { newH = pH; newW = pH * ratio; }
        canvas.style.width = `${newW}px`; canvas.style.height = `${newH}px`;
    },

    setPerformanceMode: (speed, lowGraphics) => {
        window.slotEngine.performance.speed = speed;
        window.slotEngine.performance.lowGraphics = lowGraphics;
    },

    buildGrid: () => {
        const { reelLayer, cols, rows, reelWidth, symbolSize } = window.slotEngine;
        window.slotEngine.reels = [];
        for (let c = 0; c < cols; c++) {
            const rc = new PIXI.Container(); rc.x = c * reelWidth;
            const reel = { container: rc, symbols: [], speed: 0, isStopped: true };
            for (let r = 0; r < rows + 1; r++) { 
                const id = Math.floor(Math.random() * 12) + 1;
                const sprite = new PIXI.Sprite(window.slotEngine.textures[`sym${id}`]);
                sprite.width = sprite.height = symbolSize;
                sprite.x = (reelWidth - symbolSize) / 2; sprite.y = r * symbolSize;
                rc.addChild(sprite); reel.symbols.push({ sprite, id });
            }
            reelLayer.addChild(rc); window.slotEngine.reels.push(reel);
        }
        window.slotEngine.app.ticker.add((delta) => window.slotEngine.update(delta));
    },

    spin: (resultJson) => {
        if (window.slotEngine.running) return;
        
        const data = JSON.parse(resultJson);
        const grid = data.Grid;
        const currentlyInFeature = data.IsBonusActive || data.IsRespinActive;
        const wasInFeature = window.slotEngine.isBonusActive || window.slotEngine.isRespinActive;

        window.slotEngine.wasInBonus = window.slotEngine.isBonusActive;

        // PRO FIX: If we have old sticky bells and we are NOT in a feature anymore, 
        // "release" them back to the reels so they can spin away naturally.
        if (window.slotEngine.stickyBells.length > 0 && !currentlyInFeature) {
            window.slotEngine.stickyBells.forEach(sb => {
                const reel = window.slotEngine.reels[sb.c];
                if (reel && reel.symbols[sb.r]) {
                    reel.symbols[sb.r].sprite.texture = window.slotEngine.textures[`sym9`];
                    reel.symbols[sb.r].sprite.alpha = 1;
                }
            });
            window.slotEngine.stickyBells = [];
            window.slotEngine.stickyLayer.removeChildren();
            window.slotEngine.stickyMap = Array(5).fill(0).map(() => Array(4).fill(false));
        }

        window.slotEngine.running = true;
        window.slotEngine.isRevealing = false; 
        window.slotEngine.clearWinLines();
        
        window.slotEngine.isBonusActive = data.IsBonusActive;
        window.slotEngine.isRespinActive = data.IsRespinActive; 
        window.slotEngine.lastWinningLines = data.WinningLines || [];
        
        // Store for end of spin sync
        window.slotEngine.pendingBells = data.BonusBells || [];
        window.slotEngine.pendingStickyCoords = data.StickyBells || [];

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
            reel.stopping = false; reel.isStopped = false;
            const stopDelay = window.slotEngine.performance.speed === 3 ? (100 + i * 50) : 
                             window.slotEngine.performance.speed === 2 ? (400 + i * 150) : (1000 + i * 300);
            setTimeout(() => reel.stopping = true, stopDelay);
        });
    },

    syncStickyBells: (bells, stickyCoords) => {
        const newSticky = [];
        const stickyMap = Array(5).fill(0).map(() => Array(4).fill(false));

        bells.forEach(b => { 
            newSticky.push({ r: b.Pos.R, c: b.Pos.C, id: 9, value: b.Value, type: b.Type }); 
            if (b.Pos.C < 5 && b.Pos.R < 4) stickyMap[b.Pos.C][b.Pos.R] = true;
        });
        stickyCoords.forEach(p => {
            if (!newSticky.find(s => s.r === p.R && s.c === p.C)) {
                newSticky.push({ r: p.R, c: p.C, id: 9 });
            }
            if (p.C < 5 && p.R < 4) stickyMap[p.C][p.R] = true;
        });
        window.slotEngine.stickyBells = newSticky;
        window.slotEngine.stickyMap = stickyMap;
        window.slotEngine.updateStickyBellsVisuals(false); 
    },

    updateStickyBellsVisuals: (showLabels = false) => {
        const { stickyLayer, stickyBells, symbolSize, reelWidth, textures } = window.slotEngine;
        stickyLayer.removeChildren();

        stickyBells.forEach(sb => {
            const bgX = sb.c * reelWidth + (reelWidth - symbolSize) / 2;
            const bgY = sb.r * symbolSize;

            const tex = textures[`sym${sb.id}`];
            const sprite = new PIXI.Sprite(tex);
            sprite.width = sprite.height = symbolSize;
            sprite.x = bgX;
            sprite.y = bgY;
            
            if (sb.type !== undefined || sb.value !== undefined) {
                let txt = sb.type === 1 ? "MINI" : sb.type === 2 ? "MINOR" : sb.type === 3 ? "MAJOR" : sb.type === 4 ? "MEGA" : `$${sb.value.toFixed(2)}`;
                if (sb.type === 1) sprite.tint = 0x00ff00;
                else if (sb.type === 2) sprite.tint = 0x00ffff;
                else if (sb.type === 3) sprite.tint = 0xff00ff;
                else if (sb.type === 4) { sprite.tint = 0xffd700; if (window.gsap) gsap.to(sprite, { alpha: 0.7, duration: 0.5, repeat: -1, yoyo: true }); }
                window.slotEngine.addLabel(sprite, txt, showLabels);
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
        richText.anchor.set(0.5); richText.x = parent.width / 2; richText.y = parent.height / 2 + 10;
        richText.visible = visible; richText.name = "valueLabel";
        parent.addChild(richText);
    },

    update: (delta) => {
        if (!window.slotEngine.running) return;
        let activeReels = 0;
        const { reels, symbolSize, textures, stickyMap } = window.slotEngine;

        reels.forEach((reel, i) => {
            if (reel.isStopped) return;
            activeReels++;
            reel.symbols.forEach(s => { 
                s.sprite.y += reel.speed * delta; 
                
                // --- OPTIMIZED: HIDE SYMBOLS BEHIND STICKY BELLS ---
                const row = Math.round(s.sprite.y / symbolSize);
                const isSticky = stickyMap && stickyMap[i] && stickyMap[i][row];
                
                if (isSticky) {
                    s.sprite.alpha = 0; 
                } else {
                    if (s.sprite.y < -symbolSize/2 || s.sprite.y > (4 * symbolSize - symbolSize/2)) {
                        s.sprite.alpha = 0;
                    } else {
                        s.sprite.alpha = 1;
                    }
                }
            });

            const limit = 4 * symbolSize; const bufferY = -symbolSize;
            reel.symbols.forEach(s => {
                if (s.sprite.y >= limit) {
                    s.sprite.y = bufferY + (s.sprite.y - limit);
                    let nextId = Math.floor(Math.random() * 12) + 1;
                    if (textures[`sym${nextId}`]) s.sprite.texture = textures[`sym${nextId}`];
                }
            });
            if (reel.stopping) {
                reel.speed = 0; reel.isStopped = true;
                reel.symbols.forEach((s, idx) => {
                    s.sprite.y = idx * symbolSize;
                    let tid = (idx < 4 && reel.targetSymbols) ? reel.targetSymbols[idx] : 0;
                    if (tid === 0) {
                        s.sprite.texture = textures[`sym${Math.floor(Math.random()*7)+1}`];
                        s.sprite.alpha = 0.5;
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

            // Sync all bells now that the spin has stopped
            if (window.slotEngine.pendingBells?.length > 0 || window.slotEngine.pendingStickyCoords?.length > 0) {
                window.slotEngine.syncStickyBells(window.slotEngine.pendingBells || [], window.slotEngine.pendingStickyCoords || []);
                window.slotEngine.pendingBells = [];
                window.slotEngine.pendingStickyCoords = [];
            }

            if (window.slotEngine.lastWinningLines?.length > 0) window.slotEngine.drawWinLines(window.slotEngine.lastWinningLines);
            
            if (window.slotEngine.wasInBonus === true && window.slotEngine.isBonusActive === false && window.slotEngine.stickyBells.some(b => b.value !== undefined) && !window.slotEngine.isRevealing) {
                window.slotEngine.revealBonusValues();
            } else {
                if (window.slotEngine.dotNetRef) {
                    window.slotEngine.dotNetRef.invokeMethodAsync('OnAnimationFinished');
                }
            }
        }
    },

    revealBonusValues: async () => {
        window.slotEngine.isRevealing = true;
        const { stickyLayer, dotNetRef } = window.slotEngine;
        
        // Filter sprites once to avoid index issues
        const sprites = stickyLayer.children.filter(c => c instanceof PIXI.Sprite);
        
        for (let i = 0; i < sprites.length; i++) {
            const child = sprites[i];
            const label = child.getChildByName("valueLabel");
            
            // Extract value for real-time accumulation
            const textValue = label ? label.text : "";
            let amount = 0;
            if (textValue.startsWith("$")) {
                amount = parseFloat(textValue.substring(1).replace(/,/g, ''));
            }

            if (label && window.gsap) {
                if (window.aleaAudio?.play) window.aleaAudio.play('click');
                label.visible = true;
                
                // POP ANIMATION
                gsap.fromTo(label.scale, { x: 0, y: 0 }, { x: 1, y: 1, duration: 0.4, ease: "back.out(1.7)" });
                gsap.to(child.scale, { x: 1.2, y: 1.2, duration: 0.15, yoyo: true, repeat: 1 });
                
                // Add a glow effect on reveal
                const glow = new PIXI.Graphics();
                glow.beginFill(0xffffff, 0.4);
                glow.drawCircle(child.x + child.width/2, child.y + child.height/2, child.width/2);
                glow.endFill();
                window.slotEngine.stickyLayer.addChildAt(glow, 0);
                gsap.to(glow, { alpha: 0, width: child.width*1.5, height: child.height*1.5, duration: 0.5, onComplete: () => glow.destroy() });

                // REAL-TIME WIN ACCUMULATION
                if (amount > 0 && dotNetRef) {
                    dotNetRef.invokeMethodAsync('OnBellRevealed', amount);
                }

                await new Promise(r => setTimeout(r, 400));
            }
        }
        
        setTimeout(() => {
            if (window.slotEngine.dotNetRef) {
                window.slotEngine.dotNetRef.invokeMethodAsync('OnAnimationFinished');
            }
        }, 500);
    },

    clearWinLines: () => window.slotEngine.winGraphics?.clear(),

    drawWinLines: (winningLines) => {
        const { winGraphics, paylines, reelWidth, symbolSize } = window.slotEngine;
        winGraphics.clear();
        winningLines.forEach(win => {
            const lineData = paylines[win.LineIndex]; if (!lineData) return;
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
            window.slotEngine.isBonusActive = data.IsBonusActive || false;
            window.slotEngine.isRespinActive = data.IsRespinActive || false;
            window.slotEngine.reels.forEach((reel, c) => {
                reel.symbols.forEach((s, r) => {
                    if (r < 4) {
                        const sid = data.Grid[r][c];
                        if (sid > 0) { s.sprite.texture = window.slotEngine.textures[`sym${sid}`]; s.sprite.alpha = 1; }
                        else s.sprite.alpha = 0.15;
                    }
                });
            });
        }
        if (data.BonusBells || data.StickyBells) {
            window.slotEngine.syncStickyBells(data.BonusBells || [], data.StickyBells || []);
            window.slotEngine.stickyLayer.children.forEach(c => {
                if (c instanceof PIXI.Sprite) {
                    const l = c.getChildByName("valueLabel"); if (l) l.visible = true;
                }
            });
        }
    }
};
