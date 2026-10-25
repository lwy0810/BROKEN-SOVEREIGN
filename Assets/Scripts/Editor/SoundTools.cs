using System.Collections.Generic;
using System.IO;
using System.Linq;
using BrokenSovereign.Sound;
using UnityEditor;
using UnityEngine;

namespace BrokenSovereign.Editor
{
    /// <summary>
    /// 사운드 파일 점검 (10/26): Assets/Resources/Audio에 SoundId 이름의 파일이 있는지 콘솔에 표로 출력.
    /// 기술별 소리(skill_기술ID)는 선택 사항 (없으면 skill_cast 사용).
    /// </summary>
    public static class SoundTools
    {
        private const string AUDIO_FOLDER = "Assets/Resources/Audio";
        private static readonly string[] EXTENSIONS = { ".wav", ".mp3", ".ogg", ".aif", ".aiff" };

        [MenuItem("BrokenSovereign/Audio/사운드 목록 확인")]
        private static void Check()
        {
            if (!AssetDatabase.IsValidFolder(AUDIO_FOLDER))
            {
                Directory.CreateDirectory(AUDIO_FOLDER);
                AssetDatabase.Refresh();
            }

            HashSet<string> files = new HashSet<string>(Directory.GetFiles(AUDIO_FOLDER)
                .Where(f => EXTENSIONS.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .Select(Path.GetFileNameWithoutExtension));

            var lines = new List<string>();
            int found = 0;

            foreach (string id in SoundId.ALL)
            {
                int variants = files.Count(f => f == id || f.StartsWith(id + "_") && int.TryParse(f.Substring(id.Length + 1), out _));
                found += variants > 0 ? 1 : 0;
                lines.Add(variants > 0 ? $"  O  {id}  ({variants}개)" : $"  -  {id}");
            }

            string[] skills = files.Where(f => f.StartsWith("skill_") && f != SoundId.SKILL_CAST).ToArray();
            string[] unknown = files.Where(f => !SoundId.ALL.Any(id => f == id || f.StartsWith(id + "_")) && !f.StartsWith("skill_")).ToArray();

            Debug.Log($"SoundTools: {found}/{SoundId.ALL.Length}개 준비 ({AUDIO_FOLDER})\n{string.Join("\n", lines)}" +
                (skills.Length > 0 ? $"\n기술별: {string.Join(", ", skills)}" : string.Empty) +
                (unknown.Length > 0 ? $"\n이름이 맞지 않는 파일: {string.Join(", ", unknown)}" : string.Empty));
        }
    }
}
