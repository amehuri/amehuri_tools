using UnityEngine;
using UnityEditor;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using nadena.dev.modular_avatar.core;
using System;


namespace vrclensforma
{
    public class VRCLensForMAConverter : EditorWindow
    {

        [MenuItem("Tools/AmehuriTools/VRCLensをMAで導入するやつ")]
        private static void Open()
        {
            VRCLensForMAConverter window = GetWindow<VRCLensForMAConverter>("VRCLensをMAで導入するやつ");
            window.minSize = new Vector2(400, 500);
            window.Show();
        }

        [SerializeField] private VRCAvatarDescriptor avatar; //アバター
        private RuntimeAnimatorController fx; //FXController
        private VRCExpressionsMenu menu; //EXMenu
        private GameObject prefabBaseObject; //配置するPrefab

        private string newFolderPath = "";
        private string prefabBaseObjPath = "";

        private RuntimeAnimatorController originalFX;
        private VRCExpressionsMenu originalMenu;
        private VRCExpressionParameters originalPar;

        private bool isReadySetup = false;

        private GameObject vrclens = null;
        private Transform targetParent = null;
        private Transform[] children = null;
        private int setupmode = 0;

        private void OnEnable()
        {
            avatar = null;
            fx = null;
            menu = null;
            prefabBaseObject = null;
            originalFX = null;
            originalMenu = null;
            originalPar = null;

        }
        private void OnDestroy()
        {
            if (isReadySetup)
            {
                bool isOK = EditorUtility.DisplayDialog("Warnig", "セットアップの途中です。元に戻して中断します。", "閉じる");
                if(isOK)
                {
                    Reset();
                    Remove();
                    Debug.Log("[VRCLensForMA] セットアップを中断しました。");
                }
            }
        }
        private void OnGUI()
        {
            bool isConditionMet = false;

            GUILayout.Space(5);
            GUILayout.Box("", GUILayout.Height(2), GUILayout.ExpandWidth(true));
            GUILayout.Space(5);

            EditorGUILayout.LabelField("1. アバターを指定する", EditorStyles.boldLabel);
            GUILayout.Space(5);
            avatar = EditorGUILayout.ObjectField("Target Avatar", avatar, typeof(VRCAvatarDescriptor), true) as VRCAvatarDescriptor;
            GUILayout.Space(5);
            setupmode = EditorGUILayout.Popup("Camera Handedness", setupmode, new string[] { "VR Left Hand", "VR Right Hand" });
            GUILayout.Space(5);
            EditorGUILayout.HelpBox("VRC Avatar DescriptorにFX・EXMenu・EXParametersを設定しておいてください。", MessageType.Info);
            GUILayout.Space(5);
            //アバター確認
            if (!avatar)
            {
                EditorGUILayout.HelpBox("アバターが指定されていません。", MessageType.Warning);
                isConditionMet = false;
            }
            else
            {
                if(!avatar.baseAnimationLayers[4].animatorController)
                {
                    EditorGUILayout.HelpBox("FXが設定されていません。", MessageType.Warning);
                    isConditionMet = false;
                }
                if (!avatar.expressionsMenu)
                {
                    EditorGUILayout.HelpBox("EXMenuが設定されていません。", MessageType.Warning);
                    isConditionMet = false;
                }
                if (!avatar.expressionParameters)
                {
                    EditorGUILayout.HelpBox("EXParametersが設定されていません。", MessageType.Warning);
                    isConditionMet = false;
                }

                if (avatar.baseAnimationLayers[4].animatorController && avatar.expressionsMenu && avatar.expressionParameters)
                {
                    isConditionMet = true;
                }
            
            }
            
            GUILayout.Space(5);
            GUILayout.Box("", GUILayout.Height(2), GUILayout.ExpandWidth(true));
            GUILayout.Space(5);
            
            EditorGUILayout.LabelField("2. 「準備」を押す", EditorStyles.boldLabel);
            GUILayout.Space(5);
            EditorGUI.BeginDisabledGroup(!isConditionMet);
            if (GUILayout.Button("準備"))
            {
                if (!avatar)
                {
                    Debug.LogError("[VRCLensForMA] アバターが指定されていません。");
                }
                else
                {
                    SaveFxandMenu();
                    DuplicateFolder();
                    SetPrefab();
                    Setup();
                    Debug.Log("[VRCLensForMA] 準備完了。VRCLensをApplyして「仕上げ」をしてください。");
                    isReadySetup = true;
                }
            }
            EditorGUI.EndDisabledGroup();

            GUILayout.Space(5);
            GUILayout.Box("", GUILayout.Height(2), GUILayout.ExpandWidth(true));
            GUILayout.Space(5);

            EditorGUILayout.LabelField("3. VRCLens側でパラメータを設定し、「Apply VRCLens」する", EditorStyles.boldLabel);

            GUILayout.Space(5);
            GUILayout.Box("", GUILayout.Height(2), GUILayout.ExpandWidth(true));
            GUILayout.Space(5);

            EditorGUILayout.LabelField("4. 「仕上げ」を押す", EditorStyles.boldLabel);
            GUILayout.Space(5);
            EditorGUI.BeginDisabledGroup(!isReadySetup);
            if (GUILayout.Button("仕上げ"))
            {
                Reset();
                FindVRCLens();
                SetBoneProxy();                    
                Debug.Log("[VRCLensForMA] 完了!");
                isReadySetup = false;
            }
            EditorGUI.EndDisabledGroup();

            GUILayout.Space(5);
            GUILayout.Box("", GUILayout.Height(2), GUILayout.ExpandWidth(true));
            GUILayout.Space(25);

            if(GUILayout.Button("やっぱりやめる"))
            {
                if(isReadySetup)
                {
                    Reset();
                    FindVRCLens();
                    Remove();
                    Debug.Log("[VRCLensForMA] セットアップを中断しました。");
                    isReadySetup = false;
                }
                else
                {
                    FindVRCLens();
                    Remove();
                    Debug.Log("[VRCLensForMA] セットアップを中断しました。");
                }
                
            }

        }

        //Baseフォルダを複製
        private void DuplicateFolder()
        {
            string baseFolderPath = AssetDatabase.GUIDToAssetPath("50759b10610ee2d4fbb63559c67d467f");
            newFolderPath = "Assets/VRCLensForMA/VRCLensForMA_" + avatar.name;
            AssetDatabase.CopyAsset(baseFolderPath, newFolderPath);

        }
        //Prefabをインスタンス化して配置
        private void SetPrefab()
        {
            prefabBaseObjPath = AssetDatabase.GUIDToAssetPath("3bcbab9a861bc09459c1678d5b941762");
            prefabBaseObject = AssetDatabase.LoadAssetAtPath(prefabBaseObjPath, typeof(GameObject)) as GameObject;
            prefabBaseObject = PrefabUtility.InstantiatePrefab(prefabBaseObject) as GameObject;
            prefabBaseObject.transform.SetParent(avatar.transform);
        }
        //アバターのFXとMenuを保存
        private void SaveFxandMenu()
        {
            originalFX = avatar.baseAnimationLayers[4].animatorController;
            originalMenu = avatar.expressionsMenu;
            originalPar = avatar.expressionParameters;

        }
        //FXとMenuを差し替える
        private void Setup()
        {

            string FXPath = newFolderPath + "/VRCLens_MA.controller";
            fx = AssetDatabase.LoadAssetAtPath(FXPath, typeof(RuntimeAnimatorController)) as RuntimeAnimatorController;
            string MenuPath = newFolderPath + "/VRCLens_MA.asset";
            menu = AssetDatabase.LoadAssetAtPath(MenuPath, typeof(VRCExpressionsMenu)) as VRCExpressionsMenu;
            string ParPath = newFolderPath + "/VRCLens_MA_Par.asset";

            Undo.RecordObject(avatar, "Exchange FX");
            avatar.baseAnimationLayers[4].animatorController = fx;
            avatar.expressionsMenu = menu;
            avatar.expressionParameters = AssetDatabase.LoadAssetAtPath(ParPath, typeof(VRCExpressionParameters)) as VRCExpressionParameters;
            Undo.IncrementCurrentGroup();

            prefabBaseObject.GetComponent<ModularAvatarMergeAnimator>().animator = fx;
            prefabBaseObject.GetComponent<ModularAvatarMenuInstaller>().menuToAppend = menu;

        }
        //FXとMenuを元に戻す
        private void Reset()
        {
            if(avatar != null)
            {
                Undo.RecordObject(avatar, "ResetFX");
                avatar.expressionsMenu = originalMenu;
                avatar.expressionParameters = originalPar;
                avatar.baseAnimationLayers[4].animatorController = originalFX;
                Undo.IncrementCurrentGroup();
            }
            
            if(prefabBaseObject != null)
            {
                prefabBaseObject.GetComponent<ModularAvatarMenuInstaller>().installTargetMenu = null;
            }

        }
        //Prefabを削除する
        private void Remove()
        {
            foreach (Transform child in children)
            {
                if (child.name == "VRCLens_ForMA")
                {
                    GameObject removetarget  = child.gameObject;
                    Undo.DestroyObjectImmediate(removetarget);
                }
            }
        }
        //VRCLensを検索
        private void FindVRCLens()
        {
            targetParent = avatar.GetComponent<Transform>();
            children = targetParent.GetComponentsInChildren<Transform>(true);

            foreach (Transform child in children)
            {
                if(child.name == "VRCLens")
                {
                    vrclens = child.gameObject;
                }
            }
        }
        //PickUpにBoneProxyをつけて回収
        private void SetBoneProxy()
        {                     
            foreach (Transform child in children)
            {
                if (child.name == "PickupA")
                {
                    GameObject handchild = child.gameObject;
                    handchild.AddComponent<ModularAvatarBoneProxy>();
                    if (setupmode == 1)
                    {
                        handchild.GetComponent<ModularAvatarBoneProxy>().boneReference = HumanBodyBones.RightHand;
                        handchild.GetComponent<ModularAvatarBoneProxy>().attachmentMode = BoneProxyAttachmentMode.AsChildKeepWorldPose;
                        handchild.transform.SetParent(vrclens.transform);

                    }
                    else if (setupmode == 0)
                    {
                        handchild.GetComponent<ModularAvatarBoneProxy>().boneReference = HumanBodyBones.LeftHand;
                        handchild.GetComponent<ModularAvatarBoneProxy>().attachmentMode = BoneProxyAttachmentMode.AsChildKeepWorldPose;
                        handchild.transform.SetParent(vrclens.transform);

                    }
                }

                if(child.name == "PickupB")
                {
                    GameObject handchild2 = child.gameObject;
                        handchild2.AddComponent<ModularAvatarBoneProxy>();
                    if (setupmode == 1)
                    {
                        handchild2.GetComponent<ModularAvatarBoneProxy>().boneReference = HumanBodyBones.LeftHand;
                        handchild2.GetComponent<ModularAvatarBoneProxy>().attachmentMode = BoneProxyAttachmentMode.AsChildKeepWorldPose;
                        handchild2.transform.SetParent(vrclens.transform);

                    }
                    else if (setupmode == 0)
                    {
                        handchild2.GetComponent<ModularAvatarBoneProxy>().boneReference = HumanBodyBones.RightHand;
                        handchild2.GetComponent<ModularAvatarBoneProxy>().attachmentMode = BoneProxyAttachmentMode.AsChildKeepWorldPose;
                        handchild2.transform.SetParent(vrclens.transform);

                    }
                }

                if (child.name == "PickupC")
                {
                    GameObject headchild = child.gameObject;
                    headchild.AddComponent<ModularAvatarBoneProxy>();
                    headchild.GetComponent<ModularAvatarBoneProxy>().boneReference = HumanBodyBones.Head;
                    headchild.GetComponent<ModularAvatarBoneProxy>().attachmentMode = BoneProxyAttachmentMode.AsChildKeepWorldPose;
                    headchild.transform.SetParent(vrclens.transform);

                }
      
            }
                
            vrclens.transform.SetParent(prefabBaseObject.transform);
            string assetPath = newFolderPath + "/VRCLens_ForMA" + avatar.name + ".prefab";
            
            //Parameter初期値設定
            string ParPath = newFolderPath + "/VRCLens_MA_Par.asset";
            VRCExpressionParameters p = AssetDatabase.LoadAssetAtPath(ParPath, typeof(VRCExpressionParameters)) as VRCExpressionParameters;

            float nz = Array.Find(p.parameters, e => e.name == "VRCLZoomRadial").defaultValue; //Zoom初期値
            //Debug.Log(nz);
            ParameterConfig z = prefabBaseObject.GetComponent<ModularAvatarParameters>().parameters.Find(n => n.nameOrPrefix == "VRCLZoomRadial");
            int zorder = prefabBaseObject.GetComponent<ModularAvatarParameters>().parameters.FindIndex(n => n.nameOrPrefix == "VRCLZoomRadial");
            z.defaultValue = nz;
            prefabBaseObject.GetComponent<ModularAvatarParameters>().parameters[zorder] = z;
            //Debug.Log(MAobj.GetComponent<ModularAvatarParameters>().parameters[zorder].defaultValue);

            float na = Array.Find(p.parameters, e => e.name == "VRCLApertureRadial").defaultValue; //Aparture初期値
            //Debug.Log(na);
            ParameterConfig a = prefabBaseObject.GetComponent<ModularAvatarParameters>().parameters.Find(n => n.nameOrPrefix == "VRCLApertureRadial");
            int aorder = prefabBaseObject.GetComponent<ModularAvatarParameters>().parameters.FindIndex(n => n.nameOrPrefix == "VRCLApertureRadial");
            a.defaultValue = na;
            prefabBaseObject.GetComponent<ModularAvatarParameters>().parameters[aorder] = a;
            //Debug.Log(MAobj.GetComponent<ModularAvatarParameters>().parameters[aorder].defaultValue);


            // PrefabVariantを作成
            PrefabUtility.SaveAsPrefabAssetAndConnect(prefabBaseObject, assetPath, InteractionMode.UserAction);

        }

    }

}
