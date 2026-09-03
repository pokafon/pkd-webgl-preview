using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace AngerBattle.EditorTools
{
    /// <summary>
    /// 不安戦WebGL試作をGitHub Pagesで公開するための使い捨てビルドスクリプト。
    /// バッチモードから -executeMethod で呼び出す想定。
    /// </summary>
    public static class WebGLPrototypeBuilder
    {
        private const string OutputPath = "G:/Unity_Games/PKD/_webgl_build_tmp";

        public static void Build()
        {
            try
            {
                string[] scenes = EditorBuildSettings.scenes
                    .Where(s => s.enabled)
                    .Select(s => s.path)
                    .ToArray();

                if (scenes.Length == 0)
                {
                    Debug.LogError("WEBGL_BUILD_RESULT: FAIL: Build Settingsに有効なシーンがありません。");
                    EditorApplication.Exit(1);
                    return;
                }

                var options = new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = OutputPath,
                    target = BuildTarget.WebGL,
                    options = BuildOptions.None
                };

                BuildReport report = BuildPipeline.BuildPlayer(options);
                BuildSummary summary = report.summary;

                if (summary.result == BuildResult.Succeeded)
                {
                    Debug.Log($"WEBGL_BUILD_RESULT: PASS totalSize={summary.totalSize} totalTime={summary.totalTime}");
                    EditorApplication.Exit(0);
                }
                else
                {
                    Debug.LogError($"WEBGL_BUILD_RESULT: FAIL result={summary.result} totalErrors={summary.totalErrors}");
                    EditorApplication.Exit(1);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("WEBGL_BUILD_RESULT: FAIL: " + ex);
                EditorApplication.Exit(1);
            }
        }
    }
}
