using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityMcpBridge.Editor.Helpers; // 用于响应类

namespace UnityMcpBridge.Editor.Tools
{
    /// <summary>
    /// 处理读取和清除 Unity 编辑器控制台日志条目。
    /// 使用反射访问内部 LogEntry 方法/属性。
    /// </summary>
    public static class ReadConsole
    {
        // 用于访问内部 LogEntry 数据的反射成员
        // private static MethodInfo _getEntriesMethod; // 已移除，因为未使用且反射失败
        private static MethodInfo _startGettingEntriesMethod;
        private static MethodInfo _endGettingEntriesMethod; // 从 _stopGettingEntriesMethod 重命名，尝试使用 End...
        private static MethodInfo _clearMethod;
        private static MethodInfo _getCountMethod;
        private static MethodInfo _getEntryMethod;
        private static FieldInfo _modeField;
        private static FieldInfo _messageField;
        private static FieldInfo _fileField;
        private static FieldInfo _lineField;
        private static FieldInfo _instanceIdField;
        private static FieldInfo logEntryCondition;

        // 注意：LogEntry 中没有直接可用的时间戳；需要解析消息或寻找替代方法？

        // 用于反射设置的静态构造函数
        static ReadConsole()
        {
            try
            {
                Type logEntriesType = typeof(EditorApplication).Assembly.GetType(
                    "UnityEditor.LogEntries"
                );
                if (logEntriesType == null)
                    throw new Exception("找不到内部类型 UnityEditor.LogEntries");

                // 包含 NonPublic 绑定标志，因为内部 API 可能会更改可访问性
                BindingFlags staticFlags =
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
                BindingFlags instanceFlags =
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

                _startGettingEntriesMethod = logEntriesType.GetMethod(
                    "StartGettingEntries",
                    staticFlags
                );
                if (_startGettingEntriesMethod == null)
                    throw new Exception("反射 LogEntries.StartGettingEntries 失败");

                // 尝试根据警告消息反射 EndGettingEntries
                _endGettingEntriesMethod = logEntriesType.GetMethod(
                    "EndGettingEntries",
                    staticFlags
                );
                if (_endGettingEntriesMethod == null)
                    throw new Exception("反射 LogEntries.EndGettingEntries 失败");

                _clearMethod = logEntriesType.GetMethod("Clear", staticFlags);
                if (_clearMethod == null)
                    throw new Exception("反射 LogEntries.Clear 失败");

                _getCountMethod = logEntriesType.GetMethod("GetCount", staticFlags);
                if (_getCountMethod == null)
                    throw new Exception("反射 LogEntries.GetCount 失败");

                _getEntryMethod = logEntriesType.GetMethod("GetEntryInternal", staticFlags);
                if (_getEntryMethod == null)
                    throw new Exception("反射 LogEntries.GetEntryInternal 失败");

                Type logEntryType = typeof(EditorApplication).Assembly.GetType(
                    "UnityEditor.LogEntry"
                );
                if (logEntryType == null)
                    throw new Exception("找不到内部类型 UnityEditor.LogEntry");

                _modeField = logEntryType.GetField("mode", instanceFlags);
                if (_modeField == null)
                    throw new Exception("反射 LogEntry.mode 失败");

                // _messageField = logEntryType.GetField("message", instanceFlags);
                // if (_messageField == null)
                //     throw new Exception("反射 LogEntry.message 失败");
                
                logEntryCondition = logEntryType.GetField("condition", BindingFlags.Instance | BindingFlags.Public);
                if (logEntryCondition == null)
                    throw new Exception("反射 logEntryCondition失败");
                
                _fileField = logEntryType.GetField("file", instanceFlags);
                if (_fileField == null)
                    throw new Exception("反射 LogEntry.file 失败");

                _lineField = logEntryType.GetField("line", instanceFlags);
                if (_lineField == null)
                    throw new Exception("Failed to reflect LogEntry.line");

                _instanceIdField = logEntryType.GetField("instanceID", instanceFlags);
                if (_instanceIdField == null)
                    throw new Exception("Failed to reflect LogEntry.instanceID");
            }
            catch (Exception e)
            {
                Debug.LogError(
                    $"[ReadConsole] Static Initialization Failed: Could not setup reflection for LogEntries/LogEntry. Console reading/clearing will likely fail. Specific Error: {e.Message}"
                );
                // Set members to null to prevent NullReferenceExceptions later, HandleCommand should check this.
                _startGettingEntriesMethod =
                    _endGettingEntriesMethod =
                    _clearMethod =
                    _getCountMethod =
                    _getEntryMethod =
                        null;
                _modeField = _messageField = _fileField = _lineField = _instanceIdField = null;
            }
        }

        // --- 主处理程序 ---

        public static object HandleCommand(JObject @params)
        {
            // 检查所有必需的反射成员是否都已成功初始化。
            if (
                _startGettingEntriesMethod == null
                || _endGettingEntriesMethod == null
                || _clearMethod == null
                || _getCountMethod == null
                || _getEntryMethod == null
                || _modeField == null
                || _messageField == null
                || _fileField == null
                || _lineField == null
                || _instanceIdField == null
            )
            {
                // 在此处也记录错误，以便在 Unity 控制台中更轻松地调试
                Debug.LogError(
                    "[ReadConsole] 调用 HandleCommand 时反射成员未初始化。静态构造函数可能静默失败或存在问题。"
                );
                return Response.Error(
                    "ReadConsole 处理程序由于反射错误而初始化失败。无法访问控制台日志。"
                );
            }

            string action = @params["action"]?.ToString().ToLower() ?? "get";

            try
            {
                if (action == "clear")
                {
                    return ClearConsole();
                }
                else if (action == "get")
                {
                    // 提取 'get' 操作的参数
                    var types =
                        (@params["types"] as JArray)?.Select(t => t.ToString().ToLower()).ToList()
                        ?? new List<string> { "error", "warning", "log" };
                    int? count = @params["count"]?.ToObject<int?>();
                    string filterText = @params["filterText"]?.ToString();
                    string sinceTimestampStr = @params["sinceTimestamp"]?.ToString(); // TODO: 实现时间戳过滤
                    string format = (@params["format"]?.ToString() ?? "detailed").ToLower();
                    bool includeStacktrace =
                        @params["includeStacktrace"]?.ToObject<bool?>() ?? true;

                    if (types.Contains("all"))
                    {
                        types = new List<string> { "error", "warning", "log" }; // 展开 'all'
                    }

                    if (!string.IsNullOrEmpty(sinceTimestampStr))
                    {
                        Debug.LogWarning(
                            "[ReadConsole] 目前不支持按 'since_timestamp' 进行过滤。"
                        );
                        // 需要一种方法来获取每个日志条目的时间戳。
                    }

                    return GetConsoleEntries(types, count, filterText, format, includeStacktrace);
                }
                else
                {
                    return Response.Error(
                        $"未知操作：'{action}'。有效的操作是 'get' 或 'clear'。"
                    );
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[ReadConsole] 操作 '{action}' 失败：{e}");
                return Response.Error($"处理操作 '{action}' 时发生内部错误：{e.Message}");
            }
        }

        // --- 操作实现 ---

        private static object ClearConsole()
        {
            try
            {
                _clearMethod.Invoke(null, null); // 静态方法，无实例，无参数
                return Response.Success("控制台已成功清除。");
            }
            catch (Exception e)
            {
                Debug.LogError($"[ReadConsole] 清除控制台失败：{e}");
                return Response.Error($"清除控制台失败：{e.Message}");
            }
        }

        private static object GetConsoleEntries(
            List<string> types,
            int? count,
            string filterText,
            string format,
            bool includeStacktrace
        )
        {
            List<object> formattedEntries = new List<object>();
            int retrievedCount = 0;

            try
            {
                // LogEntries 需要在 GetEntries/GetEntryInternal 前后调用 Start/Stop
                _startGettingEntriesMethod.Invoke(null, null);

                int totalEntries = (int)_getCountMethod.Invoke(null, null);
                // 创建实例以传递给 GetEntryInternal - 确保类型正确
                Type logEntryType = typeof(EditorApplication).Assembly.GetType(
                    "UnityEditor.LogEntry"
                );
                if (logEntryType == null)
                    throw new Exception(
                        "在 GetConsoleEntries 期间找不到内部类型 UnityEditor.LogEntry。"
                    );
                object logEntryInstance = Activator.CreateInstance(logEntryType);

                for (int i = 0; i < totalEntries; i++)
                {
                    // 使用反射将条目数据获取到我们的实例中
                    _getEntryMethod.Invoke(null, new object[] { i, logEntryInstance });

                    // 使用反射提取数据
                    int mode = (int)_modeField.GetValue(logEntryInstance);
                    string message = (string)_messageField.GetValue(logEntryInstance);
                    string file = (string)_fileField.GetValue(logEntryInstance);

                    int line = (int)_lineField.GetValue(logEntryInstance);
                    // int instanceId = (int)_instanceIdField.GetValue(logEntryInstance);

                    if (string.IsNullOrEmpty(message))
                        continue; // 跳过空消息

                    // --- 过滤 ---
                    // 按类型过滤
                    LogType currentType = GetLogTypeFromMode(mode);
                    if (!types.Contains(currentType.ToString().ToLowerInvariant()))
                    {
                        continue;
                    }

                    // 按文本过滤（不区分大小写）
                    if (
                        !string.IsNullOrEmpty(filterText)
                        && message.IndexOf(filterText, StringComparison.OrdinalIgnoreCase) < 0
                    )
                    {
                        continue;
                    }

                    // TODO: 按时间戳过滤（需要时间戳数据）

                    // --- 格式化 ---
                    string stackTrace = includeStacktrace ? ExtractStackTrace(message) : null;
                    // 如果需要且存在堆栈信息，则获取第一行，否则使用完整消息
                    string messageOnly =
                        (includeStacktrace && !string.IsNullOrEmpty(stackTrace))
                            ? message.Split(
                                new[] { '\n', '\r' },
                                StringSplitOptions.RemoveEmptyEntries
                            )[0]
                            : message;

                    object formattedEntry = null;
                    switch (format)
                    {
                        case "plain":
                            formattedEntry = messageOnly;
                            break;
                        case "json":
                        case "detailed": // 将 detailed 视为 json 以进行结构化返回
                        default:
                            formattedEntry = new
                            {
                                type = currentType.ToString(),
                                message = messageOnly,
                                file = file,
                                line = line,
                                // timestamp = "", // TODO
                                stackTrace = stackTrace, // 如果 includeStacktrace 为 false 或未找到堆栈，则为 null
                            };
                            break;
                    }

                    formattedEntries.Add(formattedEntry);
                    retrievedCount++;

                    // 应用数量限制（过滤后）
                    if (count.HasValue && retrievedCount >= count.Value)
                    {
                        break;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[ReadConsole] 获取日志条目时出错：{e}");
                // 即使在迭代过程中出错，也要确保调用 EndGettingEntries
                try
                {
                    _endGettingEntriesMethod.Invoke(null, null);
                }
                catch
                { /* 忽略嵌套异常 */
                }
                return Response.Error($"获取日志条目时出错：{e.Message}");
            }
            finally
            {
                // 确保始终调用 EndGettingEntries
                try
                {
                    _endGettingEntriesMethod.Invoke(null, null);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[ReadConsole] 调用 EndGettingEntries 失败：{e}");
                    // 此处不返回错误，因为可能有有效数据，但记录日志
                }
            }

            // 返回过滤并格式化后的列表（可能为空）
            return Response.Success(
                $"检索到 {formattedEntries.Count} 条日志条目。",
                formattedEntries
            );
        }

        // --- 内部辅助方法 ---

        // 将 LogEntry.mode 位映射到 LogType 枚举
        // 基于反编译的 UnityEditor 代码或常见模式。精确的位可能会在不同的 Unity 版本中变化。
        // 请参阅下面关于 LogEntry mode 位的探索注释。
        // 注意：此映射经过简化，可能无法完美覆盖所有边缘情况或未来的 Unity 版本。
        private const int ModeBitError = 1 << 0;
        private const int ModeBitAssert = 1 << 1;
        private const int ModeBitWarning = 1 << 2;
        private const int ModeBitLog = 1 << 3;
        private const int ModeBitException = 1 << 4; // 通常与错误位组合使用
        private const int ModeBitScriptingError = 1 << 9;
        private const int ModeBitScriptingWarning = 1 << 10;
        private const int ModeBitScriptingLog = 1 << 11;
        private const int ModeBitScriptingException = 1 << 18;
        private const int ModeBitScriptingAssertion = 1 << 22;

        private static LogType GetLogTypeFromMode(int mode)
        {
            // 首先，根据原始逻辑确定类型（最严重的优先）
            LogType initialType;
            if (
                (
                    mode
                    & (
                        ModeBitError
                        | ModeBitScriptingError
                        | ModeBitException
                        | ModeBitScriptingException
                    )
                ) != 0
            )
            {
                initialType = LogType.Error;
            }
            else if ((mode & (ModeBitAssert | ModeBitScriptingAssertion)) != 0)
            {
                initialType = LogType.Assert;
            }
            else if ((mode & (ModeBitWarning | ModeBitScriptingWarning)) != 0)
            {
                initialType = LogType.Warning;
            }
            else
            {
                initialType = LogType.Log;
            }

            // 应用观察到的“降低一级”修正
            switch (initialType)
            {
                case LogType.Error:
                    return LogType.Warning; // 错误变为警告
                case LogType.Warning:
                    return LogType.Log; // 警告变为日志
                case LogType.Assert:
                    return LogType.Assert; // 断言保持不变（没有更低级别）
                case LogType.Log:
                    return LogType.Log; // 日志保持不变
                default:
                    return LogType.Log; // 默认回退
            }
        }

        /// <summary>
        /// 尝试从日志消息中提取堆栈跟踪部分。
        /// Unity 日志消息通常在主消息后面附加堆栈跟踪，
        /// 从新行开始，通常带有缩进或以 "at " 开头。
        /// </summary>
        /// <param name="fullMessage">包含潜在堆栈跟踪的完整日志消息。</param>
        /// <returns>提取的堆栈跟踪字符串，如果未找到则为 null。</returns>
        private static string ExtractStackTrace(string fullMessage)
        {
            if (string.IsNullOrEmpty(fullMessage))
                return null;

            // 按行分割，移除空行以优雅处理不同的行结尾。
            // 如果堆栈跟踪中的空行很重要，使用 StringSplitOptions.None 可能更好，但此处 RemoveEmptyEntries 通常更安全。
            string[] lines = fullMessage.Split(
                new[] { '\r', '\n' },
                StringSplitOptions.RemoveEmptyEntries
            );

            // 如果只有一行或更少，则没有单独的堆栈跟踪。
            if (lines.Length <= 1)
                return null;

            int stackStartIndex = -1;

            // 从第二行开始检查。
            for (int i = 1; i < lines.Length; ++i)
            {
                // 性能提示：TrimStart 会创建一个新字符串。如果性能关键，考虑使用 IsWhiteSpace 检查。
                string trimmedLine = lines[i].TrimStart();

                // 检查常见的堆栈跟踪模式。
                if (
                    trimmedLine.StartsWith("at ")
                    || trimmedLine.StartsWith("UnityEngine.")
                    || trimmedLine.StartsWith("UnityEditor.")
                    || trimmedLine.Contains("(at ")
                    || // 涵盖 "(at Assets/..." 模式
                    // 启发式方法：检查行是否以可能的命名空间/类模式开头（大写字母.某内容）
                    (
                        trimmedLine.Length > 0
                        && char.IsUpper(trimmedLine[0])
                        && trimmedLine.Contains('.')
                    )
                )
                {
                    stackStartIndex = i;
                    break; // 找到可能的堆栈跟踪起始位置
                }
            }

            // 如果找到潜在的起始索引...
            if (stackStartIndex > 0)
            {
                // 从堆栈起始索引开始将行连接起来，使用标准换行符。
                // 这将重新构建消息的堆栈跟踪部分。
                return string.Join("\n", lines.Skip(stackStartIndex));
            }

            // 根据模式未找到明确的堆栈跟踪。
            return null;
        }

        /* LogEntry.mode 位探索（基于 Unity 反编译/观察）：
           可能会在不同版本中变化。

           基本类型：
           kError = 1 << 0 (1)
           kAssert = 1 << 1 (2)
           kWarning = 1 << 2 (4)
           kLog = 1 << 3 (8)
           kFatal = 1 << 4 (16) - 通常视为异常/错误

           修饰符/上下文：
           kAssetImportError = 1 << 7 (128)
           kAssetImportWarning = 1 << 8 (256)
           kScriptingError = 1 << 9 (512)
           kScriptingWarning = 1 << 10 (1024)
           kScriptingLog = 1 << 11 (2048)
           kScriptCompileError = 1 << 12 (4096)
           kScriptCompileWarning = 1 << 13 (8192)
           kStickyError = 1 << 14 (16384) - 即使在“播放时清除”后仍保持可见
           kMayIgnoreLineNumber = 1 << 15 (32768)
           kReportBug = 1 << 16 (65536) - 显示“报告错误”按钮
           kDisplayPreviousErrorInStatusBar = 1 << 17 (131072)
           kScriptingException = 1 << 18 (262144)
           kDontExtractStacktrace = 1 << 19 (524288) - 对控制台 UI 的提示
           kShouldClearOnPlay = 1 << 20 (1048576) - 默认行为
           kGraphCompileError = 1 << 21 (2097152)
           kScriptingAssertion = 1 << 22 (4194304)
           kVisualScriptingError = 1 << 23 (8388608)

           观察到的示例值：
           日志：2048 (ScriptingLog) 或 8 (Log)
           警告：1028 (ScriptingWarning | Warning) 或 4 (Warning)
           错误：513 (ScriptingError | Error) 或 1 (Error)
           异常：262161 (ScriptingException | Error | kFatal?) - 复杂组合
           断言：4194306 (ScriptingAssertion | Assert) 或 2 (Assert)
        */
    }
}

