using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.IMGUI.Controls;
using System.Collections.Generic;

public class AnimatorControllerTreeViewWindow : EditorWindow
{
    private AnimatorController animatorController;
    private AnimatorControllerTreeView treeView;
    private TreeViewState treeViewState;
    private SearchField searchField;
    private AnimatorController lastController;
    private int tabIndex = 0; // タブ用
    private AnimationClip replaceTargetClip; // 置き換え対象
    private AnimationClip replaceWithClip;   // 置き換え先
    private Vector2 clipScrollPos; // スクロール位置を保持

    [MenuItem("Tools/AmehuriTools/AnimatorControllerの中にあるAnimationClipを一覧するやつ", priority = 600)]
    public static void ShowWindow()
    {
        GetWindow<AnimatorControllerTreeViewWindow>("AnimationClips一覧");
    }

    private void OnEnable()
    {
        if (treeViewState == null)
            treeViewState = new TreeViewState();
        treeView = new AnimatorControllerTreeView(treeViewState);

        searchField = new SearchField();
        searchField.downOrUpArrowKeyPressed += treeView.SetFocusAndEnsureSelectedItem;

    }

    private void OnGUI()
    {
        tabIndex = GUILayout.Toolbar(tabIndex, new[] { "通常モード", "まとめて置き換え" });
        var newController = (AnimatorController)EditorGUILayout.ObjectField("Animator Controller", animatorController, typeof(AnimatorController), false);

        // AnimatorControllerが変更されたらReload
        if (newController != animatorController)
        {
            animatorController = newController;
            treeView.Reload(animatorController);
            lastController = animatorController;
        }

        if (animatorController == null) return;

        if (tabIndex == 0)
        {
            // 通常モード
            treeView.searchString = searchField.OnGUI(treeView.searchString);

            EditorGUILayout.Space();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("AnimatorControllerを再読み込み")) treeView.Reload(animatorController);
            if (GUILayout.Button("AnimationClipの変更を適用")) treeView.ApplyChangesToController();
            EditorGUILayout.EndHorizontal();

            Rect rect = GUILayoutUtility.GetRect(0, 100000, 0, 100000);
            treeView.OnGUI(rect);
        }
        else
        {
            // まとめて置き換えモード
            EditorGUILayout.HelpBox("一覧したAnimationClipのうち、同じものを一括で置き換えます。", MessageType.Info);

            replaceTargetClip = (AnimationClip)EditorGUILayout.ObjectField("置き換え対象Clip", replaceTargetClip, typeof(AnimationClip), false);
            replaceWithClip = (AnimationClip)EditorGUILayout.ObjectField("置き換え先Clip", replaceWithClip, typeof(AnimationClip), false);

            if (replaceTargetClip != null && replaceWithClip != null)
            {
                if (GUILayout.Button("まとめて置き換え"))
                {
                    ReplaceAllClips(animatorController, replaceTargetClip, replaceWithClip);
                    treeView.Reload(animatorController);
                }
            }

            // 現在のAnimatorController内のClip一覧表示
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("AnimatorController内のAnimationClip一覧", EditorStyles.boldLabel);

            var allClips = GetAllClips(animatorController);
            clipScrollPos = EditorGUILayout.BeginScrollView(clipScrollPos, GUILayout.Height(500));
            foreach (var clip in allClips)
            {
                EditorGUILayout.ObjectField(clip, typeof(AnimationClip), false);
            }
            EditorGUILayout.EndScrollView();
        }
    }
    private List<AnimationClip> GetAllClips(AnimatorController controller)
    {
        var clips = new HashSet<AnimationClip>();
        foreach (var layer in controller.layers)
        {
            CollectClipsRecursive(layer.stateMachine, clips);
        }
        return new List<AnimationClip>(clips);
    }

    // サブステートマシンも含めて再帰的にClipを収集
    private void CollectClipsRecursive(AnimatorStateMachine sm, HashSet<AnimationClip> clips)
    {
        foreach (var state in sm.states)
        {
            if (state.state.motion is AnimationClip clip)
                clips.Add(clip);
        }
        foreach (var subSm in sm.stateMachines)
        {
            CollectClipsRecursive(subSm.stateMachine, clips);
        }
    }

    // まとめて置き換え処理（サブステートマシンも含む）
    private void ReplaceAllClips(AnimatorController controller, AnimationClip target, AnimationClip replaceWith)
    {
        Undo.RecordObject(controller, "Replace AnimationClips");
        foreach (var layer in controller.layers)
        {
            ReplaceClipsRecursive(layer.stateMachine, target, replaceWith);
        }
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
    }

    // サブステートマシンも含めて再帰的にClipを置き換え
    private void ReplaceClipsRecursive(AnimatorStateMachine sm, AnimationClip target, AnimationClip replaceWith)
    {
        foreach (var state in sm.states)
        {
            if (state.state.motion == target)
            {
                state.state.motion = replaceWith;
                EditorUtility.SetDirty(state.state);
            }
        }
        foreach (var subSm in sm.stateMachines)
        {
            ReplaceClipsRecursive(subSm.stateMachine, target, replaceWith);
        }
    }
}

public class AnimatorControllerTreeView : TreeView
{
    private AnimatorController controller;
    private Dictionary<int, AnimationClip> modifiedClips = new Dictionary<int, AnimationClip>();

    public AnimatorControllerTreeView(TreeViewState state) : base(state)
    {
        showAlternatingRowBackgrounds = true;
        rowHeight = 22f;
        showBorder = true;
    }

    public void Reload(AnimatorController controller)
    {
        this.controller = controller;
        modifiedClips.Clear();
        Reload();
    }

    protected override TreeViewItem BuildRoot()
    {
        var root = new TreeViewItem { id = 0, depth = -1, displayName = "Root" };
        root.children = new List<TreeViewItem>(); // 追加
        if (controller == null) return root;

        int id = 1;
        foreach (var layer in controller.layers)
        {
            var layerItem = new AnimatorTreeItem(id++, 0, layer.name, AnimatorTreeItem.ItemType.Layer, null);
            root.AddChild(layerItem);
            AddStateMachine(layerItem, layer.stateMachine, ref id, 1);
        }

        SetupDepthsFromParentsAndChildren(root);
        return root;
    }

    private void AddStateMachine(TreeViewItem parent, AnimatorStateMachine sm, ref int id, int depth)
    {
        var smItem = new AnimatorTreeItem(id++, depth, sm.name, AnimatorTreeItem.ItemType.StateMachine, null);
        parent.AddChild(smItem);

        // States を自然順にソート
        var states = new List<ChildAnimatorState>(sm.states);
        states.Sort((a, b) => EditorUtility.NaturalCompare(a.state.name, b.state.name));
        foreach (var state in states)
        {
            var stItem = new AnimatorTreeItem(id++, depth + 1, state.state.name, AnimatorTreeItem.ItemType.State, state.state.motion as AnimationClip, state.state);
            smItem.AddChild(stItem);
        }

        // SubStateMachines を自然順にソート
        var subMachines = new List<ChildAnimatorStateMachine>(sm.stateMachines);
        subMachines.Sort((a, b) => EditorUtility.NaturalCompare(a.stateMachine.name, b.stateMachine.name));
        foreach (var sub in subMachines)
        {
            AddStateMachine(smItem, sub.stateMachine, ref id, depth + 1);
        }
    }


    protected override void RowGUI(RowGUIArgs args)
    {
        var item = (AnimatorTreeItem)args.item;
        Rect rowRect = args.rowRect;

        // 選択行ハイライト
        if (args.selected)
            EditorGUI.DrawRect(rowRect, new Color(0.24f, 0.48f, 0.90f, 0.3f));

        // アイコン
        Texture icon = null;
        switch (item.Type)
        {
            case AnimatorTreeItem.ItemType.Layer: icon = EditorGUIUtility.IconContent("AnimatorController Icon").image; break;
            case AnimatorTreeItem.ItemType.StateMachine: icon = EditorGUIUtility.IconContent("Folder Icon").image; break;
            case AnimatorTreeItem.ItemType.State: icon = EditorGUIUtility.IconContent("AnimationClip Icon").image; break;
        }
        var iconRect = rowRect;
        iconRect.x += GetContentIndent(item);
        iconRect.width = rowRect.height;
        if (icon != null) GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit);

        // ラベル
        var labelRect = rowRect;
        labelRect.xMin += GetContentIndent(item) + 20;
        EditorGUI.LabelField(labelRect, item.displayName);

        // State Clip ObjectField (差し替え可能)
        if (item.Type == AnimatorTreeItem.ItemType.State && item.StateRef != null)
        {
            var clipRect = rowRect;
            clipRect.xMin = rowRect.xMax - 300; // 幅調整

            // 現在の背景色を保存
            Color prevColor = GUI.backgroundColor;

            // 変更済みなら黄色
            GUI.backgroundColor = modifiedClips.ContainsKey(item.id) ? Color.green : Color.white;

            // ObjectField描画
            AnimationClip newClip = (AnimationClip)EditorGUI.ObjectField(clipRect, item.Clip, typeof(AnimationClip), false);
            if (newClip != item.Clip)
            {
                item.Clip = newClip;
                modifiedClips[item.id] = newClip;
            }

            // 背景色を元に戻す
            GUI.backgroundColor = prevColor;
        }


    }
    


    public void ApplyChangesToController()
    {
        if (controller == null) return;

        void Apply(TreeViewItem item)
        {
            if (item is AnimatorTreeItem tItem && tItem.Type == AnimatorTreeItem.ItemType.State && tItem.StateRef != null && modifiedClips.ContainsKey(item.id))
            {
                AnimationClip newClip = modifiedClips[item.id];

                // Undo対応
                Undo.RecordObject(tItem.StateRef, $"Change AnimatorState Motion: {tItem.StateRef.name}");

                // Motionを変更
                tItem.StateRef.motion = newClip;
            }

            if (item.hasChildren)
            {
                foreach (var child in item.children)
                    Apply(child);
            }
        }

        Apply(rootItem);

        // 反映後はクリア
        modifiedClips.Clear();
        Reload(controller);

        // Undoに反映させるためAssetに変更マーク
        EditorUtility.SetDirty(controller);
    }

}

public class AnimatorTreeItem : TreeViewItem
{
    public enum ItemType { Layer, StateMachine, State }
    public ItemType Type { get; private set; }
    public AnimationClip Clip { get; set; }
    public AnimatorState StateRef { get; private set; } // 直接保持

    public AnimatorTreeItem(int id, int depth, string name, ItemType type, AnimationClip clip, AnimatorState stateRef = null) : base(id, depth, name)
    {
        Type = type;
        Clip = clip;
        StateRef = stateRef;
    }
}
