using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using MapEditorTool.Executor.ScenePatch;
using MapEditorTool.Models;

namespace MapEditorTool.Executor.PortalAnimation
{
    public sealed class PortalAnimationExecutor
    {
        private readonly ScenePatchExecutor _scenePatchExecutor;

        public PortalAnimationExecutor()
            : this(new ScenePatchExecutor())
        {
        }

        public PortalAnimationExecutor(ScenePatchExecutor scenePatchExecutor)
        {
            _scenePatchExecutor = scenePatchExecutor ?? throw new ArgumentNullException("scenePatchExecutor");
        }

        public PortalAnimationImportResult ImportPortalVideoAndPatchScene(
            string godotRoot,
            string sceneFilePath,
            MapDefinition map,
            Portal portal,
            string newVideoPath)
        {
            if (string.IsNullOrWhiteSpace(sceneFilePath))
                throw new FileNotFoundException("Scene file path is empty.", sceneFilePath);
            if (!File.Exists(sceneFilePath))
                throw new FileNotFoundException("Scene file not found.", sceneFilePath);
            if (portal == null)
                throw new ArgumentNullException("portal");
            if (string.IsNullOrWhiteSpace(portal.NodePath))
                throw new ArgumentException("Portal node path is empty.", "portal");

            godotRoot = ValidateGodotRoot(godotRoot);
            newVideoPath = (newVideoPath ?? string.Empty).Trim();
            var fps = ComputePortalAnimFps(portal);
            var upscale = Math.Max(0.001f, portal.Upscale);

            if (newVideoPath.Length == 0)
            {
                portal.AnimationFramesDir = string.Empty;
                var clearPatch = _scenePatchExecutor.PatchPortalAnimation(sceneFilePath, portal.NodePath, string.Empty, fps, upscale);
                return new PortalAnimationImportResult
                {
                    SceneFilePath = Path.GetFullPath(sceneFilePath),
                    ClearedAnimation = true,
                    PatchedScene = clearPatch.Patched,
                    AppliedFps = fps,
                    AppliedUpscale = upscale,
                    Summary = "clearedPortalAnimation=true; patchedScene=" + clearPatch.Patched
                };
            }

            var sourceVideoFilePath = ToAbsoluteGodotPathIfNeeded(godotRoot, newVideoPath);
            if (!File.Exists(sourceVideoFilePath))
                throw new FileNotFoundException("Portal animation source video was not found.", sourceVideoFilePath);

            var outputDirectoryPath = BuildPortalAnimationOutputDirectory(godotRoot, map, portal);
            Directory.CreateDirectory(outputDirectoryPath);
            var deletedOldFrameCount = DeleteExistingFrames(outputDirectoryPath);

            var ffmpegPath = ResolveBundledFfmpegPath(godotRoot);
            var requestedFrameCount = Math.Max(0, portal.AnimationFrameCount);
            if (requestedFrameCount > 0)
            {
                var ffprobePath = ResolveBundledFfprobePath(godotRoot);
                var durationSeconds = ProbeVideoDurationSeconds(ffprobePath, sourceVideoFilePath);
                if (durationSeconds > 0.001)
                    ExtractUniformFrames(ffmpegPath, sourceVideoFilePath, outputDirectoryPath, requestedFrameCount, durationSeconds);
                else
                    ExtractAllFrames(ffmpegPath, sourceVideoFilePath, outputDirectoryPath);
            }
            else
            {
                ExtractAllFrames(ffmpegPath, sourceVideoFilePath, outputDirectoryPath);
            }

            KeyOutBlackBackgroundInDir(outputDirectoryPath, ClampByte(portal.KeyoutTolerance));
            var generatedFrames = Directory.EnumerateFiles(outputDirectoryPath, "*.png", SearchOption.TopDirectoryOnly)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .Select(Path.GetFullPath)
                .ToList();

            var outputDirectoryResPath = TryMakeResPath(godotRoot, outputDirectoryPath);
            portal.AnimationFramesDir = outputDirectoryResPath;
            var patch = _scenePatchExecutor.PatchPortalAnimation(sceneFilePath, portal.NodePath, outputDirectoryResPath, fps, upscale);

            return new PortalAnimationImportResult
            {
                SceneFilePath = Path.GetFullPath(sceneFilePath),
                SourceVideoFilePath = Path.GetFullPath(sourceVideoFilePath),
                OutputDirectoryPath = Path.GetFullPath(outputDirectoryPath),
                OutputDirectoryResPath = outputDirectoryResPath,
                PatchedScene = patch.Patched,
                AppliedFps = fps,
                AppliedUpscale = upscale,
                DeletedOldFrameCount = deletedOldFrameCount,
                GeneratedFrameCount = generatedFrames.Count,
                GeneratedFrameFiles = generatedFrames,
                Summary = "portalAnimationFrames=" + generatedFrames.Count +
                    "; deletedOldFrames=" + deletedOldFrameCount +
                    "; patchedScene=" + patch.Patched
            };
        }

        public void PatchPortalAnimationSettings(string sceneFilePath, Portal portal)
        {
            if (portal == null)
                throw new ArgumentNullException("portal");
            _scenePatchExecutor.PatchPortalAnimationSettings(
                sceneFilePath,
                portal.NodePath,
                ComputePortalAnimFps(portal),
                Math.Max(0.001f, portal.Upscale));
        }

        public PortalAnimationImportResult ExtractPortalAnimationFrames(
            string godotRoot,
            string sourceVideoPath,
            string outputDirectoryPath,
            string outputPattern,
            bool keyOutBlackBackground,
            byte keyoutTolerance)
        {
            godotRoot = ValidateGodotRoot(godotRoot);
            sourceVideoPath = ToAbsoluteGodotPathIfNeeded(godotRoot, sourceVideoPath);
            if (string.IsNullOrWhiteSpace(sourceVideoPath) || !File.Exists(sourceVideoPath))
                throw new FileNotFoundException("Input mp4 not found.", sourceVideoPath);

            outputDirectoryPath = ResolvePortalAnimationOutputDirectory(godotRoot, sourceVideoPath, outputDirectoryPath);
            outputPattern = SanitizeFramePattern(outputPattern);
            Directory.CreateDirectory(outputDirectoryPath);

            var ffmpegPath = ResolveBundledFfmpegPath(godotRoot);
            RunProcessChecked(
                ffmpegPath,
                "-y -hide_banner -loglevel error -i " + QuoteProcessArg(sourceVideoPath) +
                " -vsync 0 -start_number 0 " + QuoteProcessArg(Path.Combine(outputDirectoryPath, outputPattern)));

            if (keyOutBlackBackground)
                KeyOutBlackBackgroundInDir(outputDirectoryPath, keyoutTolerance);

            var generatedFrames = Directory.EnumerateFiles(outputDirectoryPath, "*.png", SearchOption.TopDirectoryOnly)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .Select(Path.GetFullPath)
                .ToList();
            var outputDirectoryResPath = TryMakeResPath(godotRoot, outputDirectoryPath);

            return new PortalAnimationImportResult
            {
                SourceVideoFilePath = Path.GetFullPath(sourceVideoPath),
                OutputDirectoryPath = Path.GetFullPath(outputDirectoryPath),
                OutputDirectoryResPath = outputDirectoryResPath,
                GeneratedFrameCount = generatedFrames.Count,
                GeneratedFrameFiles = generatedFrames,
                Summary = "portalAnimationFrames=" + generatedFrames.Count +
                    "; keyOutBlackBackground=" + keyOutBlackBackground +
                    "; outputDirectory=" + outputDirectoryResPath
            };
        }

        public static float ComputePortalAnimFps(Portal portal)
        {
            if (portal == null)
                throw new ArgumentNullException("portal");

            var frames = Math.Max(0, portal.AnimationFrameCount);
            var duration = portal.AnimationDurationSec;
            if (frames > 0 && duration > 0.0001f)
                return Math.Max(0.001f, frames / duration);
            return Math.Max(0.001f, portal.AnimationFps);
        }

        public static string BuildPortalAnimationOutputDirectory(string godotRoot, MapDefinition map, Portal portal)
        {
            godotRoot = ValidateGodotRoot(godotRoot);
            if (portal == null)
                throw new ArgumentNullException("portal");

            var mapName = SanitizeFolderName(map == null ? string.Empty : map.DisplayName);
            if (string.IsNullOrWhiteSpace(mapName))
                mapName = "Map";

            var portalName = SanitizeFolderName(string.IsNullOrWhiteSpace(portal.Name) ? portal.Id : portal.Name);
            if (string.IsNullOrWhiteSpace(portalName))
                portalName = "Portal";

            return Path.Combine(godotRoot, "CoreEngine", "Resources", "PortalAnimations", mapName, portalName);
        }

        public static string ResolveBundledFfmpegPath(string godotRoot)
        {
            return ResolveBundledToolPath(godotRoot, "ffmpeg.exe");
        }

        public static string ResolveBundledFfprobePath(string godotRoot)
        {
            return ResolveBundledToolPath(godotRoot, "ffprobe.exe");
        }

        public static double ProbeVideoDurationSeconds(string ffprobePath, string sourceVideoFilePath)
        {
            if (string.IsNullOrWhiteSpace(sourceVideoFilePath) || !File.Exists(sourceVideoFilePath))
                throw new FileNotFoundException("Video file not found.", sourceVideoFilePath);

            var args = "-v error -select_streams v:0 -show_entries format=duration -of default=nokey=1:noprint_wrappers=1 " +
                QuoteProcessArg(sourceVideoFilePath);
            var output = RunProcessCapture(ffprobePath, args).Trim();
            double value;
            if (double.TryParse(output, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                return value;
            return 0;
        }

        public static void KeyOutBlackBackgroundInDir(string framesDirectoryPath, byte tolerance)
        {
            if (string.IsNullOrWhiteSpace(framesDirectoryPath) || !Directory.Exists(framesDirectoryPath))
                return;

            foreach (var filePath in Directory.EnumerateFiles(framesDirectoryPath, "*.png", SearchOption.TopDirectoryOnly).ToList())
            {
                if (IsAlphaFrameFileName(filePath))
                {
                    KeyOutBlackBackgroundInPng(filePath, tolerance);
                    continue;
                }

                var alphaPath = MakeAlphaFramePath(filePath);
                if (alphaPath.Length == 0)
                    continue;

                KeyOutBlackBackgroundInPngIntoNewFile(filePath, alphaPath, tolerance);
                TryDeleteFile(filePath);
            }
        }

        public static string MakeAlphaFramePath(string pngFilePath)
        {
            var directory = Path.GetDirectoryName(pngFilePath) ?? string.Empty;
            var name = Path.GetFileNameWithoutExtension(pngFilePath);
            if (string.IsNullOrWhiteSpace(directory) || string.IsNullOrWhiteSpace(name))
                return string.Empty;
            if (name.EndsWith("_Alpha", StringComparison.OrdinalIgnoreCase))
                return pngFilePath;
            return Path.Combine(directory, name + "_Alpha.png");
        }

        private static void ExtractUniformFrames(string ffmpegPath, string sourceVideoFilePath, string outputDirectoryPath, int frameCount, double videoDurationSeconds)
        {
            if (frameCount <= 0)
                return;

            if (frameCount == 1)
            {
                RunProcessChecked(
                    ffmpegPath,
                    "-y -hide_banner -loglevel error -ss 0 -i " + QuoteProcessArg(sourceVideoFilePath) +
                    " -frames:v 1 " + QuoteProcessArg(Path.Combine(outputDirectoryPath, "frame_000.png")));
                return;
            }

            var maxTime = Math.Max(0, videoDurationSeconds - 0.0001);
            for (var index = 0; index < frameCount; index++)
            {
                var time = (double)index / (frameCount - 1) * videoDurationSeconds;
                if (index == frameCount - 1)
                    time = maxTime;
                time = Math.Max(0, Math.Min(time, maxTime));

                var timeText = time.ToString("0.######", CultureInfo.InvariantCulture);
                var outputPng = Path.Combine(outputDirectoryPath, "frame_" + index.ToString("000", CultureInfo.InvariantCulture) + ".png");
                RunProcessChecked(
                    ffmpegPath,
                    "-y -hide_banner -loglevel error -ss " + timeText + " -i " + QuoteProcessArg(sourceVideoFilePath) +
                    " -frames:v 1 " + QuoteProcessArg(outputPng));
            }
        }

        private static void ExtractAllFrames(string ffmpegPath, string sourceVideoFilePath, string outputDirectoryPath)
        {
            RunProcessChecked(
                ffmpegPath,
                "-y -hide_banner -loglevel error -i " + QuoteProcessArg(sourceVideoFilePath) +
                " -vsync 0 -start_number 0 " + QuoteProcessArg(Path.Combine(outputDirectoryPath, "frame_%03d.png")));
        }

        private static int DeleteExistingFrames(string outputDirectoryPath)
        {
            var deleted = 0;
            foreach (var filePath in Directory.EnumerateFiles(outputDirectoryPath, "*.png", SearchOption.TopDirectoryOnly))
            {
                if (TryDeleteFile(filePath))
                    deleted++;
            }

            return deleted;
        }

        private static bool TryDeleteFile(string filePath)
        {
            try
            {
                File.Delete(filePath);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsAlphaFrameFileName(string pngFilePath)
        {
            var name = Path.GetFileNameWithoutExtension(pngFilePath) ?? string.Empty;
            return name.EndsWith("_Alpha", StringComparison.OrdinalIgnoreCase);
        }

        private static void KeyOutBlackBackgroundInPng(string pngFilePath, byte tolerance)
        {
            if (string.IsNullOrWhiteSpace(pngFilePath) || !File.Exists(pngFilePath))
                return;

            try
            {
                using (var source = new Bitmap(pngFilePath))
                using (var bitmap = source.PixelFormat == PixelFormat.Format32bppArgb ? new Bitmap(source) : ConvertTo32bppArgb(source))
                {
                    ApplyKeyOutBlackToBitmap(bitmap, tolerance);
                    var tempPath = pngFilePath + ".tmp.png";
                    bitmap.Save(tempPath, ImageFormat.Png);
                    File.Copy(tempPath, pngFilePath, true);
                    File.Delete(tempPath);
                }
            }
            catch
            {
            }
        }

        private static void KeyOutBlackBackgroundInPngIntoNewFile(string sourcePngFilePath, string targetPngFilePath, byte tolerance)
        {
            if (string.IsNullOrWhiteSpace(sourcePngFilePath) || !File.Exists(sourcePngFilePath))
                return;
            if (string.IsNullOrWhiteSpace(targetPngFilePath))
                return;

            try
            {
                using (var source = new Bitmap(sourcePngFilePath))
                using (var bitmap = source.PixelFormat == PixelFormat.Format32bppArgb ? new Bitmap(source) : ConvertTo32bppArgb(source))
                {
                    ApplyKeyOutBlackToBitmap(bitmap, tolerance);
                    var tempPath = targetPngFilePath + ".tmp.png";
                    bitmap.Save(tempPath, ImageFormat.Png);
                    File.Copy(tempPath, targetPngFilePath, true);
                    File.Delete(tempPath);
                }
            }
            catch
            {
            }
        }

        private static void ApplyKeyOutBlackToBitmap(Bitmap bitmap, byte tolerance)
        {
            var rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
            var data = bitmap.LockBits(rect, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
            try
            {
                var rowBytes = data.Width * 4;
                var row = new byte[rowBytes];
                for (var y = 0; y < data.Height; y++)
                {
                    var rowPtr = IntPtr.Add(data.Scan0, y * data.Stride);
                    Marshal.Copy(rowPtr, row, 0, rowBytes);
                    for (var x = 0; x < data.Width; x++)
                    {
                        var index = x * 4;
                        var blue = row[index];
                        var green = row[index + 1];
                        var red = row[index + 2];
                        if (red <= tolerance && green <= tolerance && blue <= tolerance)
                        {
                            row[index] = 0;
                            row[index + 1] = 0;
                            row[index + 2] = 0;
                            row[index + 3] = 0;
                        }
                    }

                    Marshal.Copy(row, 0, rowPtr, rowBytes);
                }
            }
            finally
            {
                bitmap.UnlockBits(data);
            }
        }

        private static Bitmap ConvertTo32bppArgb(Bitmap source)
        {
            var destination = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);
            using (var graphics = Graphics.FromImage(destination))
            {
                graphics.DrawImage(source, 0, 0, source.Width, source.Height);
            }

            return destination;
        }

        private static string RunProcessCapture(string exePath, string args)
        {
            using (var process = CreateProcess(exePath, args, true))
            {
                process.Start();
                var stdout = process.StandardOutput.ReadToEnd();
                var stderr = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode != 0)
                    throw new InvalidOperationException(BuildProcessFailureMessage(exePath, process.ExitCode, stdout, stderr));

                return stdout;
            }
        }

        private static void RunProcessChecked(string exePath, string args)
        {
            using (var process = CreateProcess(exePath, args, true))
            {
                process.Start();
                var stdout = process.StandardOutput.ReadToEnd();
                var stderr = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode != 0)
                    throw new InvalidOperationException(BuildProcessFailureMessage(exePath, process.ExitCode, stdout, stderr));
            }
        }

        private static Process CreateProcess(string exePath, string args, bool redirectOutput)
        {
            if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath))
                throw new FileNotFoundException("Required process executable was not found.", exePath);

            var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = args ?? string.Empty,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = redirectOutput,
                RedirectStandardOutput = redirectOutput
            };
            return process;
        }

        private static string BuildProcessFailureMessage(string exePath, int exitCode, string stdout, string stderr)
        {
            return "Process failed: " + Path.GetFileName(exePath) +
                " exit=" + exitCode.ToString(CultureInfo.InvariantCulture) +
                Environment.NewLine + stdout +
                Environment.NewLine + stderr;
        }

        private static string ResolveBundledToolPath(string godotRoot, string fileName)
        {
            godotRoot = ValidateGodotRoot(godotRoot);
            var candidates = new List<string>
            {
                Path.Combine(godotRoot, "GodotTools", "MapEditorTool", fileName),
                Path.Combine(godotRoot, "GodotTools", "MapEditor", fileName),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName)
            };

            foreach (var candidate in candidates)
            {
                if (File.Exists(candidate))
                    return candidate;
            }

            throw new FileNotFoundException(fileName + " not found. Checked MapEditorTool, legacy MapEditor, and application output directories.", fileName);
        }

        private static string ResolvePortalAnimationOutputDirectory(string godotRoot, string sourceVideoPath, string outputDirectoryPath)
        {
            outputDirectoryPath = (outputDirectoryPath ?? string.Empty).Trim();
            if (outputDirectoryPath.Length > 0)
            {
                if (outputDirectoryPath.StartsWith("res://", StringComparison.Ordinal))
                    return ToAbsoluteGodotPath(godotRoot, outputDirectoryPath);
                if (Path.IsPathRooted(outputDirectoryPath))
                    return Path.GetFullPath(outputDirectoryPath);
                return Path.GetFullPath(outputDirectoryPath);
            }

            var baseName = SanitizeFolderName(Path.GetFileNameWithoutExtension(sourceVideoPath));
            if (string.IsNullOrWhiteSpace(baseName))
                baseName = "PortalAnim";

            return Path.Combine(godotRoot, "CoreEngine", "Resources", "PortalAnimations", baseName);
        }

        private static string SanitizeFramePattern(string outputPattern)
        {
            outputPattern = (outputPattern ?? string.Empty).Trim();
            if (outputPattern.Length == 0)
                return "frame_%03d.png";

            var fileName = Path.GetFileName(outputPattern);
            return string.IsNullOrWhiteSpace(fileName) ? "frame_%03d.png" : fileName;
        }

        private static byte ClampByte(int value)
        {
            if (value < 0)
                return 0;
            if (value > 255)
                return 255;
            return (byte)value;
        }

        private static string ValidateGodotRoot(string godotRoot)
        {
            if (string.IsNullOrWhiteSpace(godotRoot))
                throw new DirectoryNotFoundException("Godot root is empty.");

            return Path.GetFullPath(godotRoot);
        }

        private static string ToAbsoluteGodotPathIfNeeded(string godotRoot, string path)
        {
            path = (path ?? string.Empty).Trim();
            if (path.StartsWith("res://", StringComparison.Ordinal))
                return ToAbsoluteGodotPath(godotRoot, path);
            return Path.GetFullPath(path);
        }

        private static string ToAbsoluteGodotPath(string godotRoot, string resPath)
        {
            if (string.IsNullOrWhiteSpace(resPath))
                throw new FileNotFoundException("Godot resource path is empty.");

            var relative = resPath.StartsWith("res://", StringComparison.Ordinal)
                ? resPath.Substring("res://".Length)
                : resPath;
            relative = relative.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            return Path.Combine(godotRoot, relative);
        }

        private static string TryMakeResPath(string godotRoot, string absolutePath)
        {
            var root = EnsureTrailingSeparator(Path.GetFullPath(godotRoot));
            var full = Path.GetFullPath(absolutePath);
            if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                return full;

            var relative = full.Substring(root.Length).Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');
            return "res://" + relative.TrimStart('/');
        }

        private static string EnsureTrailingSeparator(string path)
        {
            if (path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal) ||
                path.EndsWith(Path.AltDirectorySeparatorChar.ToString(), StringComparison.Ordinal))
                return path;
            return path + Path.DirectorySeparatorChar;
        }

        private static string SanitizeFolderName(string name)
        {
            name = (name ?? string.Empty).Trim();
            if (name.Length == 0)
                return string.Empty;

            var invalid = Path.GetInvalidFileNameChars();
            var builder = new StringBuilder(name.Length);
            foreach (var ch in name)
            {
                if (invalid.Contains(ch) || char.IsControl(ch))
                    builder.Append('_');
                else
                    builder.Append(ch);
            }

            return builder.ToString().Trim(' ', '.');
        }

        private static string QuoteProcessArg(string value)
        {
            value = (value ?? string.Empty).Replace("\"", "\\\"");
            return "\"" + value + "\"";
        }
    }
}
