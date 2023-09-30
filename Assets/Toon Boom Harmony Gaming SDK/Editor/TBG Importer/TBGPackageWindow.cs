
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace ToonBoom.TBGImporter
{
    public class TBGPackageWindow : EditorWindow
    {
        public Dictionary<string, string> MissingPackages = new Dictionary<string, string>();
        public enum DownloadState
        {
            Idle,
            ListExisting,
            Downloading,
        }
        public ListRequest ListRequest;
        public AddRequest AddRequest;
        public DownloadState State = DownloadState.Idle;

        [MenuItem("Assets/Harmony/Package Dependencies")]
        public static async void Init()
        {
            var missingPackages = await FindMissingPackagesAsync();
            var window = EditorWindow.GetWindow<TBGPackageWindow>("Harmony SDK Packages");
            window.MissingPackages = missingPackages;
            window.Show();
        }
        public static async void CheckAndInit()
        {
            var missingPackages = await FindMissingPackagesAsync();
            if (missingPackages.Count == 0)
                return;
            var window = EditorWindow.GetWindow<TBGPackageWindow>("Harmony SDK Packages");
            window.MissingPackages = missingPackages;
            window.Show();
        }

        public struct Package
        {
            public string name;
            public string minimumVersion;
        }

        public struct UnityToPackages
        {
            public string unityVersion;
            public Package[] packages;
        }


        static string Unity2DAnimationPackage = "com.unity.2d.animation";
        static readonly Dictionary<string, string> UnityVersionTo2DAnimationVersion = new Dictionary<string, string>()
    {
        { "2022.2", "9" },
        { "2022.1.23f1", "8.0.5" },
        { "2022.1.16f1", "8.0.5" },
        { "2022.1", "8" },
        { "2021.3.11f1", "7.0.9" },
        { "2021.3.9f1", "7.0.6" },
        { "2021.3.6f1", "7.0.6" },
        { "2021.3", "7" },
        { "2021.2", "7" },
        { "2021.1", "6" },
        { "2020.3.36f1", "5.2.3" },
        { "2020.3", "5" },
        { "2020.2", "5" },
        { "2020.1", "4" },
        { "2019.4", "3" },
        { "2019.3", "3" },
    };

        static readonly Dictionary<string, string> PackageToVersion = new Dictionary<string, string>()
    {
        { "com.unity.burst", "1.8.1" },
        { "com.unity.collections", "1.4.0" },
    };

        public static Dictionary<string, string> PackageToVersionWithUnity2DAnimation()
        {
            var packageToVersion = new Dictionary<string, string>(PackageToVersion);
            var unityVersion = Application.unityVersion;
            // Try finding an exact match for compatibility
            if (UnityVersionTo2DAnimationVersion.TryGetValue(unityVersion, out var version))
            {
                packageToVersion[Unity2DAnimationPackage] = version;
            }
            else
            {
                // If we don't have a specific version for this Unity version, use the latest version
                var match = Regex.Match(unityVersion, @"^(\d+\.\d+)\.\d+");
                if (match.Success)
                {
                    // Find the latest version for this major.minor version
                    var majorMinorVersion = match.Groups[1].Value;
                    // Find the latest 2D Animation package version for this major.minor version
                    if (UnityVersionTo2DAnimationVersion.TryGetValue(majorMinorVersion, out version))
                    {
                        packageToVersion[Unity2DAnimationPackage] = version;
                    }
                }
            }
            return packageToVersion;
        }

        public void OnGUI()
        {
            var userSettings = GetUserSettings();

            bool dismiss;
            bool download = false;

            if (DownloadState.Downloading == State)
                GUI.enabled = false;

            GUILayout.BeginVertical(new GUIStyle
            {
                margin = new RectOffset(16, 16, 16, 16),
            });
            {
                GUILayout.Label("Additional Packages Requested", EditorStyles.boldLabel);
                GUILayout.Space(16);
                {
                    EditorGUILayout.BeginVertical(new GUILayoutOption[] {
                    GUILayout.ExpandHeight(true),
                });
                    GUILayout.Label("Toon Boom Game File workflow requires additional packages to be downloaded in order to be able to import .tbg files", new GUIStyle(EditorStyles.helpBox)
                    {
                        fontSize = EditorStyles.label.fontSize,
                    });
                    GUILayout.Space(16);
                    GUILayout.Label(MissingPackages.Count > 0
                            ? "The following packages are recommended for compatibility:"
                            : "You are compatible!",
                        new GUIStyle(EditorStyles.label)
                        {
                            alignment = TextAnchor.MiddleRight,
                            wordWrap = true,
                        });
                    var requiredPackages = PackageToVersionWithUnity2DAnimation();
                    foreach (var package in requiredPackages)
                    {
                        GUILayout.Space(16);
                        GUILayout.BeginHorizontal();
                        GUILayout.FlexibleSpace();
                        if (MissingPackages.Where(missing => missing.Key == package.Key).Count() == 0)
                            GUILayout.Label(EditorGUIUtility.IconContent("P4_CheckOutRemote"));
                        // Spinner icon while downloading
                        else if (State == DownloadState.Downloading)
                            GUILayout.Label(EditorGUIUtility.IconContent("WaitSpin00"));
                        else
                            GUILayout.Label(EditorGUIUtility.IconContent("console.warnicon.sml"));
                        GUILayout.Label($"{package.Key}@{package.Value}", new GUIStyle(EditorStyles.toolbarTextField)
                        {
                            alignment = TextAnchor.MiddleRight,
                        });
                        GUILayout.EndHorizontal();
                    }
                    EditorGUILayout.EndVertical();
                }
                {
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();
                    {
                        if (MissingPackages.Count > 0)
                        {
                            EditorGUILayout.BeginHorizontal(new GUILayoutOption[] { GUILayout.Width(300) });
                            userSettings.PackageWindowDontAskAgain = GUILayout.Toggle(userSettings.PackageWindowDontAskAgain, "Don't Ask Me Again");
                            GUILayout.Space(50);
                            dismiss = GUILayout.Button("Not Now");
                            using (new EditorGUI.DisabledScope(State != DownloadState.Idle))
                            {
                                download = GUILayout.Button("Download");
                            }
                            EditorGUILayout.EndHorizontal();
                        }
                        else
                        {
                            dismiss = GUILayout.Button("Dismiss");
                        }
                    }
                    EditorGUILayout.EndHorizontal();
                }
                GUILayout.EndVertical();
            }

            if (dismiss)
            {
                userSettings.Dismiss();
                Close();
            }
            else if (download)
                State = DownloadState.ListExisting;
            if (State != DownloadState.Idle)
            {
                DownloadThenReimport();
            }
        }
        public static Dictionary<string, string> GetMissingFromExisting(PackageCollection existingPackages)
        {
            var requiredPackages = PackageToVersionWithUnity2DAnimation();
            return requiredPackages
                .Where(required => !existingPackages
                    .Where(existing =>
                    {
                        var requiredVersion = required.Value
                            .Split('.')
                            .Select(text => Regex.Match(text, @"\d+").Value)
                            .Select(int.Parse);
                        var existingVersion = existing.version
                            .Split('.')
                            .Select(text => Regex.Match(text, @"\d+").Value)
                            .Select(int.Parse);
                        var upToDate = true;
                        foreach (var entry in requiredVersion
                            .Zip(existingVersion, (required, existing) => new { required, existing }))
                        {
                            if (entry.existing > entry.required)
                            {
                                break;
                            }
                            if (entry.existing < entry.required)
                            {
                                upToDate = false;
                                break;
                            }
                        }
                        return required.Key == existing.name && upToDate;
                    })
                    .Any())
                .ToDictionary(entry => entry.Key, entry => entry.Value);
        }
        public static async Task<Dictionary<string, string>> FindMissingPackagesAsync()
        {
            var existingPackages = Client.List();
            while (!existingPackages.IsCompleted)
                await Task.Delay(10);
            return GetMissingFromExisting(existingPackages.Result);
        }
        public void DownloadThenReimport()
        {
            switch (State)
            {
                case DownloadState.ListExisting:
                    if (ListRequest == null || ListRequest.IsCompleted && ListRequest.Result == null)
                        ListRequest = Client.List();
                    if (ListRequest.IsCompleted)
                    {
                        MissingPackages = GetMissingFromExisting(ListRequest.Result);
                        State = DownloadState.Downloading;
                        DownloadThenReimport();
                    }
                    break;
                case DownloadState.Downloading:
                    if (AddRequest == null)
                    {
                        if (MissingPackages.Count > 0)
                        {
                            var missingPackage = MissingPackages.First();
                            Debug.Log($"Requesting {missingPackage.Key}!");
                            AddRequest = Client.Add(@$"{missingPackage.Key}@{missingPackage.Value}");
                            MissingPackages.Remove(missingPackage.Key);
                            EditorApplication.update += DownloadThenReimport;
                        }
                        else
                        {
                            Debug.Log("All Downloads Complete!");
                            State = DownloadState.Idle;
                            ReimportTBGFiles();
                        }
                    }
                    else if (AddRequest.IsCompleted)
                    {
                        if (AddRequest.Error != null)
                        {
                            Debug.Log($"Request Error ({AddRequest.Error.errorCode}) - {AddRequest.Error.message}");
                        }
                        else if (AddRequest.Result != null)
                        {
                            Debug.Log($"{AddRequest.Result.name} Downloaded!");
                        }
                        AddRequest = null;
                        EditorApplication.update -= DownloadThenReimport;
                        DownloadThenReimport();
                    }
                    break;
                default:
                    break;
            }
        }
        public void ReimportTBGFiles()
        {
            var storeGUIDs = AssetDatabase.FindAssets("t:TBGStore");
            foreach (var assetPath in storeGUIDs
                .Select(AssetDatabase.GUIDToAssetPath))
            {
                Debug.Log($"Reimporing {assetPath}");
                AssetDatabase.ImportAsset(assetPath);
            }
        }
        public static TBGUserSettings GetUserSettings()
        {
            var userSettingsPath = AssetDatabase.GUIDToAssetPath(AssetDatabase.FindAssets("t:TBGUserSettings").FirstOrDefault());
            if (string.IsNullOrEmpty(userSettingsPath))
            {
                userSettingsPath = "Assets/TBGUserSettings.asset";
                AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<TBGUserSettings>(), userSettingsPath);
            }
            var userSettings = AssetDatabase.LoadAssetAtPath<TBGUserSettings>(userSettingsPath);
            return userSettings;
        }
    }
}