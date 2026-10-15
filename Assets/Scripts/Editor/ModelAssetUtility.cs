using System.IO;
using UnityEditor;
using UnityEngine;

namespace BrokenSovereign.Editor
{
    /// <summary>
    /// 모델 에셋 공용 처리 (캐릭터·소품 적용 도구가 함께 사용).
    /// 텍스처 이름 규칙: {폴더}/T_{이름}_{BaseColor|Normal|MetallicSmoothness}.png
    /// </summary>
    public static class ModelAssetUtility
    {
        public const string URP_LIT = "Universal Render Pipeline/Lit";

        public static string TexturePath(string textureRoot, string name, string role)
        {
            return $"{textureRoot}/T_{name}_{role}.png";
        }

        /// <summary>BaseColor는 sRGB, Normal은 노멀 맵, MetallicSmoothness는 선형으로 설정합니다.</summary>
        public static void ConfigureTextures(string textureRoot, string name)
        {
            ConfigureTexture(TexturePath(textureRoot, name, "BaseColor"), TextureImporterType.Default, true);
            ConfigureTexture(TexturePath(textureRoot, name, "Normal"), TextureImporterType.NormalMap, false);
            ConfigureTexture(TexturePath(textureRoot, name, "MetallicSmoothness"), TextureImporterType.Default, false);
        }

        public static void ConfigureTexture(string path, TextureImporterType type, bool sRgb)
        {
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
            {
                Debug.LogWarning($"ModelAssetUtility: 텍스처 없음 ({path})");
                return;
            }

            importer.textureType = type;
            importer.sRGBTexture = sRgb;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
        }

        /// <summary>URP Lit 머티리얼을 만들거나 갱신합니다.</summary>
        public static Material BuildLitMaterial(string materialPath, string textureRoot, string name)
        {
            EnsureFolder(Path.GetDirectoryName(materialPath)?.Replace('\\', '/'));
            Shader shader = Shader.Find(URP_LIT);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);

            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, materialPath);
            }

            material.shader = shader;
            material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath(textureRoot, name, "BaseColor")));

            Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath(textureRoot, name, "Normal"));
            material.SetTexture("_BumpMap", normal);
            SetKeyword(material, "_NORMALMAP", normal != null);

            // URP Lit: 금속성 텍스처 R = 금속성, A = 매끄러움 (_Smoothness는 배율)
            Texture2D metallic = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath(textureRoot, name, "MetallicSmoothness"));
            material.SetTexture("_MetallicGlossMap", metallic);
            material.SetFloat("_SmoothnessTextureChannel", 0f);
            material.SetFloat("_Smoothness", 1f);
            material.SetFloat("_Metallic", 0f);
            SetKeyword(material, "_METALLICSPECGLOSSMAP", metallic != null);

            EditorUtility.SetDirty(material);
            return material;
        }

        public static void SetKeyword(Material material, string keyword, bool enabled)
        {
            if (enabled)
            {
                material.EnableKeyword(keyword);
            }
            else
            {
                material.DisableKeyword(keyword);
            }
        }

        public static void EnsureFolder(string path)
        {
            if (string.IsNullOrEmpty(path) || AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
