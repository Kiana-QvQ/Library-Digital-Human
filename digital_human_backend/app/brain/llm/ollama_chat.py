"""
Ollama本地大模型对话
Simple Ollama Local LLM Chat
"""

import requests
import json
import asyncio
import aiohttp
from typing import Dict, Any, Optional, List
import logging

logger = logging.getLogger(__name__)

class OllamaChat:
    """Ollama聊天类"""
    
    def __init__(self, base_url: str = "http://localhost:11434", model: str = "gemma3:1b"):
        """
        初始化Ollama聊天
        
        Args:
            base_url: Ollama服务地址
            model: 默认模型名称
        """
        self.base_url = base_url.rstrip('/')
        self.model = model
        self.conversation_history = []
    
    async def chat(self, message: str, system_prompt: Optional[str] = None, model: Optional[str] = None) -> str:
        """
        发送消息并获取回复
        
        Args:
            message: 用户消息
            system_prompt: 系统提示词
            
        Returns:
            AI回复
        """
        try:
            # 构建消息列表
            messages = []
            
            # 添加系统提示词
            if system_prompt:
                messages.append({"role": "system", "content": system_prompt})
            
            # 添加对话历史
            messages.extend(self.conversation_history)
            
            # 添加当前用户消息
            messages.append({"role": "user", "content": message})
            
            # 发送请求到Ollama
            payload = {
                "model": model or self.model,
                "messages": messages,
                "stream": False
            }
            
            async with aiohttp.ClientSession() as session:
                async with session.post(
                    f"{self.base_url}/api/chat",
                    json=payload
                ) as response:
                    if response.status == 200:
                        data = await response.json()
                        ai_response = data.get("message", {}).get("content", "")
                        
                        # 更新对话历史
                        self.conversation_history.append({"role": "user", "content": message})
                        self.conversation_history.append({"role": "assistant", "content": ai_response})
                        
                        # 保持历史记录在合理范围内（最近10轮对话）
                        if len(self.conversation_history) > 20:
                            self.conversation_history = self.conversation_history[-20:]
                        
                        return ai_response
                    else:
                        error_text = await response.text()
                        logger.error(f"Ollama请求失败: {response.status}, {error_text}")
                        return f"抱歉，我遇到了一些问题。错误代码: {response.status}"
                        
        except Exception as e:
            logger.error(f"聊天时出错: {str(e)}")
            return f"抱歉，我遇到了一些问题: {str(e)}"
    
    def clear_history(self):
        """清除对话历史"""
        self.conversation_history = []
        logger.info("对话历史已清除")
    
    def set_model(self, model_name: str):
        """设置模型"""
        self.model = model_name
        logger.info(f"模型已切换为: {model_name}")
    
    async def get_available_models(self) -> List[str]:
        """获取可用模型列表"""
        try:
            async with aiohttp.ClientSession() as session:
                async with session.get(f"{self.base_url}/api/tags") as response:
                    if response.status == 200:
                        data = await response.json()
                        models = [model['name'] for model in data.get('models', [])]
                        return models
                    else:
                        logger.error(f"获取模型列表失败: {response.status}")
                        return []
        except Exception as e:
            logger.error(f"获取模型列表时出错: {str(e)}")
            return []
    
    async def health_check(self) -> bool:
        """健康检查"""
        try:
            async with aiohttp.ClientSession() as session:
                async with session.get(f"{self.base_url}/api/tags") as response:
                    return response.status == 200
        except Exception as e:
            logger.error(f"健康检查失败: {str(e)}")
            return False

# 使用示例
async def main():
    """主函数示例"""
    # 创建聊天实例
    chat = OllamaChat(model="gemma3:1b")
    
    # 检查Ollama服务是否可用
    if not await chat.health_check():
        print("❌ Ollama服务不可用，请确保Ollama已启动")
        return
    
    print("🤖 Ollama聊天机器人已启动")
    print("💡 输入 'quit' 或 'exit' 退出")
    print("💡 输入 'clear' 清除对话历史")
    print("💡 输入 'models' 查看可用模型")
    print("-" * 50)
    
    # 获取可用模型
    models = await chat.get_available_models()
    if models:
        print(f"📋 可用模型: {', '.join(models)}")
    else:
        print("⚠️  未找到可用模型，请先下载模型")
    
    print("-" * 50)
    
    while True:
        try:
            # 获取用户输入
            user_input = input("\n👤 您: ").strip()
            
            if not user_input:
                continue
            
            # 处理特殊命令
            if user_input.lower() in ['quit', 'exit', '退出']:
                print("👋 再见！")
                break
            elif user_input.lower() in ['clear', '清除']:
                chat.clear_history()
                print("🧹 对话历史已清除")
                continue
            elif user_input.lower() in ['models', '模型']:
                models = await chat.get_available_models()
                if models:
                    print(f"📋 可用模型: {', '.join(models)}")
                else:
                    print("⚠️  未找到可用模型")
                continue
            elif user_input.startswith('model:'):
                # 切换模型
                new_model = user_input[6:].strip()
                chat.set_model(new_model)
                print(f"🔄 模型已切换为: {new_model}")
                continue
            
            # 发送消息并获取回复
            print("🤖 AI: ", end="", flush=True)
            response = await chat.chat(user_input)
            print(response)
            
        except KeyboardInterrupt:
            print("\n👋 再见！")
            break
        except Exception as e:
            print(f"❌ 发生错误: {str(e)}")

if __name__ == "__main__":
    # 运行聊天程序
    asyncio.run(main())
