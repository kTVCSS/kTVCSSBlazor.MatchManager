namespace kTVCSSBlazor.MatchManager.Services
{
    public class DomainRestrictionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly List<string> _allowedDomains;

        public DomainRestrictionMiddleware(RequestDelegate next, List<string> allowedDomains)
        {
            _next = next;
            _allowedDomains = allowedDomains;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var host = context.Request.Headers.Host.ToString();
            var origin = context.Request.Headers.Origin.ToString();

#if DEBUG
            Console.WriteLine(context.Request.Path);

            foreach (var header in context.Request.Headers)
            {
                Console.WriteLine("header - " + header.Key + " - " + header.Value.ToString());
            }
#endif

            if (context.Request.Path.HasValue)
            {
                if (context.Request.Path == "/api/RequestUpdateTotalPlayers")
                {
                    await _next(context);
                    return;
                }
            }

            bool allow = false;

            foreach (var domain in _allowedDomains)
            {
                if (host.Contains(domain) || (!string.IsNullOrEmpty(origin) && origin.Contains(domain)))
                {
                    allow = true;
                    await _next(context);
                    break;
                }
            }

            if (!allow)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsync("403: Not allowed");
            }
        }
    }

    public static class DomainRestrictionMiddlewareExtensions
    {
        public static IApplicationBuilder UseDomainRestriction(
            this IApplicationBuilder builder, List<string> allowedDomains)
        {
            return builder.UseMiddleware<DomainRestrictionMiddleware>(allowedDomains);
        }
    }
}
