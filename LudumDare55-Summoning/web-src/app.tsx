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
    const ctx = canvas.getContext('2d');
    if (!ctx)
      return;
    console.log("hello");
    ctx.clearRect(0, 0, canvas.width, canvas.height);
    const animation = allResources.RoyalArcher_FullHD_Attack;
    const frame_time = (Date.now() - startTime) / animation.animation_data.framerate;
    const { data, width, height } = animation.sprite_sheet;
    const clampedData = sliceToArray.Uint8ClampedArray(data);
    const imageData = new ImageData(clampedData, width, height);
    const currentFrame = Math.floor(frame_time) % animation.animation_data.frames.length;
    const currentFrameData = sliceToArray.Uint32Array(animation.animation_data.frames[currentFrame]);
    // ctx.putImageData(imageData,
    //   0, 0,
    //   // -currentFrameData[5], -currentFrameData[6], // center-offset
    //   currentFrameData[0], currentFrameData[1], // sample-offset
    //   currentFrameData[2], currentFrameData[3], // dimensions
    // );
    ctx.putImageData(imageData, -306, -306, 306, 306, 296, 296);
    console.log(currentFrameData);
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
