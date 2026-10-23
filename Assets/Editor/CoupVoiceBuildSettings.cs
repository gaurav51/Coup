#if UNITY_ANDROID
using System.IO;
using System.Xml;
using UnityEditor.Android;

namespace Coup.Editor
{
    /// <summary>Declare voice permissions; the client asks for mic access only on explicit unmute.</summary>
    public sealed class CoupVoiceBuildSettings : IPostGenerateGradleAndroidProject
    {
        public int callbackOrder => 100;
        public void OnPostGenerateGradleAndroidProject(string path)
        {
            var file = Path.Combine(path, "src/main/AndroidManifest.xml");
            var xml = new XmlDocument(); xml.Load(file);
            const string android = "http://schemas.android.com/apk/res/android";
            var manifest = xml.DocumentElement;
            foreach (var permission in new[] { "android.permission.RECORD_AUDIO", "android.permission.MODIFY_AUDIO_SETTINGS" })
            {
                bool exists = false;
                foreach (XmlNode node in manifest.SelectNodes("uses-permission"))
                    if (node.Attributes?["name", android]?.Value == permission) exists = true;
                if (exists) continue;
                var item = xml.CreateElement("uses-permission"); item.SetAttribute("name", android, permission); manifest.AppendChild(item);
            }
            var application = manifest.SelectSingleNode("application");
            XmlElement dialog = null;
            foreach (XmlNode node in application.SelectNodes("meta-data"))
                if (node.Attributes?["name", android]?.Value == "unityplayer.SkipPermissionsDialog") dialog = (XmlElement)node;
            if (dialog == null) { dialog = xml.CreateElement("meta-data"); dialog.SetAttribute("name", android, "unityplayer.SkipPermissionsDialog"); application.AppendChild(dialog); }
            dialog.SetAttribute("value", android, "true");
            xml.Save(file);
        }
    }
}
#endif
