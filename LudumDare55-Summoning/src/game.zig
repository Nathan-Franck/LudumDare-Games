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
            .chamber_increase = try assets.SpriteSheetAnimation.load(allocator, "SummoningChamber_FullHD_Materlalize "),
            .ghost_idle_side = try assets.SpriteSheetAnimation.load(allocator, "Ghost_FullHD_IdleSide"),
            .ghost_idle_front = try assets.SpriteSheetAnimation.load(allocator, "Ghost_FullHD_IdleFront"),
            .ghost_idle_back = try assets.SpriteSheetAnimation.load(allocator, "Ghost_FullHD_IdleBack"),
            .ghost_fixing = try assets.SpriteSheetAnimation.load(allocator, "Ghost_FullHD_FixAnimation"),
        },
    };
}

const ViewDirection = enum {
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
    was_interacting: bool,
    was_dashing: bool,
    last_time_ms: u64,
    player: struct {
        position: Point,
        movement_direction: Point,
        view_direction: ViewDirection,
        action: Action,
        dash_time_ms: u64,
    },
};

const Point = struct { x: f32, y: f32 };

const Config = struct {
    screen: struct { width: u32, height: u32 },
    controller_dead_zone: f32,
    player_radius: f32,
    player_speed: f32,
    player_direction_smoothness: f32,
    player_dash_speed: f32,
    player_dash_duration_ms: u64,
    player_dash_cooldown_ms: u64,
    fix_proximity: f32,
    fix_snap_offset: Point,
    chamber_location: Point,
    machine_locations: [4]Point,
};

const config: Config = .{
    .screen = .{ .width = 1920, .height = 1080 },
    .controller_dead_zone = 0.1,
    .player_radius = 50.0,
    .player_speed = 400,
    .player_direction_smoothness = 5,
    .player_dash_speed = 1600,
    .player_dash_duration_ms = 200,
    .player_dash_cooldown_ms = 500,
    .fix_proximity = 200,
    .fix_snap_offset = .{ .x = 180.0, .y = 0.0 },
    .chamber_location = .{ .x = 950.0, .y = 300.0 },
    .machine_locations = .{
        .{ .x = 250, .y = 200 },
        .{ .x = 250, .y = 850 },
        .{ .x = 1650, .y = 200 },
        .{ .x = 1650, .y = 850 },
    },
};

var state: State = .{
    .was_interacting = false,
    .was_dashing = false,
    .last_time_ms = 0,
    .player = .{
        .position = .{ .x = 950.0, .y = 650.0 },
        .movement_direction = .{ .x = 0, .y = 0 },
        .view_direction = ViewDirection.Down,
        .action = Action.Idle,
        .dash_time_ms = 0,
    },
};

pub fn update(inputs: struct {
    time_ms: u64,
    keyboard: struct {
        left: bool,
        right: bool,
        up: bool,
        down: bool,
        interact: bool,
        dash: bool,
    },
    joystick: struct {
        x: f32,
        y: f32,
        interact: bool,
        dash: bool,
    },
}) !State {
    const delta_time: f32 = @as(f32, @floatFromInt(inputs.time_ms - state.last_time_ms)) / 1000.0;

    // Combine inputs from keyboard and joystick.
    const input_direction = .{
        .x = if (inputs.keyboard.left) -1.0 else if (inputs.keyboard.right) 1.0 else if (@abs(inputs.joystick.x) < config.controller_dead_zone) 0 else inputs.joystick.x,
        .y = if (inputs.keyboard.down) 1.0 else if (inputs.keyboard.up) -1.0 else if (@abs(inputs.joystick.y) < config.controller_dead_zone) 0 else inputs.joystick.y,
    };
    const interaction = inputs.keyboard.interact or inputs.joystick.interact;
    const interaction_instant = interaction and !state.was_interacting;

    const dash = inputs.keyboard.dash or inputs.joystick.dash;
    const dash_instant = dash and !state.was_dashing;

    // Check proximity to machines.
    switch (state.player.action) {
        .Idle => {
            if (dash_instant) {
                state.player.dash_time_ms = inputs.time_ms;
            }
            const is_boosting = inputs.time_ms - state.player.dash_time_ms < config.player_dash_duration_ms;

            if (!is_boosting) {
                // Smooth movement direction.
                const d_x = input_direction.x - state.player.movement_direction.x;
                const d_y = input_direction.y - state.player.movement_direction.y;
                const distance = @sqrt(d_x * d_x + d_y * d_y);
                const n_x = if (@abs(distance) > 0) d_x / distance else 0;
                const n_y = if (@abs(distance) > 0) d_y / distance else 0;
                const v_x = n_x * config.player_direction_smoothness * delta_time;
                const v_y = n_y * config.player_direction_smoothness * delta_time;
                state.player.movement_direction.x = if (@abs(d_x) < @abs(v_x)) input_direction.x else state.player.movement_direction.x + v_x;
                state.player.movement_direction.y = if (@abs(d_y) < @abs(v_y)) input_direction.y else state.player.movement_direction.y + v_y;
            }

            // Move player.
            const speed = if (is_boosting) config.player_dash_speed else config.player_speed;
            state.player.position.x += state.player.movement_direction.x * speed * delta_time;
            state.player.position.y += state.player.movement_direction.y * speed * delta_time;
            state.player.view_direction = if (@abs(input_direction.x) > @abs(input_direction.y))
                (if (input_direction.x < 0)
                    ViewDirection.Left
                else if (input_direction.x > 0)
                    ViewDirection.Right
                else
                    state.player.view_direction)
            else
                (if (input_direction.y < 0)
                    ViewDirection.Up
                else if (input_direction.y > 0)
                    ViewDirection.Down
                else
                    state.player.view_direction);

            if (interaction_instant) {
                for (config.machine_locations) |machine| {
                    const dx = state.player.position.x - machine.x;
                    const dy = state.player.position.y - machine.y;
                    const distance = @sqrt(dx * dx + dy * dy);
                    if (distance < config.fix_proximity) {
                        // Start fixing machine!
                        state.player.action = .Fixing;
                        const interaction_flip: f32 = if (machine.x < config.screen.width / 2) 1.0 else -1.0;
                        state.player.position.x = machine.x + config.fix_snap_offset.x * interaction_flip;
                        state.player.position.y = machine.y + config.fix_snap_offset.y;
                        state.player.view_direction = if (machine.x < state.player.position.x) ViewDirection.Left else ViewDirection.Right;
                    }
                }
            }
        },
        .Fixing => {
            if (interaction_instant) {
                state.player.action = .Idle;
            }
        },
    }

    // Constrain player to screen.
    state.player.position.x = @max(config.player_radius, @min(config.screen.width - config.player_radius, state.player.position.x));
    state.player.position.y = @max(config.player_radius, @min(config.screen.height - config.player_radius, state.player.position.y));

    state.was_dashing = dash;
    state.was_interacting = interaction;
    state.last_time_ms = inputs.time_ms;

    return state;
}
