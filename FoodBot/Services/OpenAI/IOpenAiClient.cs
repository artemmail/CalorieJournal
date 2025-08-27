using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FoodBot.Models;

namespace FoodBot.Services.OpenAI
{
    public interface IOpenAiClient
    {
        Task<Step1Snapshot?> DetectFromImageAsync(string dataUrl, string? userNote, string visionModel, CancellationToken ct);
        Task<FinalPayload?> ComputeFinalAsync(IEnumerable<object> messagesHistoryWithUserPrompt, string model, CancellationToken ct);
    }

    public sealed class OpenAiSettings
    {
        public string ApiKey { get; init; } = "";
        public int TimeoutSeconds { get; init; } = 60;
        public bool DebugLog { get; init; } = false;
        public int MaxRetries { get; init; } = 7;
        public int RetryBaseDelaySeconds { get; init; } = 2;

        // предоставляется из DI
        public System.Net.Http.IHttpClientFactory? ClientFactory { get; init; }
    }
}