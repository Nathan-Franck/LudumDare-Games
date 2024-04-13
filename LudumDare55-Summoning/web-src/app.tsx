import { declareStyle } from './declareStyle';
import { useEffect, useRef, useState } from 'preact/hooks'
import { sliceToArray, callWasm } from './zigWasmInterface';
import { ShaderBuilder } from "./shaderBuilder";

const { classes, encodedStyle } = declareStyle({
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
                meshVertexPosition: { type: "attribute", unit: "vec4" },
                meshVertexUV: { type: "attribute", unit: "vec2" },
                modelViewMatrix: { type: "uniform", unit: "mat4", count: 1 },
                texture: { type: "uniform", unit: "sampler2D", count: 1 },
                uv: { type: "varying", unit: "vec2" },
                modelPosition: { type: "attribute", unit: "vec3", instanced: true },
            },
            vertSource: `
                precision highp float;
                void main(void) {
                    gl_Position = modelViewMatrix * meshVertexPosition  + vec4(modelPosition, 1.0);
                    uv = meshVertexUV;
                }
            `,
            fragSource: `
                precision highp float;
                void main(void) {
                    gl_FragColor = texture2D(texture, uv);
                }
            `,
        });
        const meshTriangle = ShaderBuilder.createElementBuffer(gl, new Uint16Array([
            0, 1, 2,
            0, 2, 3
        ]));
        const meshVertexPosition = ShaderBuilder.createBuffer(gl, new Float32Array([
            0, 0, 0, 0,
            1, 0, 0, 0,
            1, 1, 0, 0,
            0, 1, 0, 0,
        ]));
        const meshVertexUV = ShaderBuilder.createBuffer(gl, new Float32Array([
            0, 0,
            1, 0,
            1, 1,
            0, 1
        ]));
        const modelPosition = ShaderBuilder.createBuffer(gl, new Float32Array([
            0, 0, 0
        ]));
        const spriteSheet = allResources.RoyalArcher_FullHD_Attack.sprite_sheet;
        const texture = ShaderBuilder.loadImageData(gl, sliceToArray.Uint8Array(spriteSheet.data), spriteSheet.width, spriteSheet.height);
        function updateAndRender() {
            if (!gl)
                return;

            {
                gl.clearColor(0, 0, 0, 1);
                gl.enable(gl.DEPTH_TEST);
                gl.clear(gl.COLOR_BUFFER_BIT);
                gl.viewport(0, 0, canvas.width, canvas.height);
                gl.enable(gl.BLEND);
                gl.blendFunc(gl.SRC_ALPHA, gl.ONE_MINUS_SRC_ALPHA);
            }

            const time = Date.now() - startTime;
            // const { player } = callWasm("update", { time_ms: time, keyboard: { left: false, right: false, up: false, down: false, interact: false }, joystick: { x: 0, y: 0, interact: false } }) as Exclude<ReturnType<typeof callWasm<"update">>, { "error": any }>;
            // const { x, y, animation } = player;

            ShaderBuilder.renderMaterial(gl, spriteMaterial, {
                modelPosition: modelPosition,
                meshTriangle: meshTriangle,
                meshVertexPosition: meshVertexPosition,
                meshVertexUV: meshVertexUV,
                texture: texture,
                modelViewMatrix: [1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1],
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
            <canvas ref={canvasRef} id="canvas" width="540" height="480"></canvas>
        </>
    )
}
