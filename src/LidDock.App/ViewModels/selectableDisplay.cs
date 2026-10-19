using System;

namespace LidDock.App.ViewModels;

public class selectableDisplay : baseViewModel
{
    private readonly Action<string, bool> onToggled;
    private bool isCheckedVal;

    public string devicePath { get; }
    public string displayName { get; }
    public string subtitle { get; }

    public selectableDisplay(string devicePath, string displayName, string subtitle, bool isChecked, Action<string, bool> onToggled)
    {
        this.devicePath = devicePath;
        this.displayName = displayName;
        this.subtitle = subtitle;
        isCheckedVal = isChecked;
        this.onToggled = onToggled;
    }

    public bool isChecked
    {
        get => isCheckedVal;
        set
        {
            if (setField(ref isCheckedVal, value))
            {
                onToggled(devicePath, value);
            }
        }
    }
}
