using System.Collections.Concurrent;

namespace DataBro.Platform.Messaging;

/// <summary>
/// Maps integration-event types to the stable names they are stored under, and back.
///
/// <para>
/// The stored name is <b>hand-written, not derived from the CLR type</b>. A row in the queue outlives
/// the code that wrote it, so an assembly-qualified name baked into it makes renaming a class or
/// moving a namespace silently undeliver every message already queued — a refactor that breaks
/// production days later, in a way no compiler catches.
/// </para>
/// <para>
/// It also decides what may cross a module boundary at all: a type absent from here is not
/// publishable, so adding one is a deliberate act rather than a side effect of implementing an
/// interface.
/// </para>
/// </summary>
public sealed class OutboxRegistry
{
    private readonly ConcurrentDictionary<Type, string> _names = new();
    private readonly ConcurrentDictionary<string, Type> _types = new();

    public void Register<TEvent>(string contractName) where TEvent : class, IIntegrationEvent
        => Register(typeof(TEvent), contractName);

    public void Register(Type eventType, string contractName)
    {
        if (string.IsNullOrWhiteSpace(contractName))
            throw new ArgumentException("A contract name is required.", nameof(contractName));

        // Two types under one name would make deserialisation a coin toss, and one type under two
        // names would split a queue in half. Both are configuration mistakes worth failing on at
        // startup rather than discovering in a dead-letter row.
        if (_types.TryGetValue(contractName, out var existing) && existing != eventType)
            throw new InvalidOperationException(
                $"Contract name '{contractName}' is already registered to {existing.Name}.");

        if (_names.TryGetValue(eventType, out var existingName) && existingName != contractName)
            throw new InvalidOperationException(
                $"{eventType.Name} is already registered as '{existingName}'.");

        _names[eventType] = contractName;
        _types[contractName] = eventType;
    }

    /// <summary>The stored name for a type, or null when it is not publishable.</summary>
    public string? NameFor(Type eventType) => _names.GetValueOrDefault(eventType);

    /// <summary>
    /// The type for a stored name, or null. Null is an ordinary state, not a bug: a message queued
    /// by a newer deployment can be read by an older one mid-rollout, and the right response is to
    /// leave it for a process that understands it.
    /// </summary>
    public Type? TypeFor(string contractName) => _types.GetValueOrDefault(contractName);
}
