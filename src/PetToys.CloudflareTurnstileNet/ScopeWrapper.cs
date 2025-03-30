using System;

namespace PetToys.CloudflareTurnstileNet;

internal sealed class ScopeWrapper
{
    public Guid Uid { get; } = Guid.NewGuid();
}
