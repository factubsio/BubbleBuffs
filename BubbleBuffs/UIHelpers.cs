using Kingmaker;
using Kingmaker.Utility;
using Owlcat.Runtime.UI.Utility;
using System.Linq;
using UniRx;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BubbleBuffs {
    static class UIHelpers {
        public static WidgetPaths_1_0 WidgetPaths;
        public static Transform Settings => SceneManager.GetSceneByName("UI_LoadingScreen_Scene").GetRootGameObjects().First(x => x.name.StartsWith("CommonPCView")).ChildTransform("Canvas/SettingsView");
        public static Transform StaticRoot => Game.Instance.UI.Canvas.transform;
        public static Transform ServiceWindow => StaticRoot.Find("ServiceWindowsPCView");

        public static Transform SpellbookScreen => ServiceWindow.Find(WidgetPaths.SpellScreen);
        public static Transform MythicInfoView => ServiceWindow.Find(WidgetPaths.MythicView);
        public static Transform EncyclopediaView => ServiceWindow.Find(WidgetPaths.EncyclopediaView);

        public static Transform CharacterScreen => ServiceWindow.Find(WidgetPaths.CharacterScreen);

        public static void SetAnchor(this RectTransform transform, double xMin, double xMax, double yMin, double yMax) {
            transform.anchorMin = new Vector2((float)xMin, (float)yMin);
            transform.anchorMax = new Vector2((float)xMax, (float)yMax);
        }

        public static void SetAnchor(this RectTransform transform, double x, double y) {
            transform.SetAnchor(x, x, y, y);
        }
        public static RectTransform ChildRect(this GameObject obj, string path) {
            return obj.ChildTransform(path) as RectTransform;
        }
        public static RectTransform Rect(this GameObject obj) {
            return obj.transform as RectTransform;
        }
        public static RectTransform Rect(this Transform obj) {
            return obj as RectTransform;
        }

        public static void SetRotate2D(this RectTransform rect, int degrees) {
            rect.eulerAngles = new Vector3(0, 0, degrees);
        }

        public static Transform ChildTransform(this GameObject obj, string path) {
            return obj.transform.Find(path);
        }
        public static GameObject ChildObject(this GameObject obj, string path) {
            return obj.ChildTransform(path)?.gameObject;
        }

        public static GameObject[] ChildObjects(this GameObject obj, params string[] paths) {
            return paths.Select(p => obj.transform.Find(p)?.gameObject).ToArray();
        }

        public static void DestroyChildren(this GameObject obj, params string[] paths) {
            obj.ChildObjects(paths).ForEach(GameObject.Destroy);
        }
        public static void DestroyChildrenImmediate(this GameObject obj, params string[] paths) {
            obj.ChildObjects(paths).ForEach(GameObject.DestroyImmediate);
        }
        public static void DestroyComponents<T>(this GameObject obj) where T : UnityEngine.Object {
            var componentList = obj.GetComponents<T>();
            foreach (var c in componentList)
                GameObject.DestroyImmediate(c);
        }
    }
    class WidgetPaths_1_0 {
        public virtual string SpellScreen => "SpellbookView/SpellbookScreen";
        public virtual string MythicView => "MythicInfoView";
        public virtual string EncyclopediaView => "EncyclopediaView";

        public virtual string CharacterScreen => "CharacterInfoView/CharacterScreen";
    }

    class WidgetPaths_1_1 : WidgetPaths_1_0 {
        public override string SpellScreen => "SpellbookPCView/SpellbookScreen";
        public override string MythicView => "MythicInfoPCView";
        public override string EncyclopediaView => "EncyclopediaPCView";
        public override string CharacterScreen => "CharacterInfoPCView/CharacterScreen";
    }
}
