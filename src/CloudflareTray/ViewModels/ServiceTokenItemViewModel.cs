using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CloudflareTray.ViewModels;

/// <summary>
/// A service token in the "Service tokens" tab. Applying and deleting save immediately.
/// The secret is never displayed; only a new one can be entered.
/// </summary>
public partial class ServiceTokenItemViewModel : ViewModelBase
{
    /// <summary>Saves the client ID and – if not null – a new secret. Returns an error message or null.</summary>
    public delegate string? SaveHandler(ServiceTokenItemViewModel token, string clientId, string? newSecret);

    /// <summary>Deletes the token. Returns an error message or null.</summary>
    public delegate string? DeleteHandler(ServiceTokenItemViewModel token);

    private readonly SaveHandler _save;
    private readonly DeleteHandler _delete;

    public ServiceTokenItemViewModel(
        string profile,
        string name,
        string clientId,
        IReadOnlyList<string> usedBy,
        SaveHandler save,
        DeleteHandler delete)
    {
        Profile = profile;
        Name = name;
        _clientId = clientId;
        _usedBy = usedBy;
        _save = save;
        _delete = delete;
    }

    public string Profile { get; }
    public string Name { get; }

    [ObservableProperty] private string _clientId;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsUnused), nameof(UsageText), nameof(DeleteTip))]
    [NotifyCanExecuteChangedFor(nameof(RequestDeleteCommand))]
    private IReadOnlyList<string> _usedBy;

    public bool IsUnused => UsedBy.Count == 0;

    public string UsageText => IsUnused
        ? "Not used by any tunnel"
        : $"Used by: {string.Join(", ", UsedBy)}";

    public string DeleteTip => IsUnused
        ? "Remove the service token from the profile"
        : "Cannot be deleted while it is in use";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    private bool _isEditing;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    private bool _isConfirmingDelete;

    public bool IsIdle => !IsEditing && !IsConfirmingDelete;

    [ObservableProperty] private string _editClientId = "";
    [ObservableProperty] private string _editSecret = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _error;

    public bool HasError => Error != null;

    /// <summary>Applies the freshly loaded state without discarding an edit in progress.</summary>
    public void Update(string clientId, IReadOnlyList<string> usedBy)
    {
        ClientId = clientId;
        UsedBy = usedBy;
        if (!IsUnused) IsConfirmingDelete = false;
    }

    [RelayCommand]
    private void Edit()
    {
        EditClientId = ClientId;
        EditSecret = "";
        Error = null;
        IsEditing = true;
    }

    [RelayCommand]
    private void CancelEdit()
    {
        EditSecret = "";
        Error = null;
        IsEditing = false;
    }

    [RelayCommand]
    private void ApplyEdit()
    {
        var clientId = EditClientId.Trim();
        if (clientId.Length == 0)
        {
            Error = "Please enter a client ID.";
            return;
        }

        // An empty secret field leaves the existing secret unchanged
        var newSecret = EditSecret.Length > 0 ? EditSecret : null;
        if (clientId != ClientId || newSecret != null)
        {
            Error = _save(this, clientId, newSecret);
            if (Error != null) return;
        }

        EditSecret = "";
        Error = null;
        IsEditing = false;
    }

    [RelayCommand(CanExecute = nameof(IsUnused))]
    private void RequestDelete()
    {
        Error = null;
        IsConfirmingDelete = true;
    }

    [RelayCommand]
    private void CancelDelete() => IsConfirmingDelete = false;

    [RelayCommand]
    private void ConfirmDelete()
    {
        IsConfirmingDelete = false;
        Error = _delete(this);
    }
}
