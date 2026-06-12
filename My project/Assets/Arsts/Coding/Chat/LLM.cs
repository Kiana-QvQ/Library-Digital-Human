using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using UnityEngine;

public class LLM:MonoBehaviour
{
    /// <summary>
    /// API地址
    /// </summary>
    [SerializeField] protected string url;
    /// <summary>
    /// 提示词，角色信息一般用
    /// </summary>
    [Header("发送的提示词设定")]
    [SerializeField] protected string m_Prompt = string.Empty;
    /// <summary>
    /// 语言
    /// </summary>
    [Header("发送和回复的语言")]
    [SerializeField] protected string lan="中文";
    /// <summary>
    /// 对话历史保留数量
    /// </summary>
    [Header("对话历史保留数量")]
    [SerializeField] protected int m_HistoryKeepCount = 15;
    /// <summary>
    /// 对话历史
    /// </summary>
    [SerializeField] public List<SendData> m_DataList = new List<SendData>();
    /// <summary>
    /// 计算方法运行的时间
    /// </summary>
    [SerializeField] protected Stopwatch stopwatch=new Stopwatch();
    /// <summary>
    /// 发送消息
    /// </summary>
    public virtual void PostMsg(string _msg,Action<string> _callback) {
        //检查历史记录数量
        CheckHistory();
        //提示词处理
        string message = "当前为角色，你的设定是：" + m_Prompt +
            " 回答使用语言：" + lan +
            " 请回答我的问题：" + _msg;

        //保存发送的消息列表
        m_DataList.Add(new SendData("user", message));

        StartCoroutine(Request(message, _callback));
    }

    public virtual IEnumerator Request(string _postWord, System.Action<string> _callback)
    {
        yield return new WaitForEndOfFrame();
          
    }

    /// <summary>
    /// 检查历史记录数量，防止太多
    /// </summary>
    public virtual void CheckHistory()
    {
        if(m_DataList.Count> m_HistoryKeepCount)
        {
            m_DataList.RemoveAt(0);
        }
    }

    [Serializable]
    public class SendData
    {
        [SerializeField] public string role;
        [SerializeField] public string content;
        public SendData() { }
        public SendData(string _role, string _content)
        {
            role = _role;
            content = _content;
        }

    }

}