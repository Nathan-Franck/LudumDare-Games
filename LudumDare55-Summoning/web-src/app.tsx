import { declareStyle } from './declareStyle';
import { useEffect, useRef, useState } from 'preact/hooks'
import { sliceToArray, callWasm } from './zigWasmInterface';
import { ShaderBuilder, Mat4, Vec2, Vec4 } from "./shaderBuilder";

const { classes, encodedStyle } = declareStyle({
    frameRate: {
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
});

const allResources = callWasm("getAllResources") as Exclude<ReturnType<typeof callWasm<"getAllResources">>, { "error": any }>;
const startTime = Date.now();

export function App() {
    const canvasRef = useRef<HTMLCanvasElement | null>(null);

    const [framerate, setFramerate] = useState(0);
    let keyboard = { left: false, right: false, up: false, down: false, interact: false };
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

        function loadAnimation(animation: typeof allResources.SummoningChamber_FullHD_ChamberProgressIncrease) {
            const spriteSheet = animation.sprite_sheet;
            return {
                ...sprite,
                texture: ShaderBuilder.loadImageData(gl as WebGL2RenderingContext, sliceToArray.Uint8Array(spriteSheet.data), spriteSheet.width, spriteSheet.height),
                textureResolution: [spriteSheet.width, spriteSheet.height] as Vec2,
                animation_data: animation.animation_data,
            };
        }

        function updateAnimation(animation: typeof character) {
            const frame_time = (Date.now() - startTime) / animation.animation_data.framerate;
            const currentFrame = Math.floor(frame_time) % animation.animation_data.frames.length;
            return animation.animation_data.frames[currentFrame];
        }

        function renderAnimation(animation: typeof character, spritePosition: { x: number, y: number }) {
            const gl2 = gl as WebGL2RenderingContext;
            const currentFrameData = updateAnimation(animation);
            ShaderBuilder.renderMaterial(gl2, spriteMaterial, {
                ...world,
                ...animation,
                spritePosition: ShaderBuilder.createBuffer(gl2, new Float32Array([
                    spritePosition.x - currentFrameData[5], spritePosition.y - currentFrameData[6]
                ])),
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
                spritePosition: { type: "attribute", unit: "vec2", instanced: true },
            },
            vertSource: `
                precision highp float;
                void main(void) {
                    gl_Position = perspectiveMatrix * vec4(meshVertexUV * sampleRect.zw + spritePosition, 0, 1);
                    uv = meshVertexUV;
                }
            `,
            fragSource: `
                precision highp float;
                void main(void) {
                    gl_FragColor = texture2D(texture, (uv * sampleRect.zw + sampleRect.xy) / textureResolution);
                }
            `,
        });
        const sprite = {
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
        const renderScale = windowSize.height / 1080;
        const world = {
            perspectiveMatrix: [
                2 / windowSize.width * renderScale, 0, 0, 0,
                0, -2 / windowSize.height * renderScale, 0, 0,
                0, 0, 1, 0,
                -1, 1, 0, 1
            ] as Mat4,
        };
        const character = loadAnimation(allResources.RoyalArcher_FullHD_Attack);
        const chamber = loadAnimation(allResources.SummoningChamber_FullHD_ChamberProgressIncrease);
        const background = {
            ...sprite,
            spritePosition: ShaderBuilder.createBuffer(gl, new Float32Array([
                0, 0
            ])),
            texture: ShaderBuilder.loadImageData(gl, sliceToArray.Uint8Array(allResources.background.data), allResources.background.width, allResources.background.height),
            textureResolution: [allResources.background.width, allResources.background.height] as Vec2,
            sampleRect: [0, 0, allResources.background.width, allResources.background.height] as Vec4,
        };

        {
            gl.clearColor(0, 0, 0, 1);
            gl.clear(gl.COLOR_BUFFER_BIT);
            gl.viewport(0, 0, windowSize.width, windowSize.height);
            gl.enable(gl.BLEND);
            gl.blendFunc(gl.SRC_ALPHA, gl.ONE_MINUS_SRC_ALPHA);
        }

        function updateAndRender() {
            if (!gl)
                return;

            let joystick = { x: 0, y: 0, interact: false };
            const gamepads = navigator.getGamepads();
            for (let i = 0; i < gamepads.length; i++) {
                const gamepad = gamepads[i];
                if (gamepad) {
                    joystick = {
                        x: gamepad.axes[0],
                        y: gamepad.axes[1],
                        interact: gamepad.buttons[0].pressed,
                    };
                    break;
                }
            }

            const time = Date.now() - startTime;
            const { player } = callWasm("update", {
                time_ms: time,
                keyboard,
                joystick,
            }) as Exclude<ReturnType<typeof callWasm<"update">>, { "error": any }>;

            ShaderBuilder.renderMaterial(gl, spriteMaterial, {
                ...world,
                ...background,
            });
            renderAnimation(character, player);
            renderAnimation(chamber, { x: 200, y: 200 });
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
            <div class={classes.frameRate}>Frame Rate: {framerate}</div>
            <style>{encodedStyle}</style>
            <canvas ref={canvasRef} class={classes.canvas} id="canvas" width={windowSize.width} height={windowSize.height}></canvas>
        </>
    )
}
