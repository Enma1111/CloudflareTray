using System.Collections.Generic;

namespace CloudflareTray.ViewModels;

public class ProfileGroupViewModel(string name, IReadOnlyList<TunnelItemViewModel> tunnels) : ViewModelBase
{
    public string Name { get; } = name;
    public IReadOnlyList<TunnelItemViewModel> Tunnels { get; } = tunnels;
    public bool IsEmpty => Tunnels.Count == 0;
}
