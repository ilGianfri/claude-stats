using System.Net;
using System.Net.Http;
using System.Text;

namespace ClaudeStats.Tests.TestSupport;

/// <summary>
/// Test <see cref="HttpMessageHandler"/> that returns a queued sequence of responses (the last one
/// repeats) and records every request it received.
/// </summary>
public sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _responders;

    /// <summary>Initializes the handler with an ordered set of responders.</summary>
    /// <param name="responders">Responders invoked in order; the last repeats for extra requests.</param>
    public StubHttpMessageHandler(params Func<HttpRequestMessage, HttpResponseMessage>[] responders)
    {
        _responders = new Queue<Func<HttpRequestMessage, HttpResponseMessage>>(responders);
    }

    /// <summary>All requests received, in order.</summary>
    public List<HttpRequestMessage> Requests { get; } = [];

    /// <summary>
    /// Request bodies as strings, in the same order as <see cref="Requests"/> (null for bodiless
    /// requests). Captured at send time because callers typically dispose their content afterwards.
    /// </summary>
    public List<string?> RequestBodies { get; } = [];

    /// <summary>Creates a JSON response with the given status code.</summary>
    /// <param name="json">JSON body.</param>
    /// <param name="status">HTTP status code.</param>
    /// <returns>A responder producing that response.</returns>
    public static Func<HttpRequestMessage, HttpResponseMessage> Json(
        string json, HttpStatusCode status = HttpStatusCode.OK) =>
        _ => new HttpResponseMessage(status)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };

    /// <summary>Creates an empty response with the given status code.</summary>
    /// <param name="status">HTTP status code.</param>
    /// <returns>A responder producing that response.</returns>
    public static Func<HttpRequestMessage, HttpResponseMessage> Status(HttpStatusCode status) =>
        _ => new HttpResponseMessage(status);

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        RequestBodies.Add(request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken));
        Func<HttpRequestMessage, HttpResponseMessage> responder = _responders.Count > 1
            ? _responders.Dequeue()
            : _responders.Peek();
        HttpResponseMessage response = responder(request);
        // Real handlers (e.g. SocketsHttpHandler) attach the originating request; mirror that so
        // consumers that inspect response.RequestMessage behave as they do in production.
        response.RequestMessage ??= request;
        return response;
    }
}
