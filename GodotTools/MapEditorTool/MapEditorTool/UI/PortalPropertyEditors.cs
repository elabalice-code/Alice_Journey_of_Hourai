using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Drawing.Design;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.Design;
using MapEditorTool.Executor.PortalEditing;
using MapEditorTool.Models;

namespace MapEditorTool.UI
{
    internal sealed class PortalEditorContext
    {
        public Func<string> ResolveGodotRoot { get; set; }
        public Func<MapProject> ResolveProject { get; set; }
        public Func<MapDefinition> ResolveSelectedMap { get; set; }
        public Action<string> ReportStatus { get; set; }
        public Action RefreshUi { get; set; }
        public PortalEditingExecutor PortalEditingExecutor { get; set; }

        public string GetGodotRoot()
        {
            return ResolveGodotRoot == null ? string.Empty : ResolveGodotRoot();
        }

        public MapProject GetProject()
        {
            return ResolveProject == null ? null : ResolveProject();
        }

        public MapDefinition GetSelectedMap()
        {
            return ResolveSelectedMap == null ? null : ResolveSelectedMap();
        }

        public void SetStatus(string status)
        {
            if (ReportStatus != null)
                ReportStatus(status ?? string.Empty);
        }

        public void RefreshEditorUi()
        {
            if (RefreshUi != null)
                RefreshUi();
        }
    }

    internal sealed class PortalCollectionEditor : CollectionEditor
    {
        private readonly PortalEditorContext _context;
        private MapDefinition _map;

        public PortalCollectionEditor(Type type, PortalEditorContext context)
            : base(type)
        {
            _context = context;
        }

        public override object EditValue(ITypeDescriptorContext context, IServiceProvider provider, object value)
        {
            _map = context == null ? null : context.Instance as MapDefinition;
            if (_map == null)
                _map = _context.GetSelectedMap();

            return base.EditValue(context, provider, value);
        }

        protected override CollectionForm CreateCollectionForm()
        {
            var form = base.CreateCollectionForm();
            var propertyGrid = FindFirstChild<PropertyGrid>(form);
            if (propertyGrid != null)
                propertyGrid.PropertyValueChanged += PortalPropertyValueChanged;

            form.FormClosed += delegate
            {
                if (propertyGrid != null)
                    propertyGrid.PropertyValueChanged -= PortalPropertyValueChanged;
            };

            return form;
        }

        protected override object CreateInstance(Type itemType)
        {
            try
            {
                var map = _map ?? _context.GetSelectedMap();
                if (map == null || _context.PortalEditingExecutor == null)
                    return base.CreateInstance(itemType);

                var result = _context.PortalEditingExecutor.CreatePortal(
                    _context.GetGodotRoot(),
                    _context.GetProject(),
                    map);

                _context.SetStatus("Portal created: " + result.Summary);
                return result.Portal;
            }
            catch (Exception ex)
            {
                _context.SetStatus("Create portal failed: " + ex.Message);
                MessageBox.Show(ex.Message, "Create portal failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return base.CreateInstance(itemType);
            }
        }

        private void PortalPropertyValueChanged(object sender, PropertyValueChangedEventArgs e)
        {
            var propertyGrid = sender as PropertyGrid;
            var portal = propertyGrid == null ? null : propertyGrid.SelectedObject as Portal;
            var map = _map ?? _context.GetSelectedMap();
            if (portal == null || map == null || _context.PortalEditingExecutor == null)
                return;

            var propertyName = e == null || e.ChangedItem == null || e.ChangedItem.PropertyDescriptor == null
                ? string.Empty
                : e.ChangedItem.PropertyDescriptor.Name;

            _context.SetStatus("Portal update started: " + propertyName);
            var owner = propertyGrid == null ? null : propertyGrid.FindForm();
            Task.Run(delegate
            {
                try
                {
                    var result = _context.PortalEditingExecutor.ApplyPortalPropertyChange(
                        _context.GetGodotRoot(),
                        map,
                        portal,
                        propertyName);

                    RunOnUiThread(owner, delegate
                    {
                        _context.SetStatus("Portal updated: " + result.Summary);
                        if (propertyGrid != null && !propertyGrid.IsDisposed)
                            propertyGrid.Refresh();
                        _context.RefreshEditorUi();
                    });
                }
                catch (Exception ex)
                {
                    RunOnUiThread(owner, delegate
                    {
                        _context.SetStatus("Portal update failed: " + ex.Message);
                        MessageBox.Show(ex.Message, "Portal update failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    });
                }
            });
        }

        private static T FindFirstChild<T>(Control root) where T : Control
        {
            if (root is T)
                return (T)root;

            foreach (Control child in root.Controls)
            {
                var found = FindFirstChild<T>(child);
                if (found != null)
                    return found;
            }

            return null;
        }

        private static void RunOnUiThread(Control owner, Action action)
        {
            if (action == null)
                return;

            if (owner == null || owner.IsDisposed)
                return;

            if (owner.InvokeRequired)
                owner.BeginInvoke(action);
            else
                action();
        }
    }

    internal sealed class PortalTargetMapEditor : UITypeEditor
    {
        private readonly PortalEditorContext _context;

        public PortalTargetMapEditor(PortalEditorContext context)
        {
            _context = context;
        }

        public override UITypeEditorEditStyle GetEditStyle(ITypeDescriptorContext context)
        {
            return UITypeEditorEditStyle.DropDown;
        }

        public override object EditValue(ITypeDescriptorContext context, IServiceProvider provider, object value)
        {
            var service = provider == null ? null : provider.GetService(typeof(IWindowsFormsEditorService)) as IWindowsFormsEditorService;
            if (service == null || _context.PortalEditingExecutor == null)
                return value;

            var choices = _context.PortalEditingExecutor.BuildTargetMapChoices(_context.GetProject());
            return PortalChoicePicker.PickChoice(service, choices, value as string) ?? value;
        }
    }

    internal sealed class PortalTargetAreaEditor : UITypeEditor
    {
        private readonly PortalEditorContext _context;

        public PortalTargetAreaEditor(PortalEditorContext context)
        {
            _context = context;
        }

        public override UITypeEditorEditStyle GetEditStyle(ITypeDescriptorContext context)
        {
            return UITypeEditorEditStyle.DropDown;
        }

        public override object EditValue(ITypeDescriptorContext context, IServiceProvider provider, object value)
        {
            var service = provider == null ? null : provider.GetService(typeof(IWindowsFormsEditorService)) as IWindowsFormsEditorService;
            if (service == null || _context.PortalEditingExecutor == null)
                return value;

            var choices = _context.PortalEditingExecutor.BuildTargetAreaChoices(_context.GetGodotRoot());
            return PortalChoicePicker.PickChoice(service, choices, value as string) ?? value;
        }
    }

    internal sealed class PortalTargetMapConverter : StringConverter
    {
        private readonly PortalEditorContext _context;

        public PortalTargetMapConverter(PortalEditorContext context)
        {
            _context = context;
        }

        public override bool GetStandardValuesSupported(ITypeDescriptorContext context)
        {
            return true;
        }

        public override bool GetStandardValuesExclusive(ITypeDescriptorContext context)
        {
            return false;
        }

        public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext context)
        {
            var choices = _context.PortalEditingExecutor == null
                ? new List<PortalChoice>()
                : _context.PortalEditingExecutor.BuildTargetMapChoices(_context.GetProject());
            return new StandardValuesCollection(choices.Select(choice => choice.Value).ToList());
        }

        public override object ConvertTo(ITypeDescriptorContext context, System.Globalization.CultureInfo culture, object value, Type destinationType)
        {
            if (destinationType == typeof(string) && _context.PortalEditingExecutor != null)
                return _context.PortalEditingExecutor.FormatMapTargetLabel(_context.GetProject(), value as string);

            return base.ConvertTo(context, culture, value, destinationType);
        }
    }

    internal sealed class PortalTargetAreaConverter : StringConverter
    {
        private readonly PortalEditorContext _context;

        public PortalTargetAreaConverter(PortalEditorContext context)
        {
            _context = context;
        }

        public override bool GetStandardValuesSupported(ITypeDescriptorContext context)
        {
            return true;
        }

        public override bool GetStandardValuesExclusive(ITypeDescriptorContext context)
        {
            return false;
        }

        public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext context)
        {
            var choices = _context.PortalEditingExecutor == null
                ? new List<PortalChoice>()
                : _context.PortalEditingExecutor.BuildTargetAreaChoices(_context.GetGodotRoot());
            return new StandardValuesCollection(choices.Select(choice => choice.Value).ToList());
        }
    }

    internal sealed class PortalEditorTypeDescriptionProvider : TypeDescriptionProvider
    {
        private readonly TypeDescriptionProvider _baseProvider;
        private readonly PortalEditorContext _context;

        public PortalEditorTypeDescriptionProvider(Type type, PortalEditorContext context)
            : this(TypeDescriptor.GetProvider(type), context)
        {
        }

        private PortalEditorTypeDescriptionProvider(TypeDescriptionProvider baseProvider, PortalEditorContext context)
        {
            _baseProvider = baseProvider;
            _context = context;
        }

        public override ICustomTypeDescriptor GetTypeDescriptor(Type objectType, object instance)
        {
            return new PortalEditorTypeDescriptor(_baseProvider.GetTypeDescriptor(objectType, instance), objectType, _context);
        }
    }

    internal sealed class PortalEditorTypeDescriptor : CustomTypeDescriptor
    {
        private readonly Type _objectType;
        private readonly PortalEditorContext _context;

        public PortalEditorTypeDescriptor(ICustomTypeDescriptor parent, Type objectType, PortalEditorContext context)
            : base(parent)
        {
            _objectType = objectType;
            _context = context;
        }

        public override PropertyDescriptorCollection GetProperties(Attribute[] attributes)
        {
            var properties = base.GetProperties(attributes);
            var list = new List<PropertyDescriptor>(properties.Count);
            foreach (PropertyDescriptor property in properties)
            {
                if (_objectType == typeof(MapDefinition) && property.Name == "Portals")
                    list.Add(new PortalCollectionPropertyDescriptor(property, _context));
                else if (_objectType == typeof(Portal) && property.Name == "TargetMapId")
                    list.Add(new PortalTargetPropertyDescriptor(property, _context, true));
                else if (_objectType == typeof(Portal) && property.Name == "TargetPortalId")
                    list.Add(new PortalTargetPropertyDescriptor(property, _context, false));
                else
                    list.Add(property);
            }

            return new PropertyDescriptorCollection(list.ToArray(), true);
        }

        public override PropertyDescriptorCollection GetProperties()
        {
            return GetProperties(null);
        }
    }

    internal sealed class PortalCollectionPropertyDescriptor : DelegatingPropertyDescriptor
    {
        private readonly PortalEditorContext _context;

        public PortalCollectionPropertyDescriptor(PropertyDescriptor inner, PortalEditorContext context)
            : base(inner)
        {
            _context = context;
        }

        public override object GetEditor(Type editorBaseType)
        {
            if (editorBaseType == typeof(UITypeEditor))
                return new PortalCollectionEditor(PropertyType, _context);

            return base.GetEditor(editorBaseType);
        }
    }

    internal sealed class PortalTargetPropertyDescriptor : DelegatingPropertyDescriptor
    {
        private readonly PortalEditorContext _context;
        private readonly bool _isMapTarget;

        public PortalTargetPropertyDescriptor(PropertyDescriptor inner, PortalEditorContext context, bool isMapTarget)
            : base(inner)
        {
            _context = context;
            _isMapTarget = isMapTarget;
        }

        public override TypeConverter Converter
        {
            get
            {
                return _isMapTarget
                    ? (TypeConverter)new PortalTargetMapConverter(_context)
                    : new PortalTargetAreaConverter(_context);
            }
        }

        public override object GetEditor(Type editorBaseType)
        {
            if (editorBaseType == typeof(UITypeEditor))
                return _isMapTarget
                    ? (object)new PortalTargetMapEditor(_context)
                    : new PortalTargetAreaEditor(_context);

            return base.GetEditor(editorBaseType);
        }
    }

    internal abstract class DelegatingPropertyDescriptor : PropertyDescriptor
    {
        private readonly PropertyDescriptor _inner;

        protected DelegatingPropertyDescriptor(PropertyDescriptor inner)
            : base(inner)
        {
            _inner = inner;
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
            return _inner.GetEditor(editorBaseType);
        }
    }

    internal static class PortalChoicePicker
    {
        public static string PickChoice(IWindowsFormsEditorService service, IList<PortalChoice> choices, string currentValue)
        {
            var listBox = new ListBox
            {
                BorderStyle = BorderStyle.None,
                IntegralHeight = true,
                DisplayMember = "Label",
                ValueMember = "Value"
            };

            foreach (var choice in choices ?? new List<PortalChoice>())
                listBox.Items.Add(choice);

            currentValue = currentValue ?? string.Empty;
            for (var index = 0; index < listBox.Items.Count; index++)
            {
                var choice = listBox.Items[index] as PortalChoice;
                if (choice != null && string.Equals(choice.Value, currentValue, StringComparison.Ordinal))
                {
                    listBox.SelectedIndex = index;
                    break;
                }
            }

            listBox.Click += delegate { service.CloseDropDown(); };
            service.DropDownControl(listBox);

            var selected = listBox.SelectedItem as PortalChoice;
            return selected == null ? null : selected.Value;
        }
    }
}
