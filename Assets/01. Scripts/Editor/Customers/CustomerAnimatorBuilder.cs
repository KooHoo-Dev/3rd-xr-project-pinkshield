using System.Collections.Generic;
using System.Linq;
using CardShop.Cards.EditorTools;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace CardShop.EditorTools
{
    /// <summary>
    /// 손님 Animator를 Kevin Iglesias Human Basic Motions 클립으로 만듭니다 (타이틀연출_손님_설계 3-2).
    /// - Customer.controller : 남성 클립. Base(Idle ↔ Walk, Speed) + Gesture(상체 마스크, 트리거 7종)
    /// - Customer_Female.overrideController : 같은 구조에 여성 클립만 바꿔 끼움
    /// 메뉴: Tools > CardShop > Customers > Build Customer Animator
    /// </summary>
    public static class CustomerAnimatorBuilder
    {
        public const string OutDir = "Assets/03. Prefabs/Customers";
        public const string ControllerPath = OutDir + "/Customer.controller";
        public const string FemaleOverridePath = OutDir + "/Customer_Female.overrideController";

        const string KevinRoot = "Assets/Kevin Iglesias/Human Animations";
        const string UpperBodyMaskPath = KevinRoot + "/Models/Avatar Masks/Human Body Upper Mask.mask";

        // (파라미터/상태 이름, Male·Female 폴더 아래 상대 경로의 클립 이름)
        static readonly (string state, string folder, string clip)[] Locomotion =
        {
            ("Idle", "Idles", "Idle01"),
            ("Walk", "Movement/Walk", "Walk01_Forward"),
        };

        // 트리거 이름은 CustomerAnim(런타임)과 같아야 합니다.
        static readonly (string trigger, string folder, string clip)[] Gestures =
        {
            ("Wave", "Social/Conversation", "HandWave01"),
            ("Question", "Social/Conversation", "Question01"),
            ("Nod", "Social/Conversation", "HeadNod01"),
            ("Shake", "Social/Conversation", "HeadShake01"),
            ("Talk", "Social/Conversation", "Talk01"),
            ("Cheer", "Social/Emotions", "Cheer01"),
            ("Angry", "Social/Emotions", "Angry01"),
        };

        [MenuItem("Tools/CardShop/Customers/Build Customer Animator", priority = 60)]
        public static void Build()
        {
            DataPaths.EnsureFolder(OutDir);

            AssetDatabase.DeleteAsset(ControllerPath);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            foreach (var g in Gestures) controller.AddParameter(g.trigger, AnimatorControllerParameterType.Trigger);

            // Base: Idle ↔ Walk
            AnimatorStateMachine baseSm = controller.layers[0].stateMachine;
            AnimatorState idle = baseSm.AddState("Idle", new Vector3(300, 0));
            idle.motion = Clip("Male", Locomotion[0].folder, Locomotion[0].clip);
            AnimatorState walk = baseSm.AddState("Walk", new Vector3(300, 120));
            walk.motion = Clip("Male", Locomotion[1].folder, Locomotion[1].clip);
            baseSm.defaultState = idle;
            Link(idle, walk, AnimatorConditionMode.Greater, 0.1f);
            Link(walk, idle, AnimatorConditionMode.Less, 0.1f);

            // Gesture: 상체만 덮어쓰기. 빈 상태(Empty)에서는 아래 레이어가 그대로 보임
            var gestureSm = new AnimatorStateMachine { name = "Gesture", hideFlags = HideFlags.HideInHierarchy };
            AssetDatabase.AddObjectToAsset(gestureSm, controller);
            var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(UpperBodyMaskPath);
            controller.AddLayer(new AnimatorControllerLayer
            {
                name = "Gesture",
                defaultWeight = 1f,
                blendingMode = AnimatorLayerBlendingMode.Override,
                avatarMask = mask,
                stateMachine = gestureSm,
            });
            if (mask == null) Debug.LogWarning($"[CustomerAnimatorBuilder] 상체 마스크 없음: {UpperBodyMaskPath} (제스처가 전신에 적용됨)");

            AnimatorState empty = gestureSm.AddState("Empty", new Vector3(300, 0));
            gestureSm.defaultState = empty;
            for (int i = 0; i < Gestures.Length; i++)
            {
                var g = Gestures[i];
                AnimatorState s = gestureSm.AddState(g.trigger, new Vector3(600, i * 60));
                s.motion = Clip("Male", g.folder, g.clip);

                AnimatorStateTransition enter = gestureSm.AddAnyStateTransition(s);
                enter.AddCondition(AnimatorConditionMode.If, 0f, g.trigger);
                enter.hasExitTime = false;
                enter.duration = 0.2f;
                enter.canTransitionToSelf = false;

                AnimatorStateTransition back = s.AddTransition(empty);
                back.hasExitTime = true;
                back.exitTime = 0.85f;
                back.duration = 0.25f;
            }

            BuildFemaleOverride(controller);
            CustomerRosterBuilder.RelinkControllers(); // 다시 만들면 GUID가 바뀌므로 목록의 참조를 새로 연결
            AssetDatabase.SaveAssets();
            Debug.Log($"[CustomerAnimatorBuilder] {ControllerPath} · {FemaleOverridePath} 생성");
        }

        static void BuildFemaleOverride(AnimatorController controller)
        {
            AssetDatabase.DeleteAsset(FemaleOverridePath);
            var ov = new AnimatorOverrideController(controller) { name = "Customer_Female" };
            var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            foreach (var (_, folder, clip) in Locomotion.Concat(Gestures))
            {
                AnimationClip male = Clip("Male", folder, clip);
                AnimationClip female = Clip("Female", folder, clip);
                if (male != null && female != null) pairs.Add(new KeyValuePair<AnimationClip, AnimationClip>(male, female));
            }
            ov.ApplyOverrides(pairs);
            AssetDatabase.CreateAsset(ov, FemaleOverridePath);
        }

        static void Link(AnimatorState from, AnimatorState to, AnimatorConditionMode mode, float threshold)
        {
            AnimatorStateTransition t = from.AddTransition(to);
            t.AddCondition(mode, threshold, "Speed");
            t.hasExitTime = false;
            t.duration = 0.15f;
        }

        /// <summary>FBX 안의 애니메이션 클립. gender = "Male" / "Female".</summary>
        static AnimationClip Clip(string gender, string folder, string name)
        {
            string prefix = gender == "Male" ? "HumanM@" : "HumanF@";
            string path = $"{KevinRoot}/Animations/{gender}/{folder}/{prefix}{name}.fbx";
            AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                .FirstOrDefault(c => !c.name.StartsWith("__preview__"));
            if (clip == null) Debug.LogError($"[CustomerAnimatorBuilder] 클립 없음: {path}");
            return clip;
        }
    }
}
