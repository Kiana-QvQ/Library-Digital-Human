# Unity Virtual Character Dialogue System

## 项目简介
基于Unity的智能虚拟角色对话系统，集成了语音识别、AI对话、语音合成、角色动画等功能。

## 功能特性
- 🎤 **多平台语音识别** - 支持讯飞、百度、Azure语音服务
- 🤖 **AI对话集成** - 集成Coze平台AI对话功能
- 🎭 **角色动画系统** - 唇形同步、眨眼控制、嘴型控制
- 👁️ **视觉追踪** - OpenCV眼部追踪功能
- 🔊 **实时语音交互** - 支持唤醒词检测和实时对话

## 技术栈
- **Unity 2022.3+**
- **C#**
- **OpenCV for Unity**
- **Oculus LipSync**
- **多平台语音API** (讯飞、百度、Azure)
- **Coze AI平台**

## 项目结构
```
Assets/Arsts/Coding/
├── ASR&TTS/          # 语音识别与合成模块
│   ├── Xunfei/       # 讯飞语音服务
│   ├── Baidu/        # 百度语音服务
│   ├── Azure/        # 微软Azure服务
│   └── Wake/         # 唤醒词检测
├── Character/        # 角色动画模块
│   ├── OVRLipSync.cs # 唇形同步
│   ├── Mouth shape.cs # 嘴型控制
│   └── BlinkController.cs # 眨眼控制
├── Chat/             # AI对话模块
│   └── Coze/         # Coze AI客户端
├── Main/             # 主控制模块
│   └── VoiceControlManager.cs # 核心语音控制管理器
└── Opencv/           # 视觉追踪模块
    └── LookingEyes_Fixed.cs # 眼部追踪
```

## 安装说明

### 环境要求
- Unity 2022.3 或更高版本
- Visual Studio 2019 或更高版本
- Git

### 安装步骤
1. **克隆仓库**
   ```bash
   git clone https://github.com/Darling2333/DL_Unity.git
   cd DL_Unity
   ```

2. **使用Unity打开项目**
   - 启动Unity Hub
   - 点击"Add"选择项目文件夹
   - 选择Unity版本2022.3+

3. **配置API密钥**
   - 讯飞语音：在`XunfeiSettings.cs`中配置
   - 百度语音：在`BaiduSettings.cs`中配置
   - Coze AI：在`CozeClient.cs`中配置

4. **运行项目**
   - 打开场景文件
   - 点击Play按钮开始测试

## 配置说明

### 语音服务配置
1. **讯飞语音服务**
   - 在讯飞开放平台申请API密钥
   - 配置`XunfeiSettings.cs`中的相关参数

2. **百度语音服务**
   - 在百度智能云申请语音服务
   - 配置`BaiduSettings.cs`中的相关参数

3. **Coze AI服务**
   - 在Coze平台创建AI助手
   - 配置`CozeClient.cs`中的API密钥和Agent ID

### 角色模型配置
1. **唇形同步**
   - 确保角色模型有正确的BlendShape
   - 配置`Mouth shape.cs`中的BlendShape索引

2. **眼部追踪**
   - 配置OpenCV资源文件
   - 设置摄像头权限

## 使用说明

### 基本功能
1. **语音输入** - 点击录音按钮或使用唤醒词
2. **AI对话** - 系统自动将语音转换为文本并发送给AI
3. **语音输出** - AI回复通过TTS转换为语音播放
4. **角色动画** - 根据语音内容自动控制角色表情和动作

### 高级功能
1. **实时对话** - 支持连续对话模式
2. **唤醒词检测** - 使用"小美小美"等唤醒词激活系统
3. **视觉追踪** - 根据用户面部位置调整角色视线

## 开发说明

### 代码结构
- 采用模块化设计，各功能模块独立
- 使用继承和接口实现多平台支持
- 支持热插拔不同的语音服务提供商

### 扩展开发
1. **添加新的语音服务**
   - 继承`TTS`和`STT`基类
   - 实现相应的接口方法

2. **添加新的AI服务**
   - 继承`LLM`基类
   - 实现对话接口

3. **添加新的动画效果**
   - 在`Character`模块中添加新的控制器

## 常见问题

### Q: 语音识别不准确怎么办？
A: 检查麦克风权限，调整语音识别参数，或尝试不同的语音服务提供商。

### Q: 角色动画不自然？
A: 检查BlendShape配置，调整动画权重和过渡时间。

### Q: AI回复延迟较高？
A: 检查网络连接，优化API调用频率，考虑使用本地AI模型。

## 贡献指南
1. Fork 本仓库
2. 创建特性分支 (`git checkout -b feature/AmazingFeature`)
3. 提交更改 (`git commit -m 'Add some AmazingFeature'`)
4. 推送到分支 (`git push origin feature/AmazingFeature`)
5. 打开 Pull Request

## 许可证
本项目采用 MIT 许可证 - 查看 [LICENSE](LICENSE) 文件了解详情

## 联系方式
- 项目作者：Darling2333
- GitHub：https://github.com/Darling2333
- 项目地址：https://github.com/Darling2333/DL_Unity

## 更新日志
- **v1.0.0** - 初始版本，包含基础语音对话功能
- 支持多平台语音服务
- 集成Coze AI对话
- 实现角色动画系统
