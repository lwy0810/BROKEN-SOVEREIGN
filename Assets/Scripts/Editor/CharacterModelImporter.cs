using System.IO;
using UnityEditor;

namespace BrokenSovereign.Editor
{
    /// <summary>
    /// 캐릭터 FBX 임포트 규칙 (Mixamo 리깅 기준).
    /// - Assets/Models/ 아래 FBX: Humanoid, 애니메이션·머티리얼 가져오지 않음 (머티리얼은 M_ 에셋을 직접 연결)
    ///   · 파일 이름이 Prop·Weapon으로 시작하면 정적 모델: 리그 없음
    /// - Assets/Animations/ 아래 FBX: Humanoid, 머티리얼 없음, 클립 설정 적용
    ///   · 루트 이동·회전·높이를 모두 포즈에 굽기 (이동은 NavMeshAgent 담당)
    ///   · 이름에 Idle·Run·Walk가 들어가면 반복 재생
    /// 처음 임포트할 때만 적용합니다. 이후 인스펙터에서 바꾼 값은 유지됩니다.
    /// </summary>
    public class CharacterModelImporter : AssetPostprocessor
    {
        public const string MODELS_ROOT = "Assets/Models/";
        public const string ANIMATIONS_ROOT = "Assets/Animations/";

        public const string PROP_PREFIX = "Prop";
        public const string WEAPON_PREFIX = "Weapon";

        private static readonly string[] LOOP_KEYWORDS = { "Idle", "Run", "Walk" };

        private void OnPreprocessModel()
        {
            if (!assetImporter.importSettingsMissing || !IsFbx(assetPath))
            {
                return;
            }

            ModelImporter importer = (ModelImporter)assetImporter;

            if (assetPath.StartsWith(MODELS_ROOT) && IsPropName(Path.GetFileNameWithoutExtension(assetPath)))
            {
                ApplyPropSettings(importer);
            }
            else if (assetPath.StartsWith(MODELS_ROOT))
            {
                ApplyModelSettings(importer);
            }
            else if (assetPath.StartsWith(ANIMATIONS_ROOT))
            {
                ApplyAnimationSettings(importer);
            }
        }

        private void OnPreprocessAnimation()
        {
            if (!assetImporter.importSettingsMissing || !IsFbx(assetPath) || !assetPath.StartsWith(ANIMATIONS_ROOT))
            {
                return;
            }

            ApplyClipSettings((ModelImporter)assetImporter, assetPath);
        }

        /// <summary>캐릭터 모델 FBX 설정. 메뉴에서 기존 에셋에 다시 적용할 때도 사용합니다.</summary>
        public static void ApplyModelSettings(ModelImporter importer)
        {
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importBlendShapes = false;
            importer.importCameras = false;
            importer.importLights = false;
        }

        /// <summary>정적 소품 FBX 설정. 충돌은 프리팹의 콜라이더가 담당합니다.</summary>
        public static void ApplyPropSettings(ModelImporter importer)
        {
            importer.animationType = ModelImporterAnimationType.None;
            importer.importAnimation = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importBlendShapes = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.addCollider = false;
        }

        public static bool IsPropName(string fileName)
        {
            return fileName.StartsWith(PROP_PREFIX) || fileName.StartsWith(WEAPON_PREFIX);
        }

        /// <summary>애니메이션 FBX 설정 (클립 설정 제외).</summary>
        public static void ApplyAnimationSettings(ModelImporter importer)
        {
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importBlendShapes = false;
            importer.importCameras = false;
            importer.importLights = false;
        }

        /// <summary>클립 이름을 파일 이름으로 바꾸고 반복·루트 굽기를 설정합니다.</summary>
        public static void ApplyClipSettings(ModelImporter importer, string path)
        {
            string fileName = Path.GetFileNameWithoutExtension(path);
            ModelImporterClipAnimation[] clips = importer.clipAnimations;

            if (clips == null || clips.Length == 0)
            {
                clips = importer.defaultClipAnimations;
            }

            if (clips == null || clips.Length == 0)
            {
                return;
            }

            bool loop = IsLoopName(fileName);

            // FBX를 같은 이름으로 교체해도 이전 클립 길이가 남지 않도록 프레임 범위를 파일 기본값으로 맞춤 (10/20)
            ModelImporterClipAnimation[] defaults = importer.defaultClipAnimations;

            for (int i = 0; i < clips.Length; i++)
            {
                ModelImporterClipAnimation clip = clips[i];
                clip.name = clips.Length == 1 ? fileName : $"{fileName}_{i}";

                foreach (ModelImporterClipAnimation source in defaults)
                {
                    if (source.takeName == clip.takeName)
                    {
                        clip.firstFrame = source.firstFrame;
                        clip.lastFrame = source.lastFrame;
                        break;
                    }
                }
                clip.loopTime = loop;
                clip.loopPose = loop;
                clip.lockRootRotation = true;
                clip.lockRootHeightY = true;
                clip.lockRootPositionXZ = true;
                clip.keepOriginalOrientation = true;
                clip.keepOriginalPositionY = true;
                clip.keepOriginalPositionXZ = true;
            }

            importer.clipAnimations = clips;
        }

        public static bool IsLoopName(string fileName)
        {
            foreach (string keyword in LOOP_KEYWORDS)
            {
                if (fileName.Contains(keyword))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsFbx(string path)
        {
            return path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase);
        }
    }
}
