using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace EarthOnline.Editor
{
    /// <summary>
    /// Alpha音频资源下载器 —— 自动下载8个P0音频文件并放入正确目录。
    ///
    /// 音频来源: Pixabay (https://pixabay.com/sound-effects/) —— 免费无版权CC0音效。
    /// Pixabay CDN URL 格式: https://cdn.pixabay.com/download/audio/{date}/audio_{hash}.mp3
    ///
    /// 如需替换下载链接:
    ///   1. 访问 https://pixabay.com/sound-effects/ 搜索音效
    ///   2. 点击下载按钮，复制 CDN 链接
    ///   3. 替换下方 P0Entries 数组中的 Url
    ///
    /// P0清单（来自 AudioManager EventBus 订阅）:
    ///   SFX:        spirit_attack, enemy_death, item_pickup, player_death, quest_complete, npc_greeting
    ///   Narration:  realm_breakthrough, achievement
    /// </summary>
    public static class AudioResourceDownloader
    {
        // ──────────────────────────────────────────────
        // P0 音频条目定义
        // 提示: 以下URL为示例格式，请替换为实际Pixabay CDN链接
        // ──────────────────────────────────────────────

        private static readonly AudioEntry[] P0Entries =
        {
            // SFX — 下载到 Resources/Audio/SFX/
            new("spirit_attack",
                "https://cdn.pixabay.com/download/audio/2023/02/14/audio_8a7b6c5d4e.mp3",
                AudioCategory.SFX),
            new("enemy_death",
                "https://cdn.pixabay.com/download/audio/2023/03/22/audio_1a2b3c4d5e.mp3",
                AudioCategory.SFX),
            new("item_pickup",
                "https://cdn.pixabay.com/download/audio/2023/04/10/audio_f9e8d7c6b5.mp3",
                AudioCategory.SFX),
            new("player_death",
                "https://cdn.pixabay.com/download/audio/2023/05/18/audio_0a1b2c3d4e.mp3",
                AudioCategory.SFX),
            new("quest_complete",
                "https://cdn.pixabay.com/download/audio/2023/06/25/audio_a0b1c2d3e4.mp3",
                AudioCategory.SFX),
            new("npc_greeting",
                "https://cdn.pixabay.com/download/audio/2023/07/12/audio_5f6g7h8i9j.mp3",
                AudioCategory.SFX),

            // Narration — 下载到 Resources/Audio/Narration/
            new("realm_breakthrough",
                "https://cdn.pixabay.com/download/audio/2023/08/30/audio_k0l1m2n3o4.mp3",
                AudioCategory.Narration),
            new("achievement",
                "https://cdn.pixabay.com/download/audio/2023/09/15/audio_p5q6r7s8t9.mp3",
                AudioCategory.Narration),
        };

        // 下载状态
        private static int s_successCount;
        private static int s_failCount;
        private static readonly List<string> s_failedFiles = new List<string>();

        // ──────────────────────────────────────────────
        // 菜单入口
        // ──────────────────────────────────────────────

        [MenuItem("EarthOnline/Download Alpha Audio")]
        private static void DownloadAlphaAudio()
        {
            // 重置状态
            s_successCount = 0;
            s_failCount = 0;
            s_failedFiles.Clear();

            // 通过 EditorApplication.update 驱动状态机进行下载
            var downloader = new AudioDownloadStateMachine(P0Entries);
            downloader.Start();
        }

        // ──────────────────────────────────────────────
        // 基于状态机的下载器（替代协程，兼容所有 Unity 版本）
        // ──────────────────────────────────────────────

        private class AudioDownloadStateMachine
        {
            private readonly AudioEntry[] _entries;
            private int _currentIndex;
            private UnityWebRequest _currentRequest;
            private bool _requestSent;
            private bool _running;

            public AudioDownloadStateMachine(AudioEntry[] entries)
            {
                _entries = entries;
            }

            public void Start()
            {
                if (_running) return;
                _running = true;
                _currentIndex = 0;

                Debug.Log("========================================");
                Debug.Log("[AudioResourceDownloader] 开始下载 Alpha P0 音频文件...");
                Debug.Log($"  目标: {_entries.Length} 个文件");
                Debug.Log("========================================");

                EditorApplication.update += Tick;
                ProcessNextFile();
            }

            private void ProcessNextFile()
            {
                // 检查是否所有文件已完成
                if (_currentIndex >= _entries.Length)
                {
                    Finish();
                    return;
                }

                var entry = _entries[_currentIndex];
                string savePath = GetSavePath(entry);

                // 确保目录存在
                string dir = Path.GetDirectoryName(savePath);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                // 如果文件已存在，跳过
                if (File.Exists(savePath))
                {
                    Debug.Log($"[{_currentIndex + 1}/{_entries.Length}] 已存在，跳过: {entry.Name} -> {savePath}");
                    s_successCount++;
                    _currentIndex++;
                    ProcessNextFile();
                    return;
                }

                // 显示进度条
                float progress = (float)_currentIndex / _entries.Length;
                if (EditorUtility.DisplayCancelableProgressBar(
                        "下载 Alpha 音频",
                        $"({_currentIndex + 1}/{_entries.Length}) {entry.Name} ({entry.Category})...",
                        progress))
                {
                    Debug.LogWarning("[AudioResourceDownloader] 用户取消下载");
                    _currentRequest?.Abort();
                    Finish();
                    return;
                }

                // 发起下载请求
                _currentRequest = UnityWebRequest.Get(entry.Url);
                _currentRequest.SendWebRequest();
                _requestSent = true;
            }

            private void Tick()
            {
                if (_currentRequest == null || !_requestSent)
                    return;

                if (!_currentRequest.isDone)
                    return;

                // 请求已完成，处理结果
                var entry = _entries[_currentIndex];
                string savePath = GetSavePath(entry);

                bool ok = false;
                string error = null;

                if (_currentRequest.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        byte[] rawData = _currentRequest.downloadHandler.data;
                        if (rawData != null && rawData.Length > 0)
                        {
                            File.WriteAllBytes(savePath, rawData);
                            ok = true;
                            Debug.Log($"[{_currentIndex + 1}/{_entries.Length}] 下载成功: {entry.Name} ({FormatBytes(rawData.Length)}) -> {savePath}");
                        }
                        else
                        {
                            error = "下载数据为空";
                        }
                    }
                    catch (Exception ex)
                    {
                        error = $"写入文件失败: {ex.Message}";
                    }
                }
                else
                {
                    error = _currentRequest.error;
                }

                _currentRequest.Dispose();
                _currentRequest = null;
                _requestSent = false;

                if (ok)
                {
                    s_successCount++;
                }
                else
                {
                    s_failCount++;
                    s_failedFiles.Add($"{entry.Name} ({entry.Category}): {error}");
                    Debug.LogError($"[{_currentIndex + 1}/{_entries.Length}] 下载失败: {entry.Name} -> {error}");
                }

                _currentIndex++;
                ProcessNextFile();
            }

            private void Finish()
            {
                _running = false;
                EditorApplication.update -= Tick;
                EditorUtility.ClearProgressBar();

                // 刷新 AssetDatabase
                if (s_successCount > 0)
                {
                    AssetDatabase.Refresh();
                    Debug.Log("[AudioResourceDownloader] AssetDatabase 已刷新");
                }

                // 输出汇总
                Debug.Log("========================================");
                Debug.Log($"[AudioResourceDownloader] 下载完成: 成功 {s_successCount}, 失败 {s_failCount}");
                if (s_failedFiles.Count > 0)
                {
                    Debug.LogError("失败文件:");
                    foreach (string f in s_failedFiles)
                        Debug.LogError($"  - {f}");
                }
                Debug.Log("========================================");

                EditorUtility.DisplayDialog(
                    "Alpha 音频下载完成",
                    $"成功: {s_successCount} 个文件\n失败: {s_failCount} 个文件\n\n" +
                    (s_failedFiles.Count > 0
                        ? $"失败列表:\n{string.Join("\n", s_failedFiles)}\n\n提示: 请检查 Pixabay CDN 链接是否有效，或前往 https://pixabay.com/sound-effects/ 获取真实链接后替换 P0Entries 数组中的 Url。"
                        : "所有音频文件已就绪。"),
                    "确定");
            }

            private static string GetSavePath(AudioEntry entry)
            {
                string subFolder = entry.Category switch
                {
                    AudioCategory.SFX => "SFX",
                    AudioCategory.Narration => "Narration",
                    _ => "Other"
                };

                return Path.Combine(
                    Application.dataPath,
                    "Resources",
                    "Audio",
                    subFolder,
                    $"{entry.Name}.mp3"
                );
            }

            private static string FormatBytes(long bytes)
            {
                if (bytes < 1024) return $"{bytes} B";
                if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
                return $"{bytes / (1024.0 * 1024.0):F1} MB";
            }
        }

        // ──────────────────────────────────────────────
        // 数据类型
        // ──────────────────────────────────────────────

        private enum AudioCategory { SFX, Narration }

        private readonly struct AudioEntry
        {
            public string Name { get; }
            public string Url { get; }
            public AudioCategory Category { get; }

            public AudioEntry(string name, string url, AudioCategory category)
            {
                Name = name;
                Url = url;
                Category = category;
            }

            public override string ToString() => $"[{Category}] {Name}";
        }
    }
}
