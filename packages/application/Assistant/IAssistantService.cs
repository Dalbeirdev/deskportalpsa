namespace Desk.Application.Assistant;

/// <summary>What the technician asked for. Each maps to one prompt; nothing else is accepted.</summary>
public enum AssistantAction
{
    Summarise,
    DraftReply,
    ImproveDraft,
    NextSteps,
    ExplainError,
    SimilarTickets,
    /// <summary>
    /// The technician's own question about this ticket. The six fixed actions cover the common
    /// asks; this covers the rest without a code change every time someone wants something new.
    /// Still scoped to the one ticket — the question rides on the same context, never replaces it.
    /// </summary>
    Ask,
}

/// <summary>
/// One assistant answer. <see cref="IsDraft"/> marks output meant to land in the composer for the
/// technician to edit and send — the model never sends anything itself.
/// </summary>
public sealed record AssistantAnswer(string Text, bool IsDraft);

/// <summary>Whether the assistant is available, and why not when it is not.</summary>
public sealed record AssistantAvailability(bool Enabled, string? Reason);

public interface IAssistantService
{
    /// <summary>Cheap check for the UI, so the rail can explain itself rather than fail on click.</summary>
    Task<AssistantAvailability> AvailabilityAsync(CancellationToken ct = default);

    /// <summary>
    /// Answers one question about one ticket. <paramref name="draft"/> carries the technician's own
    /// text for <see cref="AssistantAction.ImproveDraft"/> and is ignored otherwise;
    /// <paramref name="question"/> carries it for <see cref="AssistantAction.Ask"/>.
    /// </summary>
    Task<AssistantAnswer> AskAsync(Guid ticketId, AssistantAction action, string? draft, string? question = null, CancellationToken ct = default);
}

/// <summary>The model call itself, kept behind an interface so the provider can change.</summary>
public interface IAssistantModel
{
    Task<string> CompleteAsync(string apiKey, string model, string systemPrompt, string userPrompt, CancellationToken ct = default);
}
