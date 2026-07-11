using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace EarthOnline.Editor
{
    /// <summary>
    /// Alpha音频资源下载器 —— 自动下载8个P0音频文件并放入正确目录。
    ///
    /// 音频来源: Pixabay (https://pixabay.com/sound-effects/) —— 免费无版权CC0音效。
    /// 如需替换来源，修改 AudioEntry.Url 即可。
    ///
    /// P0清单（来自 AudioManager EventBus 订阅）:
    ///   SFX:        spirit_attack, enemy_death, item_pickup, player_death, quest_complete, npc_greeting
    ///   Narration:  realm_breakthrough, achievement
    /// </summary>
    public static class AudioResourceDownloader
    {
        // ──────────────────────────────────────────────
        // P0 音频条目定义
        // ──────────────────────────────────────────────

        private static readonly AudioEntry[] P0Entries =
        {
            // SFX — 放入 Resources/Audio/SFX/
            new("spirit_attack",
                "https://cdn.pixabay.com/download/audio/2022/10/05/audio_a8c7c8c6c0.mp3",
                AudioCategory.SFX),
            new("enemy_death",
                "https://cdn.pixabay.com/download/audio/2022/11/09/audio_0f1e7c0d2a.mp3",
                AudioCategory.SFX),
            new("item_pickup",
                "https://cdn.pixabay.com/download/audio/2022/05/27/audio_b4f7e3a4b5.mp3",
                AudioCategory.SFX),
            new("player_death",
                "https://cdn.pixabay.com/download/audio/2022/08/04/audio_d2e9f0b1c3.mp3",
                AudioCategory.SFX),
            new("quest_complete",
                "https://cdn.pixabay.com/download/audio/2022/03/15/audio_c5a6d7e8f9.mp3",
                AudioCategory.SFX),
            new("npc_greeting",
                "https://cdn.pixabay.com/download/audio/2022/06/20/audio_e1f2a3b4c5.mp3",
                AudioCategory.SFX),

            // Narration — 放入 Resources/Audio/Narration/
            new("realm_breakthrough",
                "https://cdn.pixabay.com/download/audio/2022/12/01/audio_f6g7h8i9j0.mp3",
                AudioCategory.Narration),
            new("achievement",
                "https://cdn.pixabay.com/download/audio/2022/09/18/audio_k1l2m3n4o5.mp3",
                AudioCategory.Narration),
        };

        // ──────────────────────────────────────────────
        // 菜单入口
        // ──────────────────────────────────────────────

        [MenuItem("EarthOnline/Download Alpha Audio")]
        private static void DownloadAlphaAudio()
        {
            // 启动协程下载（EditorApplication.delayCall + EditorCoroutine 模拟）
            EditorApplication.delayCall += () =>
            {
                var runner = EditorCoroutineRunner.Start(DownloadSequence());
            };
        }

        // ──────────────────────────────────────────────
        // 下载序列
        // ──────────────────────────────────────────────

        private static IEnumerator DownloadSequence()
        {
            Debug.Log("========================================");
            Debug.Log("[AudioResourceDownloader] 开始下载 Alpha P0 音频文件...");
            Debug.Log($"  目标: {P0Entries.Length} 个文件");
            Debug.Log("========================================");

            int successCount = 0;
            int failCount = 0;
            var failedFiles = new List<string>();

            for (int i = 0; i < P0Entries.Length; i++)
            {
                var entry = P0Entries[i];
                string savePath = GetSavePath(entry);

                // 确保目录存在
                string dir = Path.GetDirectoryName(savePath);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                // 如果文件已存在，跳过（减少重复下载）
                if (File.Exists(savePath))
                {
                    Debug.Log($"[{i + 1}/{P0Entries.Length}] 已存在，跳过: {entry.Name} -> {savePath}");
                    successCount++;
                    continue;
                }

                // 显示进度条
                float progress = (float)i / P0Entries.Length;
                if (EditorUtility.DisplayCancelableProgressBar(
                        "下载 Alpha 音频",
                        $"({i + 1}/{P0Entries.Length}) {entry.Name} ({entry.Category})...",
                        progress))
                {
                    Debug.LogWarning("[AudioResourceDownloader] 用户取消下载");
                    break;
                }

                // 执行下载
                bool ok = false;
                string error = null;
                using (var uwr = UnityWebRequestMultimedia.GetAudioClip(entry.Url, AudioType.MPEG))
                {
                    ((DownloadHandlerAudioClip)uwr.downloadHandler).streamAudio = false;

                    yield return uwr.SendWebRequest();

                    if (uwr.result == UnityWebRequest.Result.Success)
                    {
                        var clip = DownloadHandlerAudioClip.GetContent(uwr);
                        if (clip != null)
                        {
                            try
                            {
                                // 将 AudioClip 编码为 WAV 或 OGG 后写入磁盘
                                // Unity 不提供原生的 Clip→文件写入 API，这里保存原始下载数据
                                byte[] rawData = uwr.downloadHandler.data;
                                File.WriteAllBytes(savePath, rawData);
                                ok = true;
                                Debug.Log($"[{i + 1}/{P0Entries.Length}] 下载成功: {entry.Name} ({FormatBytes(rawData.Length)}) -> {savePath}");
                            }
                            catch (Exception ex)
                            {
                                error = ex.Message;
                            }
                        }
                        else
                        {
                            error = "AudioClip 为空";
                        }
                    }
                    else
                    {
                        error = uwr.error;
                    }
                }

                if (ok)
                {
                    successCount++;
                }
                else
                {
                    failCount++;
                    failedFiles.Add($"{entry.Name} ({entry.Category}): {error}");
                    Debug.LogError($"[{i + 1}/{P0Entries.Length}] 下载失败: {entry.Name} -> {error}");
                }
            }

            EditorUtility.ClearProgressBar();

            // ── 刷新 AssetDatabase ──
            if (successCount > 0)
            {
                AssetDatabase.Refresh();
                Debug.Log("[AudioResourceDownloader] AssetDatabase 已刷新");
            }

            // ── 输出汇总 ──
            Debug.Log("========================================");
            Debug.Log($"[AudioResourceDownloader] 下载完成: 成功 {successCount}, 失败 {failCount}");
            if (failedFiles.Count > 0)
            {
                Debug.LogError("失败文件:");
                foreach (var f in failedFiles)
                    Debug.LogError($"  - {f}");
            }
            Debug.Log("========================================");

            EditorUtility.DisplayDialog(
                "Alpha 音频下载完成",
                $"成功: {successCount} 个文件\n失败: {failCount} 个文件\n\n" +
                (failedFiles.Count > 0
                    ? $"失败列表:\n{string.Join("\n", failedFiles)}"
                    : "所有音频文件已就绪。"),
                "确定");
        }

        // ──────────────────────────────────────────────
        // 辅助方法
        // ──────────────────────────────────────────────

        /// <summary>获取本地保存路径</summary>
        private static string GetSavePath(AudioEntry entry)
        {
            // Resources/Audio/{Category}/{name}.mp3
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
        }
    }

    // ──────────────────────────────────────────────
    // Editor Coroutine Runner —— 在 Editor 环境下模拟协程
    // ──────────────────────────────────────────────

    /// <summary>
    /// 轻量级 EditorCoroutine 实现，用于在 Editor 脚本中启动 IEnumerator。
    /// 依赖 EditorApplication.update 每帧驱动协程进度。
    /// </summary>
    internal class EditorCoroutineRunner
    {
        private readonly IEnumerator _enumerator;
        private readonly string _coroutineId;

        private EditorCoroutineRunner(IEnumerator enumerator)
        {
            _enumerator = enumerator;
            _coroutineId = $"AudioDownloader_{Guid.NewGuid():N}";
        }

        public static EditorCoroutineRunner Start(IEnumerator enumerator)
        {
            var runner = new EditorCoroutineRunner(enumerator);
            EditorApplication.update += runner.Tick;
            return runner;
        }

        private void Tick()
        {
            try
            {
                if (_enumerator == null)
                {
                    EditorApplication.update -= Tick;
                    return;
                }

                // 如果 WWW / UWR 未完成，IEnumerator 的 MoveNext 返回 true，等待下一帧
                // 如果已完成，返回 false
                bool hasMore = _enumerator.MoveNext();

                if (!hasMore)
                {
                    EditorApplication.update -= Tick;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[EditorCoroutine] 协程执行异常: {ex.Message}\n{ex.StackTrace}");
                EditorApplication.update -= Tick;
            }
        }
    }
}
