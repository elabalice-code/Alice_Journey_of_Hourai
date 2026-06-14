using System.ComponentModel;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Design;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows.Forms.Design;
using MapEditor.Godot;
using MapEditor.Godot.Tscn;
using MapEditor.Models;

namespace MapEditor;

public sealed partial class MainForm
{
    private void ImportPortalVideoAndApply(MapDefinition map, string sceneAbsPath, Portal portal, string newResVideoPath)
    {
        if (string.IsNullOrWhiteSpace(sceneAbsPath) || !File.Exists(sceneAbsPath))
            return;
        if (portal == null || string.IsNullOrWhiteSpace(portal.NodePath))
            return;

        var root = _godotRoot;
        if (string.IsNullOrWhiteSpace(root))
            root = GodotProjectLocator.FindGodotRoot(AppContext.BaseDirectory);
        if (string.IsNullOrWhiteSpace(root))
            return;

        newResVideoPath = (newResVideoPath ?? "").Trim();
        if (newResVideoPath.Length == 0)
        {
            portal.AnimationFramesDir = "";
            PatchPortalAnimation(sceneAbsPath, portal.NodePath, "", portal.AnimationFps, portal.Upscale);
            return;
        }

        var absMp4 = newResVideoPath.StartsWith("res://", StringComparison.Ordinal) ? ToAbsoluteGodotPath(root, newResVideoPath) : newResVideoPath;
        if (!File.Exists(absMp4))
            return;

        var mapName = SanitizeFolderName(map?.DisplayName ?? "");
        if (string.IsNullOrWhiteSpace(mapName))
            mapName = "Map";
        var portalName = SanitizeFolderName(portal.Name ?? portal.Id ?? "");
        if (string.IsNullOrWhiteSpace(portalName))
            portalName = "Portal";

        var destAbsDir = Path.Combine(root, "CoreEngine", "Resources", "PortalAnimations", mapName, portalName);
        Directory.CreateDirectory(destAbsDir);

        var ffmpegAbs = ResolveBundledFfmpegPath(root);
        foreach (var f in Directory.EnumerateFiles(destAbsDir, "*.png", SearchOption.TopDirectoryOnly))
        {
            try { File.Delete(f); } catch { }
        }

        var frameCount = Math.Max(0, portal.AnimationFrameCount);
        if (frameCount > 0)
        {
            var ffprobeAbs = ResolveBundledFfprobePath(root);
            var durSec = ProbeVideoDurationSeconds(ffprobeAbs, absMp4);
            if (durSec > 0.001)
            {
                ExtractUniformFrames(ffmpegAbs, absMp4, destAbsDir, frameCount, durSec);
            }
            else
            {
                RunProcessChecked(ffmpegAbs, $"-y -hide_banner -loglevel error -i \"{absMp4}\" -vsync 0 -start_number 0 \"{Path.Combine(destAbsDir, "frame_%03d.png")}\"");
            }
        }
        else
        {
            RunProcessChecked(ffmpegAbs, $"-y -hide_banner -loglevel error -i \"{absMp4}\" -vsync 0 -start_number 0 \"{Path.Combine(destAbsDir, "frame_%03d.png")}\"");
        }
        KeyOutBlackBackgroundInDir(destAbsDir, ClampByte(portal.KeyoutTolerance));
        _canvas.EvictImageCacheUnderAbsoluteDir(destAbsDir);

        var resDir = TryMakeResPath(root, destAbsDir);
        portal.AnimationFramesDir = resDir;
        PatchPortalAnimation(sceneAbsPath, portal.NodePath, resDir, ComputePortalAnimFps(portal), portal.Upscale);
    }

    private static float ComputePortalAnimFps(Portal portal)
    {
        var frames = Math.Max(0, portal.AnimationFrameCount);
        var dur = portal.AnimationDurationSec;
        if (frames > 0 && dur > 0.0001f)
            return Math.Max(0.001f, frames / dur);
        return Math.Max(0.001f, portal.AnimationFps);
    }

    private static byte ClampByte(int v) => (byte)Math.Clamp(v, 0, 255);

    private static string ResolveBundledFfprobePath(string root)
    {
        var candidate = Path.Combine(root, "GodotTools", "MapEditor", "ffprobe.exe");
        if (File.Exists(candidate))
            return candidate;

        var candidate2 = Path.Combine(AppContext.BaseDirectory, "ffprobe.exe");
        if (File.Exists(candidate2))
            return candidate2;

        throw new FileNotFoundException("ffprobe.exe not found.");
    }

    private static double ProbeVideoDurationSeconds(string ffprobeAbs, string absMp4)
    {
        var args = $"-v error -select_streams v:0 -show_entries format=duration -of default=nokey=1:noprint_wrappers=1 \"{absMp4}\"";
        var outText = RunProcessCapture(ffprobeAbs, args).Trim();
        if (double.TryParse(outText, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
            return v;
        return 0;
    }

    private static void ExtractUniformFrames(string ffmpegAbs, string absMp4, string destAbsDir, int frameCount, double videoDurationSec)
    {
        if (frameCount <= 0)
            return;
        if (frameCount == 1)
        {
            RunProcessChecked(ffmpegAbs, $"-y -hide_banner -loglevel error -ss 0 -i \"{absMp4}\" -frames:v 1 \"{Path.Combine(destAbsDir, "frame_000.png")}\"");
            return;
        }

        var maxT = Math.Max(0, videoDurationSec - 0.0001);
        for (var i = 0; i < frameCount; i++)
        {
            var t = (double)i / (frameCount - 1) * videoDurationSec;
            if (i == frameCount - 1)
                t = maxT;
            t = Math.Clamp(t, 0, maxT);
            var tStr = t.ToString("0.######", CultureInfo.InvariantCulture);
            var outPng = Path.Combine(destAbsDir, $"frame_{i:000}.png");
            RunProcessChecked(ffmpegAbs, $"-y -hide_banner -loglevel error -ss {tStr} -i \"{absMp4}\" -frames:v 1 \"{outPng}\"");
        }
    }

    private static string RunProcessCapture(string exePath, string args)
    {
        using var p = new Process();
        p.StartInfo = new ProcessStartInfo
        {
            FileName = exePath,
            Arguments = args,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };
        p.Start();
        var stdout = p.StandardOutput.ReadToEnd();
        var stderr = p.StandardError.ReadToEnd();
        p.WaitForExit();

        if (p.ExitCode != 0)
            throw new InvalidOperationException($"Process failed: {Path.GetFileName(exePath)} exit={p.ExitCode}\n{stdout}\n{stderr}");
        return stdout;
    }

    private static void KeyOutBlackBackgroundInDir(string framesAbsDir, byte tolerance)
    {
        if (string.IsNullOrWhiteSpace(framesAbsDir) || !Directory.Exists(framesAbsDir))
            return;

        foreach (var f in Directory.EnumerateFiles(framesAbsDir, "*.png", SearchOption.TopDirectoryOnly))
        {
            if (IsAlphaFrameFileName(f))
            {
                KeyOutBlackBackgroundInPng(f, tolerance);
                continue;
            }

            var alphaPath = MakeAlphaFramePath(f);
            if (alphaPath.Length == 0)
                continue;

            KeyOutBlackBackgroundInPngIntoNewFile(f, alphaPath, tolerance);
            try { File.Delete(f); } catch { }
        }
    }

    private static bool IsAlphaFrameFileName(string pngAbsPath)
    {
        var name = Path.GetFileNameWithoutExtension(pngAbsPath);
        return name.EndsWith("_Alpha", StringComparison.OrdinalIgnoreCase);
    }

    private static string MakeAlphaFramePath(string pngAbsPath)
    {
        var dir = Path.GetDirectoryName(pngAbsPath) ?? "";
        var name = Path.GetFileNameWithoutExtension(pngAbsPath);
        if (string.IsNullOrWhiteSpace(dir) || string.IsNullOrWhiteSpace(name))
            return "";
        if (name.EndsWith("_Alpha", StringComparison.OrdinalIgnoreCase))
            return pngAbsPath;
        return Path.Combine(dir, name + "_Alpha.png");
    }

    private static void KeyOutBlackBackgroundInPng(string pngAbsPath, byte tolerance)
    {
        if (string.IsNullOrWhiteSpace(pngAbsPath) || !File.Exists(pngAbsPath))
            return;

        try
        {
            using var src = new Bitmap(pngAbsPath);
            using var bmp = src.PixelFormat == PixelFormat.Format32bppArgb
                ? new Bitmap(src)
                : ConvertTo32bppArgb(src);

            ApplyKeyOutBlackToBitmap(bmp, tolerance);

            var tmp = pngAbsPath + ".tmp.png";
            bmp.Save(tmp, ImageFormat.Png);
            File.Copy(tmp, pngAbsPath, overwrite: true);
            File.Delete(tmp);
        }
        catch
        {
            return;
        }
    }

    private static void KeyOutBlackBackgroundInPngIntoNewFile(string srcPngAbsPath, string dstPngAbsPath, byte tolerance)
    {
        if (string.IsNullOrWhiteSpace(srcPngAbsPath) || !File.Exists(srcPngAbsPath))
            return;
        if (string.IsNullOrWhiteSpace(dstPngAbsPath))
            return;

        try
        {
            using var src = new Bitmap(srcPngAbsPath);
            using var bmp = src.PixelFormat == PixelFormat.Format32bppArgb
                ? new Bitmap(src)
                : ConvertTo32bppArgb(src);

            ApplyKeyOutBlackToBitmap(bmp, tolerance);

            var tmp = dstPngAbsPath + ".tmp.png";
            bmp.Save(tmp, ImageFormat.Png);
            File.Copy(tmp, dstPngAbsPath, overwrite: true);
            File.Delete(tmp);
        }
        catch
        {
            return;
        }
    }

    private static void ApplyKeyOutBlackToBitmap(Bitmap bmp, byte tolerance)
    {
        var rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
        var data = bmp.LockBits(rect, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
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
                    var i = x * 4;
                    var b = row[i + 0];
                    var g = row[i + 1];
                    var r = row[i + 2];
                    if (r <= tolerance && g <= tolerance && b <= tolerance)
                    {
                        row[i + 0] = 0;
                        row[i + 1] = 0;
                        row[i + 2] = 0;
                        row[i + 3] = 0;
                    }
                }
                Marshal.Copy(row, 0, rowPtr, rowBytes);
            }
        }
        finally
        {
            bmp.UnlockBits(data);
        }
    }

    private static Bitmap ConvertTo32bppArgb(Bitmap src)
    {
        var dest = new Bitmap(src.Width, src.Height, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(dest);
        g.DrawImage(src, 0, 0, src.Width, src.Height);
        return dest;
    }

    private static string ResolveBundledFfmpegPath(string root)
    {
        var candidate = Path.Combine(root, "GodotTools", "MapEditor", "ffmpeg.exe");
        if (File.Exists(candidate))
            return candidate;

        var candidate2 = Path.Combine(AppContext.BaseDirectory, "ffmpeg.exe");
        if (File.Exists(candidate2))
            return candidate2;

        throw new FileNotFoundException("ffmpeg.exe not found.");
    }

    private static string ResolveBundledFfplayPath(string root)
    {
        var candidate = Path.Combine(root, "GodotTools", "MapEditor", "ffplay.exe");
        if (File.Exists(candidate))
            return candidate;

        var candidate2 = Path.Combine(AppContext.BaseDirectory, "ffplay.exe");
        if (File.Exists(candidate2))
            return candidate2;

        throw new FileNotFoundException("ffplay.exe not found.");
    }

    private static void RunProcessChecked(string exePath, string args)
    {
        using var p = new Process();
        p.StartInfo = new ProcessStartInfo
        {
            FileName = exePath,
            Arguments = args,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };
        p.Start();
        var stdout = p.StandardOutput.ReadToEnd();
        var stderr = p.StandardError.ReadToEnd();
        p.WaitForExit();

        if (p.ExitCode != 0)
            throw new InvalidOperationException($"Process failed: {Path.GetFileName(exePath)} exit={p.ExitCode}\n{stdout}\n{stderr}");
    }

    private bool EnsurePortalNodeExists(string sceneAbsPath, string nodePath, float x, float y)
    {
        if (string.IsNullOrWhiteSpace(sceneAbsPath) || string.IsNullOrWhiteSpace(nodePath) || !File.Exists(sceneAbsPath))
            return false;

        var scene = TscnParser.ParseFile(sceneAbsPath);
        if (scene.Nodes.Any(n => string.Equals(ComputeNodePath(n.Parent, n.Name), nodePath, StringComparison.Ordinal)))
            return true;

        var portalResPath = FindExistingPortalPrefabResPath(sceneAbsPath) ?? "res://CoreEngine/Objects/Portal.tscn";
        var root = string.IsNullOrWhiteSpace(_godotRoot) ? GodotProjectLocator.FindGodotRoot(AppContext.BaseDirectory) : _godotRoot;
        if (!string.IsNullOrWhiteSpace(root))
        {
            var portalAbs = ToAbsoluteGodotPath(root, portalResPath);
            if (!File.Exists(portalAbs))
                return false;
        }

        nodePath = nodePath.Trim().Trim('/');
        var parent = ".";
        var name = nodePath;
        var slash = nodePath.LastIndexOf('/');
        if (slash >= 0)
        {
            parent = nodePath[..slash];
            name = nodePath[(slash + 1)..];
            if (parent.Length == 0)
                parent = ".";
        }

        return TryAppendPortalNodeToTscn(sceneAbsPath, scene, portalResPath, parent, name, x, y);
    }

    private static bool TryRepairMissingPortalNode(string sceneAbsPath, string nodePath)
    {
        var project = ProjectContext.CurrentProject;
        if (project == null)
            return false;
        var form = MainFormContext.CurrentForm;
        if (form == null)
            return false;

        var root = GodotRootContext.CurrentRoot;
        if (string.IsNullOrWhiteSpace(root))
            root = GodotProjectLocator.FindGodotRoot(AppContext.BaseDirectory);
        if (string.IsNullOrWhiteSpace(root))
            return false;

        MapDefinition? map = null;
        foreach (var m in project.Maps)
        {
            if (string.IsNullOrWhiteSpace(m.ScenePath))
                continue;
            var abs = ToAbsoluteGodotPath(root, m.ScenePath);
            if (string.Equals(abs, sceneAbsPath, StringComparison.OrdinalIgnoreCase))
            {
                map = m;
                break;
            }
        }
        if (map == null)
            return false;

        var portal = map.Portals.FirstOrDefault(p => string.Equals((p.NodePath ?? "").Trim(), nodePath, StringComparison.Ordinal));
        if (portal == null)
            return false;

        return form.EnsurePortalNodeExists(sceneAbsPath, nodePath, portal.X, portal.Y);
    }

    public sealed class PortalCollectionEditor : CollectionEditor
    {
        private ITypeDescriptorContext? _ctx;
        private MapDefinition? _map;
        private string _sceneAbsPath = "";
        private Process? _ffplay;

        public PortalCollectionEditor(Type type) : base(type)
        {
        }

        public override object? EditValue(ITypeDescriptorContext context, IServiceProvider provider, object? value)
        {
            _ctx = context;
            _map = context.Instance as MapDefinition ?? SelectedMapContext.CurrentMap;
            _sceneAbsPath = "";
            var form = MainFormContext.CurrentForm;
            if (_map != null && form != null)
            {
                var root = GodotRootContext.CurrentRoot;
                if (string.IsNullOrWhiteSpace(root))
                    root = GodotProjectLocator.FindGodotRoot(AppContext.BaseDirectory);
                if (!string.IsNullOrWhiteSpace(root))
                    _sceneAbsPath = form.EnsureMapSceneExists(root, _map) ?? "";
            }
            return base.EditValue(context, provider, value);
        }

        protected override CollectionForm CreateCollectionForm()
        {
            var f = base.CreateCollectionForm();
            var pg = FindFirstChild<PropertyGrid>(f);
            if (pg != null)
                pg.PropertyValueChanged += OnPortalPropertyValueChanged;
            f.FormClosed += (_, _) =>
            {
                if (pg != null)
                    pg.PropertyValueChanged -= OnPortalPropertyValueChanged;
                try
                {
                    if (_ffplay != null && !_ffplay.HasExited)
                        _ffplay.Kill(entireProcessTree: true);
                }
                catch
                {
                }
            };
            return f;
        }

        private void OnPortalPropertyValueChanged(object? sender, PropertyValueChangedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_sceneAbsPath) || !File.Exists(_sceneAbsPath))
                return;
            if (sender is not PropertyGrid pg)
                return;
            if (pg.SelectedObject is not Portal portal)
                return;

            var form = MainFormContext.CurrentForm;
            if (form == null)
                return;

            var propName = e.ChangedItem?.PropertyDescriptor?.Name ?? "";
            if (string.Equals(propName, nameof(Portal.X), StringComparison.Ordinal)
                || string.Equals(propName, nameof(Portal.Y), StringComparison.Ordinal))
            {
                if (!string.IsNullOrWhiteSpace(portal.NodePath))
                    form._portalSyncActor.Send(new PortalSyncMessage(
                        PortalSyncKind.Position,
                        _sceneAbsPath,
                        portal.NodePath,
                        portal.X,
                        portal.Y,
                        "",
                        ""));
            }
            else if (string.Equals(propName, nameof(Portal.TargetMapId), StringComparison.Ordinal)
                || string.Equals(propName, nameof(Portal.TargetPortalId), StringComparison.Ordinal))
            {
                if (!string.IsNullOrWhiteSpace(portal.NodePath))
                    form._portalSyncActor.Send(new PortalSyncMessage(
                        PortalSyncKind.Target,
                        _sceneAbsPath,
                        portal.NodePath,
                        0,
                        0,
                        portal.TargetMapId ?? "",
                        portal.TargetPortalId ?? ""));
            }
            else if (string.Equals(propName, nameof(Portal.AnimationVideoPath), StringComparison.Ordinal)
                )
            {
                var root = GodotRootContext.CurrentRoot;
                if (string.IsNullOrWhiteSpace(root))
                    root = GodotProjectLocator.FindGodotRoot(AppContext.BaseDirectory);
                if (string.IsNullOrWhiteSpace(root))
                    return;

                var videoRes = (portal.AnimationVideoPath ?? "").Trim();
                var absVideo = videoRes.StartsWith("res://", StringComparison.Ordinal) ? ToAbsoluteGodotPath(root, videoRes) : videoRes;
                if (File.Exists(absVideo))
                {
                    try
                    {
                        if (_ffplay != null && !_ffplay.HasExited)
                            _ffplay.Kill(entireProcessTree: true);
                    }
                    catch
                    {
                    }
                    try
                    {
                        var ffplayAbs = ResolveBundledFfplayPath(root);
                        _ffplay = new Process
                        {
                            StartInfo = new ProcessStartInfo
                            {
                                FileName = ffplayAbs,
                                Arguments = $"-hide_banner -loglevel error -autoexit \"{absVideo}\"",
                                UseShellExecute = false,
                                CreateNoWindow = true
                            }
                        };
                        _ffplay.Start();
                    }
                    catch
                    {
                    }
                }

                if (_map != null && !string.IsNullOrWhiteSpace(portal.NodePath))
                {
                    Task.Run(() =>
                    {
                        try
                        {
                            form.ImportPortalVideoAndApply(_map, _sceneAbsPath, portal, videoRes);
                            void RefreshUi()
                            {
                                pg.Refresh();
                                form._canvas.Invalidate();
                                form.UpdateStatus();
                            }
                            if (form.IsDisposed)
                                return;
                            if (form.InvokeRequired)
                                form.BeginInvoke(RefreshUi);
                            else
                                RefreshUi();
                        }
                        catch (Exception ex)
                        {
                            if (form.IsDisposed)
                                return;
                            void ShowError()
                            {
                                MessageBox.Show(form, ex.Message, "导入失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            }
                            if (form.InvokeRequired)
                                form.BeginInvoke(ShowError);
                            else
                                ShowError();
                        }
                    });
                }
            }
            else if (string.Equals(propName, nameof(Portal.AnimationFps), StringComparison.Ordinal)
                || string.Equals(propName, nameof(Portal.Upscale), StringComparison.Ordinal))
            {
                if (!string.IsNullOrWhiteSpace(portal.NodePath))
                {
                    Task.Run(() =>
                    {
                        try
                        {
                            PatchPortalAnimSettings(_sceneAbsPath, portal.NodePath, ComputePortalAnimFps(portal), portal.Upscale);
                            void RefreshUi()
                            {
                                pg.Refresh();
                                form._canvas.Invalidate();
                                form.UpdateStatus();
                            }
                            if (form.IsDisposed)
                                return;
                            if (form.InvokeRequired)
                                form.BeginInvoke(RefreshUi);
                            else
                                RefreshUi();
                        }
                        catch (Exception ex)
                        {
                            if (form.IsDisposed)
                                return;
                            void ShowError()
                            {
                                MessageBox.Show(form, ex.Message, "写回失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            }
                            if (form.InvokeRequired)
                                form.BeginInvoke(ShowError);
                            else
                                ShowError();
                        }
                    });
                }
            }
            else if (string.Equals(propName, nameof(Portal.KeyoutTolerance), StringComparison.Ordinal))
            {
                var root = GodotRootContext.CurrentRoot;
                if (string.IsNullOrWhiteSpace(root))
                    root = GodotProjectLocator.FindGodotRoot(AppContext.BaseDirectory);
                if (string.IsNullOrWhiteSpace(root))
                    return;

                var dirRes = (portal.AnimationFramesDir ?? "").Trim();
                if (dirRes.StartsWith("res://", StringComparison.Ordinal) && dirRes.Length > 0)
                {
                    var absDir = ToAbsoluteGodotPath(root, dirRes);
                    if (Directory.Exists(absDir))
                    {
                        Task.Run(() =>
                        {
                            KeyOutBlackBackgroundInDir(absDir, ClampByte(portal.KeyoutTolerance));
                            form._canvas.EvictImageCacheUnderAbsoluteDir(absDir);
                            void RefreshUi()
                            {
                                pg.Refresh();
                                form._canvas.Invalidate();
                                form.UpdateStatus();
                            }
                            if (form.IsDisposed)
                                return;
                            if (form.InvokeRequired)
                                form.BeginInvoke(RefreshUi);
                            else
                                RefreshUi();
                        });
                    }
                }
            }
            else if (string.Equals(propName, nameof(Portal.AnimationDurationSec), StringComparison.Ordinal))
            {
                if (!string.IsNullOrWhiteSpace(portal.NodePath))
                {
                    Task.Run(() =>
                    {
                        try
                        {
                            PatchPortalAnimSettings(_sceneAbsPath, portal.NodePath, ComputePortalAnimFps(portal), portal.Upscale);
                            void RefreshUi()
                            {
                                pg.Refresh();
                                form._canvas.Invalidate();
                                form.UpdateStatus();
                            }
                            if (form.IsDisposed)
                                return;
                            if (form.InvokeRequired)
                                form.BeginInvoke(RefreshUi);
                            else
                                RefreshUi();
                        }
                        catch
                        {
                        }
                    });
                }
            }
            else if (string.Equals(propName, nameof(Portal.AnimationFrameCount), StringComparison.Ordinal))
            {
                if (_map != null && !string.IsNullOrWhiteSpace(portal.NodePath))
                {
                    Task.Run(() =>
                    {
                        try
                        {
                            form.ImportPortalVideoAndApply(_map, _sceneAbsPath, portal, (portal.AnimationVideoPath ?? "").Trim());
                            void RefreshUi()
                            {
                                pg.Refresh();
                                form._canvas.Invalidate();
                                form.UpdateStatus();
                            }
                            if (form.IsDisposed)
                                return;
                            if (form.InvokeRequired)
                                form.BeginInvoke(RefreshUi);
                            else
                                RefreshUi();
                        }
                        catch
                        {
                        }
                    });
                }
            }
            else
            {
                return;
            }
        }

        private static T? FindFirstChild<T>(Control root) where T : Control
        {
            if (root is T hit)
                return hit;
            foreach (Control c in root.Controls)
            {
                var found = FindFirstChild<T>(c);
                if (found != null)
                    return found;
            }
            return null;
        }

        protected override object CreateInstance(Type itemType)
        {
            var map = _ctx?.Instance as MapDefinition ?? SelectedMapContext.CurrentMap;
            var project = ProjectContext.CurrentProject;
            var form = MainFormContext.CurrentForm;
            if (map == null || project == null || form == null)
                return base.CreateInstance(itemType);

            var root = GodotRootContext.CurrentRoot;
            if (string.IsNullOrWhiteSpace(root))
                root = GodotProjectLocator.FindGodotRoot(AppContext.BaseDirectory);
            if (string.IsNullOrWhiteSpace(root))
                return base.CreateInstance(itemType);

            var sceneAbsPath = form.EnsureMapSceneExists(root, map);
            if (string.IsNullOrWhiteSpace(sceneAbsPath) || !File.Exists(sceneAbsPath))
                return base.CreateInstance(itemType);

            var scene = TscnParser.ParseFile(sceneAbsPath);
            var existingNames = new HashSet<string>(
                scene.Nodes.Select(n => (n.Name ?? "").Trim()).Where(s => s.Length > 0),
                StringComparer.Ordinal);
            foreach (var p in map.Portals)
            {
                var np = (p.NodePath ?? "").Trim().Trim('/');
                if (np.Length > 0)
                {
                    var seg = np.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).LastOrDefault() ?? "";
                    if (seg.Length > 0)
                        existingNames.Add(seg);
                }
            }

            var uniqueName = MakeUniqueName("Portal", n => existingNames.Contains(n));
            var portalResPath = FindExistingPortalPrefabResPath(sceneAbsPath) ?? "res://CoreEngine/Objects/Portal.tscn";
            var portalAbs = ToAbsoluteGodotPath(root, portalResPath);
            if (!File.Exists(portalAbs))
                return base.CreateInstance(itemType);

            if (!form.TryAppendPortalNodeToTscn(sceneAbsPath, scene, portalResPath, ".", uniqueName, 0, 0))
                return base.CreateInstance(itemType);

            return new Portal
            {
                Id = uniqueName,
                Name = uniqueName,
                NodePath = uniqueName,
                X = 0,
                Y = 0,
                TargetMapId = "",
                TargetPortalId = ""
            };
        }
    }

    public sealed class PortalTargetMapIdConverter : StringConverter
    {
        public override bool GetStandardValuesSupported(ITypeDescriptorContext? context) => true;
        public override bool GetStandardValuesExclusive(ITypeDescriptorContext? context) => false;

        public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext? context)
        {
            var project = ProjectContext.CurrentProject;
            if (project == null)
                return new StandardValuesCollection(Array.Empty<string>());
            var values = project.Maps
                .Select(m =>
                {
                    var scenePath = (m.ScenePath ?? "").Trim();
                    if (scenePath.Length > 0)
                        return scenePath;
                    return (m.Id ?? "").Trim();
                })
                .Where(s => s.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .ToList();
            values.Insert(0, "");
            return new StandardValuesCollection(values);
        }

        public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
        {
            if (destinationType == typeof(string))
            {
                var raw = (value as string ?? "").Trim();
                if (raw.Length == 0)
                    return "";
                var project = ProjectContext.CurrentProject;
                if (project != null)
                {
                    var map = ResolveMapByAnyId(project, raw);
                    if (map != null)
                    {
                        var name = (map.DisplayName ?? "").Trim();
                        if (name.Length > 0)
                            return name;
                        var mapScenePath = (map.ScenePath ?? "").Trim();
                        if (mapScenePath.StartsWith("res://", StringComparison.OrdinalIgnoreCase))
                            return Path.GetFileNameWithoutExtension(mapScenePath);
                        var mapId = (map.Id ?? "").Trim();
                        if (mapId.StartsWith("res://", StringComparison.OrdinalIgnoreCase))
                            return Path.GetFileNameWithoutExtension(mapId);
                        return mapId.Length > 0 ? mapId : raw;
                    }
                }
                return raw;
            }
            return base.ConvertTo(context, culture, value, destinationType);
        }
    }

    public sealed class PortalTargetPortalIdConverter : StringConverter
    {
        public override bool GetStandardValuesSupported(ITypeDescriptorContext? context) => true;
        public override bool GetStandardValuesExclusive(ITypeDescriptorContext? context) => false;

        public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext? context)
        {
            var values = TryGetAreaIds().ToList();
            values.Insert(0, "");
            return new StandardValuesCollection(values);
        }

        public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
        {
            if (destinationType == typeof(string))
            {
                var raw = (value as string ?? "").Trim();
                if (raw.Length == 0)
                    return "";
                return raw;
            }
            return base.ConvertTo(context, culture, value, destinationType);
        }
    }

    public sealed class PortalTargetMapIdEditor : UITypeEditor
    {
        public override UITypeEditorEditStyle GetEditStyle(ITypeDescriptorContext? context) => UITypeEditorEditStyle.DropDown;

        public override object? EditValue(ITypeDescriptorContext? context, IServiceProvider? provider, object? value)
        {
            var svc = provider?.GetService(typeof(IWindowsFormsEditorService)) as IWindowsFormsEditorService;
            var project = ProjectContext.CurrentProject;
            if (svc == null || project == null)
                return value;

            var lb = new ListBox { BorderStyle = BorderStyle.None, IntegralHeight = true };
            lb.DisplayMember = nameof(Choice.Label);
            lb.ValueMember = nameof(Choice.Value);
            lb.Items.Add(new Choice("(清空)", ""));
            foreach (var m in project.Maps.OrderBy(m => (m.DisplayName ?? "").Trim(), StringComparer.OrdinalIgnoreCase))
            {
                var mapLabel = (m.DisplayName ?? "").Trim();
                if (mapLabel.Length == 0)
                {
                    var scenePath = (m.ScenePath ?? "").Trim();
                    mapLabel = scenePath.Length > 0 ? Path.GetFileNameWithoutExtension(scenePath) : (m.Id ?? "").Trim();
                }
                var mapValue = "";
                {
                    var scenePath = (m.ScenePath ?? "").Trim();
                    mapValue = scenePath.Length > 0 ? scenePath : (m.Id ?? "").Trim();
                }
                if (mapValue.Length == 0)
                    continue;
                lb.Items.Add(new Choice(mapLabel, mapValue));
            }

            var current = (value as string ?? "").Trim();
            for (var i = 0; i < lb.Items.Count; i++)
            {
                if (lb.Items[i] is Choice c && string.Equals(c.Value, current, StringComparison.Ordinal))
                {
                    lb.SelectedIndex = i;
                    break;
                }
            }

            lb.Click += (_, _) => svc.CloseDropDown();
            svc.DropDownControl(lb);
            return (lb.SelectedItem as Choice)?.Value ?? value;
        }

        private sealed class Choice
        {
            public string Label { get; }
            public string Value { get; }
            public Choice(string label, string value)
            {
                Label = label;
                Value = value;
            }
            public override string ToString() => Label;
        }
    }

    public sealed class PortalTargetPortalIdEditor : UITypeEditor
    {
        public override UITypeEditorEditStyle GetEditStyle(ITypeDescriptorContext? context) => UITypeEditorEditStyle.DropDown;

        public override object? EditValue(ITypeDescriptorContext? context, IServiceProvider? provider, object? value)
        {
            var svc = provider?.GetService(typeof(IWindowsFormsEditorService)) as IWindowsFormsEditorService;
            if (svc == null)
                return value;

            var lb = new ListBox { BorderStyle = BorderStyle.None, IntegralHeight = true };
            lb.DisplayMember = nameof(Choice.Label);
            lb.ValueMember = nameof(Choice.Value);
            lb.Items.Add(new Choice("(清空)", ""));
            foreach (var id in TryGetAreaIds().OrderBy(s => s, StringComparer.OrdinalIgnoreCase))
            {
                if (id.Length == 0)
                    continue;
                lb.Items.Add(new Choice(id, id));
            }

            var current = (value as string ?? "").Trim();
            for (var i = 0; i < lb.Items.Count; i++)
            {
                if (lb.Items[i] is Choice c && string.Equals(c.Value, current, StringComparison.Ordinal))
                {
                    lb.SelectedIndex = i;
                    break;
                }
            }

            lb.Click += (_, _) => svc.CloseDropDown();
            svc.DropDownControl(lb);
            return (lb.SelectedItem as Choice)?.Value ?? value;
        }

        private sealed class Choice
        {
            public string Label { get; }
            public string Value { get; }
            public Choice(string label, string value)
            {
                Label = label;
                Value = value;
            }
            public override string ToString() => Label;
        }
    }

    private static IEnumerable<string> TryGetAreaIds()
    {
        var root = GodotRootContext.CurrentRoot;
        if (string.IsNullOrWhiteSpace(root))
            root = GodotProjectLocator.FindGodotRoot(AppContext.BaseDirectory);
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            return [];

        string? abs = null;
        try
        {
            abs = Directory.EnumerateFiles(root, "AreaCatalog.gd", SearchOption.AllDirectories)
                .FirstOrDefault(p => p.EndsWith(Path.Combine("Scripts", "World", "AreaCatalog.gd"), StringComparison.OrdinalIgnoreCase))
                ?? Directory.EnumerateFiles(root, "AreaCatalog.gd", SearchOption.AllDirectories).FirstOrDefault();
        }
        catch
        {
            abs = null;
        }
        if (string.IsNullOrWhiteSpace(abs) || !File.Exists(abs))
            return [];

        try
        {
            var text = File.ReadAllText(abs);
            var set = new HashSet<string>(StringComparer.Ordinal);
            foreach (Match m in Regex.Matches(text, "&\"(?<id>[^\"]+)\"\\s*:", RegexOptions.CultureInvariant))
            {
                if (!m.Success)
                    continue;
                var id = (m.Groups["id"].Value ?? "").Trim();
                if (id.Length > 0)
                    set.Add(id);
            }
            return set;
        }
        catch
        {
            return [];
        }
    }

    private static MapDefinition? ResolveMapByAnyId(MapProject project, string mapId)
    {
        mapId = (mapId ?? "").Trim();
        if (mapId.Length == 0)
            return null;

        var direct = project.Maps.FirstOrDefault(m =>
            string.Equals((m.Id ?? "").Trim(), mapId, StringComparison.Ordinal)
            || string.Equals((m.ScenePath ?? "").Trim(), mapId, StringComparison.Ordinal));
        if (direct != null)
            return direct;

        if (!mapId.StartsWith("uid://", StringComparison.OrdinalIgnoreCase))
            return null;

        var root = GodotRootContext.CurrentRoot;
        if (string.IsNullOrWhiteSpace(root))
            root = GodotProjectLocator.FindGodotRoot(AppContext.BaseDirectory);
        if (string.IsNullOrWhiteSpace(root))
            return null;

        foreach (var m in project.Maps)
        {
            var sp = (m.ScenePath ?? "").Trim();
            if (!sp.StartsWith("res://", StringComparison.OrdinalIgnoreCase))
                continue;
            var abs = ToAbsoluteGodotPath(root, sp);
            if (!File.Exists(abs))
                continue;
            var uid = TryReadSceneUid(abs);
            if (uid.Length > 0 && string.Equals(uid, mapId, StringComparison.Ordinal))
                return m;
        }

        return null;
    }

    private static string TryReadSceneUid(string sceneAbsPath)
    {
        try
        {
            using var sr = new StreamReader(sceneAbsPath);
            var line = sr.ReadLine() ?? "";
            var m = Regex.Match(line, "uid=\"(?<uid>uid://[^\"]+)\"", RegexOptions.CultureInvariant);
            return m.Success ? m.Groups["uid"].Value : "";
        }
        catch
        {
            return "";
        }
    }
}
