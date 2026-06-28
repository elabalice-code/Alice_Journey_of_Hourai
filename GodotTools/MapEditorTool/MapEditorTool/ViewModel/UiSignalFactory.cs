using System;

namespace MapEditorTool.ViewModel
{
    public static class UiSignalFactory
    {
        public static UiSignal Click(string sourceDescription, string eventName)
        {
            return new UiSignal(UiSignalKind.Click, sourceDescription, eventName, DateTimeOffset.Now);
        }

        public static UiSignal Click(string sourceDescription, string eventName, string actionKey)
        {
            return new UiSignal(UiSignalKind.Click, sourceDescription, eventName, DateTimeOffset.Now, actionKey);
        }

        public static UiSignal CheckedChanged(string sourceDescription)
        {
            return new UiSignal(UiSignalKind.CheckedChanged, sourceDescription, "CheckedChanged", DateTimeOffset.Now);
        }

        public static UiSignal SelectionChanged(string sourceDescription, string eventName, int selectedIndex)
        {
            return new UiSignal(UiSignalKind.SelectionChanged, sourceDescription, eventName, DateTimeOffset.Now, string.Empty, true, selectedIndex);
        }

        public static UiSignal SelectionChanged(string sourceDescription, string eventName, string targetValue)
        {
            return new UiSignal(UiSignalKind.SelectionChanged, sourceDescription, eventName, DateTimeOffset.Now, string.Empty, false, 0, targetValue);
        }

        public static UiSignal ValueChanged(
            string sourceDescription,
            string eventName,
            string value,
            string value2,
            string value3,
            string value4,
            string value5)
        {
            return new UiSignal(UiSignalKind.ValueChanged, sourceDescription, eventName, DateTimeOffset.Now, string.Empty, false, 0, value, value2, value3, value4, value5);
        }
    }
}
