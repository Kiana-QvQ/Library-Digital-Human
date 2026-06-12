"""
简单的HTTP服务器，用于接收Unity客户端的请求并调用Ollama聊天功能
Simple HTTP server for receiving Unity client requests and calling Ollama chat functionality
"""

import asyncio
import json
import logging
from http.server import HTTPServer, BaseHTTPRequestHandler
from urllib.parse import urlparse, parse_qs
import threading
import sys
import os

# 添加app目录到Python路径
sys.path.append(os.path.join(os.path.dirname(__file__), 'app'))

from app.brain.llm.ollama_chat import OllamaChat

# 配置日志
logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)

class ChatRequestHandler(BaseHTTPRequestHandler):
    """处理聊天请求的HTTP处理器"""
    
    def __init__(self, *args, ollama_chat=None, **kwargs):
        self.ollama_chat = ollama_chat
        super().__init__(*args, **kwargs)
    
    def do_GET(self):
        """处理GET请求 - 健康检查"""
        if self.path == '/health':
            self.send_response(200)
            self.send_header('Content-type', 'application/json')
            self.send_header('Access-Control-Allow-Origin', '*')
            self.end_headers()
            
            # 检查Ollama服务状态
            health_status = asyncio.run(self.ollama_chat.health_check())
            response = {
                "status": "healthy" if health_status else "unhealthy",
                "ollama_available": health_status
            }
            self.wfile.write(json.dumps(response).encode())
        else:
            self.send_response(404)
            self.end_headers()
    
    def do_POST(self):
        """处理POST请求 - 聊天功能"""
        if self.path == '/chat':
            try:
                # 读取请求数据
                content_length = int(self.headers['Content-Length'])
                post_data = self.rfile.read(content_length)
                request_data = json.loads(post_data.decode('utf-8'))
                
                # 获取用户消息
                user_message = request_data.get('message', '')
                system_prompt = request_data.get('system_prompt', None)
                
                if not user_message:
                    self.send_error_response(400, "消息不能为空")
                    return
                
                # 调用Ollama聊天
                loop = asyncio.new_event_loop()
                asyncio.set_event_loop(loop)
                try:
                    ai_response = loop.run_until_complete(
                        self.ollama_chat.chat(user_message, system_prompt)
                    )
                finally:
                    loop.close()
                
                # 发送响应
                self.send_success_response({
                    "response": ai_response,
                    "user_message": user_message
                })
                
            except json.JSONDecodeError:
                self.send_error_response(400, "无效的JSON数据")
            except Exception as e:
                logger.error(f"处理聊天请求时出错: {str(e)}")
                self.send_error_response(500, f"服务器内部错误: {str(e)}")
        else:
            self.send_response(404)
            self.end_headers()
    
    def do_OPTIONS(self):
        """处理CORS预检请求"""
        self.send_response(200)
        self.send_header('Access-Control-Allow-Origin', '*')
        self.send_header('Access-Control-Allow-Methods', 'GET, POST, OPTIONS')
        self.send_header('Access-Control-Allow-Headers', 'Content-Type')
        self.end_headers()
    
    def send_success_response(self, data):
        """发送成功响应"""
        self.send_response(200)
        self.send_header('Content-type', 'application/json')
        self.send_header('Access-Control-Allow-Origin', '*')
        self.end_headers()
        self.wfile.write(json.dumps(data, ensure_ascii=False).encode('utf-8'))
    
    def send_error_response(self, status_code, message):
        """发送错误响应"""
        self.send_response(status_code)
        self.send_header('Content-type', 'application/json')
        self.send_header('Access-Control-Allow-Origin', '*')
        self.end_headers()
        error_data = {"error": message}
        self.wfile.write(json.dumps(error_data, ensure_ascii=False).encode('utf-8'))
    
    def log_message(self, format, *args):
        """自定义日志格式"""
        logger.info(f"{self.address_string()} - {format % args}")

def create_handler(ollama_chat):
    """创建带有Ollama聊天实例的处理器"""
    def handler(*args, **kwargs):
        return ChatRequestHandler(*args, ollama_chat=ollama_chat, **kwargs)
    return handler

class ChatServer:
    """聊天服务器类"""
    
    def __init__(self, host='localhost', port=8080, ollama_url='http://localhost:11434', model='gemma3:1b'):
        self.host = host
        self.port = port
        self.ollama_chat = OllamaChat(base_url=ollama_url, model=model)
        self.server = None
        self.server_thread = None
    
    async def start_async(self):
        """异步启动服务器"""
        try:
            # 检查Ollama服务是否可用
            if not await self.ollama_chat.health_check():
                logger.warning("⚠️  Ollama服务不可用，请确保Ollama已启动")
                logger.info("💡 您可以继续启动服务器，但聊天功能将不可用")
            else:
                logger.info("✅ Ollama服务连接正常")
            
            # 创建HTTP服务器
            handler = create_handler(self.ollama_chat)
            self.server = HTTPServer((self.host, self.port), handler)
            
            logger.info(f"🚀 聊天服务器已启动")
            logger.info(f"📍 服务器地址: http://{self.host}:{self.port}")
            logger.info(f"🔗 健康检查: http://{self.host}:{self.port}/health")
            logger.info(f"💬 聊天接口: http://{self.host}:{self.port}/chat")
            logger.info("💡 按 Ctrl+C 停止服务器")
            
            # 启动服务器
            self.server.serve_forever()
            
        except KeyboardInterrupt:
            logger.info("👋 服务器正在关闭...")
        except Exception as e:
            logger.error(f"❌ 启动服务器时出错: {str(e)}")
        finally:
            if self.server:
                self.server.shutdown()
    
    def start(self):
        """启动服务器（同步方法）"""
        loop = asyncio.new_event_loop()
        asyncio.set_event_loop(loop)
        try:
            loop.run_until_complete(self.start_async())
        finally:
            loop.close()
    
    def stop(self):
        """停止服务器"""
        if self.server:
            self.server.shutdown()
            logger.info("🛑 服务器已停止")

def main():
    """主函数"""
    import argparse
    
    parser = argparse.ArgumentParser(description="Unity-Ollama聊天服务器")
    parser.add_argument("--host", default="localhost", help="服务器主机地址")
    parser.add_argument("--port", type=int, default=8080, help="服务器端口")
    parser.add_argument("--ollama-url", default="http://localhost:11434", help="Ollama服务地址")
    parser.add_argument("--model", default="gemma3:1b", help="默认模型名称")
    
    args = parser.parse_args()
    
    # 创建并启动服务器
    server = ChatServer(
        host=args.host,
        port=args.port,
        ollama_url=args.ollama_url,
        model=args.model
    )
    
    try:
        server.start()
    except KeyboardInterrupt:
        logger.info("再见！")

if __name__ == "__main__":
    main()
