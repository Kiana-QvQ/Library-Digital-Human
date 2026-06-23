"""LLM 调用异常"""


class LLMRequestError(Exception):
    """大模型上游请求失败（不应将错误文本写入对话历史）"""

    def __init__(self, message: str, *, status_code: int = 502):
        super().__init__(message)
        self.message = message
        self.status_code = status_code
