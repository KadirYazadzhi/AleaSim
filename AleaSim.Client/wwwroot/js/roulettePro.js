
window.roulettePro = {
    app: null,
    wheelContainer: null,
    rotatingDisk: null,
    ball: null,
    spinning: false,
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
        const { app, numbers } = window.roulettePro;
        
        // Center of the canvas
        const centerX = 300;
        const centerY = 250;

        // 1. Root Container with Perspective Tilt
        const root = new PIXI.Container();
        root.x = centerX;
        root.y = centerY;
        // Simulating 3D tilt by scaling Y slightly less than X
        // and adding an offset.
        root.scale.y = 0.7; 
        app.stage.addChild(root);

        // 2. Outer Wooden Bowl (Static)
        const bowl = new PIXI.Graphics();
        // Wood texture simulation with gradient
        bowl.beginFill(0x3d2b1f);
        bowl.lineStyle(10, 0x2a1d15);
        bowl.drawCircle(0, 0, 240);
        bowl.endFill();
        
        // Inner golden rim
        bowl.lineStyle(4, 0xd4af37, 0.8);
        bowl.drawCircle(0, 0, 220);
        root.addChild(bowl);

        // 3. Rotating Disk
        const disk = new PIXI.Container();
        root.addChild(disk);
        window.roulettePro.rotatingDisk = disk;

        // Draw segments on the disk
        const segmentAngle = (Math.PI * 2) / 37;
        
        numbers.forEach((num, i) => {
            const angle = i * segmentAngle - Math.PI / 2;
            const segment = new PIXI.Graphics();
            
            let color = 0x1a1a1a; // Blackish
            if (num === 0) color = 0x008000; // Green
            else if ([1, 3, 5, 7, 9, 12, 14, 16, 18, 19, 21, 23, 25, 27, 30, 32, 34, 36].includes(num)) color = 0xb30000; // Red
            
            segment.beginFill(color);
            segment.lineStyle(1, 0x333333, 0.5);
            segment.moveTo(0, 0);
            segment.arc(0, 0, 215, angle - segmentAngle/2, angle + segmentAngle/2);
            segment.lineTo(0, 0);
            segment.endFill();
            disk.addChild(segment);

            // Numbers on disk
            const style = new PIXI.TextStyle({
                fontFamily: 'Montserrat',
                fontSize: 18,
                fontWeight: '900',
                fill: '#ffffff',
                dropShadow: true,
                dropShadowDistance: 2,
                dropShadowAlpha: 0.5
            });
            const text = new PIXI.Text(num.toString(), style);
            text.anchor.set(0.5);
            // Position numbers near the edge
            const textDist = 185;
            text.x = Math.cos(angle) * textDist;
            text.y = Math.sin(angle) * textDist;
            text.rotation = angle + Math.PI / 2;
            disk.addChild(text);
        });

        // 4. Center Hub (Static or slow rotate)
        const hub = new PIXI.Graphics();
        hub.beginFill(0x3d2b1f);
        hub.lineStyle(5, 0xd4af37);
        hub.drawCircle(0, 0, 60);
        hub.endFill();
        
        // Add some "metal" reflections to hub
        hub.beginFill(0xffffff, 0.1);
        hub.drawEllipse(-15, -15, 20, 10);
        hub.endFill();
        
        root.addChild(hub);

        // 5. Ball (In Root container so it respects the tilt)
        const ball = new PIXI.Graphics();
        ball.beginFill(0xffffff);
        ball.drawCircle(0, 0, 8);
        ball.endFill();
        // Shine on ball
        ball.beginFill(0xffffff, 0.5);
        ball.drawCircle(-2, -2, 3);
        ball.endFill();
        
        ball.alpha = 0;
        root.addChild(ball);
        window.roulettePro.ball = ball;

        // 6. Overlay Shadows (Non-tilting, added to stage)
        // This gives the "3D depth" look by shadowing the top part
        const shadows = new PIXI.Graphics();
        shadows.beginFill(0x000000, 0.3);
        // Shadow on the top half of the bowl to simulate overhead light
        shadows.drawRect(0, 0, 600, 250); 
        shadows.endFill();
        // Mask it to the bowl circle
        const mask = new PIXI.Graphics();
        mask.beginFill(0xffffff);
        mask.drawEllipse(centerX, centerY, 240, 240 * 0.7);
        mask.endFill();
        shadows.mask = mask;
        app.stage.addChild(shadows);

        // Start Idle Animation
        app.ticker.add(() => {
            if (!window.roulettePro.spinning) {
                disk.rotation += 0.002;
            }
        });
    },

    spin: (targetNumber) => {
        if (window.roulettePro.spinning) return;
        window.roulettePro.spinning = true;

        const { rotatingDisk, ball, numbers } = window.roulettePro;
        
        // Reset Ball
        ball.alpha = 1;
        const ballState = { angle: 0, radius: 215 };

        const targetIndex = numbers.indexOf(targetNumber);
        // We want the target index to end up at the BOTTOM (Math.PI / 2)
        // Standard rotation: 0 is right. We need to offset.
        const segmentAngleRad = (Math.PI * 2) / 37;
        
        // Logic: Total Rotation = (Current) + (Many Spins) + (Adjustment to Target)
        const totalSpins = 6;
        const currentRotation = rotatingDisk.rotation;
        
        // Final rotation calculation:
        // We want: (FinalRotation + TargetAngleIndex) = DesiredStopAngle (e.g. 90 deg)
        // Adjustment = DesiredStop - (Current + ExtraSpins + TargetIndexAngle)
        const finalDiskRotation = currentRotation + (Math.PI * 2 * totalSpins) + (Math.PI * 2) - (targetIndex * segmentAngleRad);

        // Wheel Timeline
        gsap.to(rotatingDisk, {
            rotation: finalDiskRotation,
            duration: 6,
            ease: "power2.inOut"
        });

        // Ball Timeline (Orbiting faster and in opposite direction)
        gsap.to(ballState, {
            angle: -(Math.PI * 2 * 12), // 12 spins in opposite direction
            duration: 5,
            ease: "power1.out",
            onUpdate: () => {
                ball.x = Math.cos(ballState.angle) * ballState.radius;
                ball.y = Math.sin(ballState.angle) * ballState.radius;
            }
        });

        // Ball spirals in
        gsap.to(ballState, {
            radius: 195, // Pocket radius
            delay: 3,
            duration: 2,
            ease: "bounce.out"
        });

        // Sync completion
        setTimeout(() => {
            window.roulettePro.spinning = false;
            // The GSAP ease and math above ensures the ball is visually over the target segment
        }, 6000);
    }
};
