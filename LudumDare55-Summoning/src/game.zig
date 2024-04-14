const std = @import("std");
const assets = @import("assets.zig");

pub fn getAllResources() !struct {
    graphics: struct {
        background: assets.PngImage,
        machine: assets.PngImage,
        chamber_increase: assets.SpriteSheetAnimation,
        ghost_idle: assets.SpriteSheetAnimation,
        ghost_fixing: assets.SpriteSheetAnimation,
        archer: assets.SpriteSheetAnimation,
    },
    config: Config,
} {
    const allocator = std.heap.page_allocator;
    return .{
        .config = config,
        .graphics = .{
            .background = try assets.PngImage.load(allocator, "Summoning_0005_BG"),
            .machine = try assets.PngImage.load(allocator, "Summoning_0001_Machine"),
            .chamber_increase = try assets.SpriteSheetAnimation.load(allocator, "SummoningChamber_FullHD_ChamberProgressIncrease"),
            .ghost_idle = try assets.SpriteSheetAnimation.load(allocator, "Ghost_FullHD_Idle"),
            .ghost_fixing = try assets.SpriteSheetAnimation.load(allocator, "Ghost_FullHD_FixAnimation"),
            .archer = try assets.SpriteSheetAnimation.load(allocator, "RoyalArcher_FullHD_Attack"),
        },
    };
}

const State = struct {
    last_time_ms: u32,
    player: struct { x: f32, y: f32 },
};

const Config = struct {
    player_speed: f32,
    chamber_location: struct { x: f32, y: f32 },
    machine_locations: [4]struct { x: f32, y: f32 },
};

const config: Config = .{
    .player_speed = 200,
    .chamber_location = .{ .x = 950.0, .y = 200.0 },
    .machine_locations = .{
        .{ .x = 250, .y = 200 },
        .{ .x = 250, .y = 850 },
        .{ .x = 1650, .y = 200 },
        .{ .x = 1650, .y = 850 },
    },
};

var state: State = .{
    .last_time_ms = 0,
    .player = .{ .x = 950.0, .y = 650.0 },
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
        .y = if (inputs.keyboard.down) 1.0 else if (inputs.keyboard.up) -1.0 else inputs.joystick.y,
    };

    // Move player
    state.player.x += direction_input.x * config.player_speed * delta_time;
    state.player.y += direction_input.y * config.player_speed * delta_time;

    state.last_time_ms = inputs.time_ms;

    return state;
}
