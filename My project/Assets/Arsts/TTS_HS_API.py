#coding=utf-8
import base64
import os
import requests
import asyncio
import websockets
import uuid
import json
import gzip
import copy

# 公共配置
host = "https://openspeech.bytedance.com"
ws_host = "openspeech.bytedance.com"
appid = "6466234952"
token = "KfkwQs3UBxzNnpSBlqxWZkzdKbwpZptA"
cluster = "volcano_icl"

# 语音克隆相关函数
def voice_clone_train(audio_path, spk_id):
    """语音克隆训练函数"""
    url = f"{host}/api/v1/mega_tts/audio/upload"
    headers = {
        "Content-Type": "application/json",
        "Authorization": f"Bearer;{token}",
        "Resource-Id": "volc.megatts.voiceclone",
    }
    
    # 编码音频文件
    with open(audio_path, 'rb') as audio_file:
        audio_data = audio_file.read()
        encoded_data = str(base64.b64encode(audio_data), "utf-8")
        audio_format = os.path.splitext(audio_path)[1][1:]
    
    data = {
        "appid": appid,
        "speaker_id": spk_id,
        "audios": [{"audio_bytes": encoded_data, "audio_format": audio_format}],
        "source": 2,
        "language": 0,
        "model_type": 1
    }
    
    response = requests.post(url, json=data, headers=headers)
    print(f"克隆训练状态码: {response.status_code}")
    if response.status_code != 200:
        raise Exception(f"克隆训练错误: {response.text}")
    return response.json()


def voice_clone_get_status(spk_id):
    """查询语音克隆状态函数"""
    url = f"{host}/api/v1/mega_tts/status"
    headers = {
        "Content-Type": "application/json",
        "Authorization": f"Bearer;{token}",
        "Resource-Id": "volc.megatts.voiceclone",
    }
    body = {"appid": appid, "speaker_id": spk_id}
    response = requests.post(url, headers=headers, json=body)
    return response.json()


# 语音合成相关常量与函数
MESSAGE_TYPES = {11: "audio-only server response", 12: "frontend server response", 15: "error message from server"}
MESSAGE_TYPE_SPECIFIC_FLAGS = {0: "no sequence number", 1: "sequence number > 0",
                               2: "last message from server (seq < 0)", 3: "sequence number < 0"}
MESSAGE_SERIALIZATION_METHODS = {0: "no serialization", 1: "JSON", 15: "custom type"}
MESSAGE_COMPRESSIONS = {0: "no compression", 1: "gzip", 15: "custom compression method"}
default_header = bytearray(b'\x11\x10\x11\x00')
api_url = f"wss://{ws_host}/api/v1/tts/ws_binary"

request_json = {
    "app": {
        "appid": appid,
        "token": token,
        "cluster": cluster
    },
    "user": {
        "uid": "388808087185088"
    },
    "audio": {
        "voice_type": "",
        "encoding": "mp3",
        "speed_ratio": 1.0,
        "volume_ratio": 1.0,
        "pitch_ratio": 1.0,
    },
    "request": {
        "reqid": "",
        "text": "",
        "text_type": "plain",
        "operation": "submit"
    }
}


def parse_response(res, file):
    """解析WebSocket响应"""
    protocol_version = res[0] >> 4
    header_size = res[0] & 0x0f
    message_type = res[1] >> 4
    message_type_specific_flags = res[1] & 0x0f
    serialization_method = res[2] >> 4
    message_compression = res[2] & 0x0f
    reserved = res[3]
    header_extensions = res[4:header_size*4]
    payload = res[header_size*4:]

    if message_type == 0xb:  # 音频响应
        if message_type_specific_flags == 0:
            return False
        sequence_number = int.from_bytes(payload[:4], "big", signed=True)
        payload = payload[8:]
        file.write(payload)
        return sequence_number < 0  # 最后一段音频
    elif message_type == 0xf:  # 错误信息
        code = int.from_bytes(payload[:4], "big", signed=False)
        msg_size = int.from_bytes(payload[4:8], "big", signed=False)
        error_msg = payload[8:]
        if message_compression == 1:
            error_msg = gzip.decompress(error_msg)
        print(f"合成错误: {str(error_msg, 'utf-8')}")
        return True
    return False


async def text_to_speech(text, voice_type, output_file="output.mp3"):
    """语音合成函数"""
    submit_request = copy.deepcopy(request_json)
    submit_request["audio"]["voice_type"] = voice_type
    submit_request["request"]["reqid"] = str(uuid.uuid4())
    submit_request["request"]["operation"] = "submit"
    submit_request["request"]["text"] = text

    # 处理请求数据
    payload_bytes = json.dumps(submit_request).encode()
    payload_bytes = gzip.compress(payload_bytes)
    full_request = bytearray(default_header)
    full_request.extend(len(payload_bytes).to_bytes(4, 'big'))
    full_request.extend(payload_bytes)

    with open(output_file, "wb") as f:
        header = {"Authorization": f"Bearer; {token}"}
        async with websockets.connect(api_url, extra_headers=header, ping_interval=None) as ws:
            await ws.send(full_request)
            while True:
                res = await ws.recv()
                done = parse_response(res, f)
                if done:
                    break
    print(f"合成完成，音频已保存至: {output_file}")


# 单独调用示例
if __name__ == "__main__":
    # 配置参数
    speaker_id = "S_JgwRuERK1"
    audio_path = "1761723696_1.wav"
    tts_text = "这是一个语音合成测试，中午吃什么。"
    output_audio = "synthesized.mp3"

    # 单独调用语音克隆训练
    # clone_result = voice_clone_train(audio_path, speaker_id)
    # print("克隆训练结果:", clone_result)

    # 单独调用克隆状态查询
    # status = voice_clone_get_status(speaker_id)
    # print("克隆状态:", status)

    # 单独调用语音合成（需要先完成克隆获得可用的voice_type）
    loop = asyncio.get_event_loop()
    loop.run_until_complete(text_to_speech(tts_text, speaker_id, output_audio))