# TTS脚本无法挂载排查步骤

## 情况说明
已安装 `com.unity.nuget.newtonsoft-json`，Unity没有报错，但 `CozeTextToSpeech` 和 `DoubaoTextToSpeech` 仍无法在Inspector中挂载。

## 排查步骤

### 步骤1：检查Unity Console
1. 打开 `Window` → `General` → `Console`
2. 查看是否有**任何**编译错误（包括警告）
3. 如果有错误，先解决这些错误
4. **重要**：即使没有红色错误，也要检查是否有黄色警告

### 步骤2：强制重新编译
1. 关闭Unity编辑器
2. 删除项目根目录下的 `Library` 文件夹
   - 位置：`E:\Unity Hub\project\My project\Library`
   - 这个文件夹包含Unity的编译缓存
3. 重新打开Unity项目
4. 等待Unity重新编译所有脚本（可能需要几分钟）
5. 查看Console，确认没有编译错误

### 步骤3：使用诊断脚本
1. 在Unity中创建一个空的GameObject
2. 添加 `TTSScriptDiagnostic` 组件（我已经创建了这个脚本）
3. 运行游戏，查看Console输出
4. 诊断脚本会检查所有TTS相关类是否可以编译
5. 如果看到 ❌，说明对应的类无法编译

### 步骤4：检查脚本是否真的存在
在Unity的Project窗口中：
1. 搜索 `CozeTextToSpeech.cs`
2. 搜索 `DoubaoTextToSpeech.cs`
3. 确认这些文件存在且没有被Unity标记为错误

### 步骤5：检查Add Component菜单
1. 在Hierarchy中创建一个空的GameObject
2. 在Inspector中点击 `Add Component`
3. 在搜索框中输入：
   - `CozeTextToSpeech`
   - `DoubaoTextToSpeech`
   - `CozeStreamTTS`
   - `DoubaoStreamTTS`
4. 查看是否能找到这些脚本
5. 如果找不到，说明脚本确实无法编译

### 步骤6：检查依赖类
确认以下类都可以正常编译：
- `TTS` (基类)
- `CozeStreamTTS`
- `DoubaoStreamTTS`
- `DoubaoStreamTTSV3`
- `CozeAPIManager`
- `DoubaoAPIManager`

如果这些依赖类无法编译，`CozeTextToSpeech` 和 `DoubaoTextToSpeech` 也无法编译。

### 步骤7：检查Assembly Definition
如果项目使用了Assembly Definition Files (.asmdef)：
1. 检查是否有asmdef文件
2. 确认Newtonsoft.Json的引用是否正确配置
3. 可能需要添加对Newtonsoft.Json程序集的引用

## 常见问题

### Q: 为什么Unity没有显示错误，但脚本还是无法挂载？
A: 可能的原因：
1. Unity的编译顺序问题 - 依赖类还没有编译完成
2. Unity的缓存问题 - 需要删除Library文件夹
3. 脚本有隐藏的编译错误 - 使用诊断脚本检查

### Q: 如何确认脚本真的无法编译？
A: 
1. 使用我创建的 `TTSScriptDiagnostic` 脚本
2. 或者在Console中查看编译日志
3. 尝试在代码中直接引用这些类，看是否有编译错误

### Q: 删除Library文件夹安全吗？
A: 是的，完全安全。Library文件夹是Unity自动生成的缓存文件夹，删除后Unity会重新生成。这不会影响你的项目文件。

## 快速解决方案

**如果以上步骤都不行，尝试这个：**

1. 关闭Unity
2. 删除 `Library` 文件夹
3. 删除 `Temp` 文件夹（如果存在）
4. 重新打开Unity
5. 等待编译完成
6. 在Project窗口中找到 `CozeTextToSpeech.cs`
7. 右键 → `Reimport`
8. 对 `DoubaoTextToSpeech.cs` 也执行 `Reimport`
9. 等待重新编译

## 如果还是不行

请提供以下信息：
1. Unity版本号
2. Console中的完整错误信息（如果有）
3. 诊断脚本的输出结果
4. 是否能找到 `CozeStreamTTS` 和 `DoubaoStreamTTS` 脚本（在Add Component菜单中）

