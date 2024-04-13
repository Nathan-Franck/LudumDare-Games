import { declareStyle } from './declareStyle';
import { useEffect, useRef } from 'preact/hooks'
import { sliceToArray, callWasm } from './zigWasmInterface';

const { classes, encodedStyle } = declareStyle({
});

const allResources = callWasm("getAllResources") as Exclude<ReturnType<typeof callWasm<"getAllResources">>, { "error": any }>;

type Uint8Slice = { type: "Uint8Array", ptr: number, len: number };

class Rendering {
  gl: WebGLRenderingContext;
  constructor(gl: WebGLRenderingContext) {
    this.gl = gl;
  }
  clearCanvas() {
    const gl = this.gl;
    gl.clearColor(0.0, 0.0, 0.0, 1.0);
    gl.clear(gl.COLOR_BUFFER_BIT);
  }
  loadTexture(image: { data: Uint8Slice, width: number, height: number }) {
    const gl = this.gl;
    const texture = gl.createTexture();
    gl.bindTexture(gl.TEXTURE_2D, texture);
    gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA, image.width, image.height, 0, gl.RGBA, gl.UNSIGNED_BYTE, sliceToArray.Uint8Array(image.data));
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.LINEAR);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.CLAMP_TO_EDGE);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE);
    return texture;
  }
  createSpriteMesh() {
    const gl = this.gl;
    const positionBuffer = gl.createBuffer();
    gl.bindBuffer(gl.ARRAY_BUFFER, positionBuffer);
    const positions = [
      100, 100,
      100, 500,
      500, 100,
    ];
    gl.bufferData(gl.ARRAY_BUFFER, new Float32Array(positions), gl.STATIC_DRAW);
    const textureCoordBuffer = gl.createBuffer();
    gl.bindBuffer(gl.ARRAY_BUFFER, textureCoordBuffer);
    const textureCoordinates = [
      0.0, 0.0,
      1.0, 0.0,
      0.0, 1.0,
    ];
    gl.bufferData(gl.ARRAY_BUFFER, new Float32Array(textureCoordinates), gl.STATIC_DRAW);
    const positionAttributeLocation = gl.getAttribLocation(shaderProgram, 'aVertexPosition');
    gl.enableVertexAttribArray(positionAttributeLocation);
    gl.bindBuffer(gl.ARRAY_BUFFER, positionBuffer);
    gl.vertexAttribPointer(positionAttributeLocation, 2, gl.FLOAT, false, 0, 0);
    const textureCoordAttributeLocation = gl.getAttribLocation(shaderProgram, 'aTextureCoord');
    gl.enableVertexAttribArray(textureCoordAttributeLocation);
    gl.bindBuffer(gl.ARRAY_BUFFER, textureCoordBuffer);
    gl.vertexAttribPointer(textureCoordAttributeLocation, 2, gl.FLOAT, false, 0, 0);
    return { positionBuffer, textureCoordBuffer };
  }
  loadShader(fragmentShaderSource: string, vertexShaderSource: string) {
    const gl = this.gl;
    const vertexShader = gl.createShader(gl.VERTEX_SHADER);
    if (!vertexShader)
      return;
    gl.shaderSource(vertexShader, vertexShaderSource);
    gl.compileShader(vertexShader);
    if (!gl.getShaderParameter(vertexShader, gl.COMPILE_STATUS)) {
      console.error(gl.getShaderInfoLog(vertexShader));
      return;
    }
    const fragmentShader = gl.createShader(gl.FRAGMENT_SHADER);
    if (!fragmentShader)
      return;
    gl.shaderSource(fragmentShader, fragmentShaderSource);
    gl.compileShader(fragmentShader);
    if (!gl.getShaderParameter(fragmentShader, gl.COMPILE_STATUS)) {
      console.error(gl.getShaderInfoLog(fragmentShader));
      return;
    }
    const shaderProgram = gl.createProgram();
    if (!shaderProgram)
      return;
    gl.attachShader(shaderProgram, vertexShader);
    gl.attachShader(shaderProgram, fragmentShader);
    gl.linkProgram(shaderProgram);
    if (!gl.getProgramParameter(shaderProgram, gl.LINK_STATUS)) {
      console.error(gl.getProgramInfoLog(shaderProgram));
      return;
    }
    return shaderProgram;
  }
  bindMatrix(shaderProgram: WebGLProgram, matrix: Float32Array) {
    const gl = this.gl;
    const modelViewMatrix = gl.getUniformLocation(shaderProgram, 'uModelViewMatrix');
    gl.uniformMatrix4fv(modelViewMatrix, false, matrix);
  }
}
const startTime = Date.now();

export function App() {
  const canvasRef = useRef<HTMLCanvasElement | null>(null);
  const renderingRef = useRef<Rendering | null>(null);

  function updateAndRender() {

    const rendering = renderingRef.current;
    if (!rendering)
      return;

    rendering.clearCanvas();

    // TODO move out of update
    var mesh = rendering.createSpriteMesh();
    var shaderProgram = rendering.loadShader(`
      attribute vec4 aVertexPosition;
      attribute vec2 aTextureCoord;
      uniform mat4 uModelViewMatrix;
      varying highp vec2 vTextureCoord;

      void main() {
        gl_Position = uModelViewMatrix * aVertexPosition;
        vTextureCoord = aTextureCoord;
      }
    `, `
      uniform sampler2D uSampler;
      varying highp vec2 vTextureCoord;

      void main() {
        gl_FragColor = texture2D(uSampler, vTextureCoord);
      }
    `);
    if (!shaderProgram)
      return;

    // Actual binding and rendering
    rendering.gl.useProgram(shaderProgram);
    rendering.bindMatrix(shaderProgram, new Float32Array([
      2 / 1920, 0, 0, 0,
      0, -2 / 1080, 0, 0,
      0, 0, 1, 0,
      -1, 1, 0, 1,
    ]));

    // Get the attribute location
    const positionAttributeLocation = gl.getAttribLocation(shaderProgram, 'aVertexPosition');
    gl.enableVertexAttribArray(positionAttributeLocation);
    gl.bindBuffer(gl.ARRAY_BUFFER, positionBuffer);
    gl.vertexAttribPointer(positionAttributeLocation, 2, gl.FLOAT, false, 0, 0);

    const textureCoordAttributeLocation = gl.getAttribLocation(shaderProgram, 'aTextureCoord');
    gl.enableVertexAttribArray(textureCoordAttributeLocation);
    gl.bindBuffer(gl.ARRAY_BUFFER, textureCoordBuffer);
    gl.vertexAttribPointer(textureCoordAttributeLocation, 2, gl.FLOAT, false, 0, 0);

    const { data, width, height } = allResources.background;
    const texture = gl.createTexture();
    gl.bindTexture(gl.TEXTURE_2D, texture);
    gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA, width, height, 0, gl.RGBA, gl.UNSIGNED_BYTE, sliceToArray.Uint8Array(data));
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.LINEAR);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.CLAMP_TO_EDGE);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE);

    gl.activeTexture(gl.TEXTURE0);



    // Draw
    gl.drawArrays(gl.TRIANGLES, 0, 3);

  }

  useEffect(() => {
    const loop = () => {
      updateAndRender();
      requestAnimationFrame(loop);
    }
    requestAnimationFrame(loop);
  }, []);
  useEffect(() => {
    if (!canvasRef.current)
      return;
    const canvas = canvasRef.current;
    const gl = canvas.getContext('webgl');
    if (!gl)
      return;
    renderingRef.current = new Rendering(gl);
  }, []);

  return (
    <>
      <style>{encodedStyle}</style>
      <canvas ref={canvasRef} id="canvas" width="1000" height="1000"></canvas>
    </>
  )
}
