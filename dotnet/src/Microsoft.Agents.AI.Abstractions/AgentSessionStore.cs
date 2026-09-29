// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Shared.DiagnosticIds;
using Microsoft.Shared.Diagnostics;

namespace Microsoft.Agents.AI;

/// <summary>
/// Defines the contract for storing and retrieving agent conversation sessions.
/// </summary>
/// <remarks>
/// Implementations enable persistent storage of conversation sessions, allowing conversations to be
/// resumed across HTTP requests, application restarts, or different service instances in hosted scenarios.
/// </remarks>
[Experimental(DiagnosticIds.Experiments.AgentsAIExperiments)]
public abstract class AgentSessionStore
{
#pragma warning disable RS0026 // The sessionId overloads are convenience forwarders for callers that do not need partitions.
    /// <summary>
    /// Saves an agent session to persistent storage.
    /// </summary>
    /// <param name="agent">The agent that owns this session.</param>
    /// <param name="key">The key that identifies and partitions the session.</param>
    /// <param name="session">The session to save.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous save operation.</returns>
    public abstract ValueTask SaveSessionAsync(
        AIAgent agent,
        AgentSessionStoreKey key,
        AgentSession session,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves an agent session to persistent storage using a key with no partitions.
    /// </summary>
    /// <param name="agent">The agent that owns this session.</param>
    /// <param name="sessionId">The logical session identifier.</param>
    /// <param name="session">The session to save.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous save operation.</returns>
    /// <remarks>
    /// This method creates an <see cref="AgentSessionStoreKey"/> from <paramref name="sessionId"/> and calls
    /// <see cref="SaveSessionAsync(AIAgent, AgentSessionStoreKey, AgentSession, CancellationToken)"/>.
    /// </remarks>
    public ValueTask SaveSessionAsync(
        AIAgent agent,
        string sessionId,
        AgentSession session,
        CancellationToken cancellationToken = default)
        => this.SaveSessionAsync(agent, new AgentSessionStoreKey(sessionId), session, cancellationToken);

    /// <summary>
    /// Retrieves an agent session from persistent storage, or <see langword="null"/> when no session is stored
    /// for the given identifiers.
    /// </summary>
    /// <param name="agent">The agent that owns this session.</param>
    /// <param name="key">The key that identifies and partitions the session.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests.</param>
    /// <returns>
    /// A task whose result contains the restored session, or <see langword="null"/> when nothing is stored for
    /// the given identifiers. This method never creates a session.
    /// </returns>
    /// <remarks>
    /// Each successful lookup must return an independent <see cref="AgentSession"/> instance. Callers may
    /// mutate the returned session and may run concurrent branches from the same identifiers without those
    /// branches observing one another's changes or modifying the stored state. Implementations that cache a
    /// live session must return an independent copy rather than the shared instance.
    /// </remarks>
    public abstract ValueTask<AgentSession?> GetSessionAsync(
        AIAgent agent,
        AgentSessionStoreKey key,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves an agent session from persistent storage using a key with no partitions, or
    /// <see langword="null"/> when no session is stored for the given identifier.
    /// </summary>
    /// <param name="agent">The agent that owns this session.</param>
    /// <param name="sessionId">The logical session identifier.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests.</param>
    /// <returns>
    /// A task whose result contains the restored session, or <see langword="null"/> when nothing is stored for
    /// the given identifier.
    /// </returns>
    /// <remarks>
    /// This method creates an <see cref="AgentSessionStoreKey"/> from <paramref name="sessionId"/> and calls
    /// <see cref="GetSessionAsync(AIAgent, AgentSessionStoreKey, CancellationToken)"/>.
    /// </remarks>
    public ValueTask<AgentSession?> GetSessionAsync(
        AIAgent agent,
        string sessionId,
        CancellationToken cancellationToken = default)
        => this.GetSessionAsync(agent, new AgentSessionStoreKey(sessionId), cancellationToken);

    /// <summary>
    /// Retrieves the stored session for the given identifiers, or creates a new one when none is stored.
    /// </summary>
    /// <param name="agent">The agent that owns this session.</param>
    /// <param name="key">The key that identifies and partitions the session.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests.</param>
    /// <returns>A task whose result is always a usable session.</returns>
    /// <remarks>
    /// The default implementation calls <see cref="GetSessionAsync(AIAgent, AgentSessionStoreKey, CancellationToken)"/>
    /// and creates a session through <see cref="AIAgent.CreateSessionAsync"/> only when the lookup returns
    /// <see langword="null"/>. Implementations that override
    /// <see cref="GetSessionAsync(AIAgent, AgentSessionStoreKey, CancellationToken)"/> receive this behavior automatically.
    /// </remarks>
    public virtual async ValueTask<AgentSession> GetOrCreateSessionAsync(
        AIAgent agent,
        AgentSessionStoreKey key,
        CancellationToken cancellationToken = default)
    {
        _ = Throw.IfNull(agent);

        return await this.GetSessionAsync(agent, key, cancellationToken).ConfigureAwait(false)
            ?? await agent.CreateSessionAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Retrieves the stored session for the given identifier using a key with no partitions, or creates a new
    /// one when none is stored.
    /// </summary>
    /// <param name="agent">The agent that owns this session.</param>
    /// <param name="sessionId">The logical session identifier.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests.</param>
    /// <returns>A task whose result is always a usable session.</returns>
    /// <remarks>
    /// This method creates an <see cref="AgentSessionStoreKey"/> from <paramref name="sessionId"/> and calls
    /// <see cref="GetOrCreateSessionAsync(AIAgent, AgentSessionStoreKey, CancellationToken)"/>.
    /// </remarks>
    public ValueTask<AgentSession> GetOrCreateSessionAsync(
        AIAgent agent,
        string sessionId,
        CancellationToken cancellationToken = default)
        => this.GetOrCreateSessionAsync(agent, new AgentSessionStoreKey(sessionId), cancellationToken);
#pragma warning restore RS0026

    /// <summary>Asks the store for an object of the specified type.</summary>
    /// <param name="serviceType">The type of object being requested.</param>
    /// <param name="serviceKey">An optional key that identifies the requested service.</param>
    /// <returns>The requested object, or <see langword="null"/> if it is not available.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="serviceType"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// Stores can expose themselves, underlying stores, or additional services through this method.
    /// The default implementation returns this instance when no key is supplied and it is assignable to
    /// <paramref name="serviceType"/>. Otherwise, it returns <see langword="null"/>.
    /// </remarks>
#pragma warning disable RS0026 // Preserves the existing Hosting service discovery contract and matches AIAgent.
    public virtual object? GetService(Type serviceType, object? serviceKey = null)
    {
        _ = Throw.IfNull(serviceType);

        return serviceKey is null && serviceType.IsInstanceOfType(this)
            ? this
            : null;
    }
#pragma warning restore RS0026

    /// <summary>Asks the store for an object of type <typeparamref name="TService"/>.</summary>
    /// <typeparam name="TService">The type of object being requested.</typeparam>
    /// <param name="serviceKey">An optional key that identifies the requested service.</param>
    /// <returns>The requested object, or the default value of <typeparamref name="TService"/> if it is not available.</returns>
    /// <remarks>
    /// This method calls <see cref="GetService(Type, object?)"/> so that services exposed by derived stores
    /// are available through both overloads.
    /// </remarks>
#pragma warning disable RS0026 // Preserves the existing Hosting service discovery contract and matches AIAgent.
    public TService? GetService<TService>(object? serviceKey = null)
        => this.GetService(typeof(TService), serviceKey) is TService service ? service : default;
#pragma warning restore RS0026
}
