# 火山引擎声音复刻 TTS API

基于火山引擎声音复刻2.0 API的Python语音克隆与合成工具。

## 功能特性

- **语音克隆训练**：上传音频文件进行声音复刻，生成唯一的 `speaker_id`
- **克隆状态查询**：实时查询语音克隆训练状态
- **语音合成**：使用克隆后的声音ID进行文本转语音，支持WebSocket实时流式返回

## 前置条件

### 1. 开通服务

在[火山引擎控制台](https://console.volcengine.com/home)开通以下服务：

- **语音合成2.0**：用于语音合成功能
- **声音复刻2.0**：用于语音克隆功能

### 2. 获取凭证

在控制台申请并获取以下凭证：

- `appid`：应用ID
- `access_token`：访问令牌（代码中的 `token`）

## 安装依赖

```bash
pip install requests websockets asyncio
```

## 配置说明

在 `TTS_HS_API.py` 文件中修改以下配置：

```python
appid = "your_appid"        # 替换为你的appid
token = "your_access_token" # 替换为你的access_token
cluster = "volcano_icl"     # 集群名称，通常无需修改
```

## 使用流程

### 步骤1：语音克隆训练

首先上传音频文件进行声音克隆训练，生成 `speaker_id`：

```python
from TTS_HS_API import voice_clone_train

# 准备音频文件路径和speaker_id
audio_path = "your_audio.wav"  # 音频文件路径
speaker_id = "S_your_speaker_id"  # 自定义的speaker_id

# 执行克隆训练
result = voice_clone_train(audio_path, speaker_id)
print("克隆训练结果:", result)
```

### 步骤2：查询克隆状态

训练完成后，查询克隆状态确认是否成功：

```python
from TTS_HS_API import voice_clone_get_status

# 查询克隆状态
status = voice_clone_get_status(speaker_id)
print("克隆状态:", status)
```

### 步骤3：语音合成

使用克隆后的 `speaker_id` 进行文本转语音：

```python
import asyncio
from TTS_HS_API import text_to_speech

# 准备合成参数
text = "要合成的文本内容"
voice_type = speaker_id  # 使用克隆后的speaker_id
output_file = "output.mp3"

# 执行语音合成
loop = asyncio.get_event_loop()
loop.run_until_complete(text_to_speech(text, voice_type, output_file))
```

## API 说明

### `voice_clone_train(audio_path, spk_id)`

上传音频文件进行语音克隆训练。

**参数：**
- `audio_path` (str): 音频文件路径
- `spk_id` (str): 自定义的speaker_id，格式如 `S_xxxxx`
- `model_type`(int): 0为声音复刻MEGA效果(不推荐使用)1为声音复刻ICL1.0效果2为DiT标准版效果（音色、不还原用户的风格）3为DiT还原版效果（音色、还原用户口音、语速等风格）4为声音复刻ICL2.0效果

**返回：**
- `dict`: 训练结果JSON

### `voice_clone_get_status(spk_id)`

查询语音克隆训练状态。

**参数：**
- `spk_id` (str): speaker_id

**返回：**
- `dict`: 状态信息JSON

### `text_to_speech(text, voice_type, output_file="output.mp3")`

使用WebSocket进行文本转语音合成。

**参数：**
- `text` (str): 要合成的文本内容
- `voice_type` (str): 声音类型（使用克隆后的speaker_id）
- `output_file` (str): 输出音频文件路径，默认为 `output.mp3`

**说明：**
- 使用WebSocket协议，文本一次性送入，后端边合成边返回音频数据
- 支持实时流式返回，提升合成效率

## 完整示例

```python
import asyncio
from TTS_HS_API import voice_clone_train, voice_clone_get_status, text_to_speech

# 1. 语音克隆训练
speaker_id = "S_JgwRuERK1"
audio_path = "1761723696_1.wav"
clone_result = voice_clone_train(audio_path, speaker_id)
print("克隆训练结果:", clone_result)

# 2. 查询克隆状态
status = voice_clone_get_status(speaker_id)
print("克隆状态:", status)

# 3. 语音合成
tts_text = "这是一个语音合成测试"
output_audio = "synthesized.mp3"
loop = asyncio.get_event_loop()
loop.run_until_complete(text_to_speech(tts_text, speaker_id, output_audio))
```

## 注意事项

1. 确保音频文件格式支持（如 wav、mp3 等）
2. `speaker_id` 需要唯一，建议使用有意义的命名
3. 语音克隆训练需要一定时间，可通过状态查询接口确认完成
4. 语音合成使用异步WebSocket，需要配合 `asyncio` 使用
5. 请妥善保管 `appid` 和 `token`，避免泄露

## 相关文档

- [火山引擎声音复刻API文档](https://www.volcengine.com/docs/6561/1305191?lang=zh#_1-%E6%8E%A5%E5%8F%A3%E8%AF%B4%E6%98%8E)
- [火山引擎控制台](https://console.volcengine.com/home)
- [查看声音复刻大模型APPID,Token](https://console.volcengine.com/speech/app?projectName=default)

