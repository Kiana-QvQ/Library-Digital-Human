# 大模型 API 请求对接文档（简版）

> 项目：`Library Digital Human`  
> 场景：学校内网大模型，**不依赖 Ollama**  
> 原则：**统一按 OpenAI 兼容格式对接**

---

## 1. 推荐怎么接

```
Unity  →  digital_human_backend (/api/chat)  →  学校 OpenAI 兼容网关 (/v1/chat/completions)
```

向学校信息中心要这三样：

| 项目 | 示例 |
|------|------|
| Base URL | `http://10.x.x.x:8080/v1` |
| API Key | `sk-xxxx` |
| 模型名 | `qwen-plus`（以 `/v1/models` 返回为准） |

**非流式 vs 流式怎么选：**

| 模式 | `stream` | 适用 |
|------|----------|------|
| **非流式** | `false` | 实现简单；等整段回复完再 TTS 播报 |
| **流式** | `true` | 边生成边显示/边 TTS；数字人体验更自然 |

---

## 2. 公共部分

```http
POST {base_url}/chat/completions
Authorization: Bearer <API_KEY>
Content-Type: application/json
```

`messages` 格式两种模式相同：

```json
[
  { "role": "system", "content": "你是图书馆数字人助手。" },
  { "role": "user", "content": "图书馆几点关门？" }
]
```

---

## 3. 非流式（stream: false）

### 3.1 请求

```json
{
  "model": "学校提供的模型名",
  "messages": [
    { "role": "system", "content": "你是图书馆数字人助手。" },
    { "role": "user", "content": "图书馆几点关门？" }
  ],
  "temperature": 0.7,
  "max_tokens": 2048,
  "stream": false
}
```

### 3.2 响应示例（200，一次返回完整 JSON）

```json
{
  "id": "chatcmpl-xxx",
  "object": "chat.completion",
  "model": "qwen-plus",
  "choices": [
    {
      "index": 0,
      "message": {
        "role": "assistant",
        "content": "图书馆晚上 22:00 闭馆。"
      },
      "finish_reason": "stop"
    }
  ],
  "usage": {
    "prompt_tokens": 42,
    "completion_tokens": 12,
    "total_tokens": 54
  }
}
```

**取回复：** `choices[0].message.content`

### 3.3 cURL 示例

```bash
curl -X POST "http://10.x.x.x:8080/v1/chat/completions" \
  -H "Authorization: Bearer YOUR_KEY" \
  -H "Content-Type: application/json" \
  -d '{
    "model": "学校提供的模型名",
    "messages": [
      {"role": "system", "content": "你是图书馆数字人助手。"},
      {"role": "user", "content": "图书馆几点关门？"}
    ],
    "stream": false,
    "max_tokens": 2048
  }'
```

### 3.4 Python 示例

```python
from openai import OpenAI

client = OpenAI(base_url="http://10.x.x.x:8080/v1", api_key="YOUR_KEY")

resp = client.chat.completions.create(
    model="学校提供的模型名",
    messages=[
        {"role": "system", "content": "你是图书馆数字人助手。"},
        {"role": "user", "content": "图书馆几点关门？"},
    ],
    stream=False,
    max_tokens=2048,
)

print(resp.choices[0].message.content)
# 图书馆晚上 22:00 闭馆。
```

### 3.5 C# / Unity 示例（UnityWebRequest）

```csharp
using System.Collections;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

[Serializable]
public class ChatRequest
{
    public string model;
    public ChatMessage[] messages;
    public bool stream = false;
    public int max_tokens = 2048;
}

[Serializable]
public class ChatMessage
{
    public string role;
    public string content;
}

[Serializable]
public class ChatCompletionResponse
{
    public Choice[] choices;
}

[Serializable]
public class Choice
{
    public ChatMessage message;
}

IEnumerator ChatNonStream(string userMessage, System.Action<string> onDone)
{
    var body = new ChatRequest
    {
        model = "学校提供的模型名",
        messages = new[]
        {
            new ChatMessage { role = "system", content = "你是图书馆数字人助手。" },
            new ChatMessage { role = "user", content = userMessage },
        },
        stream = false,
        max_tokens = 2048,
    };

    string json = JsonConvert.SerializeObject(body);
    byte[] raw = Encoding.UTF8.GetBytes(json);

    using var req = new UnityWebRequest(
        "http://10.x.x.x:8080/v1/chat/completions", "POST");
    req.uploadHandler = new UploadHandlerRaw(raw);
    req.downloadHandler = new DownloadHandlerBuffer();
    req.SetRequestHeader("Content-Type", "application/json");
    req.SetRequestHeader("Authorization", "Bearer YOUR_KEY");
    req.timeout = 120;

    yield return req.SendWebRequest();

    if (req.result != UnityWebRequest.Result.Success)
    {
        Debug.LogError($"LLM 失败: {req.error}\n{req.downloadHandler.text}");
        yield break;
    }

    var resp = JsonConvert.DeserializeObject<ChatCompletionResponse>(req.downloadHandler.text);
    onDone?.Invoke(resp.choices[0].message.content);
}
```

---

## 4. 流式（stream: true）

### 4.1 请求

与非流式相同，仅把 `stream` 改为 `true`：

```json
{
  "model": "学校提供的模型名",
  "messages": [
    { "role": "system", "content": "你是图书馆数字人助手。" },
    { "role": "user", "content": "介绍一下图书馆" }
  ],
  "temperature": 0.7,
  "max_tokens": 2048,
  "stream": true
}
```

### 4.2 响应格式（SSE）

- `Content-Type: text/event-stream`
- 每行以 `data: ` 开头，后跟 JSON 片段
- 结束行：`data: [DONE]`

**典型片段：**

```
data: {"id":"chatcmpl-xxx","choices":[{"index":0,"delta":{"role":"assistant"},"finish_reason":null}]}

data: {"choices":[{"index":0,"delta":{"content":"图书"},"finish_reason":null}]}

data: {"choices":[{"index":0,"delta":{"content":"馆"},"finish_reason":null}]}

data: {"choices":[{"index":0,"delta":{},"finish_reason":"stop"}]}

data: [DONE]
```

**取回复：** 遍历每个 `data:` 行，拼接 `choices[0].delta.content`（忽略 `[DONE]` 和空 delta）。

### 4.3 cURL 示例

```bash
# -N 禁用缓冲，才能实时看到输出
curl -N -X POST "http://10.x.x.x:8080/v1/chat/completions" \
  -H "Authorization: Bearer YOUR_KEY" \
  -H "Content-Type: application/json" \
  -d '{
    "model": "学校提供的模型名",
    "messages": [
      {"role": "user", "content": "介绍一下图书馆"}
    ],
    "stream": true
  }'
```

### 4.4 Python 示例

```python
from openai import OpenAI

client = OpenAI(base_url="http://10.x.x.x:8080/v1", api_key="YOUR_KEY")

stream = client.chat.completions.create(
    model="学校提供的模型名",
    messages=[{"role": "user", "content": "介绍一下图书馆"}],
    stream=True,
)

full_text = ""
for chunk in stream:
    delta = chunk.choices[0].delta.content
    if delta:
        full_text += delta
        print(delta, end="", flush=True)  # 实时打印

print("\n--- 完整回复 ---")
print(full_text)
```

**不用 SDK，手动解析 SSE：**

```python
import json
import requests

url = "http://10.x.x.x:8080/v1/chat/completions"
headers = {
    "Authorization": "Bearer YOUR_KEY",
    "Content-Type": "application/json",
}
payload = {
    "model": "学校提供的模型名",
    "messages": [{"role": "user", "content": "介绍一下图书馆"}],
    "stream": True,
}

full_text = ""
with requests.post(url, headers=headers, json=payload, stream=True, timeout=120) as r:
    r.raise_for_status()
    for line in r.iter_lines(decode_unicode=True):
        if not line or not line.startswith("data: "):
            continue
        data = line[6:]  # 去掉 "data: "
        if data == "[DONE]":
            break
        obj = json.loads(data)
        delta = obj["choices"][0]["delta"].get("content")
        if delta:
            full_text += delta
            print(delta, end="", flush=True)

print("\n完整:", full_text)
```

### 4.5 C# / Unity 示例（协程 + 手动解析 SSE）

> Unity 的 `UnityWebRequest` 对流式支持较弱，生产环境建议由 **后端代理流式** 或改用支持 SSE 的库。下面是最小可用思路：

```csharp
using System.Collections;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

IEnumerator ChatStream(string userMessage, System.Action<string> onDelta, System.Action<string> onDone)
{
    var body = new
    {
        model = "学校提供的模型名",
        messages = new[] { new { role = "user", content = userMessage } },
        stream = true,
    };
    string json = JsonConvert.SerializeObject(body);
    byte[] raw = Encoding.UTF8.GetBytes(json);

    using var req = new UnityWebRequest(
        "http://10.x.x.x:8080/v1/chat/completions", "POST");
    req.uploadHandler = new UploadHandlerRaw(raw);
    req.downloadHandler = new DownloadHandlerBuffer();
    req.SetRequestHeader("Content-Type", "application/json");
    req.SetRequestHeader("Authorization", "Bearer YOUR_KEY");
    req.timeout = 120;

    var op = req.SendWebRequest();
    // 注意：DownloadHandlerBuffer 会等连接结束才给全文；
    // 真流式需 DownloadHandlerScript 或走后端 /api/chat/stream
    yield return op;

    if (req.result != UnityWebRequest.Result.Success)
    {
        Debug.LogError(req.downloadHandler.text);
        yield break;
    }

    var full = new StringBuilder();
    foreach (var line in req.downloadHandler.text.Split('\n'))
    {
        if (!line.StartsWith("data: ")) continue;
        var data = line.Substring(6).Trim();
        if (data == "[DONE]") break;
        var obj = JObject.Parse(data);
        var delta = obj["choices"]?[0]?["delta"]?["content"]?.ToString();
        if (!string.IsNullOrEmpty(delta))
        {
            full.Append(delta);
            onDelta?.Invoke(delta);  // 可边收边更新 UI / 送 TTS
        }
    }
    onDone?.Invoke(full.ToString());
}
```

**数字人项目建议：** Unity 走 `digital_human_backend` 非流式 `/api/chat` 最简单；若要做「边生成边 TTS」，在后端增加 `/api/chat/stream` 转发 SSE，Unity 只解析自家后端格式。

---

## 5. 常见错误（通用）

| HTTP | 含义 | 怎么处理 |
|------|------|----------|
| 400 / 422 | 参数错误、内容过长 | 检查 model、messages，**不要重试** |
| 401 | API Key 无效 | 更新 Key，**不要重试** |
| 403 / 402 | 无权限 / 余额不足 | 联系管理员 |
| 404 | 模型不存在 | 核对 model 名，查 `/v1/models` |
| 429 | 限流 | 等 1~4 秒后重试，最多 3 次 |
| 500 / 502 / 503 | 服务端异常 | 等 2~5 秒后重试，最多 2 次 |
| 超时 | 推理太久 | 增大 timeout（建议 120s）或缩短输入 |

```json
{
  "error": {
    "message": "错误说明",
    "type": "invalid_request_error",
    "code": "invalid_api_key"
  }
}
```

---

## 6. 本项目后端 `/api/chat`（当前仅非流式）

Unity 推荐调后端，不直接调学校网关。

```http
POST http://<内网IP>:8000/api/chat
Content-Type: application/json
```

**请求：**

```json
{
  "message": "图书馆几点关门？",
  "user_id": "unity_user",
  "session_id": null,
  "model_key": "Qwen",
  "memory_profile": 0,
  "kb_id": "可选-知识库UUID"
}
```

**响应（非流式，一次返回）：**

```json
{
  "response": "图书馆晚上 22:00 闭馆。",
  "user_message": "图书馆几点关门？",
  "session_id": "a1b2c3d4-..."
}
```

**cURL 测后端：**

```bash
curl -X POST "http://127.0.0.1:8000/api/chat" \
  -H "Content-Type: application/json" \
  -d '{
    "message": "图书馆几点关门？",
    "user_id": "unity_user",
    "model_key": "Qwen"
  }'
```

> 当前 `/api/chat` 内部走 Ollama，需改造为转发学校网关。流式接口 `/api/chat/stream` 尚未实现，需要时可按第 4 节在后端代理 SSE。

**`.env`（接入学校网关）：**

```env
LLM_BASE_URL=http://10.x.x.x:8080/v1
LLM_API_KEY=sk-your-school-key
LLM_DEFAULT_MODEL=学校提供的模型名
```

---

## 7. 落地 Checklist

- [ ] `curl` 非流式 `stream:false` 测通
- [ ] `curl -N` 流式 `stream:true` 能看到逐字输出
- [ ] 配置后端 `.env`，改造 LLM 客户端
- [ ] Unity 恢复 `useBackendForChat`，先测 `/api/chat`
- [ ] （可选）后端增加 `/api/chat/stream` 供数字人边播边说
