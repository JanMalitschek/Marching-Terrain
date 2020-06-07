using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Reflection;
using System;

namespace JamathansMarchingTerrain{
    [CustomEditor(typeof(MarchingTerrain)), CanEditMultipleObjects]
    public class MarchingTerrainEditor : Editor
    {
        MarchingTerrain t;
        GenericMenu brushMenu;
        bool resizingBrush = false;
        bool painting = false;

        [MenuItem("GameObject/3D Object/Marching Terrain")]
        public static void CreateMarchingTerrain(){
            new GameObject("Marching Terrain", typeof(MarchingTerrain));
        }

        private void Awake() {
            t = target as MarchingTerrain;
            t.FindBrushes();
            brushMenu = new GenericMenu();
            foreach(Type bt in t.brushTypes){
                BrushAttribute att = (BrushAttribute)bt.GetCustomAttribute(typeof(BrushAttribute));
                brushMenu.AddItem(new GUIContent(att.path), false, () => {
                    t.currentBrush = (Brush)Activator.CreateInstance(bt);
                    t.currentBrushName = att.name;
                });
            }

            if(t.data == null){
                InitData();
            }

            t.FindOthers();
        }   

        public void InitData(){
            CreateNewDataAsset();
            t.Init();
            t.data.Init(t.points.GetLength(0), t.points.GetLength(1), t.points.GetLength(2));
            t.CopyDensityFromData();
            t.MarchCubes(t.showLOD);
            t.data.UpdateSplatMaps(1);
            t.UpdateMaterial();
            t.ClearPrefabs();
        }

        public void CreateNewDataAsset(){
            t.data = ScriptableObject.CreateInstance<MarchingTerrainData>();
            int nameIndex = 1;
            while(AssetDatabase.FindAssets($"MT_{t.gameObject.name}_{nameIndex})").Length > 0)
                nameIndex++;
            AssetDatabase.CreateAsset(t.data, $"Assets/MT_{t.gameObject.name}_{nameIndex}.asset");
            AssetDatabase.SaveAssets();
        }

        public override void OnInspectorGUI(){
            EditorGUI.BeginChangeCheck();
            GUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("data"));
            if(GUILayout.Button("New"))
                InitData();
            GUILayout.EndHorizontal();
            serializedObject.ApplyModifiedProperties();
            if(EditorGUI.EndChangeCheck() && t.data != null){
                t.Init();
                t.MarchCubes(t.showLOD);
            }
            t.selectedOption = GUILayout.SelectionGrid(t.selectedOption, t.options, 3);

            if(t.selectedOption == 0){
                //Dimensions
                EditorGUI.BeginChangeCheck();
                serializedObject.Update();
                EditorGUILayout.PropertyField(serializedObject.FindProperty("width"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("height"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("length"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("cubeSize"));
                serializedObject.ApplyModifiedProperties();
                if(EditorGUI.EndChangeCheck() && t.data != null){
                    t.Init();
                    t.MarchCubes(t.showLOD);
                }
                EditorGUI.BeginChangeCheck();
                serializedObject.Update();
                EditorGUILayout.PropertyField(serializedObject.FindProperty("surfaceLevel"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("shadingMode"));
                serializedObject.ApplyModifiedProperties();
                if(EditorGUI.EndChangeCheck() && t.data != null){
                    t.MarchCubes(t.showLOD);
                }
            }
            else if(t.selectedOption == 1){
                OnBrushGUI();
            }
            else if(t.selectedOption == 2){
                OnBrushGUI();
                Event current = Event.current;
                Rect layerRect = GUILayoutUtility.GetRect(0.0f, 50.0f, GUILayout.ExpandWidth(true));
                GUI.Box(layerRect, "Drag Layers here");
                switch(current.type){
                    case EventType.DragUpdated:
                    case EventType.DragPerform:
                        if(!layerRect.Contains(current.mousePosition))
                            break;
                        DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                        if(current.type == EventType.DragPerform){
                            DragAndDrop.AcceptDrag();
                            foreach(UnityEngine.Object o in DragAndDrop.objectReferences)
                                if(o is TerrainLayer && !t.data.layers.Contains(o as TerrainLayer))
                                    t.data.AddLayer(o as TerrainLayer);
                            EditorUtility.SetDirty(t.data);
                            t.UpdateMaterial();
                        }
                        break;
                }
                int layerIndex = 0;
                foreach(TerrainLayer l in t.data.layers){
                    if(l != null){
                        GUILayout.BeginHorizontal();
                        if(GUILayout.Button(l.diffuseTexture, GUILayout.Width(50.0f), GUILayout.Height(50.0f)))
                            t.selectedLayer = layerIndex;
                        if(layerIndex == t.selectedLayer)
                            GUI.color = Color.green;
                        EditorGUILayout.LabelField(l.name, EditorStyles.boldLabel);
                        GUI.color = Color.white;
                        if(GUILayout.Button("R")){
                            t.data.RemoveLayer(layerIndex);
                            break;
                        }
                        GUILayout.EndHorizontal();
                    }
                    layerIndex++;
                }
            }
            else if(t.selectedOption == 3){
                EditorGUILayout.HelpBox("Please note that every modelling operation to the terrain will remove all prefabs in the area of change!", MessageType.Warning);

                serializedObject.Update();
                EditorGUILayout.PropertyField(serializedObject.FindProperty("brushRadius"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("brushFalloff"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("brushIntensity"));
                serializedObject.ApplyModifiedProperties();

                t.selectedPrefabPaintOption = GUILayout.SelectionGrid(t.selectedPrefabPaintOption, t.prefabPaintOptions, 2);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("prefabChance"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("stickToVertex"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("alignToNormal"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("normalOffset"));
                EditorGUILayout.Separator();
                EditorGUILayout.MinMaxSlider("Random Rotation", ref t.minRotation, ref t.maxRotation, 0.0f, 360.0f);
                EditorGUI.indentLevel++;
                GUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(serializedObject.FindProperty("minRotation"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("maxRotation"));
                GUILayout.EndHorizontal();
                EditorGUI.indentLevel--;
                EditorGUILayout.Separator();
                EditorGUILayout.MinMaxSlider("Random Scale", ref t.minScale, ref t.maxScale, 0.2f, 5.0f);
                EditorGUI.indentLevel++;
                GUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(serializedObject.FindProperty("minScale"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("maxScale"));
                GUILayout.EndHorizontal();
                EditorGUI.indentLevel--;
                EditorGUILayout.Separator();
                EditorGUILayout.PropertyField(serializedObject.FindProperty("maxRadiusOverlap"));
                serializedObject.ApplyModifiedProperties();

                Event current = Event.current;
                Rect layerRect = GUILayoutUtility.GetRect(0.0f, 50.0f, GUILayout.ExpandWidth(true));
                GUI.Box(layerRect, "Drag Prefabs here");
                switch(current.type){
                    case EventType.DragUpdated:
                    case EventType.DragPerform:
                        if(!layerRect.Contains(current.mousePosition))
                            break;
                        DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                        if(current.type == EventType.DragPerform){
                            DragAndDrop.AcceptDrag();
                            foreach(UnityEngine.Object o in DragAndDrop.objectReferences)
                                if(o is GameObject && !t.data.prefabs.Contains(o as GameObject))
                                    t.data.prefabs.Add(o as GameObject);
                            EditorUtility.SetDirty(t.data);
                        }
                        break;
                }
                int prefabIndex = 0;
                foreach(GameObject g in t.data.prefabs){
                        GUILayout.BeginHorizontal();
                        if(GUILayout.Button(AssetPreview.GetAssetPreview(g), GUILayout.Width(50.0f), GUILayout.Height(50.0f))){
                            if(!t.data.currentPaintingPrefabs.Contains(prefabIndex))
                                t.data.currentPaintingPrefabs.Add(prefabIndex);
                            else
                                t.data.currentPaintingPrefabs.RemoveAll(x => x == prefabIndex);
                            break;
                        }
                        if(t.data.currentPaintingPrefabs.Contains(prefabIndex))
                            GUI.color = Color.green;
                        EditorGUILayout.LabelField(g.name, EditorStyles.boldLabel);
                        GUI.color = Color.white;
                        if(GUILayout.Button("R")){
                            t.data.prefabs.RemoveAt(prefabIndex);
                            break;
                        }
                        GUILayout.EndHorizontal();
                    prefabIndex++;
                }
            }
            else if(t.selectedOption == 4){
                serializedObject.Update();
                EditorGUILayout.PropertyField(serializedObject.FindProperty("numLODs"));
                // EditorGUI.BeginChangeCheck();
                // EditorGUILayout.PropertyField(serializedObject.FindProperty("showLOD"));
                // serializedObject.ApplyModifiedProperties();
                // if(EditorGUI.EndChangeCheck()){
                //     t.MarchCubes(t.showLOD);
                // }
            }
            EditorUtility.SetDirty(t);
        }

        private void OnBrushGUI(){
            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("brushRadius"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("brushFalloff"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("brushIntensity"));
            serializedObject.ApplyModifiedProperties();
            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Current Brush");
            if(GUILayout.Button(t.currentBrushName))
                brushMenu.ShowAsContext();
            GUILayout.EndHorizontal();
        }

        private void OnSceneGUI() {
            if(t.data != null && (t.selectedOption == 1 || t.selectedOption == 2 || t.selectedOption == 3)){
                int passiveID = GUIUtility.GetControlID(FocusType.Passive);
                Event current = Event.current;
                Ray screenRay = Camera.current.ViewportPointToRay(new Vector3(current.mousePosition.x / Camera.current.pixelWidth,
                                                                    1.0f - current.mousePosition.y / Camera.current.pixelHeight,
                                                                    Camera.current.nearClipPlane));

                //Brush Resizing
                if(current.type == EventType.KeyDown && current.keyCode == KeyCode.F){
                    resizingBrush = true;
                    current.Use();
                }
                else if(current.type == EventType.KeyUp && current.keyCode == KeyCode.F){
                    resizingBrush = false;
                    current.Use();
                }
                if(resizingBrush){
                    Handles.Label(screenRay.origin + screenRay.direction * 10.0f, $"Radius: {t.brushRadius}\nIntensity: {t.brushIntensity}");
                    if(current.type == EventType.MouseMove){
                        t.brushRadius -= current.delta.y * Time.deltaTime;
                        t.brushRadius = (float)Math.Round(Mathf.Max(t.brushRadius, 1.0f), 1);
                        t.brushIntensity += current.delta.x * Time.deltaTime * 0.2f;
                        t.brushIntensity = (float)Math.Round(Mathf.Clamp(t.brushIntensity, 0.0f, 5.0f), 1);
                        current.Use();
                    }
                }

                if(current.type == EventType.KeyDown && current.keyCode == KeyCode.B){
                    brushMenu.ShowAsContext();
                    current.Use();
                }

                Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;       
                Bounds bounds = t.BoundingBox;                                                         
                if(bounds.IntersectRay(screenRay, out float distance)){
                    Handles.color = Color.Lerp(Color.blue, Color.red, t.brushIntensity / 5.0f) * new Color(1.0f, 1.0f, 1.0f, 0.5f);
                    Vector3 hitPoint = screenRay.origin + screenRay.direction * distance;
                    Vector3 surfacePoint = screenRay.origin + screenRay.direction * t.GetIntersectingCube(screenRay, hitPoint);
                    Handles.SphereHandleCap(0, surfacePoint, Quaternion.identity, t.brushRadius, EventType.Repaint);
                    if((current.type == EventType.MouseDrag || current.type == EventType.MouseDown) && current.button == 0){
                        GUIUtility.hotControl = passiveID;
                        if(!painting){
                            Undo.RegisterCompleteObjectUndo(t, "Terrain Modified");
                            Undo.FlushUndoRecordObjects();
                            painting = true;
                        }
                        if(t.selectedOption == 1 || t.selectedOption == 2){
                            t.ApplyBrush(surfacePoint);
                            foreach(MarchingTerrain mt in t.others){
                                mt.CopyEditingSettings(t);
                                Bounds inflatedBounds = mt.BoundingBox;
                                inflatedBounds.Expand(t.brushRadius * 2.0f);
                                if(inflatedBounds.Contains(surfacePoint))
                                    mt.ApplyBrush(surfacePoint);
                            }
                        }
                        else if(t.selectedOption == 3){
                            t.ApplyPrefabBrush(surfacePoint);
                            foreach(MarchingTerrain mt in t.others){
                                mt.CopyEditingSettings(t);
                                Bounds inflatedBounds = mt.BoundingBox;
                                inflatedBounds.Expand(t.brushRadius * 2.0f);
                                if(inflatedBounds.Contains(surfacePoint))
                                    mt.ApplyPrefabBrush(surfacePoint);
                            }
                        }
                        EditorUtility.SetDirty(t);
                        current.Use();
                    }
                }
                else{
                    Handles.color = new Color(0.0f, 0.0f, 0.0f, 0.5f);
                    Handles.SphereHandleCap(0, screenRay.origin + screenRay.direction * (t.brushRadius + 10.0f), Quaternion.identity, t.brushRadius, EventType.Repaint);
                }
                if(painting && current.type == EventType.MouseUp){
                    painting = false;
                    if(t.selectedOption == 1){
                        t.data.densityMap.Apply();
                        t.data.SaveDensityMap();
                    }
                    else if(t.selectedOption == 2){
                        t.data.SaveSplatMaps();
                    }
                    EditorUtility.SetDirty(t);
                    EditorUtility.SetDirty(t.data);
                }
                Handles.color = Color.white;
                Handles.DrawWireCube(t.transform.position + bounds.size * 0.5f, bounds.size);
            }
        }
    }
}