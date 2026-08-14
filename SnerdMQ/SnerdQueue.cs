using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace SnerdMQ
{
    public class SnerdQueue : IDisposable
    {
        private readonly string _binaryPath;
        private readonly string _storagePath;
        private readonly ConcurrentDictionary<string, Func<string, Task>> _handlers = new();
        
        private Process _process;
        private StreamWriter _writer;
        private CancellationTokenSource _cts = new();

        public SnerdQueue(string binaryPath = null, string storagePath = null)
        {
            _binaryPath = binaryPath ?? SnerdmqInstaller.EnsureDownloadedAsync().GetAwaiter().GetResult();
            _storagePath = storagePath;

            if (string.IsNullOrEmpty(_binaryPath))
            {
                throw new InvalidOperationException("[Snerd] Binary path cannot be null. Installer failed or path not provided.");
            }
        }

        public void RegisterHandler(string taskType, Func<string, Task> callback)
        {
            _handlers[taskType] = callback;
            if (_process != null && !_process.HasExited)
            {
                SendMessage($"{{\"action\":\"register\",\"task_type\":\"{taskType}\"}}");
            }
        }

        public void RegisterHandler(string taskType, Action<string> callback)
        {
            _handlers[taskType] = (data) =>
            {
                callback(data);
                return Task.CompletedTask;
            };
        }

        public void StartListening()
        {
            var psi = new ProcessStartInfo
            {
                FileName = _binaryPath,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            if (!string.IsNullOrEmpty(_storagePath))
            {
                psi.Arguments = $"\"{_storagePath}\"";
            }

            _process = new Process { StartInfo = psi };
            _process.Start();

            _writer = _process.StandardInput;

            // Start reading stdout asynchronously in a fire-and-forget Task
            _ = Task.Run(ReadStdoutAsync, _cts.Token);

            foreach (var taskType in _handlers.Keys)
            {
                SendMessage($"{{\"action\":\"register\",\"task_type\":\"{taskType}\"}}");
            }
        }

        public void Enqueue(string taskId, string taskType, string jsonData, int maxRetries, double retryAfterHours)
        {
            Enqueue(taskId, taskType, jsonData, maxRetries, retryAfterHours, null, null);
        }

        public void Enqueue(string taskId, string taskType, string jsonData, int maxRetries, double retryAfterHours, string rateLimitGroup, int? maxPerMinute)
        {
            if (_process == null || _process.HasExited)
            {
                throw new InvalidOperationException("[Snerd] Cannot enqueue task: Queue is not running.");
            }

            string escapedJson = jsonData.Replace("\"", "\\\"");
            
            var sb = new System.Text.StringBuilder();
            sb.Append($"{{\"action\":\"enqueue\",\"task_id\":\"{taskId}\",\"task_type\":\"{taskType}\",\"task_data\":\"{escapedJson}\",\"max_retries\":{maxRetries},\"retry_after_hours\":{retryAfterHours.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
            
            if (rateLimitGroup != null)
            {
                sb.Append($",\"rate_limit_group\":\"{rateLimitGroup}\"");
            }
            if (maxPerMinute.HasValue)
            {
                sb.Append($",\"max_per_minute\":{maxPerMinute.Value}");
            }
            sb.Append("}");
            
            SendMessage(sb.ToString());
        }

        private void SendMessage(string json)
        {
            if (_writer == null) return;
            lock (_writer)
            {
                _writer.WriteLine(json);
                _writer.Flush();
            }
        }

        private async Task ReadStdoutAsync()
        {
            try
            {
                while (!_cts.Token.IsCancellationRequested && !_process.StandardOutput.EndOfStream)
                {
                    string line = await _process.StandardOutput.ReadLineAsync();
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        ProcessLine(line.Trim());
                    }
                }
            }
            catch (Exception ex)
            {
                if (!_cts.Token.IsCancellationRequested)
                    Console.Error.WriteLine($"[Snerd] Background Reader Exception: {ex.Message}");
            }
        }

        private void ProcessLine(string line)
        {
            // Lightweight RegEx parsing to avoid external dependencies like Newtonsoft.Json
            string action = ExtractJsonField(line, "action");
            if (action == null) return;

            if (action == "execute")
            {
                string taskId = ExtractJsonField(line, "task_id");
                string taskType = ExtractJsonField(line, "task_type");
                string taskData = ExtractJsonField(line, "task_data");

                if (taskId == null || taskType == null) return;

                if (!_handlers.TryGetValue(taskType, out var handler))
                {
                    SendMessage($"{{\"action\":\"result\",\"task_id\":\"{taskId}\",\"status\":\"error\",\"error_msg\":\"No handler registered\"}}");
                    return;
                }

                // Fire-and-forget the user's workload onto the ThreadPool so we don't block stdout
                _ = Task.Run(async () =>
                {
                    try
                    {
                        string unescapedData = taskData != null ? taskData.Replace("\\\"", "\"").Replace("\\\\", "\\") : "";
                        await handler(unescapedData);
                        SendMessage($"{{\"action\":\"result\",\"task_id\":\"{taskId}\",\"status\":\"success\"}}");
                    }
                    catch (Exception ex)
                    {
                        string errorMsg = ex.Message.Replace("\"", "'");
                        SendMessage($"{{\"action\":\"result\",\"task_id\":\"{taskId}\",\"status\":\"error\",\"error_msg\":\"{errorMsg}\"}}");
                    }
                });
            }
            else if (action == "max_retries_reached")
            {
                string taskId = ExtractJsonField(line, "task_id");
                string taskType = ExtractJsonField(line, "task_type");
                Console.WriteLine($"[Snerd] Dead Letter Queue: Task {taskId} ({taskType}) permanently failed.");
            }
        }

        private string ExtractJsonField(string json, string key)
        {
            var match = Regex.Match(json, $"\"{key}\"\\s*:\\s*\"(.*?)(?<!\\\\)\"");
            return match.Success ? match.Groups[1].Value : null;
        }

        public void Dispose()
        {
            _cts.Cancel();
            if (_process != null && !_process.HasExited)
            {
                _process.Kill();
                _process.Dispose();
            }
        }
    }
}
