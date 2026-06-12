# Settings 使用说明

## 概述

`CozeSettings` 和 `DoubaoSettings` 现在使用 **ScriptableObject**，可以从 Unity 的 Project 窗口直接拖拽到 Inspector 中挂载。

## 创建 Settings 资源文件

### 方法1：通过右键菜单创建

1. 在 Project 窗口中，右键点击任意文件夹
2. 选择 `Create > TTS Settings > Coze Settings` 或 `Create > TTS Settings > Doubao Settings`
3. 命名资源文件（例如：`CozeSettings.asset`）
4. 在 Inspector 中配置 API Key、Token 等参数

### 方法2：通过代码创建（可选）

```csharp
// 在 Unity Editor 中运行
CozeSettings settings = ScriptableObject.CreateInstance<CozeSettings>();
AssetDatabase.CreateAsset(settings, "Assets/Settings/CozeSettings.asset");
```

## 挂载 Settings 到组件

### 方式1：直接拖拽（推荐）

1. 在 Project 窗口中找到创建的 Settings 资源文件（`.asset` 文件）
2. 直接拖拽到以下组件的 Inspector 字段中：
   - `APIManager` 的 `Coze Settings` 或 `Doubao Settings` 字段
   - `CozeTextToSpeech` 的 `Coze Settings` 字段
   - `DoubaoTextToSpeech` 的 `Doubao Settings` 字段
   - `CozeAPISettingsPanel` 的 `Coze Settings` 字段

### 方式2：通过 APIManager 自动获取

如果 Settings 已经挂载到 `APIManager` 上，其他组件会自动通过 `APIManager.Instance` 获取。

## 配置说明

### CozeSettings

- **API Key**: Coze API Key（Personal Access Token）
- **Voice ID**: 当前使用的音色ID
- **Voice Name**: 音色名称（用于显示）

**注意**：组件引用（如 CozeStreamTTS）会在运行时自动查找，无需手动设置。

### DoubaoSettings

- **AppID**: 火山引擎 AppID
- **Token**: 火山引擎 Access Token
- **Cluster**: 集群名称（默认：volcano_icl）
- **Enable Voice Clone**: 是否启用语音克隆功能
- **Speaker ID**: 说话人ID（仅在启用语音克隆时生效）

**注意**：组件引用（如 DoubaoStreamTTS）会在运行时自动查找，无需手动设置。

## 优势

1. **可拖拽挂载**：直接从 Project 窗口拖拽资源文件到 Inspector
2. **资源复用**：同一个 Settings 资源可以在多个场景中复用
3. **版本控制友好**：Settings 作为资源文件，便于版本管理
4. **配置分离**：配置数据与场景分离，便于管理

## 注意事项

1. Settings 资源文件（`.asset`）需要保存在 Project 中，不能删除
2. 组件引用会在运行时自动查找，无需在 Settings 中手动设置
3. 如果 Settings 未挂载，组件会尝试通过 `APIManager.Instance` 自动获取
4. `BaiduSettings` 仍然是 MonoBehaviour，需要挂载在 GameObject 上

## 示例工作流

1. **创建 Settings 资源**：
   - 右键 > Create > TTS Settings > Coze Settings
   - 命名为 `MyCozeSettings.asset`
   - 配置 API Key

2. **挂载到 APIManager**：
   - 找到场景中的 `APIManager` GameObject
   - 从 Project 窗口拖拽 `MyCozeSettings.asset` 到 `Coze Settings` 字段

3. **自动同步**：
   - `CozeTextToSpeech` 等组件会自动通过 `APIManager` 获取 Settings
   - 配置会自动同步到相关组件

