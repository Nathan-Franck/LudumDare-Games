import { declareStyle } from './declareStyle';
import { useEffect, useRef } from 'preact/hooks'
import { sliceToArray, callWasm } from './zigWasmInterface';

const { classes, encodedStyle } = declareStyle({
});

const allResources = callWasm("getAllResources") as Exclude<ReturnType<typeof callWasm<"getAllResources">>, { "error": any }>;
const startTime = Date.now();


export function App() {
  const canvasRef = useRef<HTMLCanvasElement | null>(null);
  function updateAndRender() {
    if (!canvasRef.current)
      return;
    const canvas = canvasRef.current;
    // const ctx = canvas.getContext('2d');
    // if (!ctx)
    //   return;
    // ctx.clearRect(0, 0, canvas.width, canvas.height);
    // {
    //   const { data, width, height } = allResources.background;
    //   ctx.putImageData(new ImageData(sliceToArray.Uint8ClampedArray(data), width, height), 0, 0);
    // }
    // // Archer TEMP
    // {
    //   const animation = allResources.RoyalArcher_FullHD_Attack;
    //   const frame_time = (Date.now() - startTime) / animation.animation_data.framerate;
    //   const { data, width, height } = animation.sprite_sheet;
    //   const clampedData = sliceToArray.Uint8ClampedArray(data);
    //   const imageData = new ImageData(clampedData, width, height);
    //   const currentFrame = Math.floor(frame_time) % (animation.animation_data.frames.length - 1);
    //   const currentFrameData = animation.animation_data.frames[currentFrame];
    //   // ctx.putImageData(imageData,
    //   //   -currentFrameData[0] - currentFrameData[5], -currentFrameData[1] - currentFrameData[6], // center offset
    //   //   currentFrameData[0], currentFrameData[1], // sample offset
    //   //   currentFrameData[2], currentFrameData[3], // sample dimensions
    //   // );

    //   // THE ABOVE CODE DOESNT ALPHA BLEND
    //   // Lets try again, but with nice alpha blending
    //   const tempCanvas = document.createElement('canvas');
    //   tempCanvas.width = width;
    //   tempCanvas.height = height;
    //   const tempCtx = tempCanvas.getContext('2d');
    //   if (!tempCtx)
    //     return;
    //   tempCtx.putImageData(imageData, 0, 0);
    //   const tempImageData = tempCtx.getImageData(0, 0, width, height);
    //   const tempData = tempImageData.data;
    //   for (let i = 0; i < tempData.length; i += 4) {
    //     tempData[i + 3] = clampedData[i + 3];
    //   }
    //   tempCtx.putImageData(tempImageData, 0, 0);
    //   ctx.drawImage(tempCanvas, 0, 0);
    // }
    // Canvas2D api is not going to work for this project, so we will use WebGL!
    const gl = canvas.getContext('webgl');
    if (!gl)
      return;
    // Clear the canvas
    gl.clearColor(0.0, 0.0, 0.0, 1.0);
    gl.clear(gl.COLOR_BUFFER_BIT);

    // Create a buffer
    const positionBuffer = gl.createBuffer();
    gl.bindBuffer(gl.ARRAY_BUFFER, positionBuffer);
    const positions = [
      0, 0,
      0, 0.5,
      0.7, 0,
    ];
    gl.bufferData(gl.ARRAY_BUFFER, new Float32Array(positions), gl.STATIC_DRAW);

    // Create a vertex shader
    const vertexShaderSource = `
      attribute vec4 aVertexPosition;

      void main() {
        gl_Position = aVertexPosition;
      }
    `;
    const vertexShader = gl.createShader(gl.VERTEX_SHADER);
    if (!vertexShader)
      return;
    gl.shaderSource(vertexShader, vertexShaderSource);
    gl.compileShader(vertexShader);
    if (!gl.getShaderParameter(vertexShader, gl.COMPILE_STATUS)) {
      console.error(gl.getShaderInfoLog(vertexShader));
      return;
    }

    // Create a fragment shader
    const fragmentShaderSource = `
      void main() {
        gl_FragColor = vec4(1.0, 1.0, 1.0, 1.0);
      }
    `;
    const fragmentShader = gl.createShader(gl.FRAGMENT_SHADER);
    if (!fragmentShader)
      return;
    gl.shaderSource(fragmentShader, fragmentShaderSource);
    gl.compileShader(fragmentShader);
    if (!gl.getShaderParameter(fragmentShader, gl.COMPILE_STATUS)) {
      console.error(gl.getShaderInfoLog(fragmentShader));
      return;
    }

    // Create a shader program
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

    gl.useProgram(shaderProgram);

    // Get the attribute location
    const positionAttributeLocation = gl.getAttribLocation(shaderProgram, 'aVertexPosition');
    gl.enableVertexAttribArray(positionAttributeLocation);
    gl.vertexAttribPointer(positionAttributeLocation, 2, gl.FLOAT, false, 0, 0);

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

  return (
    <>
      <style>{encodedStyle}</style>
      <canvas ref={canvasRef} id="canvas" width="1000" height="1000"></canvas>
    </>
  )
}
