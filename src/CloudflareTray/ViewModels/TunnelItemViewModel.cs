using System;
using Avalonia.Threading;
using CloudflareTray.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CloudflareTray.ViewModels;

public partial class TunnelItemViewModel : ViewModelBase, IDisposable
{
    private readonly Action<TunnelItemViewModel> _delete;
    private readonly Action _stateChanged;

    public TunnelItemViewModel(TunnelInstance instance, Action<TunnelItemViewModel> delete, Action stateChanged)
    {
        Instance = instance;
        _delete = delete;
        _stateChanged = stateChanged;
        Instance.StateChanged += OnInstanceStateChanged;
        Refresh();
    }

    public TunnelInstance Instance { get; }

    public string Title => $"{Instance.Config.Name} ({Instance.Config.Protocol})";
    public string Hostname => Instance.Config.Hostname;
    public string LocalAddress => Instance.Config.LocalAddress;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRunning), nameof(IsReconnecting), nameof(IsActive), nameof(StatusText))]
    private TunnelState _state;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _error;

    [ObservableProperty]
    private bool _isConfirmingDelete;

    public bool IsRunning => State == TunnelState.Running;
    public bool IsReconnecting => State == TunnelState.Reconnecting;
    public bool IsActive => State != TunnelState.Stopped;
    public bool HasError => Error != null;

    public string StatusText => State switch
    {
        TunnelState.Running => "Active",
        TunnelState.Reconnecting => "Reconnecting…",
        _ => "Stopped",
    };

    [RelayCommand]
    private void Start() => Instance.Start();

    [RelayCommand]
    private void Stop() => Instance.Stop();

    [RelayCommand]
    private void RequestDelete() => IsConfirmingDelete = true;

    [RelayCommand]
    private void CancelDelete() => IsConfirmingDelete = false;

    [RelayCommand]
    private void ConfirmDelete() => _delete(this);

    private void OnInstanceStateChanged(object? sender, EventArgs e) => Dispatcher.UIThread.Post(Refresh);

    private void Refresh()
    {
        State = Instance.State;
        Error = Instance.LastError;
        _stateChanged();
    }

    public void Dispose()
    {
        Instance.StateChanged -= OnInstanceStateChanged;
        GC.SuppressFinalize(this);
    }
}
