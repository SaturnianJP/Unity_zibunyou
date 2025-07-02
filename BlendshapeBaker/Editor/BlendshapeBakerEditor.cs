using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace net.satania.BlendshapeBaker
{
    public class BlendshapeBakerEditor : EditorWindow
    {
        private const string k_MenuItemPath = "GameObject/さたにあ/ブレンドシェイプベイク";

        private class BakeSetting : ScriptableSingleton<BakeSetting>
        {
            public SkinnedMeshRenderer skinnedMeshRenderer;
            public int blendshapeIndex = 0;
            public int frame = 0;
            public string[] blendshapeNames;
        }

        private static BakeSetting setting => BakeSetting.instance;
        private static SkinnedMeshRenderer SkinnedMeshRenderer
        {
            get => setting.skinnedMeshRenderer;
            set => setting.skinnedMeshRenderer = value;
        }

        private static string[] BlendshapeNames
        {
            get => setting.blendshapeNames;
            set => setting.blendshapeNames = value;
        }

        private static int BlendshapeIndex
        {
            get => setting.blendshapeIndex;
            set => setting.blendshapeIndex = value;
        }

        private static int Frame
        {
            get => setting.frame;
            set => setting.frame = value;
        }

        private static string searchBlendshapeName = "";
        private static SearchField searchField;

        [MenuItem(k_MenuItemPath, validate = false)]
        private static void OpenWindow()
        {
            setting.skinnedMeshRenderer = Selection.activeGameObject.GetComponent<SkinnedMeshRenderer>();
            setting.blendshapeIndex = 0;
            setting.frame = 0;

            if (setting.skinnedMeshRenderer.sharedMesh != null)
                setting.blendshapeNames = GetBlendshapeNames(setting.skinnedMeshRenderer.sharedMesh);

            var w = GetWindow<BlendshapeBakerEditor>("ブレンドシェイプのベイク");
            var position = w.position;
            position.size = new Vector2(400, 200);
            w.position = position;
        }
        private static string[] GetBlendshapeNames(Mesh mesh)
        {
            int blendshapeCount = mesh.blendShapeCount;
            if (blendshapeCount == 0)
                return new string[0];

            return Enumerable.Range(0, blendshapeCount).Select(x => mesh.GetBlendShapeName(x)).ToArray();
        }

        [MenuItem(k_MenuItemPath, validate = true)]
        private static bool OpenWindowValidate()
        {
            var go = Selection.activeGameObject;
            if (go == null)
                return false;

            return go.GetComponent<SkinnedMeshRenderer>() != null;
        }

        private void OnGUI()
        {
            if (searchField == null)
            {
                searchField = new SearchField();
            }

            SkinnedMeshRenderer newRenderer = EditorGUILayout.ObjectField("SkinnedMeshRenderer", SkinnedMeshRenderer, typeof(SkinnedMeshRenderer), true) as SkinnedMeshRenderer;

            if (newRenderer != SkinnedMeshRenderer)
            {
                SkinnedMeshRenderer = newRenderer;
                if (SkinnedMeshRenderer != null && SkinnedMeshRenderer.sharedMesh != null)
                    BlendshapeNames = GetBlendshapeNames(SkinnedMeshRenderer.sharedMesh);
                else
                    BlendshapeNames = new string[0];

                BlendshapeIndex = 0;
                Frame = 0;
            }

            GUILayout.BeginHorizontal();
            var rect = GUILayoutUtility.GetRect(0, 999999, GUILayout.ExpandWidth(true), GUILayout.MaxHeight(EditorGUIUtility.singleLineHeight));
            searchBlendshapeName = searchField.OnGUI(rect, searchBlendshapeName);
            if (GUILayout.Button("検索", GUILayout.Width(35)))
            {
                BlendshapeIndex = GetBlendshapeIndex(SkinnedMeshRenderer, searchBlendshapeName);
            }
            GUILayout.EndHorizontal();

            int newIndex = EditorGUILayout.Popup("BlendshapeIndex", BlendshapeIndex, setting.blendshapeNames);
            if (newIndex != BlendshapeIndex)
            {
                BlendshapeIndex = newIndex;
            }

            int newFrame = EditorGUILayout.IntSlider("Frame", Frame, 0, 10);
            if (newFrame != Frame)
            {
                Frame = newFrame;
            }

            if (GUILayout.Button("Bake"))
            {
                Bake();
            }
        }

        private int GetBlendshapeIndex(SkinnedMeshRenderer skinnedMeshRenderer, string name)
        {
            if (skinnedMeshRenderer == null || skinnedMeshRenderer.sharedMesh == null)
                return -1;

            return skinnedMeshRenderer.sharedMesh.GetBlendShapeIndex(name);
        }

        private void Bake()
        {
            if (SkinnedMeshRenderer == null || SkinnedMeshRenderer.sharedMesh == null)
                throw new NullReferenceException("Mesh is null");

            Mesh mesh = SkinnedMeshRenderer.sharedMesh;
            if (BlendshapeIndex == -1 || BlendshapeIndex >= mesh.blendShapeCount)
                throw new Exception("BlendshapeIndexが無効です");

            string path = AssetDatabase.GetAssetPath(mesh);
            string directory = Path.GetDirectoryName(path);
            path = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(directory, mesh.name + ".asset"));

            Mesh clone = UnityObject.Instantiate(mesh);

            BakeBlendshape(clone, BlendshapeIndex, Frame);

            AssetDatabase.CreateAsset(clone, path);
            SkinnedMeshRenderer.sharedMesh = clone;
            EditorUtility.SetDirty(SkinnedMeshRenderer);

            Debug.Log("完了しました！");
        }

        private void BakeBlendshape(Mesh mesh, int blendshapeIndex, int frame)
        {
            var vertices = mesh.vertices;
            var normals = mesh.normals;
            var tangents = mesh.tangents;

            Vector3[] deltaVertices = new Vector3[mesh.vertexCount];
            Vector3[] deltaNormals = new Vector3[mesh.vertexCount];
            Vector3[] deltaTangents = new Vector3[mesh.vertexCount];
            mesh.GetBlendShapeFrameVertices(blendshapeIndex, frame, deltaVertices, deltaNormals, deltaTangents);

            ApplyBlendShapeDeltas(vertices, deltaVertices, normals, deltaNormals, tangents, deltaTangents, out vertices, out normals, out tangents);

            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTangents(tangents);
        }

        public static void ApplyBlendShapeDeltas(
            Vector3[] baseVertices, Vector3[] deltaVertices,
            Vector3[] baseNormals, Vector3[] deltaNormals,
            Vector4[] baseTangents, Vector3[] deltaTangents,
            out Vector3[] vertices, out Vector3[] normals, out Vector4[] tangents)
        {
            int vertexCount = baseVertices.Length;

            Vector3[] newVertices = new Vector3[vertexCount];
            Vector3[] newNormals = new Vector3[vertexCount];
            Vector4[] newTangents = new Vector4[vertexCount];

            for (int i = 0; i < vertexCount; i++)
            {
                //頂点はそのまま加算
                newVertices[i] = baseVertices[i] + deltaVertices[i];

                //Normalは加算した後に正規化
                Vector3 blendedNormal = baseNormals[i] + deltaNormals[i];
                newNormals[i] = blendedNormal.normalized;

                //Tangentはxyzだけ加算して正規化し、Baseのwを使用する
                Vector3 blendedTangentXYZ = (Vector3)baseTangents[i] + deltaTangents[i];
                blendedTangentXYZ.Normalize();
                newTangents[i] = new Vector4(blendedTangentXYZ.x, blendedTangentXYZ.y, blendedTangentXYZ.z, baseTangents[i].w);
            }

            vertices = newVertices;
            normals = newNormals;
            tangents = newTangents;
        }

    }
}