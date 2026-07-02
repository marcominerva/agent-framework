// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;

namespace Microsoft.Agents.AI.Workflows;

/// <summary>
/// The <see cref="ChatHistoryProvider"/> used when a <see cref="Workflow"/> is hosted as an
/// <see cref="AIAgent"/> via <see cref="WorkflowHostingExtensions.AsAIAgent"/>.
/// </summary>
/// <remarks>
/// <para>
/// Unlike a regular agent's chat history provider, this provider feeds only the messages produced
/// since the last turn (tracked via a bookmark) to the underlying stateful workflow run.
/// </para>
/// <para>
/// Optional <see cref="WorkflowChatHistoryOptions"/> can be supplied (via
/// <see cref="WorkflowHostingExtensions.AsAIAgent"/>) to enable message reduction and message filtering.
/// Because per-session state is stored in the <see cref="AgentSession.StateBag"/>, a single instance can
/// safely be shared across sessions.
/// </para>
/// </remarks>
internal sealed class WorkflowChatHistoryProvider : ChatHistoryProvider
{
    private readonly ProviderSessionState<StoreState> _sessionState;
    private readonly IChatReducer? _chatReducer;
    private readonly WorkflowChatHistoryOptions.ChatReducerTriggerEvent _reducerTriggerEvent;
    private readonly Func<IEnumerable<ChatMessage>, IEnumerable<ChatMessage>>? _storageInputRequestMessageFilter;
    private readonly Func<IEnumerable<ChatMessage>, IEnumerable<ChatMessage>>? _storageInputResponseMessageFilter;
    private readonly Func<IEnumerable<ChatMessage>, IEnumerable<ChatMessage>>? _provideOutputMessageFilter;
    private IReadOnlyList<string>? _stateKeys;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkflowChatHistoryProvider"/> class.
    /// </summary>
    /// <param name="options">
    /// Optional configuration controlling message reduction, filtering, and state serialization.
    /// If <see langword="null"/>, no reduction or filtering is applied.
    /// </param>
    public WorkflowChatHistoryProvider(WorkflowChatHistoryOptions? options = null)
    {
        this._chatReducer = options?.ChatReducer;
        this._reducerTriggerEvent = options?.ReducerTriggerEvent ?? WorkflowChatHistoryOptions.ChatReducerTriggerEvent.BeforeMessagesRetrieval;
        this._storageInputRequestMessageFilter = options?.StorageInputRequestMessageFilter;
        this._storageInputResponseMessageFilter = options?.StorageInputResponseMessageFilter;
        this._provideOutputMessageFilter = options?.ProvideOutputMessageFilter;

        this._sessionState = new ProviderSessionState<StoreState>(
            _ => new StoreState(),
            this.GetType().Name,
            options?.JsonSerializerOptions);
    }

    /// <inheritdoc />
    public override IReadOnlyList<string> StateKeys => this._stateKeys ??= [this._sessionState.StateKey];

    internal sealed class StoreState
    {
        public int Bookmark { get; set; }
        public List<ChatMessage> Messages { get; set; } = [];
    }

    /// <summary>
    /// Adds caller-provided request messages to the stored history, applying the configured request storage filter.
    /// </summary>
    internal void AddRequestMessages(AgentSession session, IEnumerable<ChatMessage> messages)
    {
        IEnumerable<ChatMessage> toStore = this._storageInputRequestMessageFilter is null ? messages : this._storageInputRequestMessageFilter(messages);
        this._sessionState.GetOrInitializeState(session).Messages.AddRange(toStore);
    }

    /// <summary>
    /// Adds workflow-produced response messages to the stored history, applying the configured response storage filter.
    /// </summary>
    internal void AddResponseMessages(AgentSession session, IEnumerable<ChatMessage> messages)
    {
        IEnumerable<ChatMessage> toStore = this._storageInputResponseMessageFilter is null ? messages : this._storageInputResponseMessageFilter(messages);
        this._sessionState.GetOrInitializeState(session).Messages.AddRange(toStore);
    }

    /// <summary>
    /// Returns the messages produced since the last bookmark (to be delivered to the workflow for the current turn),
    /// applying reduction (when configured for <see cref="WorkflowChatHistoryOptions.ChatReducerTriggerEvent.BeforeMessagesRetrieval"/>)
    /// and the configured output filter.
    /// </summary>
    internal async ValueTask<List<ChatMessage>> GetFromBookmarkAsync(AgentSession session, CancellationToken cancellationToken = default)
    {
        StoreState state = this._sessionState.GetOrInitializeState(session);

        if (this._chatReducer is not null && this._reducerTriggerEvent is WorkflowChatHistoryOptions.ChatReducerTriggerEvent.BeforeMessagesRetrieval)
        {
            await this.ReduceDeliveredAsync(state, cancellationToken).ConfigureAwait(false);
        }

        IEnumerable<ChatMessage> pending = state.Messages.Skip(state.Bookmark);

        if (this._provideOutputMessageFilter is not null)
        {
            pending = this._provideOutputMessageFilter(pending);
        }

        return pending.ToList();
    }

    internal IEnumerable<ChatMessage> GetAllMessages(AgentSession session)
    {
        var state = this._sessionState.GetOrInitializeState(session);
        return state.Messages.AsReadOnly();
    }

    /// <summary>
    /// Marks all currently stored messages as delivered, applying reduction when configured for
    /// <see cref="WorkflowChatHistoryOptions.ChatReducerTriggerEvent.AfterMessageAdded"/>.
    /// </summary>
    internal async ValueTask UpdateBookmarkAsync(AgentSession session, CancellationToken cancellationToken = default)
    {
        StoreState state = this._sessionState.GetOrInitializeState(session);
        state.Bookmark = state.Messages.Count;

        if (this._chatReducer is not null && this._reducerTriggerEvent is WorkflowChatHistoryOptions.ChatReducerTriggerEvent.AfterMessageAdded)
        {
            await this.ReduceDeliveredAsync(state, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Reduces only the already-delivered prefix (messages before the bookmark) using the configured reducer,
    /// preserving any messages still pending delivery to the workflow and recomputing the bookmark accordingly.
    /// </summary>
    private async ValueTask ReduceDeliveredAsync(StoreState state, CancellationToken cancellationToken)
    {
        if (state.Bookmark <= 0)
        {
            return;
        }

        List<ChatMessage> delivered = state.Messages.Take(state.Bookmark).ToList();
        List<ChatMessage> pending = state.Messages.Skip(state.Bookmark).ToList();

        List<ChatMessage> reduced = [.. await this._chatReducer!.ReduceAsync(delivered, cancellationToken).ConfigureAwait(false)];

        int newBookmark = reduced.Count;
        reduced.AddRange(pending);

        state.Messages = reduced;
        state.Bookmark = newBookmark;
    }
}
