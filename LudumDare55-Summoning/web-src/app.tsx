import { declareStyle } from './declareStyle';
import { useEffect, useRef, useState } from 'preact/hooks'
import { sliceToArray, callWasm, sliceToString } from './zigWasmInterface';
import { ShaderBuilder, Mat4, Vec2, Vec4 } from "./shaderBuilder";

const userMessage = {
    fontFamily: 'monospace',
    fontSize: '2em',
    position: 'absolute',
    top: '50%',
    left: '50%',
    transform: 'translate(-50%, -50%)',
    overflow: 'hidden',
    whiteSpace: 'nowrap',
    animation: 'rotationalPunch 4s',
} as const;

const { classes, encodedStyle } = declareStyle({
    container: {
        backgroundColor: "#010101",
        margin: 0,
        padding: 0,
        width: '100%',
        height: '100%',
        overflow: 'hidden',
    },
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
    levelMessage: {
        ...userMessage,
        color: 'white',
    },
    dangerMessage: {
        ...userMessage,
        color: 'red',
    },
    successMessage: {
        ...userMessage,
        color: 'green',
    },
});

const allResources = callWasm("getAllResources") as Exclude<ReturnType<typeof callWasm<"getAllResources">>, { "error": any }>;
const startTime = Date.now();

export function App() {
    const canvasRef = useRef<HTMLCanvasElement | null>(null);

    const [framerate, setFramerate] = useState(0);
    const [inDanger, setInDanger] = useState(false);
    const [fail, setFail] = useState(false);
    const [win, setWin] = useState(false);
    const [success, setSuccess] = useState(false);
    const [machinesBroken, setMachinesBroken] = useState([false, false, false, false]);
    const [levelTitle, setLevelTitle] = useState(null as string | null);

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
                event.preventDefault();
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
                ...spriteMesh,
                texture: ShaderBuilder.loadImageData(gl as WebGL2RenderingContext, sliceToArray.Uint8Array(sprite.data), sprite.width, sprite.height),
                textureResolution: [sprite.width, sprite.height] as Vec2,
                sampleRect: [0, 0, sprite.width, sprite.height] as Vec4,
            }
        }

        function loadAnimation(animation: typeof allResources.graphics.chamber_increase) {
            const spriteSheet = animation.sprite_sheet;
            return {
                ...spriteMesh,
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
            const currentFrame = progress == null
                ? Math.floor((Date.now() - startTime) / animation.animation_data.framerate) % animation.animation_data.frames.length
                : Math.min(Math.max(Math.floor(progress * animation.animation_data.frames.length), 0), animation.animation_data.frames.length - 1);
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
        // Flat color blended overtop of previously rendered
        var overlayMaterial = ShaderBuilder.generateMaterial(gl, {
            mode: 'TRIANGLES',
            globals: {
                meshTriangle: { type: "element" },
                dimensions: { type: "uniform", unit: "vec2", count: 1 },
                meshVertexUV: { type: "attribute", unit: "vec2" },
                perspectiveMatrix: { type: "uniform", unit: "mat4", count: 1 },
                color: { type: "uniform", unit: "vec4", count: 1 },
            },
            vertSource: `
                precision highp float;
                void main(void) {
                    gl_Position = perspectiveMatrix * vec4(meshVertexUV * dimensions, 0, 1);
                }
            `,
            fragSource: `
                precision highp float;
                void main(void) {
                    gl_FragColor = vec4(color.rgb * color.a, color.a);
                }
            `,
        });
        const spriteMesh = {
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
        const chamber_materialize = loadAnimation(graphics.chamber_materialize);
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
            if (state.current_level < allResources.config.levels.length)
                setLevelTitle((time - state.level_start_time_ms) < 2000 ? sliceToString(allResources.config.levels[state.current_level].title) : null);
            setInDanger(state.in_danger);
            setWin(state.win_time_ms != null);
            setFail(state.fail_time_ms != null);
            setSuccess(state.victory_time_ms != null);
            setMachinesBroken(state.machine_states.map((machine) => machine.broken));

            // Build a list of things to render.
            const thingsToRender: Array<
                | { type: "sprite", sprite: typeof machine, origin: { x: number, y: number }, position: { x: number, y: number } }
                | { type: "animation", animation: typeof ghost.idleSide, position: { x: number, y: number }, scale: { x: number, y: number }, progress?: number }
            > = [
                    { type: "sprite", sprite: background, origin: { x: 0, y: 0 }, position: { x: 0, y: 0 } },
                    {
                        type: "animation", position: allResources.config.chamber_location, scale: { x: 1, y: 1 }, ...(
                            state.victory_time_ms != null
                                ? { animation: chamber_materialize, progress: (time - state.victory_time_ms) / allResources.config.victory_span_ms }
                                : { animation: chamber_increase, progress: state.chamber_progress }
                        )
                    },
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

            // Overlay fade to black on fail
            if (state.fail_time_ms != null) {
                ShaderBuilder.renderMaterial(gl, overlayMaterial, {
                    ...world,
                    ...spriteMesh,
                    dimensions: [1920, 1080] as Vec2,
                    color: [0, 0, 0, Math.min((time - state.fail_time_ms) / allResources.config.fail_span_ms, 1)],
                });
            }
            // Overlay fade to black on win
            if (state.win_time_ms != null) {
                ShaderBuilder.renderMaterial(gl, overlayMaterial, {
                    ...world,
                    ...spriteMesh,
                    dimensions: [1920, 1080] as Vec2,
                    color: [0, 0, 0, Math.min((time - state.win_time_ms) / allResources.config.victory_span_ms, 1)],
                });
            }
            // Overlay flash from white on level start
            const flash_value = Math.min(1 - (time - state.level_start_time_ms) / 100, 1);
            if (flash_value > 0) {
                ShaderBuilder.renderMaterial(gl, overlayMaterial, {
                    ...world,
                    ...spriteMesh,
                    dimensions: [1920, 1080] as Vec2,
                    color: [1, 1, 1, flash_value],
                });
            }
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
        <div class={classes.container}>
            <div class={classes.devTool}>Frame Rate: {framerate}</div>
            {win ?<div class={classes.levelMessage}>Summoning Succeeded! <div>Game by Nathan Franck and Oscar Romero</div></div>
            : success ? <div class={classes.successMessage}>Summoning Succeeded!</div>
                : fail ? <div class={classes.dangerMessage}>Summoning failed, please try again.</div>
                    : inDanger ? <div class={classes.dangerMessage}>DANGER! MACHINES MUST BE KEPT ONLINE!</div> : null}
            {success ? null : machinesBroken.map((broken, index) => broken ? <div class={classes.dangerMessage} style={{
                left: `${allResources.config.machine_locations[index].x / 1920 * 100}%`,
                top: `${allResources.config.machine_locations[index].y / 1080 * 100}%`,
            }}>{"!!! MACHINE BROKEN !!!"}</div> : null)}
            {levelTitle != null ? <div class={classes.levelMessage}>{levelTitle}</div> : null}
            <style>{`
                @keyframes rotationalPunch {
                    0% { transform: translate(-50%, -50%) rotate(0deg); }
                    5% { transform: translate(-50%, -50%) rotate(5deg); }
                    10% { transform: translate(-50%, -50%) rotate(-5deg); }
                    15% { transform: translate(-50%, -50%) rotate(3deg); }
                    20% { transform: translate(-50%, -50%) rotate(-3deg); }
                    25% { transform: translate(-50%, -50%) rotate(2deg); }
                    30% { transform: translate(-50%, -50%) rotate(-2deg); }
                    40% { transform: translate(-50%, -50%) rotate(0deg); }
                }
            `}{encodedStyle}</style>
            <canvas ref={canvasRef} class={classes.canvas} id="canvas" width={windowSize.width} height={windowSize.height}></canvas>
        </div>
    )
}
