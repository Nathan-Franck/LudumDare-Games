export type WasmInterface = {
	getAllResources: () => { RoyalArcher_FullHD_Attack: { sprite_sheet: { data: { type: "Uint8Array", ptr: number, len: number }, width: number, height: number }, animation_data: { framerate: number, frames: number[][] } } }, 
	update: (arg0: { time_ms: number, keyboard: { left: boolean, right: boolean, up: boolean, down: boolean, interact: boolean }, joystick: { x: number, y: number, interact: boolean } }) => { player: { x: number, y: number, animation: { name: { type: "Uint8Array", ptr: number, len: number }, frame: number } } }, 
}