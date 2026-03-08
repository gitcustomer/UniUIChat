using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace UIReuse.AI
{
    /// <summary>
    /// 大语言模型服务接口 - 支持普通对话和流式对话
    /// </summary>
    public interface ILLMService
    {
        /// <summary>
        /// 发送消息并获取完整回复
        /// </summary>
        Task<string> ChatAsync(string message);
        
        /// <summary>
        /// 流式发送消息，通过回调逐步接收响应
        /// </summary>
        /// <param name="message">用户消息</param>
        /// <param name="onChunkReceived">每收到一个文本块时的回调</param>
        /// <param name="onComplete">完成时的回调（传入完整文本）</param>
        /// <param name="onError">错误时的回调</param>
        Task ChatStreamAsync(string message, Action<string> onChunkReceived, Action<string> onComplete = null, Action<Exception> onError = null);
        
        /// <summary>
        /// 流式发送消息（支持取消）
        /// </summary>
        /// <param name="message">用户消息</param>
        /// <param name="onChunkReceived">每收到一个文本块时的回调</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <param name="onComplete">完成时的回调（传入完整文本）</param>
        /// <param name="onError">错误时的回调</param>
        Task ChatStreamAsync(string message, Action<string> onChunkReceived, CancellationToken cancellationToken, Action<string> onComplete = null, Action<Exception> onError = null);
        
        /// <summary>
        /// 是否支持流式响应
        /// </summary>
        bool SupportsStreaming { get; }
        
        /// <summary>
        /// 模型名称
        /// </summary>
        string ModelName { get; }
    }
}