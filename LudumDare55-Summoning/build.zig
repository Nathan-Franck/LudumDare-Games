const std = @import("std");

pub fn build(
    b: *std.Build,
) !void {
    const optimize = b.standardOptimizeOption(.{});

    var exe = b.addExecutable(.{
        .target = b.resolveTargetQuery(.{ .cpu_arch = .wasm32, .os_tag = .freestanding }),
        .optimize = optimize,
        .name = "game",
        .root_source_file = .{ .path = thisDir() ++ "/src/wasm_entry.zig" },
    });

    exe.entry = .disabled;
    exe.rdynamic = true;

    const install_artifact = b.addInstallArtifact(exe, .{ .dest_dir = .{ .override = .{ .custom = "../bin" } } });
    var install_step = b.step("web", "build a web bundle (wasm)");
    install_step.dependOn(&install_artifact.step);

    b.default_step.dependOn(install_step);
}

inline fn thisDir() []const u8 {
    return comptime std.fs.path.dirname(@src().file) orelse ".";
}
