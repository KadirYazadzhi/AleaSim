window.roulettePro = {
    app: null,
    rotatingDisk: null,
    ball: null,
    spinning: false,
    // European Order
    numbers: [0, 32, 15, 19, 4, 21, 2, 25, 17, 34, 6, 27, 13, 36, 11, 30, 8, 23, 10, 5, 24, 16, 33, 1, 20, 14, 31, 9, 22, 18, 29, 7, 28, 12, 35, 3, 26],
    
    init: (containerId) => {
        console.log("RoulettePro: Init requested for ID: " + containerId);
        let attempts = 0;
        const maxAttempts = 30;

        const doInit = () => {
            const el = document.getElementById(containerId);
            if (!el) {
                if (attempts < maxAttempts) {
                    attempts++;
                    setTimeout(doInit, 100);
                }
                return;
            }

            if (window.roulettePro.app) {
                try {
                    window.roulettePro.app.destroy(true, { children: true, texture: true, baseTexture: true });
                } catch (e) { }
                window.roulettePro.app = null;
            }

            el.innerHTML = '';
            let width = el.clientWidth || el.offsetWidth || 300;
            let height = el.clientHeight || el.offsetHeight || 300;

            if (containerId.includes('mobile') && !document.getElementById(containerId).closest('.expanded')) {
                if (width > 200) width = 120;
                if (height > 200) height = 120;
            }

            window.roulettePro.app = new PIXI.Application({
                width: width,
                height: height,
                backgroundColor: 0x000000,
                backgroundAlpha: 0,
                antialias: true,
                resolution: window.devicePixelRatio || 1,
                autoDensity: true
            });
            
            el.appendChild(window.roulettePro.app.view);
            window.roulettePro.setup(width, height);

            // Dynamic Resize Listener
            window.removeEventListener('resize', window.roulettePro.onResize);
            window.roulettePro.activeContainerId = containerId;
            window.roulettePro.onResize = () => {
                const container = document.getElementById(window.roulettePro.activeContainerId);
                if (!container || !window.roulettePro.app) return;
                
                let w = container.clientWidth || container.offsetWidth;
                let h = container.clientHeight || container.offsetHeight;
                
                if (window.roulettePro.activeContainerId.includes('mobile') && !container.closest('.expanded')) {
                    if (w > 200) w = 120;
                    if (h > 200) h = 120;
                }

                if (w > 0 && h > 0) {
                    window.roulettePro.app.renderer.resize(w, h);
                    window.roulettePro.setup(w, h);
                }
            };
            window.addEventListener('resize', window.roulettePro.onResize);
        };

        doInit();
    },
    activeContainerId: null,
    onResize: null,

    setup: (width, height) => {
        const self = window.roulettePro;
        const { app, numbers } = self;
        
        // Clear previous state to prevent stacking
        app.stage.removeChildren();
        if (self.rotationHandler) {
            app.ticker.remove(self.rotationHandler);
        }

        const centerX = width / 2;
        const centerY = height / 2;
        
        // Scale the wheel based on available space
        const baseSize = 600;
        const scale = Math.min(width, height) / baseSize;

        const root = new PIXI.Container();
        root.x = centerX; root.y = centerY;
        root.scale.set(scale);
        root.scale.y *= 0.75; // Perspective tilt
        app.stage.addChild(root);

        const frame = new PIXI.Graphics();
        frame.beginFill(0x1a0f0a);
        frame.lineStyle(8, 0x000000);
        frame.drawCircle(0, 0, 240);
        frame.endFill();
        frame.lineStyle(3, 0xc5a059, 0.6);
        frame.drawCircle(0, 0, 225);
        root.addChild(frame);

        const disk = new PIXI.Container();
        root.addChild(disk);
        self.rotatingDisk = disk;

        const segAngle = (Math.PI * 2) / 37;
        
        numbers.forEach((num, i) => {
            const angle = i * segAngle;
            const segment = new PIXI.Graphics();
            
            let color = 0x111111; 
            if (num === 0) color = 0x005500; 
            else if ([1, 3, 5, 7, 9, 12, 14, 16, 18, 19, 21, 23, 25, 27, 30, 32, 34, 36].includes(num)) color = 0x990000; 
            
            segment.beginFill(color);
            segment.lineStyle(1, 0x333333, 0.3);
            segment.moveTo(0, 0);
            segment.arc(0, 0, 220, angle - segAngle/2, angle + segAngle/2);
            segment.lineTo(0, 0);
            segment.endFill();
            disk.addChild(segment);

            const pin = new PIXI.Graphics();
            pin.beginFill(0xd4af37);
            pin.drawCircle(Math.cos(angle - segAngle/2) * 218, Math.sin(angle - segAngle/2) * 218, 2.5);
            pin.endFill();
            disk.addChild(pin);

            const text = new PIXI.Text(num.toString(), {
                fontFamily: 'Montserrat',
                fontSize: 18,
                fontWeight: '800',
                fill: '#ffffff'
            });
            text.anchor.set(0.5);
            text.x = Math.cos(angle) * 190;
            text.y = Math.sin(angle) * 190;
            text.rotation = angle + Math.PI/2;
            disk.addChild(text);
        });

        const hub = new PIXI.Graphics();
        hub.beginFill(0x2a1d15);
        hub.lineStyle(4, 0xd4af37);
        hub.drawCircle(0, 0, 60);
        hub.endFill();
        hub.lineStyle(6, 0xd4af37, 0.7);
        hub.moveTo(-50, 0); hub.lineTo(50, 0);
        hub.moveTo(0, -50); hub.lineTo(0, 50);
        disk.addChild(hub);

        const ball = new PIXI.Graphics();
        ball.beginFill(0xffffff);
        ball.drawCircle(0, 0, 7);
        ball.endFill();
        ball.alpha = 0;
        root.addChild(ball);
        self.ball = ball;

        self.rotationHandler = () => {
            if (!self.spinning) disk.rotation += 0.005;
        };
        app.ticker.add(self.rotationHandler);
    },
    rotationHandler: null,

    spin: (targetNumber) => {
        const self = window.roulettePro;
        if (self.spinning) return;
        self.spinning = true;

        const targetIndex = self.numbers.indexOf(targetNumber);
        const segmentAngleRad = (Math.PI * 2) / 37;
        
        // 1. Prepare Ball
        self.ball.alpha = 1;
        // The ball will stop at Math.PI / 2 (Bottom of the screen)
        const stopAngle = Math.PI / 2;
        const ballState = { angle: -Math.PI / 2, radius: 230 };
        
        // 2. THE SYNC MATH
        // Segment angle on disk is i * segAngle
        // Visual angle = (i * segAngle) + disk.rotation
        // We want: (targetIndex * segAngle) + finalDiskRotation = stopAngle
        // finalDiskRotation = stopAngle - (targetIndex * segAngle)
        
        const currentRotation = self.rotatingDisk.rotation;
        const fullSpins = Math.PI * 2 * 6; 
        
        // Calculate the necessary target rotation
        let targetDiskRotation = stopAngle - (targetIndex * segmentAngleRad);
        
        // Ensure finalRotation is forward and includes enough spins
        let finalRotation = currentRotation + fullSpins;
        // Adjust to hit the exact angle modulo 2PI
        const currentNorm = finalRotation % (Math.PI * 2);
        let diff = targetDiskRotation - currentNorm;
        while (diff < 0) diff += Math.PI * 2;
        finalRotation += diff;

        // Animate Disk
        gsap.to(self.rotatingDisk, {
            rotation: finalRotation,
            duration: 7,
            ease: "power2.inOut",
            onComplete: () => {
                self.spinning = false;
            }
        });

        // Animate Ball (Opposite direction)
        // End ball angle at a value that is equivalent to stopAngle (mod 2PI)
        const totalBallSpins = Math.PI * 2 * 10;
        const finalBallAngle = -totalBallSpins + stopAngle;

        gsap.to(ballState, {
            angle: finalBallAngle,
            duration: 5.5,
            ease: "power1.out",
            onUpdate: () => {
                self.ball.x = Math.cos(ballState.angle) * ballState.radius;
                self.ball.y = Math.sin(ballState.angle) * ballState.radius;
            }
        });

        // Spiral ball into the pocket
        gsap.to(ballState, {
            radius: 195,
            delay: 4.5,
            duration: 2.5,
            ease: "bounce.out"
        });
    }
};
