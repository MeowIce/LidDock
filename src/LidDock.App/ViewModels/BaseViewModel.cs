using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace LidDock.App.ViewModels;

public abstract class baseViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void onPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool setField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(field, value))
        {
            return false;
        }
        field = value;
        onPropertyChanged(propertyName);
        return true;
    }
}
