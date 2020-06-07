using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using System.Linq;
using System;

namespace JamathansMarchingTerrain{
    //[CreateAssetMenu(menuName="Marching Terrain/Data Object", fileName="New Marching Terrain Data", order=0)]
    [PreferBinarySerialization]
    public class MarchingTerrainData : ScriptableObject
    {
        public Texture3D densityMap;
        [HideInInspector]
        public byte[] densityMapBytes;
        public List<Texture3D> splatMaps;
        [HideInInspector]
        public byte[] splatMapsBytes;
        public List<TerrainLayer> layers;
        public List<int> deletedLayers;
        public List<GameObject> prefabs;
        public List<int> currentPaintingPrefabs;

        public void Init(int width, int height, int length){
            if(densityMap == null || splatMaps == null || densityMap.width != width || densityMap.height != height || densityMap.depth != length){
                densityMap = new Texture3D(width, height, length, TextureFormat.RFloat, 0);
                densityMap.wrapMode = TextureWrapMode.Clamp;
                splatMaps = new List<Texture3D>();
                for(int x = 0; x < width; x++)
                    for(int y = 0; y < height; y++)
                        for(int z = 0; z < length; z++)
                            densityMap.SetPixel(x, y, z, Color.black);
                densityMap.Apply();
                deletedLayers = new List<int>();
                layers = new List<TerrainLayer>();
                currentPaintingPrefabs = new List<int>();
            }
        }

        public void SaveDensityMap(){
            if(densityMap != null)
                densityMapBytes = Utility.Texture3DToByteArray(densityMap);
        }
        public void LoadDensityMap(int width, int height, int length){
            densityMap = new Texture3D(width, height, length, TextureFormat.RFloat, 0);
            densityMap.wrapMode = TextureWrapMode.Clamp;
            if(densityMapBytes.Length > 0)
                Utility.Texture3DFromByteArray(densityMap, densityMapBytes);
            densityMap.Apply();
        }
        
        public void SaveSplatMaps(){
            splatMapsBytes = new byte[splatMaps.Count * densityMap.width * densityMap.height * densityMap.depth * 4];
            for(int i = 0; i < splatMaps.Count; i++)
                Utility.Texture3DToByteArray(splatMaps[i]).CopyTo(splatMapsBytes, i * densityMap.width * densityMap.height * densityMap.depth * 4);
        }
        public void LoadSplatMaps(){
            int bytesPerSplat = densityMap.width * densityMap.height * densityMap.depth * 4;
            int numSplatMaps = splatMapsBytes.Length / bytesPerSplat;
            splatMaps = new List<Texture3D>();
            for(int i = 0; i < numSplatMaps; i++){
                splatMaps.Add(new Texture3D(densityMap.width, densityMap.height, densityMap.depth, TextureFormat.ARGB32, 0));
                Texture3D splat = splatMaps.Last();
                splat.wrapMode = TextureWrapMode.Clamp;
                byte[] currentSplatBytes = new byte[bytesPerSplat];
                Array.Copy(splatMapsBytes, i * bytesPerSplat, currentSplatBytes, 0, bytesPerSplat);
                Utility.Texture3DFromByteArray(splat, currentSplatBytes);
                splat.Apply();
            }
        }

        public void UpdateSplatMaps(int splatCount, bool erase = false){
            if(splatCount > splatMaps.Count){
                int newSplatCount = splatCount - splatMaps.Count;
                for(int i = 0; i < newSplatCount; i++){
                    splatMaps.Add(new Texture3D(densityMap.width, densityMap.height, densityMap.depth, TextureFormat.ARGB32, 0));
                    Texture3D splat = splatMaps.Last();
                    splat.wrapMode = TextureWrapMode.Clamp;
                    if(splatMaps.IndexOf(splat) == 0){
                        for(int x = 0; x < splat.width; x++)
                            for(int y = 0; y < splat.height; y++)
                                for(int z = 0; z < splat.depth; z++)
                                    splat.SetPixel(x, y, z, new Color(1.0f, 0.0f, 0.0f, 0.0f));
                    }
                    else{
                        for(int x = 0; x < splat.width; x++)
                            for(int y = 0; y < splat.height; y++)
                                for(int z = 0; z < splat.depth; z++)
                                    splat.SetPixel(x, y, z, new Color(0.0f, 0.0f, 0.0f, 0.0f));
                    }
                    splat.Apply();
                }
            } 
            else if(splatCount < splatMaps.Count){
                while(splatMaps.Count > splatCount)
                    splatMaps.RemoveAt(splatMaps.Count - 1);
            }
        }

        public void AddLayer(TerrainLayer layer){
            if(deletedLayers.Count > 0){
                layers[deletedLayers[0]] = layer;
                deletedLayers.RemoveAt(0);
            }
            else
                layers.Add(layer);
            UpdateSplatMaps((layers.Count + 3) / 4);
        }
        public void RemoveLayer(int index){
            if(index >= 0 && index < layers.Count && layers[index] != null){
                layers[index] = null;
                deletedLayers.Add(index);
                int splatMapIndex = index / 4;
                int splatChannelIndex = index % 4;
                for(int x = 0; x < splatMaps[splatMapIndex].width; x++)
                    for(int y = 0; y < splatMaps[splatMapIndex].height; y++)
                        for(int z = 0; z < splatMaps[splatMapIndex].depth; z++){
                            var splat = Utility.ColorToArray(splatMaps[splatMapIndex].GetPixel(x,y,z));
                            splat[splatChannelIndex] = 0.0f;
                            splatMaps[splatMapIndex].SetPixel(x,y,z, Utility.ArrayToColor(splat));
                        }
                splatMaps[splatMapIndex].Apply();
            }
        }
    }
}