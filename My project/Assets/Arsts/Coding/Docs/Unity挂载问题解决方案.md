# Unity中Coze和Doubao脚本无法挂载问题解决方案

## 问题描述

在Unity中，`CozeTextToSpeech` 和 `DoubaoTextToSpeech` 脚本无法在Inspector中挂载，但 `BaiduTextToSpeech` 可以正常挂载。

## 问题原因分析

### 1. 依赖关系对比

**BaiduTextToSpeech（可以挂载）**
- ✅ 只使用Unity内置库：`UnityEngine`, `UnityEngine.Networking`
- ✅ 不依赖第三方JSON库
- ✅ 有 `[RequireComponent(typeof(BaiduSettings))]` 特性

**CozeTextToSpeech（无法挂载）**
- ❌ 依赖 `CozeStreamTTS`，而 `CozeStreamTTS` 使用 `Newtonsoft.Json`
- ❌ 如果 `Newtonsoft.Json` 未安装，`CozeStreamTTS` 无法编译
- ❌ 如果 `CozeStreamTTS` 无法编译，`CozeTextToSpeech` 也无法编译

**DoubaoTextToSpeech（无法挂载）**
- ❌ 依赖 `DoubaoStreamTTS` 和 `DoubaoStreamTTSV3`，它们都使用 `Newtonsoft.Json`
- ❌ 如果 `Newtonsoft.Json` 未安装，这些类无法编译
- ❌ 如果依赖类无法编译，`DoubaoTextToSpeech` 也无法编译

### 2. 编译错误链

```
Newtonsoft.Json 未安装
    ↓
CozeStreamTTS 编译失败
DoubaoStreamTTS 编译失败
DoubaoStreamTTSV3 编译失败
    ↓
CozeTextToSpeech 编译失败（无法在Inspector中显示）
DoubaoTextToSpeech 编译失败（无法在Inspector中显示）
```

## 解决方案

### 方法1：通过Unity Package Manager安装（推荐）

1. 打开Unity编辑器
2. 菜单栏：`Window` → `Package Manager`
3. 点击左上角的 `+` 按钮
4. 选择 `Add package by name...`
5. 输入包名：`com.unity.nuget.newtonsoft-json`
6. 点击 `Add` 按钮
7. 等待Unity重新编译脚本

### 方法2：手动编辑manifest.json

1. 找到项目根目录下的 `Packages/manifest.json` 文件
2. 在 `dependencies` 部分添加：

```json
{
  "dependencies": {
    "com.unity.nuget.newtonsoft-json": "3.2.1",
    ...其他依赖...
  }
}
```

3. 保存文件，Unity会自动导入包

### 方法3：使用Unity内置的JsonUtility（不推荐，需要大量代码修改）

如果不想安装Newtonsoft.Json，需要将所有使用 `Newtonsoft.Json` 的代码改为使用Unity内置的 `JsonUtility`。这需要大量代码修改，不推荐。

## 验证步骤

安装Newtonsoft.Json后：

1. **检查Unity Console**
   - 打开 `Window` → `General` → `Console`
   - 确认没有编译错误
   - 如果有错误，查看具体错误信息

2. **检查脚本是否可见**
   - 在Hierarchy中选择任意GameObject
   - 在Inspector中点击 `Add Component`
   - 搜索 `CozeTextToSpeech` 或 `DoubaoTextToSpeech`
   - 应该能看到这些脚本了

3. **测试挂载**
   - 创建一个空的GameObject（命名为"TTS"）
   - 添加 `CozeTextToSpeech` 或 `DoubaoTextToSpeech` 组件
   - 确认可以正常挂载

## 挂载方式说明

根据你的描述，Baidu的挂载方式是：
1. 创建一个空的GameObject（命名为"TTS"）
2. 在该GameObject上挂载 `BaiduTextToSpeech` 组件
3. 由于 `[RequireComponent(typeof(BaiduSettings))]` 特性，Unity会自动添加 `BaiduSettings` 组件
4. 在 `VoiceControlManager` 的Inspector中，将TTS GameObject拖拽到 `baiduTextToSpeech` 字段

**Coze和Doubao的挂载方式应该相同：**
1. 创建一个空的GameObject（命名为"CozeTTS"或"DoubaoTTS"）
2. 在该GameObject上挂载 `CozeTextToSpeech` 或 `DoubaoTextToSpeech` 组件
3. 这些组件会在 `Awake()` 中自动查找或创建所需的依赖组件（如 `CozeStreamTTS`）
4. 在 `VoiceControlManager` 的Inspector中，将TTS GameObject拖拽到对应的字段

## 常见问题

### Q: 安装Newtonsoft.Json后仍然无法挂载？

**A:** 检查以下几点：
1. Unity Console中是否有编译错误
2. 确认所有依赖的类都能正常编译
3. 尝试重新导入脚本：`Assets` → `Reimport All`
4. 重启Unity编辑器

### Q: 为什么Baidu可以挂载而Coze/Doubao不能？

**A:** 因为Baidu只使用Unity内置库，不依赖第三方包。而Coze和Doubao依赖的组件使用了Newtonsoft.Json。

### Q: 能否不使用Newtonsoft.Json？

**A:** 可以，但需要将所有使用Newtonsoft.Json的代码改为使用Unity内置的JsonUtility。这需要大量代码修改，不推荐。

## 相关文件

- `Coding/ASR&TTS/Coze/CozeTextToSpeech.cs`
- `Coding/ASR&TTS/Coze/CozeStreamTTS.cs` (使用Newtonsoft.Json)
- `Coding/ASR&TTS/Doubao/DoubaoTextToSpeech.cs`
- `Coding/ASR&TTS/Doubao/DoubaoStreamTTS.cs` (使用Newtonsoft.Json)
- `Coding/ASR&TTS/Doubao/DoubaoStreamTTSV3.cs` (使用Newtonsoft.Json)
- `Coding/ASR&TTS/Baidu/BaiduTextToSpeech.cs` (不使用Newtonsoft.Json)

