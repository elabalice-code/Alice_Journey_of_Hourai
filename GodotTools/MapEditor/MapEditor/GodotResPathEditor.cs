using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Drawing.Design;
using System.Linq;
using System.Windows.Forms;

namespace MapEditor;

public sealed class GodotResPathEditor : UITypeEditor
{
    public override UITypeEditorEditStyle GetEditStyle(ITypeDescriptorContext? context)
    {
        return UITypeEditorEditStyle.Modal;
    }

    public override object? EditValue(ITypeDescriptorContext? context, IServiceProvider? provider, object? value)
    {
        var root = GodotRootContext.CurrentRoot;
        if (string.IsNullOrWhiteSpace(root))
            return value;

        var prop = context?.PropertyDescriptor;
        if (prop == null || prop.IsReadOnly)
            return value;

        var name = prop.Name ?? "";
        var currentValue = value as string ?? "";
        var isDir = name.EndsWith("Dir", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith("Directory", StringComparison.OrdinalIgnoreCase);

        var absCurrent = TryResolveToExistingPath(root, currentValue);
        var initialDir = ResolveInitialDirectory(root, absCurrent);
        if (absCurrent == null && string.IsNullOrWhiteSpace(currentValue))
            initialDir = EnsurePreferredProjectResourceDir(root, initialDir);

        string? chosenAbsPath;
        if (isDir)
        {
            using var dlg = new FolderBrowserDialog
            {
                InitialDirectory = initialDir,
                UseDescriptionForTitle = true,
                Description = "选择文件夹"
            };
            if (dlg.ShowDialog() != DialogResult.OK)
                return value;
            chosenAbsPath = dlg.SelectedPath;
        }
        else
        {
            using var dlg = new OpenFileDialog
            {
                Title = "选择文件",
                Filter = BuildFilter(name, currentValue),
                InitialDirectory = initialDir,
                FileName = absCurrent ?? ""
            };
            if (dlg.ShowDialog() != DialogResult.OK)
                return value;
            chosenAbsPath = dlg.FileName;
        }

        if (string.IsNullOrWhiteSpace(chosenAbsPath))
            return value;

        return ConvertToResPathWithAutoImport(root, chosenAbsPath, initialDir);
    }

    private static string EnsurePreferredProjectResourceDir(string godotRoot, string fallback)
    {
        var map = SelectedMapContext.CurrentMap;
        if (map == null || string.IsNullOrWhiteSpace(map.ScenePath))
            return fallback;

        if (!map.ScenePath.StartsWith("res://", StringComparison.Ordinal))
            return fallback;

        var rel = map.ScenePath["res://".Length..].TrimStart('/').Replace('\\', '/');
        var sceneBaseName = Path.GetFileNameWithoutExtension(rel);
        if (sceneBaseName.Length == 0)
            sceneBaseName = map.DisplayName.Length > 0 ? map.DisplayName : "Map";

        var safeName = SanitizeFolderName(sceneBaseName);
        if (safeName.Length == 0)
            safeName = "Map";

        var preferredAbs = "";
        var idx = rel.IndexOf("/CoreEngine/Maps/", StringComparison.OrdinalIgnoreCase);
        if (idx >= 0)
        {
            var prefix = rel[..(idx + 1)];
            var resRel = prefix + "CoreEngine/Resources/Maps/" + safeName;
            preferredAbs = Path.Combine(godotRoot, resRel.Replace('/', Path.DirectorySeparatorChar));
        }
        else
        {
            var absScene = Path.Combine(godotRoot, rel.Replace('/', Path.DirectorySeparatorChar));
            var sceneDir = Path.GetDirectoryName(absScene);
            if (!string.IsNullOrWhiteSpace(sceneDir))
                preferredAbs = Path.Combine(sceneDir, "Resources", safeName);
        }

        if (preferredAbs.Length == 0)
            return fallback;

        preferredAbs = Path.GetFullPath(preferredAbs);
        var rootAbs = Path.GetFullPath(godotRoot);
        if (!preferredAbs.StartsWith(rootAbs, StringComparison.OrdinalIgnoreCase))
            return fallback;

        Directory.CreateDirectory(preferredAbs);
        return preferredAbs;
    }

    private static string SanitizeFolderName(string name)
    {
        if (name.Length == 0)
            return "";
        var invalid = Path.GetInvalidFileNameChars();
        var chars = name.ToCharArray();
        var w = 0;
        for (var i = 0; i < chars.Length; i++)
        {
            var c = chars[i];
            if (invalid.Contains(c))
                continue;
            if (c == '/' || c == '\\')
                continue;
            chars[w++] = c;
        }
        return new string(chars, 0, w).Trim();
    }

    private static string BuildFilter(string propName, string currentValue)
    {
        var ext = "";
        if (!string.IsNullOrWhiteSpace(currentValue))
            ext = Path.GetExtension(currentValue.Trim());

        if (string.Equals(ext, ".mp4", StringComparison.OrdinalIgnoreCase) || propName.Contains("Video", StringComparison.OrdinalIgnoreCase))
            return "视频 (*.mp4)|*.mp4|所有文件 (*.*)|*.*";

        if (string.Equals(ext, ".png", StringComparison.OrdinalIgnoreCase) || string.Equals(ext, ".jpg", StringComparison.OrdinalIgnoreCase) || string.Equals(ext, ".jpeg", StringComparison.OrdinalIgnoreCase) || string.Equals(ext, ".webp", StringComparison.OrdinalIgnoreCase))
            return "图片 (*.png;*.jpg;*.jpeg;*.webp)|*.png;*.jpg;*.jpeg;*.webp|所有文件 (*.*)|*.*";

        if (string.Equals(ext, ".tscn", StringComparison.OrdinalIgnoreCase))
            return "Godot 场景 (*.tscn)|*.tscn|所有文件 (*.*)|*.*";

        if (string.Equals(ext, ".tres", StringComparison.OrdinalIgnoreCase))
            return "Godot 资源 (*.tres)|*.tres|所有文件 (*.*)|*.*";

        if (propName.Contains("TileSet", StringComparison.OrdinalIgnoreCase))
            return "Godot TileSet (*.tres)|*.tres|所有文件 (*.*)|*.*";

        if (propName.Contains("Scene", StringComparison.OrdinalIgnoreCase))
            return "Godot 场景 (*.tscn)|*.tscn|所有文件 (*.*)|*.*";

        if (propName.Contains("Texture", StringComparison.OrdinalIgnoreCase))
            return "图片 (*.png;*.jpg;*.jpeg;*.webp)|*.png;*.jpg;*.jpeg;*.webp|所有文件 (*.*)|*.*";

        return "所有文件 (*.*)|*.*";
    }

    private static string? TryResolveToExistingPath(string godotRoot, string value)
    {
        value = value.Trim();
        if (value.Length == 0)
            return null;
        if (value.StartsWith("res://", StringComparison.Ordinal))
        {
            var rel = value["res://".Length..].TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            var abs = Path.Combine(godotRoot, rel);
            if (File.Exists(abs) || Directory.Exists(abs))
                return abs;
            return null;
        }
        if (Path.IsPathRooted(value) && (File.Exists(value) || Directory.Exists(value)))
            return value;
        return null;
    }

    private static string ResolveInitialDirectory(string godotRoot, string? absCurrent)
    {
        if (!string.IsNullOrWhiteSpace(absCurrent))
        {
            if (Directory.Exists(absCurrent))
                return absCurrent;
            var dir = Path.GetDirectoryName(absCurrent);
            if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir))
                return dir;
        }
        return Directory.Exists(godotRoot) ? godotRoot : Environment.CurrentDirectory;
    }

    private static string TryMakeResPath(string godotRoot, string absPath)
    {
        if (!Path.IsPathRooted(absPath))
            return absPath;
        var rel = Path.GetRelativePath(godotRoot, absPath);
        if (!rel.StartsWith("..", StringComparison.Ordinal) && !Path.IsPathRooted(rel))
            return "res://" + rel.Replace('\\', '/');
        return absPath;
    }

    private static string ConvertToResPathWithAutoImport(string godotRoot, string chosenAbsPath, string initialDir)
    {
        chosenAbsPath = chosenAbsPath.Trim();
        if (chosenAbsPath.Length == 0)
            return chosenAbsPath;

        if (!Path.IsPathRooted(chosenAbsPath))
            return chosenAbsPath;

        if (IsUnderRoot(godotRoot, chosenAbsPath))
            return TryMakeResPath(godotRoot, chosenAbsPath);

        var destBaseDir = EnsurePreferredProjectResourceDir(godotRoot, initialDir);
        Directory.CreateDirectory(destBaseDir);

        if (File.Exists(chosenAbsPath))
        {
            var destAbs = ImportFileToDirectory(chosenAbsPath, destBaseDir);
            return TryMakeResPath(godotRoot, destAbs);
        }

        if (Directory.Exists(chosenAbsPath))
        {
            var destAbs = ImportDirectoryToDirectory(chosenAbsPath, destBaseDir);
            return TryMakeResPath(godotRoot, destAbs);
        }

        return chosenAbsPath;
    }

    private static bool IsUnderRoot(string root, string path)
    {
        var rootFull = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var pathFull = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return pathFull.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase);
    }

    private static string ImportFileToDirectory(string sourceAbsPath, string destDirAbs)
    {
        Directory.CreateDirectory(destDirAbs);
        var fileName = Path.GetFileName(sourceAbsPath);
        if (string.IsNullOrWhiteSpace(fileName))
            fileName = "ImportedFile";
        var destAbs = GetUniquePath(Path.Combine(destDirAbs, fileName));
        File.Copy(sourceAbsPath, destAbs, overwrite: false);
        return destAbs;
    }

    private static string ImportDirectoryToDirectory(string sourceDirAbs, string destDirAbs)
    {
        Directory.CreateDirectory(destDirAbs);
        var name = new DirectoryInfo(sourceDirAbs).Name;
        if (string.IsNullOrWhiteSpace(name))
            name = "ImportedFolder";
        var destAbs = GetUniquePath(Path.Combine(destDirAbs, name));
        CopyDirectory(sourceDirAbs, destAbs);
        return destAbs;
    }

    private static string GetUniquePath(string desiredAbsPath)
    {
        if (!File.Exists(desiredAbsPath) && !Directory.Exists(desiredAbsPath))
            return desiredAbsPath;

        var dir = Path.GetDirectoryName(desiredAbsPath) ?? "";
        var name = Path.GetFileNameWithoutExtension(desiredAbsPath);
        var ext = Path.GetExtension(desiredAbsPath);
        if (name.Length == 0)
            name = "Imported";

        for (var i = 2; i < 10000; i++)
        {
            var candidate = Path.Combine(dir, $"{name}_{i}{ext}");
            if (!File.Exists(candidate) && !Directory.Exists(candidate))
                return candidate;
        }

        return Path.Combine(dir, $"{name}_{Guid.NewGuid():N}{ext}");
    }

    private static void CopyDirectory(string sourceDir, string destDir)
    {
        Directory.CreateDirectory(destDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var name = Path.GetFileName(file);
            var dest = Path.Combine(destDir, name);
            File.Copy(file, dest, overwrite: true);
        }

        foreach (var sub in Directory.GetDirectories(sourceDir))
        {
            var name = new DirectoryInfo(sub).Name;
            var dest = Path.Combine(destDir, name);
            CopyDirectory(sub, dest);
        }
    }
}

internal sealed class AutoResPathEditorTypeDescriptionProvider : TypeDescriptionProvider
{
    private readonly TypeDescriptionProvider _baseProvider;

    public AutoResPathEditorTypeDescriptionProvider()
        : this(TypeDescriptor.GetProvider(typeof(object)))
    {
    }

    public AutoResPathEditorTypeDescriptionProvider(TypeDescriptionProvider baseProvider)
    {
        _baseProvider = baseProvider;
    }

    public override ICustomTypeDescriptor GetTypeDescriptor(Type objectType, object? instance)
    {
        return new AutoResPathEditorTypeDescriptor(_baseProvider.GetTypeDescriptor(objectType, instance));
    }
}

internal sealed class AutoResPathEditorTypeDescriptor : CustomTypeDescriptor
{
    public AutoResPathEditorTypeDescriptor(ICustomTypeDescriptor? parent)
        : base(parent)
    {
    }

    public override PropertyDescriptorCollection GetProperties(Attribute[]? attributes)
    {
        var props = base.GetProperties(attributes);
        if (props.Count == 0)
            return props;
        var list = new List<PropertyDescriptor>(props.Count);
        foreach (PropertyDescriptor p in props)
        {
            if (p.PropertyType == typeof(string) && !p.IsReadOnly)
                list.Add(new AutoResPathEditorPropertyDescriptor(p));
            else
                list.Add(p);
        }
        return new PropertyDescriptorCollection(list.ToArray(), true);
    }

    public override PropertyDescriptorCollection GetProperties()
    {
        return GetProperties(null);
    }
}

internal sealed class AutoResPathEditorPropertyDescriptor : PropertyDescriptor
{
    private readonly PropertyDescriptor _inner;

    public AutoResPathEditorPropertyDescriptor(PropertyDescriptor inner)
        : base(inner)
    {
        _inner = inner;
    }

    public override Type ComponentType => _inner.ComponentType;
    public override bool IsReadOnly => _inner.IsReadOnly;
    public override Type PropertyType => _inner.PropertyType;

    public override bool CanResetValue(object? component) => component != null && _inner.CanResetValue(component);
    public override object? GetValue(object? component) => component == null ? null : _inner.GetValue(component);
    public override void ResetValue(object? component)
    {
        if (component != null)
            _inner.ResetValue(component);
    }
    public override void SetValue(object? component, object? value)
    {
        if (component != null)
            _inner.SetValue(component, value);
    }
    public override bool ShouldSerializeValue(object? component) => component != null && _inner.ShouldSerializeValue(component);

    public override object? GetEditor(Type editorBaseType)
    {
        var existing = _inner.GetEditor(editorBaseType);
        if (existing != null)
            return existing;
        if (editorBaseType != typeof(UITypeEditor))
            return null;
        if (!ShouldAttachEditor(_inner))
            return null;
        return new GodotResPathEditor();
    }

    private static bool ShouldAttachEditor(PropertyDescriptor prop)
    {
        var name = prop.Name ?? "";
        if (name.EndsWith("Path", StringComparison.OrdinalIgnoreCase))
            return true;
        if (name.EndsWith("Dir", StringComparison.OrdinalIgnoreCase) || name.EndsWith("Directory", StringComparison.OrdinalIgnoreCase))
            return true;
        if (name.Contains("Texture", StringComparison.OrdinalIgnoreCase))
            return true;
        if (name.Contains("TileSet", StringComparison.OrdinalIgnoreCase))
            return true;
        if (name.Contains("Scene", StringComparison.OrdinalIgnoreCase))
            return true;
        return false;
    }
}
