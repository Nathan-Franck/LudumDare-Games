const std = @import("std");
const Png = @import("./zigimg/src/formats/png.zig");

pub const SpriteSheetAnimation = struct {
    sprite_sheet: PngImage,
    animation_data: EaselJSData,

    const EaselJSData = struct {
        framerate: u8,
        frames: []const [7]u32,
    };

    pub fn load(
        allocator: std.mem.Allocator,
        comptime animation_name: []const u8,
    ) !@This() {
        const json = @embedFile("content/" ++ animation_name ++ ".json");
        const animation_data = try std.json.parseFromSlice(EaselJSData, allocator, json, .{ .ignore_unknown_fields = true });
        return .{
            .sprite_sheet = try PngImage.load(allocator, animation_name),
            .animation_data = animation_data.value,
        };
    }

    test "easel js import" {
        const allocator = std.heap.page_allocator;
        const easel_js_data = load(allocator, "RoyalArcher_FullHD_Attack");
        _ = try easel_js_data;
    }
};

pub const PngImage = struct {
    data: []const u8,
    width: usize,
    height: usize,

    pub fn load(
        allocator: std.mem.Allocator,
        comptime path: []const u8,
    ) !@This() {
        const png_data = @embedFile("content/" ++ path ++ ".png");
        const png = blk: {
            var stream_source = std.io.StreamSource{ .const_buffer = std.io.fixedBufferStream(png_data) };
            var default_options = Png.DefaultOptions{};
            break :blk try Png.load(&stream_source, allocator, default_options.get());
        };
        return .{
            .data = switch (png.pixels) {
                .rgba32 => |rgba| std.mem.sliceAsBytes(rgba),
                else => @panic("handy axiom"),
            },
            .width = png.width,
            .height = png.height,
        };
    }
};
