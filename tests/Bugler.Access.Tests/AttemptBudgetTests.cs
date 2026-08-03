using Bugler.Access.Authentication;

namespace Bugler.Access.Tests;

/// <summary>
/// What one address may spend before Bugler stops answering it. The budgets are counted per
/// address and never per caller (ADR 0021), so everything worth asserting here is about which
/// spellings of an address share a budget and which do not.
/// </summary>
public class AttemptBudgetTests
{
    private const string Email = "alice@bugler.test";

    [Fact]
    public void Ten_sign_in_attempts_in_a_row_are_a_person_who_is_unsure_which_password_they_used()
    {
        using var budgets = new AttemptBudgets();

        for (var attempt = 1; attempt <= 10; attempt++)
        {
            Assert.True(budgets.TrySignIn(Email, out _), $"attempt {attempt} should still be affordable");
        }

        Assert.False(budgets.TrySignIn(Email, out var retryAfter));
        Assert.True(retryAfter > TimeSpan.Zero, "a spent budget must say when it is worth asking again");
    }

    /// <summary>
    /// The budget belongs to the address, and the address is the one the handler would look a User
    /// up by. Were it otherwise, spelling it differently each time would be the whole of the
    /// bypass — ten attempts per capitalisation rather than ten per address.
    /// </summary>
    [Theory]
    [InlineData("ALICE@BUGLER.TEST")]
    [InlineData("  Alice@Bugler.Test  ")]
    [InlineData("aLiCe@bUgLeR.tEsT")]
    public void An_address_spelt_differently_is_the_same_address(string spelling)
    {
        using var budgets = new AttemptBudgets();

        for (var attempt = 1; attempt <= 10; attempt++)
        {
            Assert.True(budgets.TrySignIn(Email, out _));
        }

        Assert.False(budgets.TrySignIn(spelling, out _));
    }

    [Fact]
    public void One_address_spending_its_budget_costs_another_nothing()
    {
        using var budgets = new AttemptBudgets();

        for (var attempt = 1; attempt <= 11; attempt++)
        {
            budgets.TrySignIn(Email, out _);
        }

        Assert.True(budgets.TrySignIn("bob@bugler.test", out _));
    }

    /// <summary>
    /// An address nobody holds is counted like any other: the refusal has to arrive for a stranger
    /// exactly as it does for a User, or a spent budget would be the answer the sign-in form is
    /// careful never to give.
    /// </summary>
    [Fact]
    public void An_address_belonging_to_nobody_has_a_budget_of_its_own()
    {
        using var budgets = new AttemptBudgets();

        for (var attempt = 1; attempt <= 10; attempt++)
        {
            Assert.True(budgets.TrySignIn("nobody@bugler.test", out _));
        }

        Assert.False(budgets.TrySignIn("nobody@bugler.test", out _));
    }

    /// <summary>Asking for a reset link is rarer than signing in, and counted separately.</summary>
    [Fact]
    public void Asking_for_a_reset_link_is_a_budget_of_its_own()
    {
        using var budgets = new AttemptBudgets();

        for (var attempt = 1; attempt <= 3; attempt++)
        {
            Assert.True(budgets.TryResetRequest(Email, out _));
        }
        Assert.False(budgets.TryResetRequest(Email, out _));

        // Spending one budget must not spend the other: somebody who asked for a link too often
        // can still sign in with the password they turned out to remember.
        Assert.True(budgets.TrySignIn(Email, out _));
    }
}
