namespace Potato.Domain.Vdf;

/// <summary>
/// Base class for Valve KeyValue (VDF) document nodes.
/// </summary>
public abstract class VdfNode
{
    public abstract bool IsObject { get; }
    public abstract bool IsValue { get; }

    public VdfObject AsObject() =>
        this as VdfObject ?? throw new InvalidCastException($"Cannot cast {GetType().Name} to {nameof(VdfObject)}.");

    public VdfValue AsValue() =>
        this as VdfValue ?? throw new InvalidCastException($"Cannot cast {GetType().Name} to {nameof(VdfValue)}.");
}
