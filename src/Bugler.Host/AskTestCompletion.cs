using Bugler.Ai;
using Bugler.SharedKernel;

namespace Bugler.Host;

/// <summary>What the model answered, so the screen can show the words rather than a green dot.</summary>
public sealed record TestCompletionResult(string Model, string Answer);

/// <summary>
/// Proves the AI path out of this deployment while somebody is still standing at the screen —
/// the same job the test mail does for SMTP. Everything else that asks the model does so from a
/// background loop and degrades to silence, so a misconfigured provider looks exactly like the
/// feature being off. A deployment concern, so it lives here beside /health: Ai stays the
/// transport that never learns what a prompt means (ADR 0027).
/// </summary>
internal static class AskTestCompletion
{
    public static async Task<IResult> Handle(
        IAiCompletion completion,
        IAiSettingsSource settingsSource,
        IRequestLanguage requestLanguage,
        CancellationToken cancellationToken)
    {
        var messages = HostMessages.For(await requestLanguage.GetAsync(cancellationToken));

        var settings = await settingsSource.GetCurrentAsync(cancellationToken);
        if (!settings.IsConfigured)
        {
            return Results.Problem(
                title: messages.AiNotConfigured,
                statusCode: StatusCodes.Status400BadRequest);
        }

        try
        {
            var answer = await completion.CompleteAsync(
                new AiPrompt(
                    "You are the connectivity test of a Bugler observability server. "
                    + "Answer in one short sentence and do not ask anything back.",
                    "An administrator is checking that Bugler can reach you. Confirm you hear them."),
                cancellationToken);
            return Results.Ok(new TestCompletionResult(settings.Model, answer));
        }
        catch (AiException exception)
        {
            // Whatever the provider said, said back: an operator setting this up needs the
            // refusal itself — "the call failed" would send them to the container log anyway.
            return Results.Problem(
                title: messages.TestCompletionFailed,
                detail: exception.Message,
                statusCode: StatusCodes.Status502BadGateway);
        }
    }
}
