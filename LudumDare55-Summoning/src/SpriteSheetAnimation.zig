const std = @import("std");
const Png = @import("./zigimg/src/formats/png.zig");

sprite_sheet: struct {
    data: []const u8,
    width: usize,
    height: usize,
},
animation_data: EaselJSData,

const EaselJSData = struct {
    framerate: u8,
    frames: []const [7]u32,
};

pub fn load(
    allocator: std.mem.Allocator,
    comptime animation_name: []const u8,
) !@This() {
    const png_data = @embedFile("content/" ++ animation_name ++ ".png");
    const json = @embedFile("content/" ++ animation_name ++ ".json");

    const animation_data = try std.json.parseFromSlice(EaselJSData, allocator, json, .{ .ignore_unknown_fields = true });
    const sprite_sheet = blk: {
        var stream_source = std.io.StreamSource{ .const_buffer = std.io.fixedBufferStream(png_data) };
        var default_options = Png.DefaultOptions{};
        break :blk try Png.load(&stream_source, allocator, default_options.get());
    };

    return .{
        .sprite_sheet = .{
            .data = switch (sprite_sheet.pixels) {
                .rgba32 => |rgba| std.mem.sliceAsBytes(rgba),
                else => @panic("handy axiom"),
            },
            .width = sprite_sheet.width,
            .height = sprite_sheet.height,
        },
        .animation_data = animation_data.value,
    };
}

test "easel js import" {
    const allocator = std.heap.page_allocator;
    const easel_js_data = load(allocator, "RoyalArcher_FullHD_Attack");
    _ = try easel_js_data;
}
