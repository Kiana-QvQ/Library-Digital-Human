# TTS脚本无法挂载问题诊断和解决

## 问题现象
在Unity中，`CozeTextToSpeech` 和 `DoubaoTextToSpeech` 脚本无法在Inspector中挂载到GameObject上。

## 诊断步骤

### 1. 检查Unity Console
打开 `Window` → `General` → `Console`，查看是否有编译错误：
- 如果看到 `The type or namespace name 'Newtonsoft' could not be found`，说明Newtonsoft.Json未正确引用
- 如果看到其他编译错误，先解决这些错误

### 2. 检查Newtonsoft.Json.dll配置
确认以下文件存在且配置正确：
- `Assets/Arsts/Coding/ASR&TTS/Newtonsoft.Json.dll`
- `Assets/Arsts/Coding/ASR&TTS/Newtonsoft.Json.dll.meta`

在meta文件中，Editor平台应该设置为 `enabled: 1`

### 3. 强制重新编译
1. 关闭Unity编辑器
2. 删除 `Library` 文件夹（Unity会自动重建）
3. 重新打开Unity项目
4. 等待编译完成

## 解决方案

### 方案1：使用Unity Package Manager安装Newtonsoft.Json（推荐）

这是最可靠的方法，因为Unity Package Manager会自动处理所有依赖关系。

**步骤：**
1. 打开Unity编辑器
2. 菜单栏：`Window` → `Package Manager`
3. 点击左上角的 `+` 按钮
4. 选择 `Add package by name...`
5. 输入包名：`com.unity.nuget.newtonsoft-json`
6. 点击 `Add` 按钮
7. 等待Unity下载并导入包
8. 等待编译完成

**验证：**
- 检查 `Packages` 文件夹下是否有 `com.unity.nuget.newtonsoft-json` 文件夹
- 在Console中应该没有编译错误
- 尝试在GameObject上添加 `CozeTextToSpeech` 或 `DoubaoTextToSpeech` 组件

### 方案2：手动配置DLL引用

如果必须使用DLL文件，需要确保正确配置：

1. **检查DLL位置**
   - DLL应该在 `Assets` 文件夹下的某个位置
   - 确保DLL文件没有被移动或删除

2. **检查meta文件**
   - 打开 `Newtonsoft.Json.dll.meta`
   - 确认Editor平台设置为 `enabled: 1`

3. **重新导入DLL**
   - 在Project窗口中找到 `Newtonsoft.Json.dll`
   - 右键 → `Reimport`
   - 等待导入完成

4. **强制重新编译**
   - `Assets` → `Reimport All`
   - 或者删除 `Library` 文件夹后重新打开项目

### 方案3：检查脚本编译顺序

如果某些脚本在Newtonsoft.Json可用之前就尝试编译，可能会导致问题：

1. 确保所有使用Newtonsoft.Json的脚本都在 `Assets` 文件夹下
2. 检查是否有编译顺序问题
3. 尝试将Newtonsoft.Json.dll移动到更早的文件夹（按字母顺序）

## 验证方法

### 方法1：检查脚本是否可见
1. 在Hierarchy中创建一个空的GameObject
2. 在Inspector中点击 `Add Component`
3. 搜索 `CozeTextToSpeech` 或 `DoubaoTextToSpeech`
4. 如果能看到这些脚本，说明问题已解决

### 方法2：检查编译错误
1. 打开Console窗口
2. 查看是否有红色错误信息
3. 如果没有错误，说明编译成功

### 方法3：创建测试脚本
创建一个简单的测试脚本来验证Newtonsoft.Json是否可用：

```csharp
using UnityEngine;
using Newtonsoft.Json;

public class TestNewtonsoft : MonoBehaviour
{
    void Start()
    {
        var test = JsonConvert.SerializeObject(new { test = "value" });
        Debug.Log("Newtonsoft.Json可用: " + test);
    }
}
```

如果这个脚本可以编译并运行，说明Newtonsoft.Json配置正确。

## 常见问题

### Q: 为什么BaiduTextToSpeech可以挂载？
A: 因为BaiduTextToSpeech不依赖Newtonsoft.Json，只使用Unity内置库。

### Q: 为什么修改meta文件后还是不行？
A: 可能需要：
1. 重新导入DLL文件
2. 强制重新编译（删除Library文件夹）
3. 或者使用Package Manager安装（更可靠）

### Q: 使用Package Manager安装后，还需要DLL文件吗？
A: 不需要。Package Manager安装的版本会自动管理，可以删除Assets中的DLL文件。

### Q: 如何确认当前使用的是哪个Newtonsoft.Json？
A: 在Console中查看编译信息，或者检查 `Packages` 文件夹中是否有 `com.unity.nuget.newtonsoft-json`。

## 推荐操作流程

1. **首先尝试方案1（Package Manager）**
   - 这是最可靠的方法
   - 自动处理所有依赖关系
   - 不需要手动配置

2. **如果方案1不行，尝试方案2**
   - 检查DLL配置
   - 重新导入文件
   - 强制重新编译

3. **如果还是不行**
   - 检查Unity版本兼容性
   - 查看Unity官方文档
   - 考虑升级Unity版本

