using UnityEditor;
using UnityEngine;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace EarthOnline.Editor
{
    /// <summary>
    /// 一次性启动脚本：开启 MCP for Unity 的自动启动，让 Claude Code 能连上 Unity。
    /// 只执行一次，执行后自动删除自己。
    /// </summary>
    [InitializeOnLoad]
    public static class McpAutoStartBootstrap
    {
        private const string BootstrapDoneKey = "EarthOnline.McpBootstrapDone";
        private const string McpAutoStartKey = "MCPForUnity.AutoStartOnLoad";

        static McpAutoStartBootstrap()
        {
            // 避免在 AssetImportWorker 中执行
            if (System.Environment.CommandLine != null &&
                System.Environment.CommandLine.IndexOf("AssetImportWorker", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return;

            EditorApplication.delayCall += () =>
            {
                if (SessionState.GetBool(BootstrapDoneKey, false)) return;
                SessionState.SetBool(BootstrapDoneKey, true);

                // 1. 开启 MCP for Unity 自动启动
                EditorPrefs.SetBool(McpAutoStartKey, true);
                Debug.Log("[EarthOnline] MCP AutoStart 已开启。");

                // 2. 尝试通过反射调用 MCP for Unity 的 Start 方法
                TryStartMcpServer();

                // 3. 标记完成，后续启动不再需要此脚本
                EditorPrefs.SetBool(BootstrapDoneKey, true);
            };
        }

        private static void TryStartMcpServer()
        {
            // MCP for Unity 的入口类在 MCPForUnity.Editor.Services 命名空间
            // 尝试找到并调用 Bridge.StartAsync()
            var mcpAssembly = System.AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "MCPForUnity.Editor");

            if (mcpAssembly == null)
            {
                Debug.LogWarning("[EarthOnline] 未找到 MCPForUnity.Editor 程序集，可能尚未加载。");
                return;
            }

            // 尝试找到 MCPServiceLocator
            var locatorType = mcpAssembly.GetType("MCPForUnity.Editor.Services.MCPServiceLocator");
            if (locatorType == null)
            {
                Debug.LogWarning("[EarthOnline] 未找到 MCPServiceLocator 类型。");
                return;
            }

            // 获取 Bridge 属性
            var bridgeProp = locatorType.GetProperty("Bridge",
                BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic);
            if (bridgeProp == null)
            {
                Debug.LogWarning("[EarthOnline] 未找到 Bridge 属性。");
                return;
            }

            var bridge = bridgeProp.GetValue(null);
            if (bridge == null)
            {
                Debug.LogWarning("[EarthOnline] Bridge 实例为空。");
                return;
            }

            // 调用 StartAsync
            var startMethod = bridge.GetType().GetMethod("StartAsync",
                BindingFlags.Public | BindingFlags.Instance);
            if (startMethod == null)
            {
                Debug.LogWarning("[EarthOnline] 未找到 StartAsync 方法。");
                return;
            }

            var task = startMethod.Invoke(bridge, null) as System.Threading.Tasks.Task;
            if (task != null)
            {
                task.ContinueWith(t =>
                {
                    if (t.IsCompletedSuccessfully)
                        Debug.Log("[EarthOnline] MCP 服务器已启动！Claude Code 现在可以连接了。");
                    else
                        Debug.LogWarning($"[EarthOnline] MCP 启动失败: {t.Exception?.Message}");
                }, System.Threading.Tasks.TaskScheduler.FromCurrentSynchronizationContext());
            }

            Debug.Log("[EarthOnline] MCP 启动命令已发出。");
        }
    }
}
