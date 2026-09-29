// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Moq.Protected;

namespace Microsoft.Agents.AI.Abstractions.UnitTests;

/// <summary>
/// Unit tests for <see cref="AgentSessionStore"/>.
/// </summary>
public sealed class AgentSessionStoreTests
{
    [Fact]
    public void GetService_CompatibleUnkeyedType_ReturnsStore()
    {
        // Arrange
        var store = new TestAgentSessionStore(session: null);

        // Act and assert
        Assert.Same(store, store.GetService(typeof(TestAgentSessionStore)));
        Assert.Same(store, store.GetService<AgentSessionStore>());
        Assert.Same(store, store.GetService<object>());
    }

    [Fact]
    public void GetService_UnsupportedRequest_ReturnsDefault()
    {
        // Arrange
        var store = new TestAgentSessionStore(session: null);

        // Act and assert
        Assert.Null(store.GetService(typeof(IDisposable)));
        Assert.Null(store.GetService<IDisposable>());
        Assert.Null(store.GetService<AgentSessionStore>("key"));
        Assert.Equal(0, store.GetService<int>());
    }

    [Fact]
    public void GetService_NullType_Throws()
    {
        // Arrange
        var store = new TestAgentSessionStore(session: null);

        // Act and assert
        Assert.Throws<ArgumentNullException>("serviceType", () => store.GetService(null!));
    }

    [Fact]
    public void GetService_GenericOverload_UsesVirtualMethod()
    {
        // Arrange
        var service = new object();
        var key = new object();
        var store = new Mock<AgentSessionStore>();
        store.Setup(s => s.GetService(typeof(object), key)).Returns(service);

        // Act
        var result = store.Object.GetService<object>(key);

        // Assert
        Assert.Same(service, result);
        store.Verify(s => s.GetService(typeof(object), key), Times.Once);
    }

    [Fact]
    public async Task GetOrCreateSessionAsync_StoredSession_ReturnsStoredSessionAsync()
    {
        // Arrange
        var storedSession = new TestAgentSession();
        var store = new TestAgentSessionStore(storedSession);
        var agent = new Mock<AIAgent>();
        var key = new AgentSessionStoreKey("conversation-1").WithPartition("user", "user-1");

        // Act
        AgentSession session = await store.GetOrCreateSessionAsync(agent.Object, key);

        // Assert
        Assert.Same(storedSession, session);
        Assert.Same(key, store.LastKey);
        agent.Protected().Verify(
            "CreateSessionCoreAsync",
            Times.Never(),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task GetOrCreateSessionAsync_MissingSession_CreatesSessionAsync()
    {
        // Arrange
        var createdSession = new TestAgentSession();
        var store = new TestAgentSessionStore(session: null);
        var agent = new Mock<AIAgent>();
        var key = new AgentSessionStoreKey("conversation-1");
        agent.Protected()
            .Setup<ValueTask<AgentSession>>("CreateSessionCoreAsync", ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(createdSession);

        // Act
        AgentSession session = await store.GetOrCreateSessionAsync(agent.Object, key);

        // Assert
        Assert.Same(createdSession, session);
        agent.Protected().Verify(
            "CreateSessionCoreAsync",
            Times.Once(),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task GetOrCreateSessionAsync_NullAgent_ThrowsAsync()
    {
        // Arrange
        var store = new TestAgentSessionStore(session: null);

        // Act and assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => store.GetOrCreateSessionAsync(null!, new AgentSessionStoreKey("conversation-1")).AsTask());
    }

    [Fact]
    public async Task GetSessionAsync_SessionId_ForwardsUnpartitionedKeyAsync()
    {
        // Arrange
        var storedSession = new TestAgentSession();
        var store = new TestAgentSessionStore(storedSession);
        var agent = new Mock<AIAgent>();
        using var cts = new CancellationTokenSource();

        // Act
        AgentSession? session = await store.GetSessionAsync(agent.Object, "conversation-1", cts.Token);

        // Assert
        Assert.Same(storedSession, session);
        Assert.Equal(new AgentSessionStoreKey("conversation-1"), store.LastKey);
        Assert.Null(store.LastKey!.Partitions);
        Assert.Equal(cts.Token, store.LastCancellationToken);
    }

    [Fact]
    public async Task SaveSessionAsync_SessionId_ForwardsUnpartitionedKeyAsync()
    {
        // Arrange
        var store = new TestAgentSessionStore(session: null);
        var agent = new Mock<AIAgent>();
        var session = new TestAgentSession();
        using var cts = new CancellationTokenSource();

        // Act
        await store.SaveSessionAsync(agent.Object, "conversation-1", session, cts.Token);

        // Assert
        Assert.Equal(new AgentSessionStoreKey("conversation-1"), store.LastKey);
        Assert.Null(store.LastKey!.Partitions);
        Assert.Same(session, store.LastSavedSession);
        Assert.Equal(cts.Token, store.LastCancellationToken);
    }

    [Fact]
    public async Task GetOrCreateSessionAsync_SessionId_ForwardsUnpartitionedKeyAsync()
    {
        // Arrange
        var storedSession = new TestAgentSession();
        var store = new TestAgentSessionStore(storedSession);
        var agent = new Mock<AIAgent>();

        // Act
        AgentSession session = await store.GetOrCreateSessionAsync(agent.Object, "conversation-1");

        // Assert
        Assert.Same(storedSession, session);
        Assert.Equal(new AgentSessionStoreKey("conversation-1"), store.LastKey);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SessionIdOverloads_InvalidSessionId_ThrowAsync(string sessionId)
    {
        // Arrange
        var store = new TestAgentSessionStore(session: null);
        var agent = new Mock<AIAgent>();

        // Act and assert
        await Assert.ThrowsAsync<ArgumentException>(
            nameof(sessionId),
            () => store.GetSessionAsync(agent.Object, sessionId).AsTask());
        await Assert.ThrowsAsync<ArgumentException>(
            nameof(sessionId),
            () => store.SaveSessionAsync(agent.Object, sessionId, new TestAgentSession()).AsTask());
        await Assert.ThrowsAsync<ArgumentException>(
            nameof(sessionId),
            () => store.GetOrCreateSessionAsync(agent.Object, sessionId).AsTask());
        Assert.Null(store.LastKey);
    }

    [Fact]
    public async Task SessionIdOverloads_NullSessionId_ThrowAsync()
    {
        // Arrange
        var store = new TestAgentSessionStore(session: null);
        var agent = new Mock<AIAgent>();

        // Act and assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            "sessionId",
            () => store.GetSessionAsync(agent.Object, (string)null!).AsTask());
        await Assert.ThrowsAsync<ArgumentNullException>(
            "sessionId",
            () => store.SaveSessionAsync(agent.Object, (string)null!, new TestAgentSession()).AsTask());
        await Assert.ThrowsAsync<ArgumentNullException>(
            "sessionId",
            () => store.GetOrCreateSessionAsync(agent.Object, (string)null!).AsTask());
        Assert.Null(store.LastKey);
    }

    private sealed class TestAgentSessionStore(AgentSession? session) : AgentSessionStore
    {
        public AgentSessionStoreKey? LastKey { get; private set; }

        public AgentSession? LastSavedSession { get; private set; }

        public CancellationToken LastCancellationToken { get; private set; }

        public override ValueTask<AgentSession?> GetSessionAsync(
            AIAgent agent,
            AgentSessionStoreKey key,
            CancellationToken cancellationToken = default)
        {
            this.LastKey = key;
            this.LastCancellationToken = cancellationToken;
            return new(session);
        }

        public override ValueTask SaveSessionAsync(
            AIAgent agent,
            AgentSessionStoreKey key,
            AgentSession session,
            CancellationToken cancellationToken = default)
        {
            this.LastKey = key;
            this.LastSavedSession = session;
            this.LastCancellationToken = cancellationToken;
            return default;
        }
    }

    private sealed class TestAgentSession : AgentSession;
}
