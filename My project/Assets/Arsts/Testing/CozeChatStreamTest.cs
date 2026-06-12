// Standalone .NET console tester for Coze chat API (stream & non-stream).
// This file is guarded to NOT compile inside Unity. Build with `dotnet build` or `csc` on your PC.
#if !UNITY_5_3_OR_NEWER
using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Arsts.Testing
{
    internal class CozeChatStreamTest
    {
        // TODO: 填入你的 PAT 与 BotID
        private const string ApiKey = "pat_UDkT4W9EmMA6GAgX5o5ZBpeJnOL9VYB1CLEe8DLZ5pmSXB754qfRp8vsn8W5ITwu";
        private const string BotId = "7510160526350614562";
        private const string Endpoint = "https://api.coze.cn/v3/chat";

        public static async Task Main(string[] args)
        {
            if (ApiKey.StartsWith("YOUR_") || BotId.StartsWith("YOUR_"))
            {
                Console.WriteLine("请先在源码中配置 ApiKey 与 BotId");
                return;
            }

            Console.WriteLine("选择模式: 1) 非流式  2) 流式");
            var mode = Console.ReadLine();

            Console.WriteLine("输入用户问题:");
            var question = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(question))
            {
                Console.WriteLine("问题为空，退出。");
                return;
            }

            if (mode == "2")
            {
                await RunStream(question);
            }
            else
            {
                await RunNormal(question);
            }
        }

        private static async Task RunNormal(string question)
        {
            using var http = new HttpClient();
            http.DefaultRequestHeaders.Add("Authorization", $"Bearer {ApiKey}");

            var body = new
            {
                bot_id = BotId,
                user_id = "user_123",
                stream = false,
                auto_save_history = true,
                additional_messages = new object[]
                {
                    new { role = "user", content = question, content_type = "text" }
                }
            };

            var json = JsonSerializer.Serialize(body);
            var resp = await http.PostAsync(Endpoint,
                new StringContent(json, Encoding.UTF8, "application/json"));

            Console.WriteLine($"HTTP {resp.StatusCode}");
            Console.WriteLine(await resp.Content.ReadAsStringAsync());
        }

        private static async Task RunStream(string question)
        {
            using var http = new HttpClient();
            http.DefaultRequestHeaders.Add("Authorization", $"Bearer {ApiKey}");

            var body = new
            {
                bot_id = BotId,
                user_id = "user_123",
                stream = true,
                auto_save_history = true,
                additional_messages = new object[]
                {
                    new { role = "user", content = question, content_type = "text" }
                }
            };

            var json = JsonSerializer.Serialize(body);
            var req = new HttpRequestMessage(HttpMethod.Post, Endpoint)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

            // 流式读取响应体
            using var resp = await http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
            Console.WriteLine($"HTTP {resp.StatusCode}");
            using var stream = await resp.Content.ReadAsStreamAsync();
            using var reader = new StreamReader(stream, Encoding.UTF8);

            string? line;
            while ((line = await reader.ReadLineAsync()) != null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                // Coze SSE 格式：event: xxx / data: {...}
                Console.WriteLine(line);
                if (line.Trim() == "data: [DONE]" || line.Trim() == "data:[DONE]")
                {
                    break;
                }
            }
        }
    }
}
#endif

