import { declareStyle } from './declareStyle';
import { useEffect, useRef, useState } from 'preact/hooks'
import { sliceToArray, callWasm } from './zigWasmInterface';
import { ShaderBuilder, Mat4, Vec2, Vec4 } from "./shaderBuilder";

const { classes, encodedStyle } = declareStyle({
    frameRate: {
        fontFamily: 'monospace',
    },
    canvas: {
        width: "100%",
        height: "100%",
        position: "absolute",
        left: 0,
        top: 0,
        zIndex: 0,
    },
});

const allResources = callWasm("getAllResources") as Exclude<ReturnType<typeof callWasm<"getAllResources">>, { "error": any }>;
const startTime = Date.now();

export function App() {
    const canvasRef = useRef<HTMLCanvasElement | null>(null);
    const [framerate, setFramerate] = useState(0);

    useEffect(() => {
    }, []);
    useEffect(() => {
        if (!canvasRef.current)
            return;
        const canvas = canvasRef.current;
        const gl = canvas.getContext('webgl2');
        if (!gl)
            return;
        // renderingRef.current = new Rendering(gl);

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
        const animation = allResources.RoyalArcher_FullHD_Attack;
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
        const renderScale = window.innerHeight / 1080;
        const world = {
            perspectiveMatrix: [
                2 / canvas.width * renderScale, 0, 0, 0,
                0, -2 / canvas.height * renderScale, 0, 0,
                0, 0, 1, 0,
                -1, 1, 0, 1
            ] as Mat4,
        };
        const spriteSheet = animation.sprite_sheet;
        const character = {
            ...sprite,
            texture: ShaderBuilder.loadImageData(gl, sliceToArray.Uint8Array(spriteSheet.data), spriteSheet.width, spriteSheet.height),
            textureResolution: [spriteSheet.width, spriteSheet.height] as Vec2,
            // sampleRect: [0, 0, spriteSheet.width, spriteSheet.height] as Vec4,
        };
        const background = {
            ...sprite,
            spritePosition: ShaderBuilder.createBuffer(gl, new Float32Array([
                0, 0
            ])),
            texture: ShaderBuilder.loadImageData(gl, sliceToArray.Uint8Array(allResources.background.data), allResources.background.width, allResources.background.height),
            textureResolution: [allResources.background.width, allResources.background.height] as Vec2,
            sampleRect: [0, 0, allResources.background.width, allResources.background.height] as Vec4,
        };
        function updateAndRender() {
            if (!gl)
                return;

            {
                gl.clearColor(0, 0, 0, 1);
                // gl.enable(gl.DEPTH_TEST);
                gl.clear(gl.COLOR_BUFFER_BIT);
                gl.viewport(0, 0, canvas.width, canvas.height);
                gl.enable(gl.BLEND);
                gl.blendFunc(gl.SRC_ALPHA, gl.ONE_MINUS_SRC_ALPHA);
            }

            const time = Date.now() - startTime;
            // const { player } = callWasm("update", { time_ms: time, keyboard: { left: false, right: false, up: false, down: false, interact: false }, joystick: { x: 0, y: 0, interact: false } }) as Exclude<ReturnType<typeof callWasm<"update">>, { "error": any }>;
            // const { x, y, animation } = player;

            const frame_time = (Date.now() - startTime) / animation.animation_data.framerate;
            const currentFrame = Math.floor(frame_time) % animation.animation_data.frames.length;
            const currentFrameData = animation.animation_data.frames[currentFrame];

            ShaderBuilder.renderMaterial(gl, spriteMaterial, {
                ...world,
                ...background,
            });
            ShaderBuilder.renderMaterial(gl, spriteMaterial, {
                ...world,
                ...character,
                spritePosition: ShaderBuilder.createBuffer(gl, new Float32Array([
                    0 - currentFrameData[5], 0 - currentFrameData[6]
                ])),
                sampleRect: [currentFrameData[0], currentFrameData[1], currentFrameData[2], currentFrameData[3]] as Vec4,
            });
        }
        let lastTime = Date.now();
        let frameCount = 0;
        const loop = () => {
            const time = Date.now();
            frameCount++;
            if (time - lastTime > 1000) {
                setFramerate(frameCount);
                frameCount = 0;
                lastTime = time;
            }
            updateAndRender();
            requestAnimationFrame(loop);
        }
        requestAnimationFrame(loop);
    }, []);

    return (
        <>
            <div class={classes.frameRate}>Frame Rate: {framerate}</div>
            <style>{encodedStyle}</style>
            <canvas ref={canvasRef} class={classes.canvas} id="canvas" width={window.innerWidth} height={window.innerHeight}></canvas>
        </>
    )
}
