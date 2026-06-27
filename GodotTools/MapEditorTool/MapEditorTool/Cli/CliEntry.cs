using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text;
using MapEditorTool.Executor.ForegroundTextureCollision;
using MapEditorTool.Executor.MapImport;
using MapEditorTool.Executor.MapReport;
using MapEditorTool.Executor.PortalAnimation;
using MapEditorTool.Executor.ProjectFile;
using MapEditorTool.Executor.RuntimeVerify;
using MapEditorTool.Executor.ScenePatch;
using MapEditorTool.Models;

namespace MapEditorTool.Cli
{
    public static class CliEntry
    {
        public static int Run(string[] args)
        {
            try
            {
                if (args == null || args.Length == 0)
                    return RunHelp();

                var command = (args[0] ?? string.Empty).Trim().ToLowerInvariant();
                var opts = ParseOptions(args.Skip(1).ToArray());
                switch (command)
                {
                    case "status":
                        return RunStatus(opts);
                    case "portal-review":
                        return RunPortalReview(opts);
                    case "runtime-verify":
                        return RunRuntimeVerify(opts);
                    case "import":
                        return RunImport(opts);
                    case "validate":
                        return RunValidate(opts);
                    case "patchpos":
                        return RunPatchPosition(opts);
                    case "tracealpha":
                        return RunTraceAlpha(opts);
                    case "portalanim":
                        return RunPortalAnimation(opts);
                    case "agent-self-test":
                    case "--agent-self-test":
                        return RunAgentSelfTest(opts);
                    case "help":
                    case "--help":
                    case "-h":
                        return RunHelp();
                    default:
                        return RunHelp();
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                return 2;
            }
        }

        private static int RunHelp()
        {
            Console.WriteLine("MapEditorTool CLI");
            Console.WriteLine("  MapEditorTool.exe status --godotRoot <dir> [--summary]");
            Console.WriteLine("  MapEditorTool.exe portal-review --godotRoot <dir> [--summary]");
            Console.WriteLine("  MapEditorTool.exe runtime-verify --godotRoot <dir> [--summary]");
            Console.WriteLine("  MapEditorTool.exe import --godotRoot <dir> --out <file> [--summary]");
            Console.WriteLine("  MapEditorTool.exe validate --godotRoot <dir> --in <file> [--summary]");
            Console.WriteLine("  MapEditorTool.exe patchpos --godotRoot <dir> --scene <res://...> --nodePath <path> --x <num> --y <num>");
            Console.WriteLine("  MapEditorTool.exe tracealpha --in <image> [--worldW <num>] [--worldH <num>] [--threshold <0-254>] [--summary]");
            Console.WriteLine("  MapEditorTool.exe portalanim --godotRoot <dir> --in <mp4> [--outDir <res://...|abs>] [--pattern <name_%03d.png>] [--noKeyout] [--summary]");
            Console.WriteLine("  MapEditorTool.exe agent-self-test [--godotRoot <dir>]");
            return 0;
        }

        private static int RunAgentSelfTest(Dictionary<string, string> opts)
        {
            var project = MapProject.CreateDefault();
            if (project.Maps.Count != 1 || !string.Equals(project.Maps[0].Id, "main", StringComparison.Ordinal))
            {
                Console.Error.WriteLine("MapEditorTool agent self-test failed project defaults.");
                return 1;
            }

            var godotRoot = ResolveGodotRoot(opts, false);
            if (!string.IsNullOrWhiteSpace(godotRoot))
            {
                var status = new MapReportExecutor().BuildStatus(godotRoot);
                if (!status.ProjectFileExists || status.MapCount == 0)
                {
                    Console.Error.WriteLine("MapEditorTool agent self-test failed Godot status scan.");
                    return 1;
                }
            }

            Console.WriteLine("MapEditorTool agent self-test OK.");
            return 0;
        }

        private static int RunStatus(Dictionary<string, string> opts)
        {
            var godotRoot = ResolveGodotRoot(opts, true);
            var executor = new MapReportExecutor();
            var status = executor.BuildStatus(godotRoot);
            if (opts.ContainsKey("summary"))
                Console.WriteLine(executor.FormatStatusSummary(status));
            else
                WriteJson(status);

            return status.ProjectFileExists ? 0 : 1;
        }

        private static int RunPortalReview(Dictionary<string, string> opts)
        {
            var godotRoot = ResolveGodotRoot(opts, true);
            var executor = new MapReportExecutor();
            var review = executor.BuildPortalReview(godotRoot);
            if (opts.ContainsKey("summary"))
                Console.WriteLine(executor.FormatPortalReviewSummary(review));
            else
                WriteJson(review);

            return review.ProjectFileExists ? 0 : 1;
        }

        private static int RunRuntimeVerify(Dictionary<string, string> opts)
        {
            var godotRoot = ResolveGodotRoot(opts, true);
            var executor = new RuntimeVerificationExecutor();
            var report = executor.BuildRuntimeVerificationReport(godotRoot);
            if (opts.ContainsKey("summary"))
                Console.WriteLine(executor.FormatRuntimeVerificationSummary(report));
            else
                WriteJson(report);

            return report.Ok ? 0 : 1;
        }

        private static int RunImport(Dictionary<string, string> opts)
        {
            var godotRoot = ResolveGodotRoot(opts, true);
            var output = GetOption(opts, "out");
            if (string.IsNullOrWhiteSpace(output))
                output = Path.Combine(Environment.CurrentDirectory, "map_project.json");

            var project = new MapImportExecutor().ImportFromGodotRoot(godotRoot);
            new ProjectFileExecutor().SaveProject(output, project);

            if (opts.ContainsKey("summary"))
                Console.WriteLine(FormatImportSummary(godotRoot, output, project));
            else
            {
                Console.WriteLine("Imported " + project.Maps.Count + " maps, " + project.Links.Count + " links.");
                Console.WriteLine(Path.GetFullPath(output));
            }

            return 0;
        }

        private static int RunValidate(Dictionary<string, string> opts)
        {
            var godotRoot = ResolveGodotRoot(opts, true);
            var input = GetOption(opts, "in");
            if (string.IsNullOrWhiteSpace(input))
                throw new ArgumentException("Missing --in <file>.");

            var project = new ProjectFileExecutor().LoadProject(input);
            var executor = new MapReportExecutor();
            var report = executor.ValidateProjectAgainstGodot(godotRoot, input, project);
            if (opts.ContainsKey("summary"))
                Console.WriteLine(executor.FormatValidationSummary(report));
            else
                WriteJson(report);

            return report.Ok ? 0 : 1;
        }

        private static int RunPatchPosition(Dictionary<string, string> opts)
        {
            var godotRoot = ResolveGodotRoot(opts, true);
            var scenePath = RequireOption(opts, "scene");
            var nodePath = RequireOption(opts, "nodePath");
            var x = ParseFloat(RequireOption(opts, "x"), "x");
            var y = ParseFloat(RequireOption(opts, "y"), "y");

            var sceneFilePath = ToAbsoluteGodotPath(godotRoot, scenePath);
            var result = new ScenePatchExecutor().PatchNodePosition(sceneFilePath, nodePath, x, y);
            Console.WriteLine(result.Patched ? "patched" : "no changes");
            Console.WriteLine(result.NewRawValue);
            Console.WriteLine(result.SceneFilePath);
            return 0;
        }

        private static int RunTraceAlpha(Dictionary<string, string> opts)
        {
            var input = RequireOption(opts, "in");
            var worldWidth = ParseOptionalInt(GetOption(opts, "worldW"), 0, "worldW");
            var worldHeight = ParseOptionalInt(GetOption(opts, "worldH"), 0, "worldH");
            var threshold = ParseOptionalInt(GetOption(opts, "threshold"), 254, "threshold");
            var report = new ForegroundTextureCollisionExecutor().TraceAlpha(input, worldWidth, worldHeight, threshold);

            if (opts.ContainsKey("summary"))
                Console.WriteLine(FormatTraceAlphaSummary(report));
            else
            {
                Console.WriteLine("polygons=" + report.PolygonCount);
                if (report.PolygonCount > 0)
                {
                    Console.WriteLine("poly0_points=" + report.FirstPolygonPointCount);
                    for (var i = 0; i < report.SamplePoints.Count; i++)
                    {
                        var point = report.SamplePoints[i];
                        Console.WriteLine("p" + i + "=" + FormatFloat(point.X) + "," + FormatFloat(point.Y));
                    }
                }
            }

            return 0;
        }

        private static int RunPortalAnimation(Dictionary<string, string> opts)
        {
            var godotRoot = ResolveGodotRoot(opts, true);
            var input = RequireOption(opts, "in");
            var outputDirectory = GetOption(opts, "outDir");
            var pattern = GetOption(opts, "pattern");
            var keyout = !opts.ContainsKey("noKeyout");
            var result = new PortalAnimationExecutor().ExtractPortalAnimationFrames(
                godotRoot,
                input,
                outputDirectory,
                pattern,
                keyout,
                24);

            if (opts.ContainsKey("summary"))
                Console.WriteLine(FormatPortalAnimationSummary(result, keyout));
            else
                Console.WriteLine(result.OutputDirectoryResPath);

            return 0;
        }

        private static string FormatImportSummary(string godotRoot, string output, MapProject project)
        {
            var lines = new List<string>
            {
                "MapEditorTool import",
                "Project: " + Path.GetFullPath(godotRoot),
                "Generated UTC: " + DateTimeOffset.UtcNow.ToString("O"),
                "Output: " + Path.GetFullPath(output),
                "Counts: maps=" + project.Maps.Count +
                    " links=" + project.Links.Count +
                    " portals=" + project.Maps.Sum(x => x.Portals.Count) +
                    " tileLayers=" + project.Maps.Sum(x => x.TileLayers.Count) +
                    " entities=" + project.Maps.Sum(x => x.Entities.Count),
                "Sample scenes:"
            };

            var sampleScenes = project.Maps
                .Select(x => x.ScenePath)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Take(10)
                .ToList();
            if (sampleScenes.Count == 0)
                lines.Add("  none");
            foreach (var scene in sampleScenes)
                lines.Add("  " + scene);

            return string.Join(Environment.NewLine, lines.ToArray());
        }

        private static string FormatPortalAnimationSummary(PortalAnimationImportResult result, bool keyout)
        {
            var lines = new List<string>
            {
                "MapEditorTool portalanim",
                "Input: " + result.SourceVideoFilePath,
                "Output: " + result.OutputDirectoryPath,
                "Resource: " + result.OutputDirectoryResPath,
                "Frames: " + result.GeneratedFrameCount,
                "Keyout: " + keyout
            };

            foreach (var frame in result.GeneratedFrameFiles.Take(5))
                lines.Add("  " + frame);

            return string.Join(Environment.NewLine, lines.ToArray());
        }

        private static string FormatTraceAlphaSummary(ForegroundTextureAlphaTraceReport report)
        {
            var lines = new List<string>
            {
                "MapEditorTool tracealpha",
                "Input: " + report.ImageFilePath,
                "Image: " + report.ImageWidth + "x" + report.ImageHeight + " alpha=" + report.HasAlphaChannel,
                "World: " + report.WorldWidth + "x" + report.WorldHeight + " threshold=" + report.AlphaThreshold,
                "Counts: polygons=" + report.PolygonCount + " poly0_points=" + report.FirstPolygonPointCount
            };

            for (var i = 0; i < report.SamplePoints.Count; i++)
            {
                var point = report.SamplePoints[i];
                lines.Add("  p" + i + "=" + FormatFloat(point.X) + "," + FormatFloat(point.Y));
            }

            return string.Join(Environment.NewLine, lines.ToArray());
        }

        private static string ResolveGodotRoot(Dictionary<string, string> opts, bool required)
        {
            var godotRoot = GetOption(opts, "godotRoot");
            if (string.IsNullOrWhiteSpace(godotRoot))
            {
                if (!required)
                    return string.Empty;
                godotRoot = GodotProjectLocator.FindGodotRoot(Environment.CurrentDirectory);
            }

            return Path.GetFullPath(godotRoot);
        }

        private static string RequireOption(Dictionary<string, string> opts, string key)
        {
            var value = GetOption(opts, key);
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Missing --" + key + ".");
            return value;
        }

        private static string GetOption(Dictionary<string, string> opts, string key)
        {
            string value;
            return opts.TryGetValue(key, out value) ? value : string.Empty;
        }

        private static float ParseFloat(string value, string key)
        {
            float parsed;
            if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed))
                throw new ArgumentException("Invalid --" + key + ".");
            return parsed;
        }

        private static int ParseOptionalInt(string value, int defaultValue, string key)
        {
            if (string.IsNullOrWhiteSpace(value))
                return defaultValue;

            int parsed;
            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
                throw new ArgumentException("Invalid --" + key + ".");
            return parsed;
        }

        private static string FormatFloat(float value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
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

        private static void WriteJson<T>(T value)
        {
            var serializer = new DataContractJsonSerializer(typeof(T), new DataContractJsonSerializerSettings
            {
                UseSimpleDictionaryFormat = true
            });
            using (var stream = new MemoryStream())
            {
                serializer.WriteObject(stream, value);
                Console.WriteLine(Encoding.UTF8.GetString(stream.ToArray()));
            }
        }

        private static Dictionary<string, string> ParseOptions(string[] args)
        {
            var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < args.Length; i++)
            {
                var arg = args[i] ?? string.Empty;
                if (!arg.StartsWith("--", StringComparison.Ordinal))
                    continue;

                var key = arg.Substring(2);
                var value = string.Empty;
                var equals = key.IndexOf('=');
                if (equals >= 0)
                {
                    value = key.Substring(equals + 1);
                    key = key.Substring(0, equals);
                }
                else if (i + 1 < args.Length && !(args[i + 1] ?? string.Empty).StartsWith("--", StringComparison.Ordinal))
                {
                    value = args[i + 1];
                    i++;
                }

                if (key.Length > 0)
                    options[key] = value;
            }

            return options;
        }
    }
}
