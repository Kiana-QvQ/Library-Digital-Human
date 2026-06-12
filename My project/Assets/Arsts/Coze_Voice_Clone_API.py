#coding=utf-8
"""
Coze 声音复刻 API 测试脚本
用于测试 Coze 语音克隆功能
"""

import os
import requests
import json

# 公共配置
api_key = "your_coze_api_key_here"  # 替换为你的 Coze API Key
host = "https://api.coze.cn"
clone_endpoint = f"{host}/v1/audio/voices/clone"
list_voices_endpoint = f"{host}/v1/audio/voices"

# 语音克隆相关函数
def voice_clone_train(audio_path, voice_name, text=None, language="zh", 
                     voice_id=None, preview_text=None, space_id=None):
    """
    语音克隆训练函数
    
    参数:
        audio_path: 音频文件路径（支持 wav、mp3、ogg、m4a、aac、pcm）
        voice_name: 音色名称（必选，长度限制128字节）
        text: 音频文件对应的文案（可选，最大1024字节）
        language: 语种（可选：zh/en/ja/es/id/pt，默认zh）
        voice_id: 需要训练的音色ID（可选，用于重新训练）
        preview_text: 预览音频的文案（可选）
        space_id: 工作空间ID（可选）
    
    返回:
        dict: 克隆结果
    """
    url = clone_endpoint
    
    # 检查文件是否存在
    if not os.path.exists(audio_path):
        raise FileNotFoundError(f"音频文件不存在: {audio_path}")
    
    # 检查文件大小（最大10MB）
    file_size = os.path.getsize(audio_path)
    if file_size > 10 * 1024 * 1024:
        raise ValueError(f"音频文件过大 ({file_size / 1024 / 1024:.2f}MB)，最大支持10MB")
    
    # 获取文件扩展名作为格式
    audio_format = os.path.splitext(audio_path)[1][1:].lower()
    if not audio_format:
        audio_format = "wav"  # 默认格式
    
    # 支持的格式
    supported_formats = ["wav", "mp3", "ogg", "m4a", "aac", "pcm"]
    if audio_format not in supported_formats:
        raise ValueError(f"不支持的音频格式: {audio_format}，支持格式: {', '.join(supported_formats)}")
    
    # 构建multipart form data
    files = {
        'file': (f'audio.{audio_format}', open(audio_path, 'rb'), f'audio/{audio_format}')
    }
    
    data = {
        'voice_name': voice_name,
        'audio_format': audio_format
    }
    
    # 可选参数
    if text:
        # 检查文案长度（最大1024字节）
        text_bytes = text.encode('utf-8')
        if len(text_bytes) > 1024:
            print(f"警告: 文案长度 ({len(text_bytes)} 字节) 超过1024字节限制，将被截断")
            text = text_bytes[:1024].decode('utf-8', errors='ignore')
        data['text'] = text
    
    if language:
        data['language'] = language
    
    if voice_id:
        data['voice_id'] = voice_id
    
    if preview_text:
        data['preview_text'] = preview_text
    
    if space_id:
        data['space_id'] = space_id
    
    headers = {
        "Authorization": f"Bearer {api_key}"
    }
    
    print(f"开始克隆音色...")
    print(f"  文件: {audio_path}")
    print(f"  格式: {audio_format}")
    print(f"  大小: {file_size / 1024:.2f} KB")
    print(f"  音色名称: {voice_name}")
    if text:
        print(f"  文案: {text[:50]}...")
    
    try:
        response = requests.post(url, files=files, data=data, headers=headers)
        files['file'][1].close()  # 关闭文件
        
        print(f"克隆训练状态码: {response.status_code}")
        
        if response.status_code != 200:
            print(f"错误响应: {response.text}")
            
            # 尝试解析错误信息
            try:
                error_data = response.json()
                error_code = error_data.get('code', '未知')
                error_msg = error_data.get('msg', '未知错误')
                
                # 特殊处理配额不足错误
                if error_code == 4032 or 'quota' in error_msg.lower() or '配额' in error_msg:
                    print("\n" + "=" * 80)
                    print("❌ 配额不足错误")
                    print("=" * 80)
                    print(f"错误码: {error_code}")
                    print(f"错误信息: {error_msg}")
                    print("\n可能的原因:")
                    print("  1. 语音克隆资源包配额已用完")
                    print("  2. 未购买语音克隆资源包")
                    print("  3. 账户没有足够的语音克隆额度")
                    print("\n解决方案:")
                    print("  1. 前往 Coze 控制台查看配额使用情况")
                    print("  2. 购买或续费语音克隆资源包")
                    print("  3. 联系 Coze 客服申请增加配额")
                    print("  4. 或者使用 Doubao（火山引擎）的语音克隆功能作为替代")
                    print("=" * 80)
                # 处理权限错误
                elif error_code == 403 or 'permission' in error_msg.lower() or '权限' in error_msg:
                    print("\n" + "=" * 80)
                    print("❌ 权限不足错误")
                    print("=" * 80)
                    print(f"错误码: {error_code}")
                    print(f"错误信息: {error_msg}")
                    print("\n可能的原因:")
                    print("  1. API Key 没有 createVoice 权限")
                    print("  2. 未在 Coze 控制台中开通语音克隆功能")
                    print("  3. 账户没有使用语音克隆的权限")
                    print("\n解决方案:")
                    print("  1. 检查 API Key 是否有 createVoice 权限")
                    print("  2. 在 Coze 控制台中开通语音克隆功能")
                    print("  3. 联系 Coze 客服申请权限")
                    print("=" * 80)
                else:
                    print(f"\n错误码: {error_code}")
                    print(f"错误信息: {error_msg}")
            except:
                pass  # 如果无法解析JSON，使用原始错误信息
            
            raise Exception(f"克隆训练错误: {response.text}")
        
        result = response.json()
        return result
    
    except Exception as e:
        if 'file' in files:
            files['file'][1].close()
        raise e


def list_voices(filter_system_voice=None, model_type=None, voice_state=None, 
                page_num=1, page_size=100):
    """
    查询音色列表
    
    参数:
        filter_system_voice: 是否过滤系统音色（True/False）
        model_type: 模型类型（big/small）
        voice_state: 音色状态（init/cloned/all）
        page_num: 页码（最小值为1）
        page_size: 每页数量（1~100）
    
    返回:
        dict: 音色列表
    """
    url = list_voices_endpoint
    
    params = {
        'page_num': page_num,
        'page_size': page_size
    }
    
    if filter_system_voice is not None:
        params['filter_system_voice'] = str(filter_system_voice).lower()
    
    if model_type:
        params['model_type'] = model_type
    
    if voice_state:
        params['voice_state'] = voice_state
    
    headers = {
        "Authorization": f"Bearer {api_key}",
        "Content-Type": "application/json"
    }
    
    print(f"查询音色列表...")
    print(f"  参数: {params}")
    
    response = requests.get(url, params=params, headers=headers)
    
    print(f"查询状态码: {response.status_code}")
    
    if response.status_code != 200:
        print(f"错误响应: {response.text}")
        raise Exception(f"查询音色列表错误: {response.text}")
    
    result = response.json()
    return result


def get_voice_info(voice_id):
    """
    根据音色ID获取音色信息（通过查询所有音色列表）
    
    参数:
        voice_id: 音色ID
    
    返回:
        dict: 音色信息，如果未找到返回None
    """
    try:
        # 查询所有音色
        result = list_voices(voice_state="all")
        
        if result.get('code') == 0 and result.get('data'):
            voice_list = result['data'].get('voice_list', [])
            for voice in voice_list:
                if voice.get('voice_id') == voice_id:
                    return voice
        
        return None
    except Exception as e:
        print(f"查询音色信息失败: {e}")
        return None


# 单独调用示例
if __name__ == "__main__":
    # ========== 配置参数 ==========
    # 替换为你的实际配置
    api_key = "pat_UDkT4W9EmMA6GAgX5o5ZBpeJnOL9VYB1CLEe8DLZ5pmSXB754qfRp8vsn8W5ITwu"  # 替换为你的API Key
    audio_path = "Coze_test.mp3"  # 替换为你的音频文件路径
    voice_name = "申鹤测试音色"  # 音色名称
    text = "这是测试音频对应的文案"  # 音频对应的文案（可选）
    language = "zh"  # 语种：zh/en/ja/es/id/pt
    preview_text = "你好，我是你的专属AI克隆声音，希望未来可以一起好好相处。"  # 预览文案（可选）
    space_id = None  # 工作空间ID（可选）
    
    # ========== 测试语音克隆 ==========
    print("=" * 80)
    print("测试 Coze 语音克隆功能")
    print("=" * 80)
    
    try:
        # 检查API Key
        if api_key == "your_coze_api_key_here" or not api_key:
            print("错误: 请先设置 api_key 变量")
            exit(1)
        
        # 检查音频文件
        if not os.path.exists(audio_path):
            print(f"错误: 音频文件不存在: {audio_path}")
            print("提示: 请将音频文件放在脚本同目录下，或使用绝对路径")
            exit(1)
        
        # 执行克隆训练
        print("\n[1] 开始语音克隆训练...")
        clone_result = voice_clone_train(
            audio_path=audio_path,
            voice_name=voice_name,
            text=text,
            language=language,
            preview_text=preview_text,
            space_id=space_id
        )
        
        print("\n克隆训练结果:")
        print(json.dumps(clone_result, indent=2, ensure_ascii=False))
        
        # 检查结果
        if clone_result.get('code') == 0:
            voice_id = clone_result.get('data', {}).get('voice_id')
            if voice_id:
                print(f"\n✅ 克隆成功！")
                print(f"音色ID: {voice_id}")
                print(f"请保存此音色ID，后续语音合成时需要用到")
                
                # 查询音色信息
                print("\n[2] 查询音色信息...")
                voice_info = get_voice_info(voice_id)
                if voice_info:
                    print("音色信息:")
                    print(json.dumps(voice_info, indent=2, ensure_ascii=False))
                else:
                    print("提示: 音色可能还在训练中，请稍后再查询")
            else:
                print("\n⚠️ 克隆请求已提交，但未返回音色ID")
        else:
            error_code = clone_result.get('code')
            error_msg = clone_result.get('msg', '未知错误')
            print(f"\n❌ 克隆失败 (Code: {error_code}): {error_msg}")
            
            # 检查是否是配额不足问题
            if error_code == 4032 or 'quota' in error_msg.lower() or '配额' in error_msg:
                print("\n" + "=" * 80)
                print("⚠️ 配额不足提示")
                print("=" * 80)
                print("当前账户的语音克隆资源包配额已用完或未购买。")
                print("\n解决方案:")
                print("  1. 前往 Coze 控制台查看配额使用情况")
                print("     https://www.coze.cn -> 控制台 -> 配额管理")
                print("  2. 购买或续费语音克隆资源包")
                print("  3. 联系 Coze 客服申请增加配额")
                print("  4. 或者使用 Doubao（火山引擎）的语音克隆功能作为替代")
                print("     参考: TTS_HS_API.py")
                print("=" * 80)
            # 检查是否是权限问题
            elif error_code == 403 or "权限" in error_msg or "permission" in error_msg.lower():
                print("\n提示: 可能是权限不足，请检查:")
                print("  1. API Key 是否有 createVoice 权限")
                print("  2. 是否在 Coze 控制台中开通了语音克隆功能")
                print("  3. 账户是否有使用语音克隆的权限")
    
    except FileNotFoundError as e:
        print(f"\n❌ 文件错误: {e}")
    except ValueError as e:
        print(f"\n❌ 参数错误: {e}")
    except Exception as e:
        error_str = str(e)
        print(f"\n❌ 发生错误: {error_str}")
        
        # 检查是否是配额不足错误
        if '4032' in error_str or 'quota' in error_str.lower() or '配额' in error_str:
            print("\n" + "=" * 80)
            print("⚠️ 配额不足提示")
            print("=" * 80)
            print("当前账户的语音克隆资源包配额已用完或未购买。")
            print("\n解决方案:")
            print("  1. 前往 Coze 控制台查看配额使用情况")
            print("     https://www.coze.cn -> 控制台 -> 配额管理")
            print("  2. 购买或续费语音克隆资源包")
            print("  3. 联系 Coze 客服申请增加配额")
            print("  4. 或者使用 Doubao（火山引擎）的语音克隆功能作为替代")
            print("     参考: TTS_HS_API.py")
            print("=" * 80)
        else:
            # 其他错误显示完整堆栈
            import traceback
            traceback.print_exc()
    
    # ========== 测试音色列表查询 ==========
    print("\n" + "=" * 80)
    print("测试 Coze 音色列表查询功能")
    print("=" * 80)
    
    try:
        print("\n[3] 查询音色列表...")
        voices_result = list_voices(
            filter_system_voice=False,  # 不过滤系统音色
            model_type=None,  # 所有模型类型
            voice_state="cloned",  # 已克隆的音色
            page_num=1,
            page_size=10
        )
        
        print("\n音色列表查询结果:")
        print(json.dumps(voices_result, indent=2, ensure_ascii=False))
        
        if voices_result.get('code') == 0:
            voice_list = voices_result.get('data', {}).get('voice_list', [])
            print(f"\n✅ 查询成功！找到 {len(voice_list)} 个音色")
            
            if voice_list:
                print("\n音色列表:")
                for i, voice in enumerate(voice_list, 1):
                    print(f"  [{i}] {voice.get('name', '未知')}")
                    print(f"      ID: {voice.get('voice_id', '未知')}")
                    print(f"      类型: {voice.get('model_type', '未知')}")
                    print(f"      系统音色: {voice.get('is_system_voice', False)}")
                    print(f"      状态: {voice.get('state', '未知')}")
                    if voice.get('preview_audio'):
                        print(f"      预览音频: {voice.get('preview_audio', '')[:50]}...")
                    print()
        else:
            error_code = voices_result.get('code')
            error_msg = voices_result.get('msg', '未知错误')
            print(f"\n❌ 查询失败 (Code: {error_code}): {error_msg}")
    
    except Exception as e:
        print(f"\n❌ 发生错误: {e}")
        import traceback
        traceback.print_exc()
    
    print("\n" + "=" * 80)
    print("测试完成")
    print("=" * 80)
