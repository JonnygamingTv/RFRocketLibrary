using System;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Logger = Rocket.Core.Logging.Logger;

namespace RFRocketLibrary.Helpers.DiscordWebhook
{
    public static class WebhookService
    {
        public static void PostMessage(this WebhookMessage webhookMessage, string webhookURL, int? threadId = null)
        {
            var request = WebRequest.CreateHttp(webhookURL + (threadId == null ? string.Empty : $"?thread_id={threadId}"));
            request.Proxy = new WebProxy();
            request.Method = "POST";
            request.ContentType = "application/json";

            var payload = JsonConvert.SerializeObject(webhookMessage);
            var buffer = Encoding.UTF8.GetBytes(payload);

            request.ContentLength = buffer.Length;
            using (var write = request.GetRequestStream())
            {
                write.Write(buffer, 0, buffer.Length);
                write.Flush();
            }

            // _ = (HttpWebResponse)request.GetResponse();
            request.GetResponse();
        }

        public static async void PostMessageAsync(this WebhookMessage webhookMessage, string webhookURL, string? threadId = null)
        {
            var request = WebRequest.CreateHttp(webhookURL + (threadId == null ? string.Empty : $"?thread_id={threadId}"));
            request.Proxy = new WebProxy();
            request.Method = "POST";
            request.ContentType = "application/json";

            var buffer = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(webhookMessage));
            await SendAsync(request, buffer);
        }

        // public static void GetMessage(this WebhookMessage webhookMessage, string webhookURL, string? threadId = null, string? messageId = null)
        // {
        //     throw new NotImplementedException();
        // }
        //
        // public static async void GetMessageAsync(this WebhookMessage webhookMessage, string webhookURL, string? threadId = null, string? messageId = null)
        // {
        //     throw new NotImplementedException();
        // }

        public static void EditMessage(this WebhookMessage webhookMessage, string webhookURL, string messageId, int? threadId = null)
        {
            var request = WebRequest.CreateHttp(webhookURL + $"/messages/{messageId}" + (threadId == null ? string.Empty : $"?thread_id={threadId}"));
            request.Proxy = new WebProxy();
            request.Method = "PATCH";
            request.ContentType = "application/json";

            var payload = JsonConvert.SerializeObject(webhookMessage);
            var buffer = Encoding.UTF8.GetBytes(payload);

            request.ContentLength = buffer.Length;
            using (var write = request.GetRequestStream())
            {
                write.Write(buffer, 0, buffer.Length);
                write.Flush();
            }

            // _ = (HttpWebResponse)request.GetResponse();
            request.GetResponse();
        }

        public static async void EditMessageAsync(this WebhookMessage webhookMessage, string webhookURL, string messageId, string? threadId = null)
        {
            var request = WebRequest.CreateHttp(webhookURL + $"/messages/{messageId}" + (threadId == null ? string.Empty : $"?thread_id={threadId}"));
            request.Proxy = new WebProxy();
            request.Method = "PATCH";
            request.ContentType = "application/json";

            var buffer = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(webhookMessage));
            await SendAsync(request, buffer);
        }

        public static void DeleteMessage(this WebhookMessage webhookMessage, string webhookURL, string messageId, string? threadId = null)
        {
            var request = WebRequest.CreateHttp(webhookURL + $"/messages/{messageId}" + (threadId == null ? string.Empty : $"?thread_id={threadId}"));
            request.Proxy = new WebProxy();
            request.Method = "DELETE";
            request.GetResponse();
        }

        public static async void DeleteMessageAsync(this WebhookMessage webhookMessage, string webhookURL, string messageId, string? threadId = null)
        {
            var request = WebRequest.CreateHttp(webhookURL + $"/messages/{messageId}" + (threadId == null ? string.Empty : $"?thread_id={threadId}"));
            request.Proxy = new WebProxy();
            request.Method = "DELETE";
            await SendAsync(request, null);
        }

        /// <summary>
        /// The three *Async extension methods are public `async void` APIs whose signatures
        /// are fixed by binary compatibility: an unhandled exception inside them would crash
        /// the whole server process. Log failures instead of letting them escape.
        /// </summary>
        private static async Task SendAsync(HttpWebRequest request, byte[]? buffer)
        {
            try
            {
                if (buffer != null)
                {
                    request.ContentLength = buffer.Length;
                    using (var write = await request.GetRequestStreamAsync())
                    {
                        await write.WriteAsync(buffer, 0, buffer.Length);
                        await write.FlushAsync();
                    }
                }

                await request.GetResponseAsync();
            }
            catch (Exception e)
            {
                // Logging goes through Rocket's chain, which needs the game engine; on hosts
                // without it the report must not escape the async void and crash the process.
                try
                {
                    Logger.LogError($"[WebhookService] {request.Method} request failed: {e.Message}");
                    Logger.LogError($"[WebhookService] Details: {e}");
                }
                catch
                {
                    // Ignore the reporter failure.
                }
            }
        }
    }
}
