using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FoodBot.Services;
using Microsoft.Extensions.Configuration;
using Xunit;

public class SpeechToTextServiceTests
{
    private SpeechToTextService CreateService(HttpMessageHandler handler)
    {
        var factory = new FakeFactory(handler);
        var cfg = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OpenAI:ApiKey"] = "test",
                ["OpenAI:TranscribeModel"] = "whisper-1"
            })
            .Build();
        return new SpeechToTextService(factory, cfg);
    }

    private sealed class FakeFactory : IHttpClientFactory
    {
        private readonly HttpClient _client;
        public FakeFactory(HttpMessageHandler handler)
        {
            _client = new HttpClient(handler);
        }
        public HttpClient CreateClient(string name) => _client;
    }

    private sealed class TestHandler : HttpMessageHandler
    {
        private readonly Queue<object> _responses;
        public int Calls { get; private set; }
        public TestHandler(IEnumerable<object> responses)
        {
            _responses = new Queue<object>(responses);
        }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            var next = _responses.Dequeue();
            if (next is Exception ex)
                return Task.FromException<HttpResponseMessage>(ex);
            return Task.FromResult((HttpResponseMessage)next);
        }
    }

    [Fact]
    public async Task TranscribeAsync_RetriesOnServerError()
    {
        var handler = new TestHandler(new object[]
        {
            new HttpResponseMessage(HttpStatusCode.InternalServerError) { Content = new StringContent("e1") },
            new HttpResponseMessage(HttpStatusCode.InternalServerError) { Content = new StringContent("e2") },
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"text\":\"ok\"}") }
        });

        var svc = CreateService(handler);
        var res = await svc.TranscribeAsync(new byte[] {1}, null, null, null, CancellationToken.None);

        Assert.Equal("ok", res);
        Assert.Equal(3, handler.Calls);
    }

    [Fact]
    public async Task TranscribeAsync_ThrowsAfterTimeout()
    {
        var handler = new TestHandler(new object[]
        {
            new TaskCanceledException(),
            new TaskCanceledException(),
            new TaskCanceledException()
        });

        var svc = CreateService(handler);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.TranscribeAsync(new byte[] {1}, null, null, null, CancellationToken.None));

        Assert.IsType<TaskCanceledException>(ex.InnerException);
        Assert.Equal(3, handler.Calls);
    }
}
