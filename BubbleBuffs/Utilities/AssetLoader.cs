using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BubbleBuffs.Config;
using UnityEngine;

namespace BubbleBuffs.Utilities {
    public class AssetLoader {
        public static Sprite LoadInternal(string folder, string file, Vector2Int size) {
            return Image2Sprite.Create($"{ModSettings.ModEntry.Path}Assets{Path.DirectorySeparatorChar}{folder}{Path.DirectorySeparatorChar}{file}", size);
        }
        // Loosely based on https://forum.unity.com/threads/generating-sprites-dynamically-from-png-or-jpeg-files-in-c.343735/
        public static class Image2Sprite {
            public static string icons_folder = "";
            public static Sprite Create(string filePath, Vector2Int size) {
                var bytes = File.ReadAllBytes(icons_folder + filePath);
                var texture = new Texture2D(size.x, size.y, TextureFormat.DXT5, false);
                texture.mipMapBias = 15.0f;
                _ = texture.LoadImage(bytes);
                return Sprite.Create(texture, new Rect(0, 0, size.x, size.y), new Vector2(0, 0));
            }
        }

        private static Dictionary<string, GameObject> Objects = new();
        public static Dictionary<string, Sprite> Sprites = new();
        public static Dictionary<string, Mesh> Meshes = new();
        public static Dictionary<string, Material> Materials = new();

        public static void RemoveBundle(string loadAss, bool unloadAll = false) {
            AssetBundle bundle;
            if (bundle = AssetBundle.GetAllLoadedAssetBundles().FirstOrDefault(x => x.name == loadAss))
                bundle.Unload(unloadAll);
            if (unloadAll) {
                Objects.Clear();
                Sprites.Clear();
                Meshes.Clear();
            }
        }

        public static UnityEngine.Object[] Assets;

        public static void AddBundle(string loadAss) {
            try {
                AssetBundle bundle;

                RemoveBundle(loadAss, true);

                var path = Path.Combine(Main.ModPath + loadAss);
                Main.Log($"loading from: {path}");

                bundle = AssetBundle.LoadFromFile(path);
                if (!bundle) throw new Exception($"Failed to load AssetBundle! {Main.ModPath + loadAss}");

                Assets = bundle.LoadAllAssets();

                foreach (var obj in Assets) {
                    Main.Log($"Found asset <{obj.name}> of type [{obj.GetType()}]");
                }

                foreach (var obj in Assets) {
                    if (obj is GameObject gobj)
                        Objects[obj.name] = gobj;
                    else if (obj is Mesh mesh)
                        Meshes[obj.name] = mesh;
                    else if (obj is Sprite sprite)
                        Sprites[obj.name] = sprite;
                    else if (obj is Material material)
                        Materials[obj.name] = material;
                }

                RemoveBundle(loadAss);
            } catch (Exception ex) {
                Main.Error(ex, "LOADING ASSET");
            }
        }
    }
}
