# Coze 声音复刻 API 测试脚本

基于 Coze 声音复刻 API 的 Python 语音克隆测试工具。

## 功能特性

- **语音克隆训练**：上传音频文件进行声音复刻，生成唯一的 `voice_id`
- **音色列表查询**：查询当前可用的音色列表（系统音色和自定义音色）
- **音色信息查询**：根据音色ID查询详细信息

## 前置条件

### 1. 开通服务

在[Coze控制台](https://www.coze.cn)开通以下服务：

- **语音克隆功能**：需要 `createVoice` 权限
- 确保个人令牌（API Key）有相应权限

### 2. 获取凭证

在控制台申请并获取：

- `api_key`：个人访问令牌（Personal Access Token）
  - 获取方式：https://www.coze.cn -> 开发者中心 -> API管理

## 安装依赖

```bash
pip install requests
```

## 配置说明

在 `Coze_Voice_Clone_API.py` 文件中修改以下配置：

```python
api_key = "your_coze_api_key_here"  # 替换为你的 Coze API Key
```

## 使用流程

### 步骤1：语音克隆训练

首先上传音频文件进行声音克隆训练，生成 `voice_id`：

```python
from Coze_Voice_Clone_API import voice_clone_train

# 准备音频文件路径和音色名称
audio_path = "your_audio.wav"  # 音频文件路径
voice_name = "我的克隆音色"  # 音色名称（必选，最大128字节）

# 执行克隆训练
result = voice_clone_train(
    audio_path=audio_path,
    voice_name=voice_name,
    text="这是音频对应的文案",  # 可选
    language="zh",  # 可选：zh/en/ja/es/id/pt
    preview_text="你好，我是你的专属AI克隆声音"  # 可选
)

print("克隆训练结果:", result)
```

### 步骤2：查询音色列表

查询当前可用的音色列表：

```python
from Coze_Voice_Clone_API import list_voices

# 查询音色列表
result = list_voices(
    filter_system_voice=False,  # 是否过滤系统音色
    model_type="big",  # 模型类型：big/small
    voice_state="cloned",  # 音色状态：init/cloned/all
    page_num=1,
    page_size=100
)

print("音色列表:", result)
```

### 步骤3：使用音色进行语音合成

克隆完成后，可以使用 `voice_id` 进行语音合成（通过 Coze WebSocket TTS 或其他方式）。

## API 说明

### `voice_clone_train(audio_path, voice_name, ...)`

上传音频文件进行语音克隆训练。

**参数：**
- `audio_path` (str): 音频文件路径（支持 wav、mp3、ogg、m4a、aac、pcm）
- `voice_name` (str): 音色名称（必选，长度限制128字节）
- `text` (str, 可选): 音频文件对应的文案，最大1024字节
- `language` (str, 可选): 语种，支持：zh/en/ja/es/id/pt，默认zh
- `voice_id` (str, 可选): 需要训练的音色ID（用于重新训练）
- `preview_text` (str, 可选): 预览音频的文案
- `space_id` (str, 可选): 工作空间ID

**返回：**
- `dict`: 训练结果JSON，包含 `voice_id`

**音频文件要求：**
- 文件格式：wav、mp3、ogg、m4a、aac、pcm（pcm仅支持24k采样率，单通道）
- 文件大小：最大不超过10MB
- 音频时长：建议10s~30s
- 语种：支持中文、英文、日语、西班牙语、印度尼西亚语、葡萄牙语

### `list_voices(...)`

查询可用的音色列表。

**参数：**
- `filter_system_voice` (bool, 可选): 是否过滤系统音色
- `model_type` (str, 可选): 模型类型（big/small）
- `voice_state` (str, 可选): 音色状态（init/cloned/all）
- `page_num` (int, 可选): 页码，最小值为1，默认为1
- `page_size` (int, 可选): 每页数量，1~100，默认为100

**返回：**
- `dict`: 音色列表JSON

### `get_voice_info(voice_id)`

根据音色ID获取音色信息。

**参数：**
- `voice_id` (str): 音色ID

**返回：**
- `dict`: 音色信息，如果未找到返回None

## 完整示例

```python
from Coze_Voice_Clone_API import voice_clone_train, list_voices, get_voice_info

# 1. 语音克隆训练
audio_path = "test_audio.wav"
voice_name = "我的测试音色"

clone_result = voice_clone_train(
    audio_path=audio_path,
    voice_name=voice_name,
    text="这是测试音频对应的文案",
    language="zh"
)

print("克隆训练结果:", clone_result)

# 2. 获取音色ID
if clone_result.get('code') == 0:
    voice_id = clone_result.get('data', {}).get('voice_id')
    print(f"音色ID: {voice_id}")
    
    # 3. 查询音色信息
    voice_info = get_voice_info(voice_id)
    if voice_info:
        print("音色信息:", voice_info)
    
    # 4. 查询所有音色列表
    voices_result = list_voices()
    print("所有音色:", voices_result)
```

## 运行测试

直接运行脚本进行测试：

```bash
python Coze_Voice_Clone_API.py
```

在运行前，请确保：
1. 已设置正确的 `api_key`
2. 已准备好测试音频文件
3. 音频文件符合要求（格式、大小、时长等）

## 注意事项

1. **权限要求**：
   - API Key 需要有 `createVoice` 权限
   - 如果权限不足，会返回403错误

2. **音频文件要求**：
   - 确保音频文件格式支持
   - 文件大小不超过10MB
   - 建议时长10-30秒
   - 确保录音质量良好

3. **音色名称限制**：
   - 长度限制128字节
   - 建议使用有意义的命名

4. **文案要求**：
   - 如果提供文案，需要和音频内容大致一致
   - 最大长度1024字节
   - 差异过大会报错（错误码1109 WERError）

5. **错误处理**：
   - 脚本包含完整的错误处理
   - 会显示详细的错误信息
   - 如果是权限问题，会给出提示

## 常见错误

### 错误1：权限不足
```
错误码: 403
错误信息: 权限不足
```
**解决方法**：
- 检查 API Key 是否有 `createVoice` 权限
- 在 Coze 控制台中开通语音克隆功能
- 联系 Coze 客服申请权限

### 错误2：音频文件格式不支持
```
错误: 不支持的音频格式
```
**解决方法**：
- 确保文件格式为：wav、mp3、ogg、m4a、aac、pcm
- 检查文件扩展名是否正确

### 错误3：文件过大
```
错误: 音频文件过大，最大支持10MB
```
**解决方法**：
- 压缩音频文件
- 截取音频片段（建议10-30秒）

### 错误4：文案差异过大（WERError）
```
错误码: 1109
错误信息: WERError
```
**解决方法**：
- 确保文案和音频内容一致
- 或者不提供文案参数

## 相关文档

- [Coze API 文档](https://www.coze.cn/docs)
- [Coze 控制台](https://www.coze.cn)
- [获取 API Key](https://www.coze.cn -> 开发者中心 -> API管理)

## 与 Doubao 的对比

| 功能 | Coze | Doubao |
|------|------|--------|
| 语音克隆 | ✅ 支持（需要权限） | ✅ 支持 |
| 克隆状态查询 | ❌ 不支持（直接返回结果） | ✅ 支持 |
| 音色列表查询 | ✅ 支持 | ❌ 不支持 |
| 权限要求 | 需要 createVoice 权限 | 需要开通服务 |

**注意**：如果 Coze 权限不足，建议使用 Doubao（火山引擎）的语音克隆功能。

