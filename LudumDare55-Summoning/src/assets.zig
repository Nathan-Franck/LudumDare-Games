const std = @import("std");
const Png = @import("./zigimg/src/formats/png.zig");

const ImageSizeLimit = 4096;

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

const ImageSizeLimit = 2048;

pub const PngImage = struct {
    data: []const u8,
    width: usize,
    height: usize,
    scale: u8, // If we downsample, we have to declare that we have to scale up upon displaying.

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
        const data = switch (png.pixels) {
            .rgba32 => |rgba| std.mem.sliceAsBytes(rgba),
            else => @panic("handy axiom"),
        };
        if (png.width > ImageSizeLimit or png.height > ImageSizeLimit) {
            // Return a down-sampled version instead
            var dimensions: struct { width: usize, height: usize } = .{ .width = png.width, .height = png.height };
            var downSampleRate: u8 = 1;
            while (dimensions.width > ImageSizeLimit or dimensions.height > ImageSizeLimit) {
                downSampleRate *= 2;
                dimensions.width /= 2;
                dimensions.height /= 2;
            }

            var new_data = try allocator.alloc(u8, dimensions.width * dimensions.height * 4);
            for (0..dimensions.height) |y| {
                for (0..dimensions.width) |x| {
                    var pixel_accum = [4]u32{ 0, 0, 0, 0 };
                    for (0..downSampleRate) |sy| {
                        for (0..downSampleRate) |sx| {
                            if (x * downSampleRate + sx < png.width and y * downSampleRate + sy < png.height) {
                                const sample_index = 4 * (png.width * (y * downSampleRate + sy) + x * downSampleRate + sx);
                                const pixel = data[sample_index .. sample_index + 4];
                                pixel_accum[0] += pixel[0];
                                pixel_accum[1] += pixel[1];
                                pixel_accum[2] += pixel[2];
                                pixel_accum[3] += pixel[3];
                            }
                        }
                    }
                    const pixel_index = 4 * (dimensions.width * y + x);
                    new_data[pixel_index + 0] = @intCast(pixel_accum[0] / downSampleRate / downSampleRate);
                    new_data[pixel_index + 1] = @intCast(pixel_accum[1] / downSampleRate / downSampleRate);
                    new_data[pixel_index + 2] = @intCast(pixel_accum[2] / downSampleRate / downSampleRate);
                    new_data[pixel_index + 3] = @intCast(pixel_accum[3] / downSampleRate / downSampleRate);
                }
            }
            return .{
                .data = new_data,
                .width = dimensions.width,
                .height = dimensions.height,
                .scale = downSampleRate,
            };
        }
        return .{
            .data = data,
            .width = png.width,
            .height = png.height,
            .scale = 1,
        };
    }
};

test "png import" {
    const allocator = std.heap.page_allocator;
    const png_image = PngImage.load(allocator, "SummoningChamber_FullHD_ChamberProgressIncrease");
    _ = try png_image;
}
