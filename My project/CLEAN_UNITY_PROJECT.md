# Unity 项目清理指南

## 解决 Burst 编译器错误：Assembly-CSharp-Editor 未找到

这个错误通常是因为 Unity 的构建缓存问题。请按照以下步骤操作：

### 方法 1：清理 Library 文件夹（推荐）

1. **关闭 Unity Editor**（如果正在运行）

2. **删除以下文件夹**：
   - `Library` 文件夹
   - `Temp` 文件夹（如果存在）
   - `obj` 文件夹（如果存在）

3. **重新打开 Unity 项目**
   - Unity 会自动重新生成这些文件夹
   - 这可能需要几分钟时间

### 方法 2：在 Unity 中强制重新编译

1. 在 Unity Editor 中：
   - 菜单：`Assets` → `Reimport All`
   - 或者：`Assets` → `Refresh` (Ctrl+R)

2. 如果问题仍然存在，尝试：
   - 菜单：`Edit` → `Preferences` → `External Tools`
   - 点击 `Regenerate project files`

### 方法 3：临时禁用 Burst 编译（如果急需）

如果上述方法都不行，可以临时禁用 Burst：

1. 菜单：`Edit` → `Project Settings` → `Player` → `Other Settings`
2. 找到 `Burst AOT Settings`
3. 取消勾选 `Enable Burst Compilation`

**注意**：禁用 Burst 可能会影响性能，建议使用方法 1 解决问题。

### 为什么会出现这个错误？

- 编译错误修复后，Unity 需要重新生成程序集
- Library 文件夹缓存了旧的编译信息
- Burst 编译器在查找 Editor 程序集时使用了过时的缓存

### 验证修复

清理后，检查 Unity Console：
- 应该不再有 `Assembly-CSharp-Editor` 相关的错误
- 所有脚本应该能正常编译

