using Avalonia.Controls;
using Avalonia.Interactivity;
using CloudflareTray.ViewModels;

namespace CloudflareTray.Views;

public partial class NewTunnelWindow : Window
{
    public NewTunnelWindow()
    {
        InitializeComponent();
    }

    private void OnCreateClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is NewTunnelViewModel vm && vm.Validate())
            Close(true);
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(false);
}
