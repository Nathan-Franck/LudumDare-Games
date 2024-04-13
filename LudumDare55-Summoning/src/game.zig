const std = @import("std");
const assets = @import("assets.zig");

pub fn getAllResources() !struct {
    background: assets.PngImage,
    RoyalArcher_FullHD_Attack: assets.SpriteSheetAnimation,
} {
    const allocator = std.heap.page_allocator;
    return .{
        .background = try assets.PngImage.load(allocator, "background"),
        .RoyalArcher_FullHD_Attack = try assets.SpriteSheetAnimation.load(allocator, "RoyalArcher_FullHD_Attack"),
    };
}

const State = struct {
    player: struct { x: f32, y: f32 },
};

var state = {};

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
}) !struct {} {
    _ = inputs;
    unreachable;
}
