const std = @import("std");
const assets = @import("assets.zig");
const wasm_entry = @import("wasm_entry.zig");

pub fn getAllResources() !struct {
    graphics: struct {
        background: assets.PngImage,
        machine: assets.PngImage,
        chamber_increase: assets.SpriteSheetAnimation,
        ghost_idle_side: assets.SpriteSheetAnimation,
        ghost_idle_front: assets.SpriteSheetAnimation,
        ghost_idle_back: assets.SpriteSheetAnimation,
        ghost_fixing: assets.SpriteSheetAnimation,
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
            .ghost_idle_side = try assets.SpriteSheetAnimation.load(allocator, "Ghost_FullHD_IdleSide"),
            .ghost_idle_front = try assets.SpriteSheetAnimation.load(allocator, "Ghost_FullHD_IdleFront"),
            .ghost_idle_back = try assets.SpriteSheetAnimation.load(allocator, "Ghost_FullHD_IdleBack"),
            .ghost_fixing = try assets.SpriteSheetAnimation.load(allocator, "Ghost_FullHD_FixAnimation"),
        },
    };
}

const Direction = enum {
    Left,
    Right,
    Up,
    Down,
};

const Action = enum {
    Idle,
    Fixing,
};

const State = struct {
    last_time_ms: u32,
    player: struct { x: f32, y: f32, direction: Direction, action: Action },
};

const Config = struct {
    controller_dead_zone: f32,
    player_speed: f32,
    fix_proximity: f32,
    fix_snap_offset: struct { x: f32, y: f32 },
    chamber_location: struct { x: f32, y: f32 },
    machine_locations: [4]struct { x: f32, y: f32 },
};

const config: Config = .{
    .controller_dead_zone = 0.1,
    .player_speed = 200,
    .fix_proximity = 200,
    .fix_snap_offset = .{ .x = 180.0, .y = 0.0 },
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
    .player = .{ .x = 950.0, .y = 650.0, .direction = Direction.Down, .action = Action.Idle },
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
    const allocator = std.heap.page_allocator;
    _ = allocator; // autofix

    const delta_time: f32 = @as(f32, @floatFromInt(inputs.time_ms - state.last_time_ms)) / 1000.0;
    const screen_width = 1920;

    // Combine inputs from keyboard and joystick
    const direction_input = .{
        .x = if (inputs.keyboard.left) -1.0 else if (inputs.keyboard.right) 1.0 else if (@abs(inputs.joystick.x) < config.controller_dead_zone) 0 else inputs.joystick.x,
        .y = if (inputs.keyboard.down) 1.0 else if (inputs.keyboard.up) -1.0 else if (@abs(inputs.joystick.y) < config.controller_dead_zone) 0 else inputs.joystick.y,
    };
    // wasm_entry.dumpDebugLog(try std.fmt.allocPrint(allocator, "{?}", .{direction_input}));

    // Move player
    state.player.x += direction_input.x * config.player_speed * delta_time;
    state.player.y += direction_input.y * config.player_speed * delta_time;
    state.player.direction = if (direction_input.x < 0)
        Direction.Left
    else if (direction_input.x > 0)
        Direction.Right
    else if (direction_input.y < 0)
        Direction.Up
    else if (direction_input.y > 0)
        Direction.Down
    else
        state.player.direction;

    // Check proximity to machines
    for (config.machine_locations) |machine| {
        const dx = state.player.x - machine.x;
        const dy = state.player.y - machine.y;
        const distance = @sqrt(dx * dx + dy * dy);
        if (distance < config.fix_proximity) {
            state.player.action = .Fixing;
            const interaction_flip: f32 = if (machine.x < screen_width / 2) 1.0 else -1.0;
            state.player.x = machine.x + config.fix_snap_offset.x * interaction_flip;
            state.player.y = machine.y + config.fix_snap_offset.y;
            state.player.direction = if (machine.x < state.player.x) Direction.Left else Direction.Right;
        }
    }

    state.last_time_ms = inputs.time_ms;

    return state;
}
