namespace MarketService.Infrastructure.Data;

using System.Net.Http.Headers;
using Microsoft.AspNetCore.Http;

public sealed class ForwardAuthHeaderHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _ctx;

    public ForwardAuthHeaderHandler(IHttpContextAccessor ctx) => _ctx = ctx;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var httpCtx = _ctx.HttpContext;
        Console.WriteLine($"[ForwardAuthHeaderHandler] Invoked. HttpContext null? {httpCtx == null}]");
        
        var auth = _ctx.HttpContext?.Request.Headers["Authorization"].ToString();
        Console.WriteLine($"[ForwardAuthHeaderHandler] Incoming Authorization:" +
                          $"{(string.IsNullOrWhiteSpace(auth) ? string.Empty : auth[..Math.Min(auth.Length, 25)] + "...")}");
        if (!string.IsNullOrWhiteSpace(auth))
        {
            request.Headers.Remove("Authorization");
            request.Headers.TryAddWithoutValidation("Authorization", auth);
        }

        return base.SendAsync(request, ct);
    }
}
