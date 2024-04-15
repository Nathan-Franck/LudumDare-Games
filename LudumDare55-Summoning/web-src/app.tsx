import { declareStyle } from './declareStyle';
import { useEffect, useRef, useState } from 'preact/hooks'
import { sliceToArray, callWasm, sliceToString } from './zigWasmInterface';
import { ShaderBuilder, Mat4, Vec2, Vec4 } from "./shaderBuilder";

const { classes, encodedStyle } = declareStyle({
    devTool: {
        fontFamily: 'monospace',
        color: 'white',
    },
    canvas: {
        width: "100%",
        height: "100%",
        position: "absolute",
        left: 0,
        top: 0,
        zIndex: -1,
    },
    dangerMessage: {
        color: 'red',
        fontFamily: 'monospace',
        fontSize: '2em',
        position: 'absolute',
        top: '50%',
        left: '50%',
        transform: 'translate(-50%, -50%)',
        animation: 'rotationalPunch 4s',
    },
});

const allResources = callWasm("getAllResources") as Exclude<ReturnType<typeof callWasm<"getAllResources">>, { "error": any }>;
const startTime = Date.now();

export function App() {
    const canvasRef = useRef<HTMLCanvasElement | null>(null);

    const [framerate, setFramerate] = useState(0);
    const [currentLevel, setCurrentLevel] = useState(0);
    const [levelTitle, setLevelTitle] = useState("");
    const [inDanger, setInDanger] = useState(false);
    const [machineTimes, setMachineTimes] = useState([0, 0, 0, 0]);

    let keyboard = { left: false, right: false, up: false, down: false, interact: false, dash: false };
    const [windowSize, setWindowSize] = useState({ width: window.innerWidth, height: window.innerHeight });

    useEffect(() => {
        const resizeHandler = () => {
            setWindowSize({ width: window.innerWidth, height: window.innerHeight });
        };
        window.addEventListener('resize', resizeHandler);
        return () => {
            window.removeEventListener('resize', resizeHandler);
        };
    }, []);

    useEffect(() => {
        const eventKeyToKey = {
            ArrowLeft: 'left',
            ArrowRight: 'right',
            ArrowUp: 'up',
            ArrowDown: 'down',
            ' ': 'interact',
            Shift: 'dash',
        } as const;
        const keyHandler = (activate: boolean) => (event: KeyboardEvent) => {
            const key = eventKeyToKey[event.key as keyof typeof eventKeyToKey];
            if (key) {
                keyboard = { ...keyboard, [key]: activate };
            }
        };
        const keyHandlers = { down: keyHandler(true), up: keyHandler(false) };
        window.addEventListener('keydown', keyHandlers.down);
        window.addEventListener('keyup', keyHandlers.up)
        return () => {
            window.removeEventListener('keydown', keyHandlers.down);
            window.removeEventListener('keyup', keyHandlers.up);
        };
    }, []);

    useEffect(() => {
        if (!canvasRef.current)
            return;
        const canvas = canvasRef.current;
        const gl = canvas.getContext('webgl2');
        if (!gl)
            return;

        // Rendering utility functions

        function loadStaticSprite(sprite: typeof graphics.background) {
            return {
                ...sprite_mesh,
                texture: ShaderBuilder.loadImageData(gl as WebGL2RenderingContext, sliceToArray.Uint8Array(sprite.data), sprite.width, sprite.height),
                textureResolution: [sprite.width, sprite.height] as Vec2,
                sampleRect: [0, 0, sprite.width, sprite.height] as Vec4,
            }
        }

        function loadAnimation(animation: typeof allResources.graphics.chamber_increase) {
            const spriteSheet = animation.sprite_sheet;
            return {
                ...sprite_mesh,
                texture: ShaderBuilder.loadImageData(gl as WebGL2RenderingContext, sliceToArray.Uint8Array(spriteSheet.data), spriteSheet.width, spriteSheet.height),
                textureResolution: [spriteSheet.width, spriteSheet.height] as Vec2,
                animation_data: animation.animation_data,
            };
        }

        function renderStaticSprite(sprite: typeof machine, origin: { x: number, y: number }, position: { x: number, y: number }) {
            const gl2 = gl as WebGL2RenderingContext;
            ShaderBuilder.renderMaterial(gl2, spriteMaterial, {
                ...world,
                ...sprite,
                spriteOrigin: ShaderBuilder.createBuffer(gl2, new Float32Array([origin.x, origin.y])),
                spriteScale: ShaderBuilder.createBuffer(gl2, new Float32Array([1, 1])),
                spritePosition: ShaderBuilder.createBuffer(gl2, new Float32Array([
                    position.x, position.y
                ])),
            });
        }

        function updateAnimation(animation: typeof ghost.idleSide, progress: number | null = null) {
            const frame_time = progress == null
                ? (Date.now() - startTime) / animation.animation_data.framerate
                : progress * animation.animation_data.frames.length;
            if (progress != null) console.log(progress);
            const currentFrame = Math.floor(frame_time) % animation.animation_data.frames.length;
            return animation.animation_data.frames[currentFrame];
        }

        function renderAnimation(animation: typeof ghost.idleSide, spritePosition: { x: number, y: number }, spriteScale: { x: number, y: number }, progress: number | null = null) {
            const gl2 = gl as WebGL2RenderingContext;
            const currentFrameData = updateAnimation(animation, progress);
            ShaderBuilder.renderMaterial(gl2, spriteMaterial, {
                ...world,
                ...animation,
                spriteOrigin: ShaderBuilder.createBuffer(gl2, new Float32Array([currentFrameData[5], currentFrameData[6]])),
                spritePosition: ShaderBuilder.createBuffer(gl2, new Float32Array([spritePosition.x, spritePosition.y])),
                spriteScale: ShaderBuilder.createBuffer(gl2, new Float32Array([spriteScale.x, spriteScale.y])),
                sampleRect: [currentFrameData[0], currentFrameData[1], currentFrameData[2], currentFrameData[3]] as Vec4,
            });
        }

        var spriteMaterial = ShaderBuilder.generateMaterial(gl, {
            mode: 'TRIANGLES',
            globals: {
                meshTriangle: { type: "element" },
                meshVertexUV: { type: "attribute", unit: "vec2" },
                perspectiveMatrix: { type: "uniform", unit: "mat4", count: 1 },
                texture: { type: "uniform", unit: "sampler2D", count: 1 },
                textureResolution: { type: "uniform", unit: "vec2", count: 1 },
                sampleRect: { type: "uniform", unit: "vec4", count: 1 },
                uv: { type: "varying", unit: "vec2" },
                spriteOrigin: { type: "attribute", unit: "vec2", instanced: true },
                spritePosition: { type: "attribute", unit: "vec2", instanced: true },
                spriteScale: { type: "attribute", unit: "vec2", instanced: true },
            },
            vertSource: `
                precision highp float;
                void main(void) {
                    gl_Position = perspectiveMatrix * vec4((meshVertexUV * sampleRect.zw - spriteOrigin) * spriteScale + spritePosition, 0, 1);
                    uv = meshVertexUV;
                }
            `,
            fragSource: `
                precision highp float;
                void main(void) {
                    vec4 sampleColor = texture2D(texture, (uv * sampleRect.zw + sampleRect.xy) / textureResolution);
                    gl_FragColor = vec4(sampleColor.rgb * sampleColor.a, sampleColor.a);
                }
            `,
        });
        const sprite_mesh = {
            meshTriangle: ShaderBuilder.createElementBuffer(gl, new Uint16Array([
                0, 1, 2,
                0, 2, 3
            ])),
            meshVertexUV: ShaderBuilder.createBuffer(gl, new Float32Array([
                0, 0,
                1, 0,
                1, 1,
                0, 1
            ])),
        };
        // Convert 1080p to window height
        const renderScale = windowSize.height / allResources.config.screen.height;
        const world = {
            perspectiveMatrix: [
                2 / windowSize.width * renderScale, 0, 0, 0,
                0, -2 / windowSize.height * renderScale, 0, 0,
                0, 0, 1, 0,
                -1, 1, 0, 1
            ] as Mat4,
        };
        const graphics = allResources.graphics;
        const ghost = {
            idleSide: loadAnimation(graphics.ghost_idle_side),
            idleFront: loadAnimation(graphics.ghost_idle_front),
            idleBack: loadAnimation(graphics.ghost_idle_back),
            fixing: loadAnimation(graphics.ghost_fixing),
        };
        const chamber_increase = loadAnimation(graphics.chamber_increase);
        const background = loadStaticSprite(graphics.background);
        const machine = loadStaticSprite(graphics.machine);

        {
            gl.clearColor(0, 0, 0, 1);
            gl.clear(gl.COLOR_BUFFER_BIT);
            gl.viewport(0, 0, windowSize.width, windowSize.height);
            gl.enable(gl.BLEND);
            gl.blendFunc(gl.ONE, gl.ONE_MINUS_SRC_ALPHA);
        }

        function updateAndRender() {
            if (!gl)
                return;

            let joystick = { x: 0, y: 0, interact: false, dash: false };
            const gamepads = navigator.getGamepads();
            for (let i = 0; i < gamepads.length; i++) {
                const gamepad = gamepads[i];
                if (gamepad) {
                    joystick = {
                        x: gamepad.axes[0],
                        y: gamepad.axes[1],
                        interact: gamepad.buttons[0].pressed,
                        dash: gamepad.buttons[1].pressed,
                    };
                    break;
                }
            }

            const time = Date.now();
            const state = callWasm("update", {
                time_ms: time,
                keyboard,
                joystick,
            }) as Exclude<ReturnType<typeof callWasm<"update">>, { "error": any }>;
            const { player } = state;
            setCurrentLevel(state.current_level);
            setLevelTitle(sliceToString(allResources.config.levels[state.current_level].title));
            setInDanger(state.in_danger);
            setMachineTimes(state.machine_states.map((machine) => machine.delay_until_breakdown_ms));

            // Build a list of things to render.
            const thingsToRender: Array<
                | { type: "sprite", sprite: typeof machine, origin: { x: number, y: number }, position: { x: number, y: number } }
                | { type: "animation", animation: typeof ghost.idleSide, position: { x: number, y: number }, scale: { x: number, y: number }, progress?: number}
            > = [
                { type: "sprite", sprite: background, origin: { x: 0, y: 0 }, position: { x: 0, y: 0 } },
                { type: "animation", animation: chamber_increase, position: allResources.config.chamber_location, scale: { x: 1, y: 1 }, progress: state.chamber_progress },
                {
                    type: "animation",
                    animation: player.action === "Fixing" ? ghost.fixing : player.view_direction === "Up" ? ghost.idleBack : player.view_direction === "Down" ? ghost.idleFront : ghost.idleSide,
                    position: player.position,
                    scale: { x: player.view_direction === "Left" ? -1 : 1, y: 1 },
                },
                ...allResources.config.machine_locations.map((location) => ({ type: "sprite" as const, sprite: machine, origin: { x: graphics.machine.width / 2, y: graphics.machine.height / 2 }, position: location })),
            ];

            // Sort by y position.
            thingsToRender.sort((a, b) => a.position.y - b.position.y);

            // Render the things!
            thingsToRender.forEach((thing) => {
                switch (thing.type) {
                    case "sprite":
                        renderStaticSprite(thing.sprite, thing.origin, thing.position);
                        break;
                    case "animation":
                        renderAnimation(thing.animation, thing.position, thing.scale, thing.progress);
                        break;
                }
            });
        }
        let lastTime = Date.now();
        let frameCount = 0;
        let quit = false;
        const loop = () => {
            const time = Date.now();
            frameCount++;
            if (time - lastTime > 1000) {
                setFramerate(frameCount);
                frameCount = 0;
                lastTime = time;
            }
            updateAndRender();
            if (!quit)
                requestAnimationFrame(loop);
        }
        requestAnimationFrame(loop);
        return () => {
            quit = true;
        };
    }, [canvasRef, windowSize]);

    return (
        <>
            <div class={classes.devTool}>Frame Rate: {framerate}</div>
            <div class={classes.devTool}>Current Level: {currentLevel}</div>
            <div class={classes.devTool}>Level Title: {levelTitle}</div>
            <div class={classes.devTool}>In Danger: {inDanger ? "Yes" : "No"}</div>
            <div class={classes.devTool}>Machine Times: {machineTimes.join(", ")}</div>
            {inDanger ? <div class={classes.dangerMessage}>{"DANGER! MACHINES MUST BE KEPT ONLINE!"}</div> : null}
            <style>{`
                @keyframes rotationalPunch {
                    0% { transform: rotate(0deg); }
                    5% { transform: rotate(5deg); }
                    10% { transform: rotate(-5deg); }
                    15% { transform: rotate(3deg); }
                    20% { transform: rotate(-3deg); }
                    25% { transform: rotate(2deg); }
                    30% { transform: rotate(-2deg); }
                    40% { transform: rotate(0deg); }
                }
            `}{encodedStyle}</style>
            <canvas ref={canvasRef} class={classes.canvas} id="canvas" width={windowSize.width} height={windowSize.height}></canvas>
        </>
    )
}
