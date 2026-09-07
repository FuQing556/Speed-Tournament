#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace SpeedTournament.Editor
{
    public static class AndroidBuild
    {
        [Serializable] sealed class Summary
        {
            public string result,output,version,applicationId,unityVersion;
            public double seconds;
            public ulong bytes;
            public uint errors,warnings;
        }
        [MenuItem("Speed Tournament/Build Android APK")]
        public static void BuildApk()
        {
            if(BuildPipeline.isBuildingPlayer)throw new BuildFailedException("Another build is running.");
            if(!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android,BuildTarget.Android))
                throw new BuildFailedException("Install this Unity version's Android Build Support, SDK, NDK and OpenJDK in Unity Hub.");
            string sdk=AndroidExternalToolsSettings.sdkRootPath,ndk=AndroidExternalToolsSettings.ndkRootPath,jdk=AndroidExternalToolsSettings.jdkRootPath;
            try
            {
                // Optional local ASCII junction; never copy/install toolchains into source control.
                string alias=Path.GetFullPath("LocalTools/Android");
                if(Directory.Exists(Path.Combine(alias,"NDK")))
                {
                    AndroidExternalToolsSettings.sdkRootPath=Path.Combine(alias,"SDK");
                    AndroidExternalToolsSettings.ndkRootPath=Path.Combine(alias,"NDK");
                    AndroidExternalToolsSettings.jdkRootPath=Path.Combine(alias,"OpenJDK");
                }
                if(EditorUserBuildSettings.activeBuildTarget!=BuildTarget.Android&&
                   !EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android,BuildTarget.Android))
                    throw new BuildFailedException("Could not activate Android. Switch target in Build Profiles first.");
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android,ScriptingImplementation.IL2CPP);
                PlayerSettings.Android.targetArchitectures=AndroidArchitecture.ARM64;
                PlayerSettings.defaultInterfaceOrientation=UIOrientation.LandscapeLeft;
                EditorUserBuildSettings.buildAppBundle=false;
                EditorUserBuildSettings.development=false;
                EditorUserBuildSettings.androidCreateSymbols=AndroidCreateSymbols.Debugging;
                AssetDatabase.SaveAssets();
                string output="Builds/Android/SpeedTournament-"+PlayerSettings.bundleVersion+"-arm64.apk";
                Directory.CreateDirectory(Path.GetDirectoryName(output));
                var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes=new[]{"Assets/Scenes/SampleScene.unity"},target=BuildTarget.Android,
                    locationPathName=output,options=BuildOptions.CompressWithLz4|BuildOptions.DetailedBuildReport
                });
                var s=report.summary;
                File.WriteAllText("Builds/Android/build-summary.json",JsonUtility.ToJson(new Summary
                {
                    result=s.result.ToString(),output=output,version=PlayerSettings.bundleVersion,
                    applicationId=PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android),unityVersion=Application.unityVersion,
                    seconds=s.totalTime.TotalSeconds,bytes=s.totalSize,errors=(uint)s.totalErrors,warnings=(uint)s.totalWarnings
                },true));
                if(s.result!=BuildResult.Succeeded)throw new BuildFailedException("Android build "+s.result+"; inspect the Console and build-summary.json.");
                Debug.Log("Android APK ready: "+Path.GetFullPath(output));
            }
            finally
            {
                AndroidExternalToolsSettings.sdkRootPath=sdk;
                AndroidExternalToolsSettings.ndkRootPath=ndk;
                AndroidExternalToolsSettings.jdkRootPath=jdk;
            }
        }
    }
}
#endif
