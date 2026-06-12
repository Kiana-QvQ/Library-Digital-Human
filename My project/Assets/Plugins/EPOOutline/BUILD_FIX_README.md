# EPO Outline 打包/运行报错与粉色模型修复

## 现象
- 打包后运行报错：`ArgumentNullException: Value cannot be null. Parameter name: shader`（在 `OutlineEffect.LoadMaterial` / `InitMaterials`）。
- 场景卡住、模型显示为粉色（缺少 Shader）。

## 原因
- 打包时 Unity 会**剥离**未被引用到的 Shader，而 EPO Outline 的 Shader 是通过 `Resources.Load` 按路径加载的，若被剥离则加载为 null，导致报错。
- 粉色说明材质使用的 Shader 未被打进包或加载失败。

## 在本项目或其它项目中的通用解决步骤

### 1. 确保 EPO Outline 的 Shader 始终被打包（必做）

1. 打开 **Edit → Project Settings → Graphics**。
2. 找到 **Always Included Shaders** 列表。
3. 将以下目录中的**所有 `.shader` 文件**拖进该列表（或点 `+` 逐个添加）：
   - `Assets/Plugins/EPOOutline/Resources/Easy performant outline/Shaders/`
   - `Assets/Plugins/EPOOutline/Resources/Easy performant outline/Shaders/Fills/`
4. 保存项目。

这样打包时这些 Shader 不会被剥离，`Resources.Load` 和 `Shader.Find` 都能找到。

### 2. 确保 Resources 参与构建

- `Assets/Plugins/EPOOutline/Resources` 必须在项目中存在且**未被排除在构建之外**（不要从 Build Settings 的 Scenes/Assets 中排除，也不要放在 EditorOnly 等特殊目录）。
- 若“另一个项目”里 EPO Outline 的目录不同，请把对应路径下的 `Resources` 同样保留并加入 Always Included Shaders。

### 3. 代码已做的修改（本项目）

- 在 `OutlineEffect.cs` 中已增加：
  - 先用 `Resources.Load<Shader>`，失败再用 `Shader.Find`（按 Shader 名称）作为回退。
  - 若仍加载不到 Shader，会打 Log 并抛出明确异常，提示去设置 Always Included Shaders。
- **若把 EPO Outline 复制到“另一个项目”**，请一并复制修改后的 `OutlineEffect.cs`，并同样完成上面的 **步骤 1、2**。

### 4. 粉色模型的其它可能原因

- 若**不是** EPO Outline 的物体也发粉，多半是其它材质用的 Shader 被剥离或路径错误：
  - 同样在 **Graphics → Always Included Shaders** 中加入这些 Shader。
  - 或检查材质引用的是否是正确的 Shader、该 Shader 是否在构建的 Unity 版本/管线（URP/Built-in 等）下可用。

## 小结

| 问题           | 处理方式 |
|----------------|----------|
| `ArgumentNullException: shader` | 把 EPO Outline 下所有 Shader 加入 **Always Included Shaders**，并保留代码中的 null 检查与 Shader.Find 回退。 |
| 场景卡住       | 同上，避免 InitMaterials 因 Shader 为 null 抛错。 |
| 模型全粉       | 保证所有用到的 Shader（含 EPO）都在 Always Included Shaders 或显式被场景/预制体引用。 |

在“另一个项目”中：复制本插件（含已改的 `OutlineEffect.cs`）+ 按上述步骤 1、2 设置一次即可。
