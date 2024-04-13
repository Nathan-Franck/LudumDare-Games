const std = @import("std");

fn EaselJSData(comptime animation_name: []const u8) type {
    return struct {
        framerate: u8,
        images: []const []const u8,
        frames: []const [7]u32,
        animations: @Type(std.builtin.Type{ .Struct = .{
            .is_tuple = false,
            .layout = .auto,
            .decls = &.{},
            .fields = &.{std.builtin.Type.StructField{
                .alignment = @alignOf([]const u8),
                .default_value = null,
                .is_comptime = false,
                .name = animation_name[0.. :0],
                .type = [2]u32,
            }},
        } }),
    };
}

pub fn loadEaselJSData(
    allocator: std.mem.Allocator,
    comptime character_name: []const u8,
    comptime animation_name: []const u8,
) !EaselJSData(animation_name) {
    const json = @embedFile("content/" ++ character_name ++ "_" ++ animation_name ++ ".json");
    const easel_js_data = try std.json.parseFromSlice(EaselJSData(animation_name), allocator, json, .{});
    return easel_js_data.value;
}

test "easel js import" {
    const allocator = std.heap.page_allocator;
    const easel_js_data = loadEaselJSData(allocator, "RoyalArcher", "FullHD_Attack");
    std.debug.print("easel_js_data: {any}\n", .{easel_js_data});
}
