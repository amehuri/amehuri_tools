using UnityEditor;
using UnityEngine;
using System;
using System.Linq;
using System.Collections.Generic;
using VRC.SDK3.Dynamics.Constraint.Components;
using VRC.SDK3.Dynamics.PhysBone.Components;
using VRC.SDKBase;
using VRC.SDKBase.Network;
using nadena.dev.modular_avatar.core;


public class ThinningOutPhysBones : EditorWindow
{
    [MenuItem("Tools/AmehuriTools/PhysBoneを間引くやつ", priority = 500)]
    public static void ShowWindow()
    {
        GetWindow<ThinningOutPhysBones>("PhysBoneを間引くやつ");
    }

    private Vector2 _scrollPosition = Vector2.zero;
    private VRC_AvatarDescriptor avatar;
    public GameObject[] PBrootObjects; // 複数のルートオブジェクトを指定
    [SerializeField] private VRCPhysBone basePB; // PBコンポーネントのベース
    [SerializeField]private bool selectOdd = true; // trueで奇数、falseで偶数
    private bool toggleOption;           // トグルの状態
    private bool previousToggleState;    // 前回のトグル状態
    private string parentObjectName = "Root_Mobile"; // 親オブジェクトの名前
    private Transform posForSetGenerateObj;
    private HumanBodyBones boneRef = HumanBodyBones.LastBone;
    public GameObject[] SubBoneList; // SubBone用のオブジェクトリスト

    // 設定モードの列挙型を定義
    private enum SettingMode { Simple, Advance }
    private SettingMode settingMode = SettingMode.Advance; // 初期モードは自動設定
    // 設定モードの表示名リスト
    private readonly string[] settingModeOptions = { "シンプル", "アドバンス" };

    // Constraint設定モードの列挙型を定義
    private enum ConstraintMode { Auto, ManualOne, ManualTwo }
    private ConstraintMode constraintMode = ConstraintMode.Auto; // 初期モードは自動設定

    // 手動選択用インデックス（1か所と2か所）
    private int[] manualSelectionOneIndex;
    private int[,] manualSelectionTwoIndices;

    // Constraint設定モードの表示名リスト
    private readonly string[] constraintModeOptions = { "自動設定", "手動設定(1か所)", "手動設定(2か所)" };

    // 間引き用List
    [SerializeField]private List<List<GameObject>> filteredArrayPBList = new List<List<GameObject>>();
    [SerializeField]private List<List<GameObject>> filteredArraySubList = new List<List<GameObject>>();

    // OnValidateメソッドを利用して変数が更新されるたびに間引き処理を実行
    void OnValidate()
    {
        // 間引き処理を呼び出す
        filteredArrayPBList =  FilterChildrenBasedOnSelectOdd(PBrootObjects, selectOdd);
        filteredArraySubList = FilterChildrenBasedOnSelectOdd(SubBoneList, selectOdd);
    }

    private void OnEnable()
    {
        // 初期状態の設定
        previousToggleState = toggleOption;
    }

    void OnGUI()
    {
        GUILayout.Space(10f);
        GUILayout.Box("", GUILayout.Height(2), GUILayout.ExpandWidth(true));
        GUILayout.Space(10f);

        // 親オブジェクトの名前を入力するフィールドを追加
        parentObjectName = EditorGUILayout.TextField("親オブジェクトの名前", parentObjectName);
        GUILayout.Space(5f);
        posForSetGenerateObj = EditorGUILayout.ObjectField("配置場所", posForSetGenerateObj, typeof(Transform), true) as Transform;
        GUILayout.Space(5f);
        boneRef = (HumanBodyBones)EditorGUILayout.EnumPopup("MA Bone Proxy設定", boneRef);
        GUILayout.Space(5f);
        EditorGUILayout.HelpBox("Skirt・Hairなど、揺らすものの名前をつけることを推奨します。\n配置場所が未設定の場合、Hierarchyに直接配置されます。\nBoneProxyは揺れボーンの親を設定します。（髪のボーンならHeadなど）", MessageType.Info);

        GUILayout.Space(10f);
        GUILayout.Box("", GUILayout.Height(2), GUILayout.ExpandWidth(true));
        GUILayout.Space(10f);
        
        basePB = EditorGUILayout.ObjectField("PBパラメータ参照元", basePB, typeof(VRCPhysBone), true) as VRCPhysBone;

        GUILayout.Space(10f);
        GUILayout.Box("", GUILayout.Height(2), GUILayout.ExpandWidth(true));
        GUILayout.Space(10f);

         _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
        
        GUILayout.Space(10f);
        GUILayout.Label("揺らすボーン(メインボーン)", EditorStyles.boldLabel);
        GUILayout.Space(5f);
        
        // 複数のルートオブジェクトを指定できるように
        ScriptableObject target = this;
        SerializedObject so = new SerializedObject(target);
        SerializedProperty stringsProperty = so.FindProperty("PBrootObjects");
        SerializedProperty subBonesProperty = so.FindProperty("SubBoneList");

        EditorGUILayout.PropertyField(stringsProperty, new GUIContent("MainBone List"), true);
        
        GUILayout.Space(5f);
        selectOdd = EditorGUILayout.Toggle("奇数番を残す", selectOdd);
        EditorGUILayout.HelpBox("縦の間引きのとき、階層の奇数番目のボーンを使います。", MessageType.None);
        // トグルの状態に変更があれば処理を実行
        if (selectOdd != previousToggleState)
        {
            previousToggleState = selectOdd;  // 新しい状態を保存
            toggleOption = selectOdd;

            // トグル変更時の処理
            filteredArrayPBList =  FilterChildrenBasedOnSelectOdd(PBrootObjects, selectOdd);
            filteredArraySubList = FilterChildrenBasedOnSelectOdd(SubBoneList, selectOdd);
        }

        GUILayout.Space(10f);
        GUILayout.Box("", GUILayout.Height(2), GUILayout.ExpandWidth(true));
        GUILayout.Space(10f);

        GUILayout.Label("揺らさないボーン(サブボーン)", EditorStyles.boldLabel);
        GUILayout.Space(5f);
        EditorGUILayout.PropertyField(subBonesProperty, new GUIContent("SubBone List"), true);

        so.ApplyModifiedProperties();

        GUILayout.Space(10f);
        GUILayout.Box("", GUILayout.Height(2), GUILayout.ExpandWidth(true));
        GUILayout.Space(10f);

        // Constraint設定モードのカスタムドロップダウンメニュー
        int settingModeIndex = (int)settingMode;
        settingModeIndex = EditorGUILayout.Popup("設定モード", settingModeIndex, settingModeOptions);
        settingMode = (SettingMode)settingModeIndex;

        GUILayout.Space(5f);
        
        // Constraint設定モードのカスタムドロップダウンメニュー
        int selectedModeIndex = (int)constraintMode;
        selectedModeIndex = EditorGUILayout.Popup("サブボーン追従設定", selectedModeIndex, constraintModeOptions);
        constraintMode = (ConstraintMode)selectedModeIndex;

        // 選択されたモードに応じて説明を表示
        switch (settingMode)
        {
            case SettingMode.Simple:
                EditorGUILayout.HelpBox("シンプル: \n各サブボーンチェーンの階層の一番上だけがメインボーンに追従します。\n揺れにくくなりますがConstraint使用数が少なくなります。", MessageType.Info);
                break;
            case SettingMode.Advance:
                EditorGUILayout.HelpBox("アドバンス: \n各サブボーンチェーンの階層の下位ボーンも個別にメインボーンに追従します。\n揺れ方を維持しやすいですがConstraint使用数が多くなります。", MessageType.Info);
                break;
        }

        GUILayout.Space(5f);

        // 選択されたモードに応じて説明を表示
        switch (constraintMode)
        {
            case ConstraintMode.Auto:
                EditorGUILayout.HelpBox("自動設定: \n各サブボーンが最近接する2つのメインボーンに追従するよう自動で設定します。", MessageType.Info);
                break;
            case ConstraintMode.ManualOne:
                EditorGUILayout.HelpBox("手動設定(1か所): \n各サブボーンが1つの指定したメインボーンに追従します。", MessageType.Info);
                break;
            case ConstraintMode.ManualTwo:
                EditorGUILayout.HelpBox("手動設定(2か所): \n各サブボーンが2つの指定したメインボーンに追従します。", MessageType.Info);
                break;
        }

        // 手動設定モードのUI表示
        if (constraintMode == ConstraintMode.ManualOne || constraintMode == ConstraintMode.ManualTwo)
        {
            if (SubBoneList == null)
            {
                EditorGUILayout.HelpBox("サブボーンが指定されていません。", MessageType.Warning);
            }
            else
            {
                // SimpleモードまたはAdvanceモードでのループ
                if (constraintMode == ConstraintMode.ManualOne || constraintMode == ConstraintMode.ManualTwo)
                {
                    switch (settingMode)
                    {
                        case SettingMode.Simple:

                            if (manualSelectionOneIndex == null || manualSelectionOneIndex.Length != SubBoneList.Length)
                                manualSelectionOneIndex = new int[SubBoneList.Length];
                            if (manualSelectionTwoIndices == null || manualSelectionTwoIndices.GetLength(0) != SubBoneList.Length)
                                manualSelectionTwoIndices = new int[SubBoneList.Length, 2];

                            for (int i = 0; i < filteredArraySubList.Count; i++)
                            {
                                GUILayout.Label($"{filteredArraySubList[i].First().name} の設定", EditorStyles.boldLabel);
                                if (constraintMode == ConstraintMode.ManualOne)
                                {
                                    manualSelectionOneIndex[i] = EditorGUILayout.Popup($"Source 1", manualSelectionOneIndex[i], GetPBObjectNames());
                                }
                                else if (constraintMode == ConstraintMode.ManualTwo)
                                {
                                    manualSelectionTwoIndices[i, 0] = EditorGUILayout.Popup($"Source 1", manualSelectionTwoIndices[i, 0], GetPBObjectNames());
                                    manualSelectionTwoIndices[i, 1] = EditorGUILayout.Popup($"Source 2", manualSelectionTwoIndices[i, 1], GetPBObjectNames());
                                }
                            }
                            break;

                        case SettingMode.Advance:
                            GameObject[] objects = filteredArraySubList
                                .SelectMany(innerList => innerList)
                                .ToArray();

                            if (manualSelectionOneIndex == null || manualSelectionOneIndex.Length != objects.Length)
                                manualSelectionOneIndex = new int[objects.Length];
                            if (manualSelectionTwoIndices == null || manualSelectionTwoIndices.GetLength(0) != objects.Length)
                                manualSelectionTwoIndices = new int[objects.Length, 2];

                            foreach (var n in objects)
                            {
                                GUILayout.Label($"{n.name} の設定", EditorStyles.boldLabel);
                                int nindex = Array.IndexOf(objects, n);

                                if (constraintMode == ConstraintMode.ManualOne)
                                {
                                    manualSelectionOneIndex[nindex] = EditorGUILayout.Popup($"Source 1", manualSelectionOneIndex[nindex], GetPBObjectNamesWithChildren());
                                }
                                else if (constraintMode == ConstraintMode.ManualTwo)
                                {
                                    manualSelectionTwoIndices[nindex, 0] = EditorGUILayout.Popup($"Source 1", manualSelectionTwoIndices[nindex, 0], GetPBObjectNamesWithChildren());
                                    manualSelectionTwoIndices[nindex, 1] = EditorGUILayout.Popup($"Source 2", manualSelectionTwoIndices[nindex, 1], GetPBObjectNamesWithChildren());
                                }
                            }
                            break;
                    }
                } 
            }
            

        }


        EditorGUILayout.EndScrollView();
        GUILayout.Space(10f);
        GUILayout.Box("", GUILayout.Height(2), GUILayout.ExpandWidth(true));
        GUILayout.Space(10f);

        if (GUILayout.Button("間引く！"))
        {
            if (PBrootObjects != null && PBrootObjects.Length > 0)
            {
                DuplicateSelectedChildren(PBrootObjects, selectOdd, parentObjectName);
            }
            else
            {
                Debug.LogError("[PhysBoneを間引くやつ]メインボーンが指定されていません。");
            }
        }

        GUILayout.Space(10f);
    }

    private void DuplicateSelectedChildren(GameObject[] roots, bool selectOdd, string parentObjectName)
    {
        // ユーザーが指定した名前で親オブジェクトを作成
        GameObject duplicatedHierarchy = new GameObject(parentObjectName);
        if(posForSetGenerateObj != null)
        {
            duplicatedHierarchy.transform.SetParent(posForSetGenerateObj);
        }
        ModularAvatarBoneProxy mabp = duplicatedHierarchy.AddComponent<ModularAvatarBoneProxy>();
        mabp.attachmentMode = BoneProxyAttachmentMode.AsChildKeepWorldPose;
        mabp.boneReference = boneRef;
        
        // "PB"と"Constraint"という名前の空の親オブジェクトを作成
        GameObject pbParent = new GameObject($"PB_{parentObjectName}");
        GameObject constraintParent = new GameObject($"Constraint_{parentObjectName}");
        if (pbParent != null)
        {
            UnityEditorInternal.ComponentUtility.CopyComponent(basePB);
            UnityEditorInternal.ComponentUtility.PasteComponentAsNew(pbParent);
        }

        // "DuplicatedHierarchy"の子オブジェクトとしてPBとConstraintを配置
        pbParent.transform.SetParent(duplicatedHierarchy.transform);
        constraintParent.transform.SetParent(duplicatedHierarchy.transform);

        // "SubBones"用の親オブジェクトを作成
        GameObject subBonesParent = new GameObject("SubBones");
        subBonesParent.transform.SetParent(duplicatedHierarchy.transform);

        // MainBone処理
        if (filteredArrayPBList != null && filteredArrayPBList.Count > 0)
        {
            foreach (var subList in filteredArrayPBList)
            {
                // _PBオブジェクト用の親
                GameObject previousObjectPB = pbParent;
                // _Constraintオブジェクト用の親
                GameObject previousObjectConstraint = constraintParent;

                foreach (var gameObject in subList)
                {
                    if (gameObject != null)
                    {
                        // _PBオブジェクトを生成
                        GameObject duplicatedObjectPB = new GameObject($"{gameObject.name}_PB");
                        duplicatedObjectPB.transform.position = gameObject.transform.position;
                        duplicatedObjectPB.transform.rotation = gameObject.transform.rotation;

                        // _PBオブジェクトを連ねて階層化
                        duplicatedObjectPB.transform.SetParent(previousObjectPB.transform);
                        previousObjectPB = duplicatedObjectPB; // 次の親に設定

                        // _Constraint用のオブジェクトも生成
                        GameObject duplicatedObjectConstraint = new GameObject($"{gameObject.name}_Constraint");
                        duplicatedObjectConstraint.transform.position = gameObject.transform.position;
                        duplicatedObjectConstraint.transform.rotation = gameObject.transform.rotation;

                        // _Constraintオブジェクトを連ねて階層化
                        duplicatedObjectConstraint.transform.SetParent(previousObjectConstraint.transform);
                        previousObjectConstraint = duplicatedObjectConstraint; // 次の親に設定

                        // VRCParentConstraintを追加して、_Constraintオブジェクトにのみ関連付け
                        VRCParentConstraint vrcconstraint = duplicatedObjectConstraint.AddComponent<VRCParentConstraint>();
                        vrcconstraint.Sources.Add(new VRC.Dynamics.VRCConstraintSource { SourceTransform = duplicatedObjectPB.transform, Weight = 1f });
                        vrcconstraint.TargetTransform = gameObject.transform;
                        vrcconstraint.ActivateConstraint();

                        
                    }
                    
                }

                if(PBrootObjects.Length == 1)
                {
                    pbParent.GetComponent<VRCPhysBone>().rootTransform = pbParent.transform.GetChild(0);
                }
                else
                {
                    pbParent.GetComponent<VRCPhysBone>().rootTransform = pbParent.transform;
                }

            }
        }
        // SubBone処理
        if (filteredArraySubList != null && filteredArraySubList.Count > 0)
        {
            foreach (var subList in filteredArraySubList)
            {
                int index = filteredArraySubList.IndexOf(subList);

                switch(settingMode)
                {
                    case SettingMode.Simple:
                    
                        // SubBone用の親オブジェクトを生成
                        GameObject subBoneObject = new GameObject($"SubBone_{subList[0]}");
                        subBoneObject.transform.SetParent(subBonesParent.transform);
                        VRCRotationConstraint subBoneConstraint = subBoneObject.AddComponent<VRCRotationConstraint>();
                        subBoneConstraint.TargetTransform = subList.First().transform;

                        switch (constraintMode)
                        {
                            case ConstraintMode.Auto:
                                // 最近接2つのオブジェクトを自動選択
                                var closestSources = GetClosestPBObjects(SubBoneList[index].transform.position, PBrootObjects);
                                subBoneConstraint.Sources.Add(new VRC.Dynamics.VRCConstraintSource { SourceTransform = closestSources[0].transform, Weight = 0.5f });
                                subBoneConstraint.Sources.Add(new VRC.Dynamics.VRCConstraintSource { SourceTransform = closestSources[1].transform, Weight = 0.5f });
                                subBoneConstraint.ActivateConstraint();
                                break;

                            case ConstraintMode.ManualOne:
                                // 手動設定(1か所)
                                subBoneConstraint.Sources.Add(new VRC.Dynamics.VRCConstraintSource { SourceTransform = PBrootObjects[manualSelectionOneIndex[index]].transform, Weight = 1f });
                                subBoneConstraint.ActivateConstraint();
                                break;

                            case ConstraintMode.ManualTwo:
                                // 手動設定(2か所)
                                subBoneConstraint.Sources.Add(new VRC.Dynamics.VRCConstraintSource { SourceTransform = PBrootObjects[manualSelectionTwoIndices[index, 0]].transform, Weight = 0.5f });
                                subBoneConstraint.Sources.Add(new VRC.Dynamics.VRCConstraintSource { SourceTransform = PBrootObjects[manualSelectionTwoIndices[index, 1]].transform, Weight = 0.5f });
                                subBoneConstraint.ActivateConstraint();
                                break;
                        }
                        break;

                    case SettingMode.Advance:

                        // SubBoneオブジェクト用の親
                        GameObject previousObjectSubBone = subBonesParent;

                        foreach (var gameObject in subList)
                        {
                            if (gameObject != null)
                            {
                                // Subbone用のオブジェクト生成
                                GameObject duplicatedObjectConstraint = new GameObject($"SubBone_{gameObject.name}");
                                duplicatedObjectConstraint.transform.position = gameObject.transform.position;
                                duplicatedObjectConstraint.transform.rotation = gameObject.transform.rotation;

                                // Subboneオブジェクトを連ねて階層化
                                duplicatedObjectConstraint.transform.SetParent(previousObjectSubBone.transform);
                                previousObjectSubBone = duplicatedObjectConstraint; // 次の親に設定

                                // VRCParentConstraintを追加
                                VRCRotationConstraint vrcconstraint = duplicatedObjectConstraint.AddComponent<VRCRotationConstraint>();
                                vrcconstraint.TargetTransform = gameObject.transform;

                                GameObject[] pbl = filteredArrayPBList
                                    .SelectMany(innerList => innerList)       // List<GameObject>の各要素を平坦化
                                    .Select(gameObject => gameObject)    // GameObjectの名前を取得
                                    .ToArray();                               // 配列に変換

                                GameObject[] sbl = filteredArraySubList
                                    .SelectMany(innnerList => innnerList)
                                    .Select(gameObject => gameObject)
                                    .ToArray();

                                int adIndex = Array.IndexOf(pbl, gameObject);
                                int adSubIndex = Array.IndexOf(sbl, gameObject);
                               
                               switch (constraintMode)
                               {
                                    case ConstraintMode.Auto:
                                        // 最近接2つのオブジェクトを自動選択
                                        var closestSources = GetClosestPBObjects(gameObject.transform.position, pbl);
                                        vrcconstraint.Sources.Add(new VRC.Dynamics.VRCConstraintSource { SourceTransform = closestSources[0].transform, Weight = 0.5f });
                                        vrcconstraint.Sources.Add(new VRC.Dynamics.VRCConstraintSource { SourceTransform = closestSources[1].transform, Weight = 0.5f });
                                        vrcconstraint.ActivateConstraint();
                                        break;

                                    case ConstraintMode.ManualOne:
                                        // 手動設定(1か所)
                                        vrcconstraint.Sources.Add(new VRC.Dynamics.VRCConstraintSource { SourceTransform = pbl[manualSelectionOneIndex[adSubIndex]].transform, Weight = 1f });
                                        vrcconstraint.ActivateConstraint();
                                        break;

                                    case ConstraintMode.ManualTwo:
                                        // 手動設定(2か所)
                                        vrcconstraint.Sources.Add(new VRC.Dynamics.VRCConstraintSource { SourceTransform = pbl[manualSelectionTwoIndices[adSubIndex, 0]].transform, Weight = 0.5f });
                                        vrcconstraint.Sources.Add(new VRC.Dynamics.VRCConstraintSource { SourceTransform = pbl[manualSelectionTwoIndices[adSubIndex, 1]].transform, Weight = 0.5f });
                                        vrcconstraint.ActivateConstraint();
                                        break;
                               }

                            }
                        }

                        break;
                }
            }
        }
 
    }

    // GameObject 配列の間引き処理を関数化したもの
    private List<List<GameObject>> FilterChildrenBasedOnSelectOdd(GameObject[] objects, bool selectOdd)
    {
        List<List<GameObject>> filteredParallelChains = new List<List<GameObject>>();

        // 配列内のすべてのオブジェクトを処理
        if (objects != null)
        {
            foreach (GameObject obj in objects)
            {
                List<GameObject> FilteredOneChainBone = new List<GameObject>();
                Transform[] children = obj.GetComponentsInChildren<Transform>();

                // 子要素を selectOdd に基づいて選別
                for (int i = 0; i < children.Length; i++)
                {  
                    if (selectOdd)
                    {
                        // 偶数番目の子要素を選択
                        if (i % 2 == 0)
                        {
                            FilteredOneChainBone.Add(children[i].gameObject);
                        }
                    }
                    else
                    {
                        // 奇数番目の子要素を選択
                        if (i % 2 != 0)
                        {
                            FilteredOneChainBone.Add(children[i].gameObject);
                        }
                    }
                    
                }

                filteredParallelChains.Add(FilteredOneChainBone);

            }

        }
        // 結果のリストを返す
        return filteredParallelChains;
    }

    // PBrootObjectsとその子要素の名前を取得するメソッド
    private string[] GetPBObjectNames()
    {
        List<string> nl = new List<string>();

        if (filteredArrayPBList != null)
        {
            foreach (var i in filteredArrayPBList)
            {
                string n = i.First().name;
                nl.Add(n);
            }
        }

        return nl.ToArray();
    }

    private string[] GetPBObjectNamesWithChildren()
    {
        string[] objectNames = filteredArrayPBList
            .SelectMany(innerList => innerList)       // List<GameObject>の各要素を平坦化
            .Select(gameObject => gameObject.name)    // GameObjectの名前を取得
            .ToArray();                               // 配列に変換
        return objectNames;
    }

    // 位置に基づいてPBrootObjectsとその子要素の中で最近接の2つを返すメソッド
    private GameObject[] GetClosestPBObjects(Vector3 targetPosition, GameObject[] objects)
    {
        return objects
            .OrderBy(obj => Vector3.Distance(targetPosition, obj.transform.position))
            .Take(2)
            .ToArray();
    }

    private void AddNetworkIDs(int IDnumber, GameObject pb)
    { 
        VRC_AvatarDescriptor avatarDescriptor = avatar;
        var ids = avatarDescriptor.NetworkIDCollection;
        var pair = new NetworkIDPair();
        bool alreadySetId = false;

        foreach (var id in ids)
        {
            if(id.gameObject == basePB.gameObject)
            {
                alreadySetId = true;
                pair.ID = id.ID;
                pair.gameObject = pb;
            }
        }
        if (!alreadySetId)
        {
            pair.ID = IDnumber + 101;
            pair.gameObject = pb;
            avatarDescriptor.NetworkIDCollection.Add(pair);
            var pair2 = new NetworkIDPair();
            pair2.ID = IDnumber + 101;
            pair2.gameObject = basePB.gameObject;
            avatarDescriptor.NetworkIDCollection.Add(pair2);
        }
        else
        {
            
            avatarDescriptor.NetworkIDCollection.Add(pair);
        }
        
        
    }
    
       




       
        
   

}
