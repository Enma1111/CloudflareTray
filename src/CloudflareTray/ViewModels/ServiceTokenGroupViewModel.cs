using System.Collections.Generic;

namespace CloudflareTray.ViewModels;

public class ServiceTokenGroupViewModel(string name, IReadOnlyList<ServiceTokenItemViewModel> tokens) : ViewModelBase
{
    public string Name { get; } = name;
    public IReadOnlyList<ServiceTokenItemViewModel> Tokens { get; } = tokens;
    public bool IsEmpty => Tokens.Count == 0;
}
