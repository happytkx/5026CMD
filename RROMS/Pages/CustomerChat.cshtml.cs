using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using RROMS.Services;

namespace RROMS.Pages;

// JSON-only public endpoint. Razor Pages' normal anti-forgery validation still
// applies; the widget sends its request-verification token in a header.
[AllowAnonymous]
[EnableRateLimiting("customer-chat")]
[RequestSizeLimit(8192)]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class CustomerChatModel(CustomerConciergeService concierge) : PageModel
{
    public sealed record AskRequest(string? Message, List<ConciergeTurn>? History);

    public IActionResult OnGet() => RedirectToPage("/Index");

    public async Task<IActionResult> OnPostAskAsync([FromBody] AskRequest? request)
    {
        var question = request?.Message?.Trim();
        if (string.IsNullOrWhiteSpace(question))
            return BadRequest(new { error = "Please enter a question." });
        if (question.Length > 500)
            return BadRequest(new { error = "Please limit your question to 500 characters." });

        var safeHistory = (request?.History ?? []).TakeLast(6)
            .Where(t => t is not null && (t.Role is "user" or "assistant") &&
                !string.IsNullOrWhiteSpace(t.Content) && t.Content.Length <= 500)
            .ToList();

        var reply = await concierge.AnswerAsync(question, safeHistory, HttpContext.RequestAborted);
        return new JsonResult(new { reply = reply.Message, poweredByAi = reply.PoweredByAi });
    }
}
