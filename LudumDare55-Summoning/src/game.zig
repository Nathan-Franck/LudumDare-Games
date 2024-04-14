const std = @import("std");
const assets = @import("assets.zig");

pub fn getAllResources() !struct {
    background: assets.PngImage,
    SummoningChamber_FullHD_ChamberProgressIncrease: assets.SpriteSheetAnimation,
    Ghost_FullHD_Idle: assets.SpriteSheetAnimation,
} {
    const allocator = std.heap.page_allocator;
    return .{
        .background = try assets.PngImage.load(allocator, "background"),
        .SummoningChamber_FullHD_ChamberProgressIncrease = try assets.SpriteSheetAnimation.load(allocator, "SummoningChamber_FullHD_ChamberProgressIncrease"),
        .Ghost_FullHD_Idle = try assets.SpriteSheetAnimation.load(allocator, "Ghost_FullHD_Idle"),
    };
}

const State = struct {
    last_time_ms: u32,
    player: struct { x: f32, y: f32 },
};

var state: State = .{
    .last_time_ms = 0,
    .player = .{ .x = 0.0, .y = 0.0 },
};

pub fn update(inputs: struct {
    time_ms: u32,
    keyboard: struct {
        left: bool,
        right: bool,
        up: bool,
        down: bool,
        interact: bool,
    },
    joystick: struct {
        x: f32,
        y: f32,
        interact: bool,
    },
}) !State {
    const delta_time: f32 = @as(f32, @floatFromInt(inputs.time_ms - state.last_time_ms)) / 1000.0;

    // Combine inputs from keyboard and joystick
    const direction_input = .{
        .x = if (inputs.keyboard.left) -1.0 else if (inputs.keyboard.right) 1.0 else inputs.joystick.x,
        .y = if (inputs.keyboard.down) -1.0 else if (inputs.keyboard.up) 1.0 else inputs.joystick.y,
    };

    // Move player
    state.player.x += direction_input.x * 200 * delta_time;
    state.player.y += direction_input.y * 200 * delta_time;

    state.last_time_ms = inputs.time_ms;

    return state;
}
