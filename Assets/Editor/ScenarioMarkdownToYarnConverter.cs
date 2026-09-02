using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace PKD.EditorTools
{
    /// <summary>
    /// シナリオMarkdown（"## scene_id"見出し＋「ラベル: 値」の演出指定＋話者名/セリフ）を、
    /// 既存のDialogueVisualsコマンド（&lt;&lt;background&gt;&gt;等）を使ったYarnノードへ変換する。
    ///
    /// Markdownに明示されていない演出はYarn側にも出力しない。DialogueVisualsは
    /// 明示コマンドが来ない限り直前の状態を維持するので、これで
    /// 「未指定なら現在の状態を維持する」という最重要ルールを満たす。
    /// </summary>
    public static class ScenarioMarkdownToYarnConverter
    {
        private static readonly Regex SceneHeaderPattern = new Regex(@"^##\s+(\S+)\s*$");
        private static readonly Regex DirectivePattern = new Regex(@"^(背景|立ち絵|位置|BGM|SE|暗転|待機)\s*[:：]\s*(.+?)\s*$", RegexOptions.IgnoreCase);

        [MenuItem("Tools/Scenario/Convert Markdown To Yarn")]
        public static void ConvertFromMenu()
        {
            string sourcePath = EditorUtility.OpenFilePanel("変換元のシナリオMarkdown", Application.dataPath + "/..", "md");
            if (string.IsNullOrEmpty(sourcePath)) return;

            string defaultName = Path.GetFileNameWithoutExtension(sourcePath);
            string destPath = EditorUtility.SaveFilePanel("出力先のYarnファイル", Path.GetDirectoryName(sourcePath), defaultName, "yarn");
            if (string.IsNullOrEmpty(destPath)) return;

            ConvertFile(sourcePath, destPath);
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Scenario", "変換しました:\n" + destPath, "OK");
        }

        public static void ConvertFile(string sourcePath, string destPath)
        {
            string markdown = File.ReadAllText(sourcePath);
            string yarn = Convert(markdown);
            File.WriteAllText(destPath, yarn);
        }

        public static string Convert(string markdown)
        {
            var output = new StringBuilder();
            string[] lines = markdown.Replace("\r\n", "\n").Split('\n');

            int index = 0;
            bool firstScene = true;

            while (index < lines.Length)
            {
                Match headerMatch = SceneHeaderPattern.Match(lines[index].TrimEnd());
                if (!headerMatch.Success)
                {
                    // 最初の "## " 見出しより前、または見出しの間に挟まる非見出し行は対象外。
                    index++;
                    continue;
                }

                string sceneId = headerMatch.Groups[1].Value;
                index++;

                var bodyLines = new List<string>();
                while (index < lines.Length && !SceneHeaderPattern.IsMatch(lines[index].TrimEnd()))
                {
                    bodyLines.Add(lines[index]);
                    index++;
                }

                if (!firstScene) output.Append('\n');
                firstScene = false;

                output.Append("title: ").Append(sceneId).Append('\n');
                output.Append("---\n");
                AppendConvertedBody(output, bodyLines);
                output.Append("===\n");
            }

            return output.ToString();
        }

        private static void AppendConvertedBody(StringBuilder output, List<string> bodyLines)
        {
            List<List<string>> paragraphs = SplitIntoParagraphs(bodyLines);

            for (int p = 0; p < paragraphs.Count; p++)
            {
                List<string> paragraph = paragraphs[p];
                if (p > 0) output.Append('\n');

                if (IsDirectiveParagraph(paragraph))
                {
                    foreach (string line in paragraph)
                    {
                        output.Append(ConvertDirectiveLine(line)).Append('\n');
                    }
                }
                else if (IsDialogueParagraph(paragraph))
                {
                    string speaker = paragraph[0].Trim();
                    for (int i = 1; i < paragraph.Count; i++)
                    {
                        output.Append(speaker).Append(": ").Append(paragraph[i].Trim()).Append('\n');
                    }
                }
                else
                {
                    // 演出指定にもセリフ形式（話者名＋本文）にも当てはまらない行は、
                    // 既存のYarn構文（選択肢・ジャンプ・コメント・素のナレーション行など）
                    // としてそのまま素通しする。書かれていない演出を推測して補わない。
                    foreach (string line in paragraph)
                    {
                        output.Append(line).Append('\n');
                    }
                }
            }
        }

        private static List<List<string>> SplitIntoParagraphs(List<string> bodyLines)
        {
            var paragraphs = new List<List<string>>();
            List<string> current = null;

            foreach (string rawLine in bodyLines)
            {
                string line = rawLine.TrimEnd();
                if (string.IsNullOrWhiteSpace(line))
                {
                    if (current != null)
                    {
                        paragraphs.Add(current);
                        current = null;
                    }
                    continue;
                }

                if (current == null) current = new List<string>();
                current.Add(line);
            }

            if (current != null) paragraphs.Add(current);
            return paragraphs;
        }

        private static bool IsDirectiveParagraph(List<string> paragraph)
        {
            foreach (string line in paragraph)
            {
                if (!DirectivePattern.IsMatch(line)) return false;
            }
            return true;
        }

        private static bool IsDialogueParagraph(List<string> paragraph)
        {
            if (paragraph.Count < 2) return false;
            // 1行目が演出指定行でなければ話者名とみなす。
            return !DirectivePattern.IsMatch(paragraph[0]);
        }

        private static string ConvertDirectiveLine(string line)
        {
            Match match = DirectivePattern.Match(line);
            string label = match.Groups[1].Value;
            string value = match.Groups[2].Value.Trim();

            switch (label)
            {
                case "背景":
                    return $"<<background \"{value}\">>";
                case "立ち絵":
                    return $"<<portrait \"{value}\">>";
                case "位置":
                    return $"<<portrait_position \"{value}\">>";
                case "BGM":
                case "bgm":
                    return $"<<bgm \"{value}\">>";
                case "SE":
                case "se":
                    return $"<<se \"{value}\">>";
                case "暗転":
                    return $"<<blackout \"{value}\">>";
                case "待機":
                    return $"<<wait {value}>>";
                default:
                    throw new InvalidOperationException($"未知の演出指定です: {label}");
            }
        }
    }
}
