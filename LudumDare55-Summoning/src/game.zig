const std = @import("std");
const assets = @import("assets.zig");
const wasm_entry = @import("wasm_entry.zig");

pub fn getAllResources() !struct {
    graphics: struct {
        background: assets.PngImage,
        machine: assets.PngImage,
        chamber_increase: assets.SpriteSheetAnimation,
        chamber_materialize: assets.SpriteSheetAnimation,
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
            .chamber_materialize = try assets.SpriteSheetAnimation.load(allocator, "SummoningChamber_FullHD_Materlalize "),
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

const Point = struct { x: f32, y: f32 };

const Config = struct {
    screen: struct { width: u32, height: u32 },
    controller_dead_zone: f32,
    victory_span_ms: u64,
    fail_span_ms: u64,
    player_radius: f32,
    player_speed: f32,
    player_direction_smoothness: f32,
    player_dash_speed: f32,
    player_dash_duration_ms: u64,
    player_dash_cooldown_ms: u64,
    player_repair_delay_ms: u64,
    levels: [4]struct {
        title: []const u8,
        duration_ms: u64,
        breakdown_delay_ms: i32,
        required_online: u32,
    },
    fix_proximity: f32,
    fix_snap_offset: Point,
    chamber_location: Point,
    machine_locations: [4]Point,
};

const config: Config = .{
    .screen = .{ .width = 1920, .height = 1080 },
    .controller_dead_zone = 0.1,
    .victory_span_ms = 1000 * 5,
    .fail_span_ms = 1000 * 5,
    .player_radius = 50.0,
    .player_speed = 400,
    .player_direction_smoothness = 5,
    .player_dash_speed = 1600,
    .player_dash_duration_ms = 200,
    .player_dash_cooldown_ms = 500,
    .player_repair_delay_ms = 250,
    .levels = .{
        .{ .title = "Level 1 - Bring Me Back", .duration_ms = 1000 * 5, .breakdown_delay_ms = 1000 * 30, .required_online = 1 },
        .{ .title = "Level 2 - Poor Reliability", .duration_ms = 1000 * 20, .breakdown_delay_ms = 1000 * 15, .required_online = 1 },
        .{ .title = "Level 3 - We Need More Power", .duration_ms = 1000 * 30, .breakdown_delay_ms = 1000 * 15, .required_online = 2 },
        .{ .title = "Level 4 - The Final Spawn", .duration_ms = 1000 * 40, .breakdown_delay_ms = 1000 * 13, .required_online = 2 },
    },
    .fix_proximity = 200,
    .fix_snap_offset = .{ .x = 180.0, .y = 50.0 },
    .chamber_location = .{ .x = 950.0, .y = 300.0 },
    .machine_locations = .{
        .{ .x = 300, .y = 200 },
        .{ .x = 300, .y = 850 },
        .{ .x = 1600, .y = 200 },
        .{ .x = 1600, .y = 850 },
    },
};

const pseudo_random_list = [_]u64{
    12329318230,
    45658934597,
    23423423423,
    12705234855,
    39057693473,
    23423423423,
    56897345879,
    39466123480,
    39482304849,
    89516128347,
    93293472340,
    98324987234,
    89234723489,
    12346850175,
    87123657458,
    12346850175,
    87123657458,
};

const MachineState = struct {
    delay_until_breakdown_ms: i32,
    broken: bool,
};

const State = struct {
    win_time_ms: ?u64,
    in_danger: bool,
    current_level: u32,
    level_start_time_ms: u64,
    was_interacting: bool,
    was_dashing: bool,
    last_time_ms: ?u64,
    chamber_progress: f32,
    machine_states: [4]MachineState,
    victory_time_ms: ?u64,
    fail_time_ms: ?u64,
    player: struct {
        position: Point,
        movement_direction: Point,
        view_direction: ViewDirection,
        action: Action,
        target_machine_index: u32,
        dash_time_ms: u64,
        last_repair_time_ms: u64,
    },
};

const first_level_state = .{
    .win_time_ms = null,
    .in_danger = false,
    .current_level = 0,
    .level_start_time_ms = 0,
    .was_interacting = false,
    .was_dashing = false,
    .last_time_ms = null,
    .chamber_progress = 0.0,
    .machine_states = levelStateState(0),
    .victory_time_ms = null,
    .fail_time_ms = null,
    .player = .{
        .position = .{ .x = 950.0, .y = 650.0 },
        .movement_direction = .{ .x = 0, .y = 0 },
        .view_direction = ViewDirection.Down,
        .action = Action.Idle,
        .target_machine_index = 0,
        .dash_time_ms = 0,
        .last_repair_time_ms = 0,
    },
};
fn randomFromBreakdownDelay(breakdown_delay_ms: i32, random_number: u64) i32 {
    return @as(i32, @intCast(random_number % @as(u64, @intCast(breakdown_delay_ms))));
}
fn levelStateState(level: u32) [4]MachineState {
    const level_config = config.levels[level];
    var machine_states: [4]MachineState = undefined;
    for (&machine_states, 0..) |*machine_state, i| {
        machine_state.delay_until_breakdown_ms = randomFromBreakdownDelay(level_config.breakdown_delay_ms, pseudo_random_list[level * config.levels.len + i]);
        machine_state.broken = false;
    }
    return machine_states;
}

var state: State = first_level_state;

fn repairMachineTick(time_ms: u64) void {
    state.player.last_repair_time_ms = time_ms;

    const target_machine_index = state.player.target_machine_index;
    const breakdown_delay = config.levels[target_machine_index].breakdown_delay_ms;
    const the_machine = &state.machine_states[target_machine_index];

    // Machine breakdown delay APPROACHES (doesn't reach) the level's breakdown delay each tick.
    the_machine.broken = false;
    the_machine.delay_until_breakdown_ms += @as(i32, @intFromFloat(@as(f32, @floatFromInt(breakdown_delay - the_machine.delay_until_breakdown_ms)) * 0.1));
}

pub fn update(inputs: struct {
    time_ms: u64,
    joystick: struct {
        x: f32,
        y: f32,
        interact: bool,
        dash: bool,
    },
    keyboard: struct {
        left: bool,
        right: bool,
        up: bool,
        down: bool,
        interact: bool,
        dash: bool,
    },
}) !State {
    if (state.win_time_ms) |win_time_ms| {
        _ = win_time_ms; // autofix
        state.player.action = .Idle;
        return state;
    }

    if (state.victory_time_ms) |victory_time_ms| {
        state.player.action = .Idle;
        if (inputs.time_ms - victory_time_ms > config.victory_span_ms) {
            const next_level = state.current_level;
            if (next_level >= config.levels.len) {
                state.win_time_ms = inputs.time_ms;
                return state;
            }
            state = first_level_state;
            state.current_level = next_level;
            state.level_start_time_ms = inputs.time_ms;
            state.machine_states = levelStateState(state.current_level);
        } else {
            return state;
        }
    }

    if (state.fail_time_ms) |fail_time_ms| {
        state.player.action = .Idle;
        if (inputs.time_ms - fail_time_ms > config.fail_span_ms) {
            const next_level = state.current_level;
            state = first_level_state;
            state.current_level = next_level;
            state.level_start_time_ms = inputs.time_ms;
            state.machine_states = levelStateState(state.current_level);
        } else {
            return state;
        }
    }

    const delta_time: f32 = if (state.last_time_ms) |last_time_ms| @as(f32, @floatFromInt(inputs.time_ms - last_time_ms)) / 1000.0 else 0.0;

    // Progress the chamber!
    state.chamber_progress += delta_time * 1000.0 / @as(f32, @floatFromInt(config.levels[state.current_level].duration_ms));
    if (state.chamber_progress >= 1) {
        const next_level = state.current_level + 1;
        state.current_level = next_level;
        state.victory_time_ms = inputs.time_ms;
    }

    // Degrade machines - these are what keep the chamber going.
    var currently_online: u32 = 0;
    for (&state.machine_states) |*machine_state| {
        if (!machine_state.broken) {
            machine_state.delay_until_breakdown_ms -= @as(i32, @intFromFloat(delta_time * 1000.0));
            if (machine_state.delay_until_breakdown_ms <= 0) {
                machine_state.broken = true;
            }
        }
        if (!machine_state.broken) {
            currently_online += 1;
        }
    }

    // In danger if there's only one machine left before failure.
    state.in_danger = currently_online == config.levels[state.current_level].required_online;

    // If too many machines are currently broken, the chamber will stop.
    if (currently_online < config.levels[state.current_level].required_online) {
        const next_level = state.current_level;
        state.current_level = next_level;
        state.fail_time_ms = inputs.time_ms;
    }

    // Combine inputs from keyboard and joystick.
    const input_direction = .{
        .x = if (inputs.keyboard.left) -1.0 else if (inputs.keyboard.right) 1.0 else if (@abs(inputs.joystick.x) < config.controller_dead_zone) 0 else inputs.joystick.x,
        .y = if (inputs.keyboard.down) 1.0 else if (inputs.keyboard.up) -1.0 else if (@abs(inputs.joystick.y) < config.controller_dead_zone) 0 else inputs.joystick.y,
    };
    const interaction = inputs.keyboard.interact or inputs.joystick.interact;
    const interaction_instant = interaction and !state.was_interacting;

    const dash = inputs.keyboard.dash or inputs.joystick.dash;
    const dash_instant = dash and !state.was_dashing;

    switch (state.player.action) {
        .Fixing => {
            if (inputs.time_ms - state.player.last_repair_time_ms > config.player_repair_delay_ms) {
                repairMachineTick(inputs.time_ms);
            }
            if (interaction_instant or dash_instant) {
                state.player.action = .Idle;
            }
        },
        .Idle => {
            if (dash_instant and inputs.time_ms - state.player.dash_time_ms > config.player_dash_cooldown_ms) {
                state.player.dash_time_ms = inputs.time_ms;
            }
            const is_boosting = inputs.time_ms - state.player.dash_time_ms < config.player_dash_duration_ms;

            if (!is_boosting) {
                // Smooth movement direction for normal movement.
                const d_x = input_direction.x - state.player.movement_direction.x;
                const d_y = input_direction.y - state.player.movement_direction.y;
                const distance = @sqrt(d_x * d_x + d_y * d_y);
                const n_x = if (@abs(distance) > 0) d_x / distance else 0;
                const n_y = if (@abs(distance) > 0) d_y / distance else 0;
                const v_x = n_x * config.player_direction_smoothness * delta_time;
                const v_y = n_y * config.player_direction_smoothness * delta_time;
                state.player.movement_direction.x = if (@abs(d_x) < @abs(v_x)) input_direction.x else state.player.movement_direction.x + v_x;
                state.player.movement_direction.y = if (@abs(d_y) < @abs(v_y)) input_direction.y else state.player.movement_direction.y + v_y;
            } else {
                // Normalize movement direction for boosting.
                const d_x = state.player.movement_direction.x;
                const d_y = state.player.movement_direction.y;
                const distance = @sqrt(d_x * d_x + d_y * d_y);
                state.player.movement_direction = if (@abs(distance) > 0) .{ .x = d_x / distance, .y = d_y / distance } else switch (state.player.view_direction) {
                    .Left => .{ .x = -1, .y = 0 },
                    .Right => .{ .x = 1, .y = 0 },
                    .Up => .{ .x = 0, .y = -1 },
                    .Down => .{ .x = 0, .y = 1 },
                };
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
                // Check proximity to machines.
                for (config.machine_locations, 0..) |machine_location, machine_index| {
                    const dx = state.player.position.x - machine_location.x;
                    const dy = state.player.position.y - machine_location.y;
                    const distance = @sqrt(dx * dx + dy * dy);
                    if (distance < config.fix_proximity) {
                        // Start fixing machine!
                        state.player.action = .Fixing;
                        const interaction_flip: f32 = if (machine_location.x < config.screen.width / 2) 1.0 else -1.0;
                        state.player.position.x = machine_location.x + config.fix_snap_offset.x * interaction_flip;
                        state.player.position.y = machine_location.y + config.fix_snap_offset.y;
                        state.player.view_direction = if (machine_location.x < state.player.position.x) ViewDirection.Left else ViewDirection.Right;
                        state.player.target_machine_index = machine_index;
                        repairMachineTick(inputs.time_ms);
                    }
                }
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
