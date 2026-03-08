using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using UIReuse.AI;

namespace UIReuse.Service
{
    /// <summary>
    /// OpenAI API 服务实现 - 支持普通和流式响应
    /// </summary>
    public class OpenAIService : ILLMService
    {
        private readonly string apiKey;
        private readonly string model;
        private readonly string baseUrl;
        private UnityWebRequest currentRequest;
        
        public string ModelName => model;
        public bool SupportsStreaming => true;
        
        public OpenAIService(AIConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            this.apiKey = config.ApiKey;
            this.model = config.ModelName;
            this.baseUrl = config.BaseUrl;
        }
        
        public OpenAIService(string apiKey, string model = "gpt-3.5-turbo", string baseUrl = "https://api.openai.com/v1")
        {
            this.apiKey = apiKey;
            this.model = model;
            this.baseUrl = baseUrl;
        }
        
        #region API 调用
        
        public async Task<string> ChatAsync(string message)
        {
            if (string.IsNullOrEmpty(apiKey))
                throw new Exception("API Key 未设置");
            
            var requestBody = $@"{{
                ""model"": ""{model}"",
                ""messages"": [{{""role"": ""user"", ""content"": ""{EscapeJson(message)}""}}],
                ""max_tokens"": 2000,
                ""stream"": false
            }}";
            
            var request = new UnityWebRequest($"{baseUrl}/chat/completions", "POST");
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(requestBody));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", $"Bearer {apiKey}");
            
            var operation = request.SendWebRequest();
            while (!operation.isDone) await Task.Yield();
            
            try
            {
                if (request.result == UnityWebRequest.Result.Success)
                    return ExtractContent(request.downloadHandler.text) ?? "无法解析响应";
                else
                    throw new Exception($"请求失败: {request.error}\n{request.downloadHandler.text}");
            }
            finally
            {
                request.Dispose();
            }
        }
        
        public Task ChatStreamAsync(string message, Action<string> onChunkReceived, Action<string> onComplete = null, Action<Exception> onError = null)
        {
            return ChatStreamAsync(message, onChunkReceived, CancellationToken.None, onComplete, onError);
        }
        
        public async Task ChatStreamAsync(string message, Action<string> onChunkReceived, CancellationToken cancellationToken, Action<string> onComplete = null, Action<Exception> onError = null)
        {
            if (string.IsNullOrEmpty(apiKey))
            {
                onError?.Invoke(new Exception("API Key 未设置"));
                return;
            }
            
            var requestBody = $@"{{
                ""model"": ""{model}"",
                ""messages"": [{{""role"": ""user"", ""content"": ""{EscapeJson(message)}""}}],
                ""max_tokens"": 2000,
                ""stream"": true
            }}";
            
            currentRequest = new UnityWebRequest($"{baseUrl}/chat/completions", "POST");
            currentRequest.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(requestBody));
            currentRequest.downloadHandler = new StreamingDownloadHandler(onChunkReceived);
            currentRequest.SetRequestHeader("Content-Type", "application/json");
            currentRequest.SetRequestHeader("Authorization", $"Bearer {apiKey}");
            currentRequest.SetRequestHeader("Accept", "text/event-stream");
            
            var operation = currentRequest.SendWebRequest();
            
            try
            {
                while (!operation.isDone)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        currentRequest.Abort();
                        onComplete?.Invoke((currentRequest.downloadHandler as StreamingDownloadHandler)?.FullText ?? "");
                        return;
                    }
                    await Task.Yield();
                }
                
                if (currentRequest.result == UnityWebRequest.Result.Success)
                {
                    var handler = currentRequest.downloadHandler as StreamingDownloadHandler;
                    onComplete?.Invoke(handler?.FullText ?? "");
                }
                else if (currentRequest.result != UnityWebRequest.Result.ConnectionError || !cancellationToken.IsCancellationRequested)
                {
                    onError?.Invoke(new Exception($"请求失败: {currentRequest.error}"));
                }
            }
            catch (Exception ex)
            {
                if (!cancellationToken.IsCancellationRequested)
                    onError?.Invoke(ex);
            }
            finally
            {
                currentRequest?.Dispose();
                currentRequest = null;
            }
        }
        
        public void CancelCurrentRequest()
        {
            if (currentRequest != null && !currentRequest.isDone)
                currentRequest.Abort();
        }
        
        #endregion
        
        private string EscapeJson(string text)
        {
            return text
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t");
        }
        
        private string ExtractContent(string json)
        {
            var contentKey = "\"content\":\"";
            var startIndex = json.IndexOf(contentKey);
            if (startIndex < 0) return null;
            
            startIndex += contentKey.Length;
            var endIndex = startIndex;
            
            while (endIndex < json.Length)
            {
                var c = json[endIndex];
                if (c == '\\' && endIndex + 1 < json.Length) { endIndex += 2; continue; }
                if (c == '"') break;
                endIndex++;
            }
            
            return UnescapeJson(json.Substring(startIndex, endIndex - startIndex));
        }
        
        private string UnescapeJson(string text)
        {
            return text
                .Replace("\\n", "\n")
                .Replace("\\r", "\r")
                .Replace("\\t", "\t")
                .Replace("\\\"", "\"")
                .Replace("\\\\", "\\");
        }
    }
    
    /// <summary>
    /// 流式下载处理器 - 处理 SSE (Server-Sent Events) 响应
    /// </summary>
    internal class StreamingDownloadHandler : DownloadHandlerScript
    {
        private readonly Action<string> onChunkReceived;
        private readonly StringBuilder fullTextBuilder = new StringBuilder();
        private string buffer = "";
        
        public string FullText => fullTextBuilder.ToString();
        
        public StreamingDownloadHandler(Action<string> onChunkReceived) : base()
        {
            this.onChunkReceived = onChunkReceived;
        }
        
        protected override bool ReceiveData(byte[] data, int dataLength)
        {
            if (data == null || dataLength == 0) return true;
            
            var text = Encoding.UTF8.GetString(data, 0, dataLength);
            buffer += text;
            
            var lines = buffer.Split('\n');
            buffer = lines[lines.Length - 1];
            
            for (int i = 0; i < lines.Length - 1; i++)
            {
                var line = lines[i].Trim();
                if (string.IsNullOrEmpty(line) || line == "data: [DONE]") continue;
                
                if (line.StartsWith("data: "))
                {
                    var content = ExtractDeltaContent(line.Substring(6));
                    if (!string.IsNullOrEmpty(content))
                    {
                        fullTextBuilder.Append(content);
                        onChunkReceived?.Invoke(content);
                    }
                }
            }
            
            return true;
        }
        
        private string ExtractDeltaContent(string json)
        {
            var contentKey = "\"content\":\"";
            var startIndex = json.IndexOf(contentKey);
            if (startIndex < 0) return null;
            
            startIndex += contentKey.Length;
            var endIndex = startIndex;
            
            while (endIndex < json.Length)
            {
                var c = json[endIndex];
                if (c == '\\' && endIndex + 1 < json.Length) { endIndex += 2; continue; }
                if (c == '"') break;
                endIndex++;
            }
            
            return json.Substring(startIndex, endIndex - startIndex)
                .Replace("\\n", "\n")
                .Replace("\\r", "\r")
                .Replace("\\t", "\t")
                .Replace("\\\"", "\"")
                .Replace("\\\\", "\\");
        }
    }
}
