window.roulettePro = {
    app: null,
    rotatingDisk: null,
    ball: null,
    spinning: false,
    // European Order
    numbers: [0, 32, 15, 19, 4, 21, 2, 25, 17, 34, 6, 27, 13, 36, 11, 30, 8, 23, 10, 5, 24, 16, 33, 1, 20, 14, 31, 9, 22, 18, 29, 7, 28, 12, 35, 3, 26],
    
    init: (containerId) => {
        const el = document.getElementById(containerId);
        if (!el) return;
        el.innerHTML = '';

        window.roulettePro.app = new PIXI.Application({
            width: 600,
            height: 500,
            backgroundColor: 0x000000,
            backgroundAlpha: 0,
            antialias: true,
            resolution: window.devicePixelRatio || 1,
            autoDensity: true
        });
        el.appendChild(window.roulettePro.app.view);
        window.roulettePro.setup();
    },

    setup: () => {
        const self = window.roulettePro;
        const { app, numbers } = self;
        const centerX = 300;
        const centerY = 250;

        const root = new PIXI.Container();
        root.x = centerX; root.y = centerY;
        root.scale.y = 0.72; // Perspective
        app.stage.addChild(root);

        // 1. Outer Frame (Wooden)
        const frame = new PIXI.Graphics();
        frame.beginFill(0x2a1d15);
        frame.lineStyle(12, 0x1a120d);
        frame.drawCircle(0, 0, 245);
        frame.endFill();
        frame.lineStyle(2, 0xd4af37, 0.3);
        frame.drawCircle(0, 0, 235);
        root.addChild(frame);

        // 2. Rotating Disk Container
        const disk = new PIXI.Container();
        root.addChild(disk);
        self.rotatingDisk = disk;

        const segAngle = (Math.PI * 2) / 37;
        
        numbers.forEach((num, i) => {
            const angle = i * segAngle - Math.PI / 2;
            const segment = new PIXI.Graphics();
            
            let color = 0x1a1a1a;
            if (num === 0) color = 0x006400;
            else if ([1, 3, 5, 7, 9, 12, 14, 16, 18, 19, 21, 23, 25, 27, 30, 32, 34, 36].includes(num)) color = 0x8b0000;
            
            segment.beginFill(color);
            segment.lineStyle(1, 0x333333, 0.4);
            segment.moveTo(0, 0);
            segment.arc(0, 0, 220, angle - segAngle/2, angle + segmentAngle/2);
            segment.lineTo(0, 0);
            segment.endFill();
            disk.addChild(segment);

            // Metal Divider Pins
            const pin = new PIXI.Graphics();
            pin.beginFill(0xaaaaaa);
            pin.drawCircle(Math.cos(angle - segAngle/2) * 218, Math.sin(angle - segAngle/2) * 218, 2);
            pin.endFill();
            disk.addChild(pin);

            // Numbers
            const text = new PIXI.Text(num.toString(), {
                fontFamily: 'Montserrat',
                fontSize: 16,
                fontWeight: '900',
                fill: '#ffffff'
            });
            text.anchor.set(0.5);
            text.x = Math.cos(angle) * 195;
            text.y = Math.sin(angle) * 195;
            text.rotation = angle + Math.PI / 2;
            disk.addChild(text);
        });

        // 3. Spindle/Hub (Center part that rotates)
        const hub = new PIXI.Container();
        disk.addChild(hub);
        const hubG = new PIXI.Graphics();
        hubG.beginFill(0x3d2b1f);
        hubG.lineStyle(4, 0xd4af37);
        hubG.drawCircle(0, 0, 65);
        hubG.endFill();
        // Golden Cross
        hubG.lineStyle(8, 0xd4af37, 0.8);
        hubG.moveTo(-55, 0); hubG.lineTo(55, 0);
        hubG.moveTo(0, -55); hubG.lineTo(0, 55);
        hub.addChild(hubG);

        // 4. Ball
        const ball = new PIXI.Graphics();
        ball.beginFill(0xffffff);
        ball.drawCircle(0, 0, 7);
        ball.endFill();
        ball.beginFill(0xffffff, 0.6);
        ball.drawCircle(-2, -2, 2);
        ball.endFill();
        ball.alpha = 0;
        root.addChild(ball); // Ball is in root (world space during spin)
        self.ball = ball;

        // 5. Lighting Overlays
        const shine = new PIXI.Graphics();
        shine.beginFill(0xffffff, 0.05);
        shine.drawEllipse(0, -100, 200, 100);
        shine.endFill();
        app.stage.addChild(shine); shine.x = centerX; shine.y = centerY;

        app.ticker.add(() => {
            if (!self.spinning) disk.rotation += 0.003;
        });
    },

    spin: (targetNumber) => {
        const self = window.roulettePro;
        if (self.spinning) return;
        self.spinning = true;

        const targetIndex = self.numbers.indexOf(targetNumber);
        const segmentAngleRad = (Math.PI * 2) / 37;
        
        // 1. Setup Ball for world-space orbit
        self.ball.alpha = 1;
        const ballState = { angle: 0, radius: 230 };
        
        // 2. Math Logic: 
        // We want the targetIndex segment to be at the TOP (-PI/2) when we stop.
        // Current angle of segment i is: i * segAngle + disk.rotation
        // We want: targetIndex * segAngle + disk.rotation = -PI/2
        // So: targetRotation = -PI/2 - (targetIndex * segAngle)
        
        const extraSpins = Math.PI * 2 * 5; // 5 full loops
        const baseTarget = - (targetIndex * segmentAngleRad);
        const finalRotation = self.rotatingDisk.rotation + extraSpins + (Math.PI * 2) + baseTarget;

        // Animate Disk
        gsap.to(self.rotatingDisk, {
            rotation: finalRotation,
            duration: 7,
            ease: "power2.inOut",
            onComplete: () => { self.spinning = false; }
        });

        // Animate Ball
        // Ball orbits faster in opposite direction
        gsap.to(ballState, {
            angle: -(Math.PI * 2 * 10), // 10 fast orbits
            duration: 5.5,
            ease: "power1.out",
            onUpdate: () => {
                self.ball.x = Math.cos(ballState.angle) * ballState.radius;
                self.ball.y = Math.sin(ballState.angle) * ballState.radius;
            }
        });

        // Ball spiraling down into the pocket
        gsap.to(ballState, {
            radius: 205,
            delay: 4,
            duration: 2,
            ease: "bounce.out",
            onUpdate: () => {
                // Ensure ball is visible over segments
            }
        });
    }
};
