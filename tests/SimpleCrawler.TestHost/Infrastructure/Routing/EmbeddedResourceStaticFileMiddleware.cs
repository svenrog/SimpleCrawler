using System.Net;

namespace Crawler.TestHost.Infrastructure.Routing;

public class EmbeddedResourceStaticFileMiddleware
{
    private readonly RequestDelegate _next;
    private readonly EmbeddedResourceRouteResolver _routeResolver;

    public EmbeddedResourceStaticFileMiddleware(RequestDelegate next, EmbeddedResourceRouteResolver routeResolver)
    {
        _next = next;
        _routeResolver = routeResolver;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var requestPath = context.Request.Path;
        var routeResult = _routeResolver.Route(requestPath);
        if (routeResult.Match)
        {
            context.Response.StatusCode = (int)HttpStatusCode.OK;
            context.Response.ContentLength = routeResult.Content.Length;
            context.Response.ContentType = routeResult.MimeType;

            await context.Response.Body.WriteAsync(routeResult.Content);
            return;
        }

        await _next(context);
    }

}
