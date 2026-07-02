// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Text.Json;
using Microsoft.Extensions.AI;

namespace Microsoft.Agents.AI.Workflows;

/// <summary>
/// Represents configuration options that control how chat history is managed when a <see cref="Workflow"/>
/// is hosted as an <see cref="AIAgent"/> via <see cref="WorkflowHostingExtensions.AsAIAgent"/>.
/// </summary>
/// <remarks>
/// <para>
/// These options mirror the customization points exposed by <c>InMemoryChatHistoryProvider</c> (message reduction
/// and message filtering), adapted to the workflow hosting model. They allow controlling memory growth and which
/// messages are retained without having to implement a custom chat history provider.
/// </para>
/// <para>
/// Because the hosted workflow is stateful and is only fed the messages produced since the previous turn, message
/// reduction is applied to the already-delivered portion of the conversation only; messages that are still pending
/// delivery to the workflow are always preserved.
/// </para>
/// </remarks>
public sealed class WorkflowChatHistoryOptions
{
    /// <summary>
    /// Gets or sets an optional <see cref="IChatReducer"/> instance used to process, reduce, or optimize chat messages.
    /// This can be used to implement strategies like message summarization, truncation, or cleanup.
    /// </summary>
    /// <value>When <see langword="null"/>, no reduction is applied.</value>
    public IChatReducer? ChatReducer { get; set; }

    /// <summary>
    /// Gets or sets when the message reducer should be invoked.
    /// The default is <see cref="ChatReducerTriggerEvent.BeforeMessagesRetrieval"/>.
    /// </summary>
    /// <remarks>
    /// Message reducers enable automatic management of message storage by implementing strategies to keep memory
    /// usage under control while preserving important conversation context. This setting only has an effect when
    /// <see cref="ChatReducer"/> is set.
    /// </remarks>
    public ChatReducerTriggerEvent ReducerTriggerEvent { get; set; } = ChatReducerTriggerEvent.BeforeMessagesRetrieval;

    /// <summary>
    /// Gets or sets optional JSON serializer options for serializing the state of the chat history provider.
    /// This is valuable for cases like when the chat history contains custom <see cref="AIContent"/> types
    /// and source generated serializers are required, or Native AOT / Trimming is required.
    /// </summary>
    public JsonSerializerOptions? JsonSerializerOptions { get; set; }

    /// <summary>
    /// Gets or sets an optional filter function applied to request messages before they are stored.
    /// </summary>
    /// <value>
    /// When <see langword="null"/>, no filtering is applied and all request messages are stored.
    /// Provide a filter to exclude messages, for example those with
    /// <see cref="AgentRequestMessageSourceType.ChatHistory"/> source type or produced by AI context providers.
    /// </value>
    public Func<IEnumerable<ChatMessage>, IEnumerable<ChatMessage>>? StorageInputRequestMessageFilter { get; set; }

    /// <summary>
    /// Gets or sets an optional filter function applied to response messages before they are stored.
    /// </summary>
    /// <value>
    /// When <see langword="null"/>, no filtering is applied and all response messages are stored.
    /// </value>
    public Func<IEnumerable<ChatMessage>, IEnumerable<ChatMessage>>? StorageInputResponseMessageFilter { get; set; }

    /// <summary>
    /// Gets or sets an optional filter function applied to the messages produced from storage before they are
    /// delivered to the hosted workflow for the current turn.
    /// </summary>
    /// <remarks>
    /// This filter is only applied to the messages produced from the provider's own storage.
    /// </remarks>
    /// <value>When <see langword="null"/>, no filtering is applied to the output messages.</value>
    public Func<IEnumerable<ChatMessage>, IEnumerable<ChatMessage>>? ProvideOutputMessageFilter { get; set; }

    /// <summary>
    /// Defines the events that can trigger the <see cref="ChatReducer"/> when a workflow is hosted as an agent.
    /// </summary>
    public enum ChatReducerTriggerEvent
    {
        /// <summary>
        /// Trigger the reducer after the turn's messages have been added to storage.
        /// </summary>
        AfterMessageAdded,

        /// <summary>
        /// Trigger the reducer before messages are retrieved for delivery to the workflow.
        /// </summary>
        BeforeMessagesRetrieval
    }
}
