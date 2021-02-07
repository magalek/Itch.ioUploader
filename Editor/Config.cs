using UnityEngine;

namespace Itch.io_Uploader {
    public class Config : ScriptableObject {
        [Header("Butler Settings")]
        public string butlerExecutablePath;
        public string userName;
        public string gameName;
        public string channelName;

        [Header("Discord Settings")]
        public string discordHookUrl;

        public bool Filled() {
            if (butlerExecutablePath == null) {
                return false;
            }
            
            return butlerExecutablePath != "" &&
                   userName != "" &&
                   gameName != "" &&
                   channelName != "";
        }
    }
}