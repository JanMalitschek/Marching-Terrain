using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Reflection;
using System.Linq;
using UnityEngine.Rendering;

namespace JamathansMarchingTerrain{
    [ExecuteAlways]
    public class MarchingTerrain : MonoBehaviour
    {
        //Data
        public MarchingTerrainData data;

        //General Setttings
        public int width = 100;
        public int height = 100;
        public int length = 100;
        public float cubeSize = 5.0f;
        public float[,,] points = null;

        [Range(0.1f, 0.9f)]
        public float surfaceLevel = 0.5f;

        public enum ShadingMode{
            Flat,
            Smooth
        }
        public ShadingMode shadingMode = ShadingMode.Flat;

        public Bounds BoundingBox{
            get{
                Vector3 size = new Vector3(points.GetLength(0) - 1, points.GetLength(1) - 1, points.GetLength(2) - 1) * cubeSize;
                return new Bounds(transform.position + size * 0.5f, size);
            }
        }

        //Brush
        public float brushRadius = 10.0f;
        public AnimationCurve brushFalloff = AnimationCurve.EaseInOut(0.0f, 0.0f, 1.0f, 1.0f);
        [Range(0.0f, 5.0f)]
        public float brushIntensity = 1.0f;
        public Brush currentBrush;
        public string currentBrushName;
        public List<Type> brushTypes;

        //LOD
        [Range(1, 4)]
        public int numLODs = 1;
        [Range(0, 3)]
        public int showLOD = 0;

        //Painting
        public int selectedLayer = -1;
        public List<MarchingTerrain> others;

        //Prefabs
        public string[] prefabPaintOptions = new string[]{
            "Place",
            "Remove"
        };
        public int selectedPrefabPaintOption = 0;
        [Range(0.0f, 1.0f)]
        public float prefabChance = 0.5f;
        public bool stickToVertex = false;
        public bool alignToNormal = true;
        public float normalOffset = 0.0f;
        public float minRotation = 0.0f;
        public float maxRotation = 360.0f;
        public float minScale = 0.5f;
        public float maxScale = 2.0f;
        [Range(0.0f, 1.0f)]
        public float maxRadiusOverlap = 0.0f;

        public string[] options = new string[]{
            "General",
            "Modeling",
            "Painting",
            "Prefabs",
            "LOD"
        };
        public int selectedOption = 0;

        //Rendering
        Material mat;
        Material matAddpass;
        public Mesh mesh;
        MaterialPropertyBlock[] propertyBlocks;

        private void Awake() {
            mat = new Material(Shader.Find("MarchingTerrain/MarchingTerrainStandard"));
            matAddpass = new Material(Shader.Find("MarchingTerrain/MarchingTerrainStandardAddpass"));
            propertyBlocks = new MaterialPropertyBlock[0];
            Init();
            if(data != null){
                data.LoadDensityMap(points.GetLength(0), points.GetLength(1), points.GetLength(2));
                data.LoadSplatMaps();
                CopyDensityFromData();
                UpdateMaterial();
            }
            MarchCubes(showLOD);
        }

        public void Init(){
            points = new float[Mathf.CeilToInt(width / cubeSize) + 1,
                               Mathf.CeilToInt(height / cubeSize) + 1,
                               Mathf.CeilToInt(length / cubeSize) + 1];
        }
        public void CopyDensityFromData(){
            for(int x = 0; x < points.GetLength(0); x++)
                for(int y = 0; y < points.GetLength(1); y++)
                    for(int z = 0; z < points.GetLength(2); z++)
                        points[x,y,z] = data.densityMap.GetPixel(x,y,z).r;
        }
        public void ClearPrefabs(){
            while(transform.childCount > 0)
                Destroy(transform.GetChild(0).gameObject);
        }
        public void FindOthers(){
            var rawOthers = FindObjectsOfType<MarchingTerrain>();
            others = new List<MarchingTerrain>();
            foreach(var other in rawOthers)
                if(other != this)
                    others.Add(other);
        }

        private void OnEnable() {
            Camera.onPreCull -= RenderTerrain;
            Camera.onPreCull += RenderTerrain;
        }
        private void OnDisable() {
            Camera.onPreCull -= RenderTerrain;
        }

        public void FindBrushes(){
            brushTypes = new List<Type>();
            foreach(Assembly a in AppDomain.CurrentDomain.GetAssemblies())
                foreach(Type t in a.GetTypes()){
                    BrushAttribute att = (BrushAttribute)t.GetCustomAttribute(typeof(BrushAttribute));
                    if(att != null)
                        brushTypes.Add(t);
                }
            currentBrush = new AddBrush();
            currentBrushName = "Add";
        }
        public void CopyEditingSettings(MarchingTerrain other){
            this.brushRadius = other.brushRadius;
            this.brushFalloff = other.brushFalloff;
            this.brushIntensity = other.brushIntensity;
            this.currentBrush = other.currentBrush;
            this.selectedOption = other.selectedOption;
            if(other.selectedLayer < data.layers.Count)
                this.selectedLayer = other.selectedLayer;
        }

        public void MarchCubes(int lod){
            // if(!IsLODValid(lod)){
            //     Debug.LogWarning("The terrain cannot be generated with the gived LOD!");
            //     return;
            // }
            Mesh m = new Mesh();
            List<Vector3> vertices = new List<Vector3>();
            List<int> indices = new List<int>();
            Dictionary<Vector3, int> vertIndexPairs = new Dictionary<Vector3, int>();
            int indicesIndex = 0;
            int lodIncrement = (int)Mathf.Pow(2, lod);
            for(int x = 0; x < points.GetLength(0) - 1; x += lodIncrement){
                for(int y = 0; y < points.GetLength(1) - 1 ; y += lodIncrement){
                    for(int z = 0; z < points.GetLength(2) - 1; z += lodIncrement){
                        var cube = GetCube(x, y, z, lod);
                        int cubeIndex = 0;
                        for(int i = 0; i < 8; i++)
                            if(cube[i] >= surfaceLevel)
                                cubeIndex |= (1 << i);
                        List<int> edgeIndices = new List<int>();
                        for(int i = 0; i < 16; i++){
                            int edgeIdx = TriangulationTable.triTable[cubeIndex, i];
                            if(edgeIdx < 0) break;
                            edgeIndices.Add(edgeIdx);
                        }
                        for(int v = edgeIndices.Count - 1; v >= 0; v--){
                            int indexA = TriangulationTable.cornerIndexAFromEdge[edgeIndices[v]];
                            int indexB = TriangulationTable.cornerIndexBFromEdge[edgeIndices[v]];
                            Vector3 cornerA = GetPositionFromCube(x, y, z, indexA, lod);
                            Vector3 cornerB = GetPositionFromCube(x, y, z, indexB, lod);
                            float weightA = GetPointFromCube(x, y, z, indexA, lod);
                            float weightB = GetPointFromCube(x, y, z, indexB, lod);
                            float t = weightA > weightB ? weightA : 1.0f - weightB;
                            Vector3 vert = Vector3.Lerp(cornerA, cornerB, t);
                            if(shadingMode == ShadingMode.Smooth){
                                if(vertIndexPairs.ContainsKey(vert))
                                    indices.Add(vertIndexPairs[vert]);    
                                else{
                                    vertices.Add(vert);
                                    vertIndexPairs.Add(vert, indicesIndex);
                                    indices.Add(indicesIndex++);
                                }
                            }
                            else{
                                vertices.Add(vert);
                                indices.Add(indicesIndex++);
                            }
                        }
                    }
                }
            }
            m.SetVertices(vertices);
            m.SetIndices(indices, MeshTopology.Triangles, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            mesh = m;
        }

        public bool IsLODValid(int lod){
            if(lod == 0) return true;
            else{
                Vector3Int dims = new Vector3Int(points.GetLength(0), points.GetLength(1), points.GetLength(2));
                while(lod >= 0){
                    lod--;
                    if(dims.x % 2 != 0 || dims.y % 2 != 0 || dims.z % 2 != 0)
                        return false;
                    dims /= 2;
                }
                return true;
            }
        }

        public float[] GetCube(int cubeX, int cubeY, int cubeZ, int lod = 0){
            float[] cube = new float[8];
            for(int i = 0; i < 8; i++)
                cube[i] = GetPointFromCube(cubeX, cubeY, cubeZ, i, lod);
            return cube;
        }

        public float GetPointFromCube(int cubeX, int cubeY, int cubeZ, int pointIndex, int lod = 0){
            int lodIncrement = (int)Mathf.Pow(2, lod);
            switch(pointIndex){
                case 0: return points[cubeX, cubeY, cubeZ];
                case 1: return points[cubeX, cubeY, cubeZ + lodIncrement];
                case 2: return points[cubeX + lodIncrement, cubeY, cubeZ + lodIncrement];
                case 3: return points[cubeX + lodIncrement, cubeY, cubeZ];
                case 4: return points[cubeX, cubeY + lodIncrement, cubeZ];
                case 5: return points[cubeX, cubeY + lodIncrement, cubeZ + lodIncrement];
                case 6: return points[cubeX + lodIncrement, cubeY + lodIncrement, cubeZ + lodIncrement];
                case 7: return points[cubeX + lodIncrement, cubeY + lodIncrement, cubeZ];
                default: return points[cubeX, cubeY, cubeZ];
            }
        }
        public Vector3 GetPositionFromCube(int cubeX, int cubeY, int cubeZ, int pointIndex, int lod = 0){
            int lodIncrement = (int)Mathf.Pow(2, lod);
            switch(pointIndex){
                case 0: return new Vector3(cubeX, cubeY, cubeZ) * cubeSize;
                case 1: return new Vector3(cubeX, cubeY, cubeZ + lodIncrement) * cubeSize;
                case 2: return new Vector3(cubeX + lodIncrement, cubeY, cubeZ + lodIncrement) * cubeSize;
                case 3: return new Vector3(cubeX + lodIncrement, cubeY, cubeZ) * cubeSize;
                case 4: return new Vector3(cubeX, cubeY + lodIncrement, cubeZ) * cubeSize;
                case 5: return new Vector3(cubeX, cubeY + lodIncrement, cubeZ + lodIncrement) * cubeSize;
                case 6: return new Vector3(cubeX + lodIncrement, cubeY + lodIncrement, cubeZ + lodIncrement) * cubeSize;
                case 7: return new Vector3(cubeX + lodIncrement, cubeY + lodIncrement, cubeZ) * cubeSize;
                default : return new Vector3(cubeX, cubeY, cubeZ) * cubeSize;
            }
        }

        public float GetIntersectingCube(Ray ray, Vector3 boundsHitPosition){
            Vector3 cubePosition = boundsHitPosition + ray.direction * cubeSize;
            Vector3 quantizedCubePosition = Quantize(cubePosition);
            Vector3Int cubeIndex = new Vector3Int((int)((quantizedCubePosition.x - transform.position.x) / cubeSize),
                                                   (int)((quantizedCubePosition.y - transform.position.y) / cubeSize),
                                                   (int)((quantizedCubePosition.z - transform.position.z) / cubeSize));
            while(IsInBounds(cubeIndex)){
                if(points[cubeIndex.x, cubeIndex.y, cubeIndex.z] >= surfaceLevel)
                    return Vector3.Distance(cubePosition, ray.origin);
                else{
                    cubePosition += ray.direction * cubeSize * 0.5f;
                    quantizedCubePosition = Quantize(cubePosition);
                    cubeIndex = new Vector3Int((int)((quantizedCubePosition.x - transform.position.x) / cubeSize),
                                               (int)((quantizedCubePosition.y - transform.position.y) / cubeSize),
                                               (int)((quantizedCubePosition.z - transform.position.z) / cubeSize));
                }
            }
            return Vector3.Distance(cubePosition, ray.origin);
        }

        public Vector3 Quantize(Vector3 vec){
            vec.x = Mathf.Round(vec.x / cubeSize) * cubeSize;
            vec.y = Mathf.Round(vec.y / cubeSize) * cubeSize;
            vec.z = Mathf.Round(vec.z / cubeSize) * cubeSize;
            return vec;
        }

        private bool IsInBounds(Vector3Int cubeIndex){
            return cubeIndex.x >= 0 && cubeIndex.x < points.GetLength(0) &&
                    cubeIndex.y >= 0 && cubeIndex.y < points.GetLength(1) &&
                    cubeIndex.z >= 0 && cubeIndex.z < points.GetLength(2);
        }

        public void ApplyBrush(Vector3 worldPosition){
            Vector3 originalWorldPos = worldPosition;
            if(currentBrush != null){
                worldPosition -= transform.position;
                worldPosition = Quantize(worldPosition) / cubeSize;
                Vector3Int brushIndex = new Vector3Int((int)worldPosition.x, (int)worldPosition.y, (int)worldPosition.z);
                int pointBrushRadius = Mathf.CeilToInt(brushRadius / cubeSize);
                Vector3Int startIndex = brushIndex - Vector3Int.one * pointBrushRadius;
                startIndex.x = Mathf.Clamp(startIndex.x, 0, points.GetLength(0) - 1);
                startIndex.y = Mathf.Clamp(startIndex.y, 0, points.GetLength(1) - 1);
                startIndex.z = Mathf.Clamp(startIndex.z, 0, points.GetLength(2) - 1);
                Vector3Int endIndex = brushIndex + Vector3Int.one * pointBrushRadius;
                endIndex.x = Mathf.Clamp(endIndex.x, 0, points.GetLength(0));
                endIndex.y = Mathf.Clamp(endIndex.y, 0, points.GetLength(1));
                endIndex.z = Mathf.Clamp(endIndex.z, 0, points.GetLength(2));
                if(selectedOption == 1){
                    for(int x = startIndex.x; x < endIndex.x; x++)
                        for(int y = startIndex.y; y < endIndex.y; y++)
                            for(int z = startIndex.z; z < endIndex.z; z++){
                                currentBrush.ProcessPoint(ref points[x,y,z],
                                                        new Vector3(x, y, z),
                                                        worldPosition,
                                                        brushRadius / cubeSize,
                                                        brushFalloff,
                                                        brushIntensity);
                                points[x,y,z] = Mathf.Clamp01(points[x,y,z]);
                                data.densityMap.SetPixel(x, y, z, new Color(points[x,y,z], 0.0f, 0.0f));
                            }
                    selectedPrefabPaintOption = 1;
                    ApplyPrefabBrush(originalWorldPos);
                    MarchCubes(showLOD);
                }
                else if(selectedOption == 2 && selectedLayer >= 0){
                    int splatIndex = selectedLayer / 4;
                    for(int x = startIndex.x; x < endIndex.x; x++)
                        for(int y = startIndex.y; y < endIndex.y; y++)
                            for(int z = startIndex.z; z < endIndex.z; z++){
                                float[] splat = Utility.ColorToArray(data.splatMaps[splatIndex].GetPixel(x, y, z));
                                float previousSplat = splat[selectedLayer % 4];
                                currentBrush.ProcessPoint(ref splat[selectedLayer % 4],
                                                          new Vector3(x, y, z),
                                                          worldPosition,
                                                          brushRadius / cubeSize,
                                                          brushFalloff,
                                                          brushIntensity);
                                splat[selectedLayer % 4] = Mathf.Clamp01(splat[selectedLayer % 4]);
                                float splatDifference = splat[selectedLayer % 4] - previousSplat;
                                for(int s = 0; s < data.splatMaps.Count; s++){
                                    if(s == splatIndex){
                                        for(int i = 0; i < 4; i++)
                                            if(i != (selectedLayer % 4) && splatDifference > 0.0f){
                                                splat[i] -= splatDifference;
                                                splat[i] = Mathf.Clamp01(splat[i]);
                                            }
                                    }
                                    else
                                        data.splatMaps[s].SetPixel(x,y,z, data.splatMaps[s].GetPixel(x,y,z) - Color.white * splatDifference);
                                }
                                data.splatMaps[splatIndex].SetPixel(x, y, z, Utility.ArrayToColor(splat));
                            }
                    foreach(var splatMap in data.splatMaps)
                        splatMap.Apply();
                    UpdateMaterial();
                }
            }
        }

        public void ApplyPrefabBrush(Vector3 worldPosition){
            worldPosition -= transform.position;
            if(selectedPrefabPaintOption == 0){
                float density = brushIntensity / 5.0f;
                List<Vector3> normals = new List<Vector3>();
                mesh.GetNormals(normals);
                Vector3[] verts = mesh.vertices;
                int[] tris = mesh.triangles;
                for(int i = 0; i < tris.Length; i += 3){
                    if(UnityEngine.Random.Range(0.0f, 1.0f) < prefabChance && Vector3.Distance(verts[tris[i]], worldPosition) <= brushRadius){
                        int chosenIndex = data.currentPaintingPrefabs[UnityEngine.Random.Range(0, data.currentPaintingPrefabs.Count)];
                        float chosenRadius = Utility.GetPrefabRadius(data.prefabs[chosenIndex]);
                        Vector3 chosenPosition = stickToVertex ? verts[tris[i]]
                                                               : Vector3.Lerp(Vector3.Lerp(verts[tris[i]], verts[tris[i + 1]], UnityEngine.Random.Range(0.0f, 1.0f)), verts[tris[i + 2]], UnityEngine.Random.Range(0.0f, 1.0f));
                        chosenPosition += transform.position;
                        //Check agains all placed prefabs
                        bool canBePlaced = true;
                        for(int p = 0; p < transform.childCount; p++){
                            if(Vector3.Distance(chosenPosition, transform.GetChild(p).position) < (chosenRadius + Utility.GetPrefabRadius(transform.GetChild(p).gameObject)) * (1.0f - maxRadiusOverlap)){
                                canBePlaced = false;
                                break;
                            }
                        }
                        if(canBePlaced){
                            Quaternion rotation = alignToNormal ? Quaternion.FromToRotation(Vector3.up, normals[tris[i]]) : Quaternion.identity;
                            GameObject g = Instantiate(data.prefabs[chosenIndex], chosenPosition, rotation, transform);
                            g.transform.Rotate(Vector3.up, UnityEngine.Random.Range(minRotation, maxRotation), Space.Self);
                            g.transform.localScale = Vector3.one * UnityEngine.Random.Range(minScale, maxScale);
                            g.transform.position += g.transform.up * normalOffset;
                            #if UNITY_EDITOR
                            UnityEditor.Undo.RegisterCreatedObjectUndo(g, $"Placed Prefab");
                            #endif
                        }
                    }
                }
            }
            else{
                List<GameObject> toRemove = new List<GameObject>();
                for(int p = 0; p < transform.childCount; p++){
                    if(Vector3.Distance(transform.GetChild(p).position, worldPosition + transform.position) < brushRadius){
                        toRemove.Add(transform.GetChild(p).gameObject);
                    }
                }
                #if UNITY_EDITOR
                UnityEditor.Undo.RegisterFullObjectHierarchyUndo(gameObject, "Removed Prefabs");
                #endif
                foreach(GameObject g in toRemove)
                    DestroyImmediate(g);
            }
        }

        public void UpdateMaterial(){
            //Update Property Blocks
            int propBlockCount = (data.layers.Count + 3) / 4;
            propertyBlocks = new MaterialPropertyBlock[propBlockCount];
            data.UpdateSplatMaps(propBlockCount);
            for(int i = 0; i < propBlockCount; i++){
                propertyBlocks[i] = new MaterialPropertyBlock();
                propertyBlocks[i].SetTexture("_Splat", data.splatMaps[i]);
                propertyBlocks[i].SetInt("_SplatWidth", width);
                propertyBlocks[i].SetInt("_SplatHeight", height);
                propertyBlocks[i].SetInt("_SplatLength", length);
                for(int l = 0; l < Mathf.Min(4, data.layers.Count - i * 4); l++){
                    if(data.layers[l + i * 4] != null)
                        propertyBlocks[i].SetTexture($"_Layer{l}", data.layers[l + i * 4].diffuseTexture);
                }
            }
        }

        public void RenderTerrain(Camera cam){
            if(data.layers.Count <= 0){
                Graphics.DrawMesh(mesh,
                                Matrix4x4.Translate(transform.position),
                                mat,
                                gameObject.layer,
                                cam,
                                0);
            }
            else if(propertyBlocks != null && data != null){
                for(int i = 0; i < propertyBlocks.Length; i++){
                    Graphics.DrawMesh(mesh,
                                    Matrix4x4.Translate(transform.position),
                                    i == 0 ? mat : matAddpass,
                                    gameObject.layer,
                                    cam,
                                    0,
                                    propertyBlocks[i],
                                    true,
                                    true,
                                    true);
                }
            }
        }

        private void OnDrawGizmos() {
            Gizmos.color = Color.clear;
            Gizmos.matrix = Matrix4x4.Translate(transform.position);
            Gizmos.DrawMesh(mesh);
        }
    }
}