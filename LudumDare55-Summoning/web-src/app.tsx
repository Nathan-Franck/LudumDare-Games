import { declareStyle } from './declareStyle';
import { useEffect, useRef, useState } from 'preact/hooks'
import { sliceToArray, callWasm } from './zigWasmInterface';
import { ShaderBuilder, Mat4, Vec2, Vec4 } from "./shaderBuilder";

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
                    gl_Position = perspectiveMatrix * vec4(uv * sampleRect.zw + spritePosition, 0, 1);
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
        const spriteSheet = allResources.RoyalArcher_FullHD_Attack.sprite_sheet;
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
        // Map canvas dimensions to 3D space
        const world = {
            perspectiveMatrix: [2 / canvas.width, 0, 0, 0, 0, -2 / canvas.height, 0, 0, 0, 0, 1, 0, -1, 1, 0, 1] as Mat4,
        };
        const character = {
            ...sprite,
            spritePosition: ShaderBuilder.createBuffer(gl, new Float32Array([
                0, 0
            ])),
            textureResolution: [spriteSheet.width, spriteSheet.height] as Vec2,
            texture: ShaderBuilder.loadImageData(gl, sliceToArray.Uint8Array(spriteSheet.data), spriteSheet.width, spriteSheet.height),
            sampleRect: [0, 0, spriteSheet.width, spriteSheet.height] as Vec4,
        };
        const background = {
            ...sprite,
            spritePosition: ShaderBuilder.createBuffer(gl, new Float32Array([
                0, 0
            ])),
            textureResolution: [allResources.background.width, allResources.background.height] as Vec2,
            texture: ShaderBuilder.loadImageData(gl, sliceToArray.Uint8Array(allResources.background.data), allResources.background.width, allResources.background.height),
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


            ShaderBuilder.renderMaterial(gl, spriteMaterial, {
                ...world,
                ...background,
            });
            ShaderBuilder.renderMaterial(gl, spriteMaterial, {
                ...world,
                ...character,
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
