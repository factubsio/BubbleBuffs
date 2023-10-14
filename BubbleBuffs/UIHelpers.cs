﻿using Kingmaker;
using Kingmaker.Globalmap;
using Kingmaker.UI.Common;
using Kingmaker.UI;
using Kingmaker.Utility;
using Owlcat.Runtime.UI.Utility;
using System;
using System.Linq;
using UniRx;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BubbleBuffs {
    static class UIHelpers {
        public static WidgetPaths_1_0 WidgetPaths;
        public static Transform Settings => SceneManager.GetSceneByName("UI_LoadingScreen_Scene").GetRootGameObjects().First(x => x.name.StartsWith("CommonPCView")).ChildTransform("Canvas/SettingsView");
        public static Transform UIRoot => UIUtility.IsGlobalMap() ? GlobalMapUI.Instance.transform : StaticCanvas.Instance.transform;
        public static Transform ServiceWindow => UIUtility.IsGlobalMap() ? UIRoot.Find("ServiceWindowsConfig").transform : UIRoot.Find("ServiceWindowsPCView");
        // We deal with two different cases for finding our UI bits (thanks Owlcat!)
        // InGamePCView(Clone)/InGameStaticPartPCView/StaticCanvas/ServiceWindowsPCView
        // GlobalMapPCView(Clone)/StaticCanvas/ServiceWindowsConfig

        public static Transform SpellbookScreen => ServiceWindow.Find(WidgetPaths.SpellScreen);
        public static Transform MythicInfoView => ServiceWindow.Find(WidgetPaths.MythicView);
        public static Transform EncyclopediaView => ServiceWindow.Find(WidgetPaths.EncyclopediaView);

        public static Transform CharacterScreen => ServiceWindow.Find(WidgetPaths.CharacterScreen);

        public static Transform InventoryScreen => ServiceWindow.Find(WidgetPaths.InventoryScreen);

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
        public static T EditComponent<T>(this GameObject obj, Action<T> build) where T : Component {
            var component = obj.GetComponent<T>();
            build(component);
            return component;
        }

        public static string NullStr(this object obj) {
            return obj == null ? "<null>" : $"<good:{obj}>";
        }

        public static Image AddChildImage(this GameObject obj, Sprite sprite) {
            var (child, _) = Create("child-image", obj.transform);
            child.FillParent();
            var img = child.AddComponent<Image>();
            img.sprite = sprite;
            return img;
        }

        public static T MakeComponent<T>(this GameObject obj, Action<T> build) where T : Component {
            var component = obj.AddComponent<T>();
            build(component);
            return component;
        }
        public static void AddTo(this Transform obj, Transform parent) {
            obj.SetParent(parent);
            obj.localPosition = Vector3.zero;
            obj.localScale = Vector3.one;
            obj.localRotation = Quaternion.identity;
        }

        public static void FillParent(this RectTransform rect) {
            rect.SetAnchor(0, 1, 0, 1);
            rect.sizeDelta = Vector2.zero;
        }

        public static void FillParent(this GameObject obj) {
            obj.Rect().FillParent();
        }

        public static (GameObject, RectTransform) Create(string name, Transform parent = null) {
            var obj = new GameObject(name, typeof(RectTransform));
            if (parent != null)
                obj.AddTo(parent);
            return (obj, obj.Rect());
        }

        public static void AddTo(this Transform obj, GameObject parent) { obj.AddTo(parent.transform); }
        public static void AddTo(this GameObject obj, Transform parent) { obj.transform.AddTo(parent); }
        public static void AddTo(this GameObject obj, GameObject parent) { obj.transform.AddTo(parent.transform); }

        private static string MakeTitleCharacter(this char ch) {
            string voffset = "0.1";
            if (ch == 'F' || ch == 'f')
                voffset = "0.2";

            return $"<voffset={voffset}em><font=\"Saber_Dist32\"><color=#672B31><size=130%>{ch}</size></color></font></voffset>";
        }

        public static string MakeTitle(this string str) {
            if (str.Length == 0)
                return "";

            var ret = str[0].MakeTitleCharacter();
            if (str.Length > 1)
                ret += str.Substring(1);
            return ret;
    }
    }
    class WidgetPaths_1_0 {
        public virtual string SpellScreen => "SpellbookView/SpellbookScreen";
        public virtual string MythicView => "MythicInfoView";
        public virtual string EncyclopediaView => "EncyclopediaView";

        public virtual string CharacterScreen => "CharacterInfoView/CharacterScreen";
        public virtual string InventoryScreen => throw new NotImplementedException(); // If we ever need to support old stuff then put something here

    }

    class WidgetPaths_1_1 : WidgetPaths_1_0 {
        public override string SpellScreen => "SpellbookPCView/SpellbookScreen";
        public override string MythicView => "MythicInfoPCView";
        public override string EncyclopediaView => "EncyclopediaPCView";
        public override string CharacterScreen => "CharacterInfoPCView/CharacterScreen";
    }

    class WidgetPaths_1_2 : WidgetPaths_1_1 {

    }
    class WidgetPaths_1_4 : WidgetPaths_1_2 {
        public override string SpellScreen => "Background/Windows/SpellbookPCView/SpellbookScreen";
        public override string MythicView => "Background/Windows/MythicInfoPCView";
        public override string EncyclopediaView => "Background/Windows/EncyclopediaPCView";
        public override string CharacterScreen => "Background/Windows/CharacterInfoPCView/CharacterScreen";

    }
    class WidgetPaths_2_0 : WidgetPaths_1_4 {
        public override string InventoryScreen => "Background/Windows/InventoryPCView";
    }
}
