using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;
using Debug = UnityEngine.Debug;

namespace Itch.io_Uploader {

    public class BuildWindow : EditorWindow {

        private string buildDescription;

        private bool incrementVersion = true;

        private bool sendToDiscord = true;

        private static Config config;
        
        private static readonly string configPath = "Assets/Itch.io Uploader/Uploader Config.asset";
        
        [MenuItem("Build Options/Build Window")]
        public static void ShowWindow () {
            GetWindow(typeof(BuildWindow), false, "Build Uploader");
            Debug.Log("Shown");
            
            LoadConfig();
        }

        private static void LoadConfig() {
            config = AssetDatabase.LoadAssetAtPath<Config>(
                configPath);

            if (config == null) {
                Debug.LogWarning("No config found. Creating one.");
                AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<Config>(),
                    configPath);
                config = AssetDatabase.LoadAssetAtPath<Config>(
                    configPath);
                Selection.activeObject = config;
            }
        }

        void OnGUI()
        {
            if (config == null) {
                LoadConfig();
            }
            
            if (config.Filled()) {
                buildDescription = EditorGUILayout.TextArea(buildDescription, GUILayout.MaxHeight(100));
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label($"Current version: {PlayerSettings.bundleVersion}");
                incrementVersion = EditorGUILayout.Toggle("Increment Version", incrementVersion);
                EditorGUILayout.EndHorizontal();

                if (config.discordHookUrl != "") {
                    sendToDiscord = EditorGUILayout.Toggle("Send to Discord", sendToDiscord);
                }
                else {
                    var style = new GUIStyle {normal = {textColor = Color.yellow}};
                    EditorGUILayout.LabelField("Discord hook url not configured, cannot send message to discord.", style);
                    sendToDiscord = false;
                }
                
                
                if (GUILayout.Button("Build and upload")) {
                    if (EditorUtility.DisplayDialog("Build and upload", "Are you sure you want to upload a new build?", "Yes", "No")) {
                        BuildAndUpload(buildDescription);
                        buildDescription = "";
                    }
                }
            }
            else {
                var style = new GUIStyle {normal = {textColor = Color.red}, alignment = TextAnchor.MiddleCenter};
                EditorGUILayout.LabelField("Paths in config are not configured!", style);
                if (GUILayout.Button("Open Config")) {
                    config = AssetDatabase.LoadAssetAtPath<Config>(
                        configPath);
                    Selection.activeObject = config;
                }
            }
        }

        private void BuildAndUpload(string description) {
            LoadConfig();


            var activeScenes = EditorBuildSettingsScene.GetActiveSceneList(EditorBuildSettings.scenes);
            
            if (activeScenes.Length == 0) {
                Debug.LogError("No scenes to build selected. Set scenes in Build Settings.");
                return;
            }
            string version = incrementVersion ? IncrementVersion() : PlayerSettings.bundleVersion;
            
            PlayerSettings.bundleVersion = version;
            string path = $"Builds/{PlayerSettings.productName} {version}/{PlayerSettings.productName}.exe";
            
            BuildPlayerOptions options = new BuildPlayerOptions {
                scenes = activeScenes,
                locationPathName = path,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };
            
            BuildPipeline.BuildPlayer(options);

            string zippedBuildPath = CompressBuild(version);
            
            PushBuildWithButler(zippedBuildPath, version);
            
            Debug.Log("Done uploading");

            if (sendToDiscord) {
                SendMessageToDiscord($"Build ver. {PlayerSettings.bundleVersion} uploaded to itch.io!\n" +
                                  $"{description}");
            }
        }

        private static string CompressBuild(string version) {
            string buildsPath = $@"{Directory.GetCurrentDirectory()}\Builds\";

            string fromPath = buildsPath + $"{PlayerSettings.productName} {version}";
            string toPath = buildsPath + $"{PlayerSettings.productName} {version}.zip";

            try {
                ZipFile.CreateFromDirectory(fromPath, toPath);
            }
            catch (Exception e) {
                Console.WriteLine(e);
                throw;
            }
            return toPath;
        }

        private static void PushBuildWithButler(string zipPath, string version) {
            Process process;
            process = Process.Start(config.butlerExecutablePath,
                $"push \"{zipPath}\" {config.userName}/{config.gameName}:{config.channelName} --userversion {version}");
            process?.WaitForExit();
        }

        private static string IncrementVersion() {
            string[] nums = PlayerSettings.bundleVersion.Split('.');

            if (nums.Length == 2) {
                nums = new[] {
                    nums[0],
                    nums[1],
                    "0"
                };
            }
            
            int first = Int32.Parse(nums[0]);
            int second = Int32.Parse(nums[1]);
            int third = Int32.Parse(nums[2]);

            third++;
            if (third >= 10) {
                third = 0;
                second++;
            }

            if (second >= 10) {
                second = 0;
                first++;
            }

            return $"{first}.{second}.{third}";
        }

        private static void SendMessageToDiscord(string message) {
            List<IMultipartFormSection> formData = new List<IMultipartFormSection>();
            formData.Add(new MultipartFormDataSection("username", "Build Manager"));
            formData.Add(new MultipartFormDataSection("content", message));
        
            var request = UnityWebRequest.Post(config.discordHookUrl, formData);
            //request.SetRequestHeader("Content-Type", "application/json");
            request.SendWebRequest();
        }
    }
}