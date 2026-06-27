using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing.Design;
using System.IO;
using System.Windows.Forms;
using MapEditorTool.Executor.ResourcePath;
using MapEditorTool.Models;

namespace MapEditorTool.UI
{
    public sealed class GodotResPathEditor : UITypeEditor
    {
        private readonly Func<string> _resolveGodotRoot;
        private readonly Func<MapDefinition> _resolveSelectedMap;
        private readonly ResourcePathExecutor _resourcePathExecutor;

        public GodotResPathEditor(
            Func<string> resolveGodotRoot,
            Func<MapDefinition> resolveSelectedMap,
            ResourcePathExecutor resourcePathExecutor)
        {
            _resolveGodotRoot = resolveGodotRoot;
            _resolveSelectedMap = resolveSelectedMap;
            _resourcePathExecutor = resourcePathExecutor;
        }

        public override UITypeEditorEditStyle GetEditStyle(ITypeDescriptorContext context)
        {
            return UITypeEditorEditStyle.Modal;
        }

        public override object EditValue(ITypeDescriptorContext context, IServiceProvider provider, object value)
        {
            var property = context == null ? null : context.PropertyDescriptor;
            if (property == null || property.IsReadOnly)
                return value;

            var godotRoot = ResolveGodotRoot();
            if (string.IsNullOrWhiteSpace(godotRoot))
                return value;

            var propertyName = property.Name ?? string.Empty;
            var currentValue = value as string ?? string.Empty;
            var currentAbsolutePath = _resourcePathExecutor.TryResolveToExistingPath(godotRoot, currentValue);
            var initialDirectory = _resourcePathExecutor.ResolveInitialDirectory(godotRoot, currentAbsolutePath);
            if (string.IsNullOrWhiteSpace(currentAbsolutePath) && string.IsNullOrWhiteSpace(currentValue))
            {
                initialDirectory = _resourcePathExecutor.EnsurePreferredProjectResourceDirectory(
                    godotRoot,
                    initialDirectory,
                    ResolveSelectedMap());
            }

            var chosenPath = ChoosePath(propertyName, currentValue, currentAbsolutePath, initialDirectory);
            if (string.IsNullOrWhiteSpace(chosenPath))
                return value;

            var result = _resourcePathExecutor.ConvertToProjectResourcePath(
                godotRoot,
                chosenPath,
                initialDirectory,
                ResolveSelectedMap());

            return string.IsNullOrWhiteSpace(result.ResultPath) ? value : result.ResultPath;
        }

        private string ChoosePath(string propertyName, string currentValue, string currentAbsolutePath, string initialDirectory)
        {
            if (IsDirectoryProperty(propertyName))
            {
                using (var dialog = new FolderBrowserDialog())
                {
                    dialog.Description = "Select a folder";
                    dialog.ShowNewFolderButton = true;
                    if (!string.IsNullOrWhiteSpace(initialDirectory) && Directory.Exists(initialDirectory))
                        dialog.SelectedPath = initialDirectory;

                    return dialog.ShowDialog() == DialogResult.OK ? dialog.SelectedPath : string.Empty;
                }
            }

            using (var dialog = new OpenFileDialog())
            {
                dialog.Title = "Select a file";
                dialog.Filter = BuildFilter(propertyName, currentValue);
                if (!string.IsNullOrWhiteSpace(initialDirectory) && Directory.Exists(initialDirectory))
                    dialog.InitialDirectory = initialDirectory;
                if (!string.IsNullOrWhiteSpace(currentAbsolutePath) && File.Exists(currentAbsolutePath))
                    dialog.FileName = currentAbsolutePath;

                return dialog.ShowDialog() == DialogResult.OK ? dialog.FileName : string.Empty;
            }
        }

        private string ResolveGodotRoot()
        {
            try
            {
                return _resolveGodotRoot == null ? string.Empty : _resolveGodotRoot();
            }
            catch
            {
                return string.Empty;
            }
        }

        private MapDefinition ResolveSelectedMap()
        {
            return _resolveSelectedMap == null ? null : _resolveSelectedMap();
        }

        private static bool IsDirectoryProperty(string propertyName)
        {
            propertyName = propertyName ?? string.Empty;
            return propertyName.EndsWith("Dir", StringComparison.OrdinalIgnoreCase) ||
                propertyName.EndsWith("Directory", StringComparison.OrdinalIgnoreCase);
        }

        private static string BuildFilter(string propertyName, string currentValue)
        {
            propertyName = propertyName ?? string.Empty;
            currentValue = currentValue ?? string.Empty;
            var extension = Path.GetExtension(currentValue.Trim());

            if (string.Equals(extension, ".mp4", StringComparison.OrdinalIgnoreCase) ||
                propertyName.IndexOf("Video", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Video (*.mp4)|*.mp4|All files (*.*)|*.*";

            if (IsImageExtension(extension) ||
                propertyName.IndexOf("Texture", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Images (*.png;*.jpg;*.jpeg;*.webp)|*.png;*.jpg;*.jpeg;*.webp|All files (*.*)|*.*";

            if (string.Equals(extension, ".tscn", StringComparison.OrdinalIgnoreCase) ||
                propertyName.IndexOf("Scene", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Godot scene (*.tscn)|*.tscn|All files (*.*)|*.*";

            if (string.Equals(extension, ".tres", StringComparison.OrdinalIgnoreCase) ||
                propertyName.IndexOf("TileSet", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Godot resource (*.tres)|*.tres|All files (*.*)|*.*";

            if (string.Equals(extension, ".json", StringComparison.OrdinalIgnoreCase) ||
                propertyName.IndexOf("Collision", StringComparison.OrdinalIgnoreCase) >= 0)
                return "JSON (*.json)|*.json|All files (*.*)|*.*";

            return "All files (*.*)|*.*";
        }

        private static bool IsImageExtension(string extension)
        {
            return string.Equals(extension, ".png", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(extension, ".jpg", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(extension, ".jpeg", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(extension, ".webp", StringComparison.OrdinalIgnoreCase);
        }
    }

    internal sealed class AutoResPathEditorTypeDescriptionProvider : TypeDescriptionProvider
    {
        private readonly TypeDescriptionProvider _baseProvider;
        private readonly Func<UITypeEditor> _createEditor;

        public AutoResPathEditorTypeDescriptionProvider(Type type, Func<UITypeEditor> createEditor)
            : this(TypeDescriptor.GetProvider(type), createEditor)
        {
        }

        private AutoResPathEditorTypeDescriptionProvider(TypeDescriptionProvider baseProvider, Func<UITypeEditor> createEditor)
        {
            _baseProvider = baseProvider;
            _createEditor = createEditor;
        }

        public override ICustomTypeDescriptor GetTypeDescriptor(Type objectType, object instance)
        {
            return new AutoResPathEditorTypeDescriptor(_baseProvider.GetTypeDescriptor(objectType, instance), _createEditor);
        }
    }

    internal sealed class AutoResPathEditorTypeDescriptor : CustomTypeDescriptor
    {
        private readonly Func<UITypeEditor> _createEditor;

        public AutoResPathEditorTypeDescriptor(ICustomTypeDescriptor parent, Func<UITypeEditor> createEditor)
            : base(parent)
        {
            _createEditor = createEditor;
        }

        public override PropertyDescriptorCollection GetProperties(Attribute[] attributes)
        {
            var properties = base.GetProperties(attributes);
            if (properties.Count == 0)
                return properties;

            var list = new List<PropertyDescriptor>(properties.Count);
            foreach (PropertyDescriptor property in properties)
            {
                if (property.PropertyType == typeof(string) && !property.IsReadOnly && ShouldAttachEditor(property))
                    list.Add(new AutoResPathEditorPropertyDescriptor(property, _createEditor));
                else
                    list.Add(property);
            }

            return new PropertyDescriptorCollection(list.ToArray(), true);
        }

        public override PropertyDescriptorCollection GetProperties()
        {
            return GetProperties(null);
        }

        private static bool ShouldAttachEditor(PropertyDescriptor property)
        {
            var name = property.Name ?? string.Empty;
            if (name.EndsWith("Path", StringComparison.OrdinalIgnoreCase))
                return true;
            if (name.EndsWith("Dir", StringComparison.OrdinalIgnoreCase) ||
                name.EndsWith("Directory", StringComparison.OrdinalIgnoreCase))
                return true;
            if (name.IndexOf("Texture", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            if (name.IndexOf("TileSet", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            if (name.IndexOf("Scene", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            if (name.IndexOf("Video", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            if (name.IndexOf("Collision", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            return false;
        }
    }

    internal sealed class AutoResPathEditorPropertyDescriptor : PropertyDescriptor
    {
        private readonly PropertyDescriptor _inner;
        private readonly Func<UITypeEditor> _createEditor;

        public AutoResPathEditorPropertyDescriptor(PropertyDescriptor inner, Func<UITypeEditor> createEditor)
            : base(inner)
        {
            _inner = inner;
            _createEditor = createEditor;
        }

        public override Type ComponentType
        {
            get { return _inner.ComponentType; }
        }

        public override bool IsReadOnly
        {
            get { return _inner.IsReadOnly; }
        }

        public override Type PropertyType
        {
            get { return _inner.PropertyType; }
        }

        public override bool CanResetValue(object component)
        {
            return component != null && _inner.CanResetValue(component);
        }

        public override object GetValue(object component)
        {
            return component == null ? null : _inner.GetValue(component);
        }

        public override void ResetValue(object component)
        {
            if (component != null)
                _inner.ResetValue(component);
        }

        public override void SetValue(object component, object value)
        {
            if (component != null)
                _inner.SetValue(component, value);
        }

        public override bool ShouldSerializeValue(object component)
        {
            return component != null && _inner.ShouldSerializeValue(component);
        }

        public override object GetEditor(Type editorBaseType)
        {
            var existing = _inner.GetEditor(editorBaseType);
            if (existing != null)
                return existing;

            if (editorBaseType != typeof(UITypeEditor))
                return null;

            return _createEditor == null ? null : _createEditor();
        }
    }
}
