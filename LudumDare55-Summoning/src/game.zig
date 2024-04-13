const std = @import("std");
const SpriteSheetAnimation = @import("SpriteSheetAnimation.zig");

pub fn getAllResources() !struct {
    RoyalArcher_FullHD_Attack: SpriteSheetAnimation,
} {
    const allocator = std.heap.page_allocator;
    return .{
        .RoyalArcher_FullHD_Attack = try SpriteSheetAnimation.load(allocator, "RoyalArcher_FullHD_Attack"),
    };
}

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
}) !struct {
    player: struct { x: f32, y: f32, animation: struct {
        name: []const u8,
        frame: u32,
    } },
} {
    _ = inputs;
    unreachable;
}
