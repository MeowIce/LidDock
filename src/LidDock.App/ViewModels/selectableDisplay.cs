using System;

namespace LidDock.App.ViewModels;

public class selectableDisplay : baseViewModel
{
    private readonly Action<string, bool> onToggled;
    private bool isCheckedVal;

    public string devicePath { get; }
    public string label { get; }

    public selectableDisplay(string devicePath, string label, bool isChecked, Action<string, bool> onToggled)
    {
        this.devicePath = devicePath;
        this.label = label;
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
