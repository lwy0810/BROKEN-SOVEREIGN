using System.Linq;
using BrokenSovereign.Map;
using BrokenSovereign.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BrokenSovereign.Editor
{
    /// <summary>
    /// HUD 아트 연결 도구 (에디터 전용). 메뉴: BrokenSovereign > UI > HUD 아트 연결 (열린 씬)
    ///
    /// 1. NPC 레이어 만들기 (커서 규칙·NPC 프리팹용)
    /// 2. 커서 그림(Sprites/Cursors) → Cursor 텍스처, 지도 아이콘(Sprites/Map/Icons) → Sprite 로 임포트 설정
    /// 3. Settings/CursorSettings.asset (기본 + Enemy → 공격, NPC → 대화), Settings/MapIconSet.asset 생성·갱신
    /// 4. 열린 씬의 모든 HudView에 연결
    /// 커서 그림·아이콘은 Python 스크립트로 그린 임시 아트입니다 (Docs/AiPipeline.md).
    /// </summary>
    public static class UiArtTools
    {
        public const string NPC_LAYER = "NPC";
        private const string CURSOR_ROOT = "Assets/Sprites/Cursors";
        private const string ICON_ROOT = "Assets/Sprites/Map/Icons";
        private const string CURSOR_SETTINGS = "Assets/Settings/CursorSettings.asset";
        private const string ICON_SET = "Assets/Settings/MapIconSet.asset";
        private const float SHOP_VIEW_WIDTH = 420f;   // 이 폭(m)보다 넓게 축소하면 상점 아이콘 숨김

        [MenuItem("BrokenSovereign/UI/HUD 아트 연결 (열린 씬)")]
        private static void Connect()
        {
            EnsureLayer(NPC_LAYER);
            CursorSettings cursor = BuildCursorSettings();
            MapIconSet icons = BuildIconSet();
            AssetDatabase.SaveAssets();

            int count = 0;

            foreach (HudView hud in Object.FindObjectsByType<HudView>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var so = new SerializedObject(hud);
                so.FindProperty("_cursorSettings").objectReferenceValue = cursor;
                so.FindProperty("_mapIconSet").objectReferenceValue = icons;
                so.ApplyModifiedProperties();
                EditorSceneManager.MarkSceneDirty(hud.gameObject.scene);
                count++;
            }

            Debug.Log($"UiArtTools: 커서·지도 아이콘 연결 (HudView {count}개). NPC 레이어 적용은 Town > 상점·NPC 프리팹 다시 만들기 후 맵을 다시 생성하세요. 씬을 저장하세요.");
        }

        /// <summary>레이어가 없으면 빈 칸(8번 이후)에 추가하고 번호를 돌려 줍니다.</summary>
        public static int EnsureLayer(string name)
        {
            int existing = LayerMask.NameToLayer(name);

            if (existing >= 0)
            {
                return existing;
            }

            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");

            for (int i = 8; i < layers.arraySize; i++)
            {
                SerializedProperty layer = layers.GetArrayElementAtIndex(i);

                if (string.IsNullOrEmpty(layer.stringValue))
                {
                    layer.stringValue = name;
                    tagManager.ApplyModifiedProperties();
                    Debug.Log($"UiArtTools: 레이어 {i}번 '{name}' 추가");
                    return i;
                }
            }

            Debug.LogError($"UiArtTools: 빈 레이어 칸이 없어 '{name}'를 만들지 못했습니다.");
            return -1;
        }

        private static CursorSettings BuildCursorSettings()
        {
            Texture2D Load(string name) => ConfigureCursor($"{CURSOR_ROOT}/T_Cursor{name}.png");

            CursorSettings settings = LoadOrCreate<CursorSettings>(CURSOR_SETTINGS);
            settings.DefaultTexture = Load("Default");
            settings.DefaultHotspot = new Vector2(2f, 2f);
            settings.Rules = new[]
            {
                new CursorSettings.Rule
                {
                    Name = "적 (공격)", Layers = LayerMask.GetMask("Enemy"), Texture = Load("Attack"), Hotspot = new Vector2(2f, 2f),
                },
                new CursorSettings.Rule
                {
                    Name = "NPC (대화)", Layers = LayerMask.GetMask(NPC_LAYER), Texture = Load("Talk"), Hotspot = new Vector2(2f, 2f),
                },
            };
            EditorUtility.SetDirty(settings);
            ConfigureCursor($"{CURSOR_ROOT}/T_CursorBlocked.png");
            return settings;
        }

        private static MapIconSet BuildIconSet()
        {
            MapIconSet set = LoadOrCreate<MapIconSet>(ICON_SET);
            var kinds = new[]
            {
                MapMarkerKind.Merchant, MapMarkerKind.Smith, MapMarkerKind.Healer, MapMarkerKind.Waypoint,
                MapMarkerKind.Event, MapMarkerKind.Trial, MapMarkerKind.Boss,
            };

            set.Entries = kinds.Select(kind => new MapIconSet.Entry
            {
                Kind = kind,
                Sprite = ConfigureSprite($"{ICON_ROOT}/T_MapIcon{kind}.png"),
                MaxViewWidth = MapIcons.IsShop(kind) ? SHOP_VIEW_WIDTH : 0f,
            }).ToArray();
            EditorUtility.SetDirty(set);
            return set;
        }

        private static Texture2D ConfigureCursor(string path)
        {
            if (AssetImporter.GetAtPath(path) is TextureImporter importer)
            {
                importer.textureType = TextureImporterType.Cursor;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.SaveAndReimport();
            }
            else
            {
                Debug.LogWarning($"UiArtTools: 커서 그림 없음 ({path})");
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static Sprite ConfigureSprite(string path)
        {
            if (AssetImporter.GetAtPath(path) is TextureImporter importer)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = true;
                importer.SaveAndReimport();
            }
            else
            {
                Debug.LogWarning($"UiArtTools: 지도 아이콘 없음 ({path})");
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);

            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(asset, path);
            }

            return asset;
        }
    }
}
