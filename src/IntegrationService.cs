// IntegrationService.cs
// Receives SAP Business One order-change webhooks and syncs them to Wrike
// as fulfillment tasks. Demonstrates the retry/backoff pattern used in
// production integration work: transient failures get retried with
// exponential backoff; anything that still fails gets dead-lettered
// instead of silently dropped.

using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace SyncHub.Integration
{
    public record SapOrderEvent(string OrderId, string CustomerName, decimal Total, DateTime CreatedAt);

    public class IntegrationService
    {
        private readonly HttpClient _wrikeClient;
        private readonly IDeadLetterQueue _deadLetterQueue;
        private const int MaxAttempts = 3;

        public IntegrationService(HttpClient wrikeClient, IDeadLetterQueue deadLetterQueue)
        {
            _wrikeClient = wrikeClient;
            _deadLetterQueue = deadLetterQueue;
        }

        /// <summary>
        /// Entry point for the SAP B1 webhook. Maps the order event to a
        /// Wrike task payload and attempts delivery with retry/backoff.
        /// </summary>
        public async Task HandleOrderEventAsync(SapOrderEvent orderEvent)
        {
            var payload = MapToWrikeTask(orderEvent);

            for (var attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                try
                {
                    var response = await SendToWrikeAsync(payload);

                    if (response.IsSuccessStatusCode)
                    {
                        return;
                    }

                    // Only retry on transient failures. 4xx errors (bad payload,
                    // auth issues) won't fix themselves on retry, so fail fast.
                    if (!IsTransient(response.StatusCode))
                    {
                        await _deadLetterQueue.EnqueueAsync(orderEvent, $"Non-transient error: {response.StatusCode}");
                        return;
                    }
                }
                catch (HttpRequestException ex) when (attempt < MaxAttempts)
                {
                    var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt)); // 2s, 4s, 8s
                    await Task.Delay(delay);
                    continue;
                }
                catch (HttpRequestException ex)
                {
                    await _deadLetterQueue.EnqueueAsync(orderEvent, $"Failed after {MaxAttempts} attempts: {ex.Message}");
                    return;
                }
            }

            await _deadLetterQueue.EnqueueAsync(orderEvent, $"Exhausted {MaxAttempts} attempts");
        }

        private async Task<HttpResponseMessage> SendToWrikeAsync(WrikeTaskPayload payload)
        {
            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            return await _wrikeClient.PostAsync("/api/v4/tasks", content);
        }

        private static bool IsTransient(System.Net.HttpStatusCode status)
        {
            return status == System.Net.HttpStatusCode.RequestTimeout
                || status == System.Net.HttpStatusCode.TooManyRequests
                || (int)status >= 500;
        }

        private static WrikeTaskPayload MapToWrikeTask(SapOrderEvent order)
        {
            return new WrikeTaskPayload
            {
                Title = $"Fulfill {order.OrderId} — {order.CustomerName}",
                Description = $"Order total: {order.Total:C}. Created {order.CreatedAt:u}.",
                CustomFields = new()
                {
                    ["sap_order_id"] = order.OrderId
                }
            };
        }
    }

    public class WrikeTaskPayload
    {
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public System.Collections.Generic.Dictionary<string, string> CustomFields { get; set; } = new();
    }

    public interface IDeadLetterQueue
    {
        Task EnqueueAsync(SapOrderEvent order, string reason);
    }
}
