using System;

namespace MapEditorTool.ViewModel
{
    public static class UiSignalFactory
    {
        public static UiSignal Click(string sourceDescription, string eventName)
        {
            return new UiSignal(UiSignalKind.Click, sourceDescription, eventName, DateTimeOffset.Now);
        }

        public static UiSignal CheckedChanged(string sourceDescription)
        {
            return new UiSignal(UiSignalKind.CheckedChanged, sourceDescription, "CheckedChanged", DateTimeOffset.Now);
        }

    }
}
