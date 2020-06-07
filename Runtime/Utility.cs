using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace JamathansMarchingTerrain{
    public static class Utility{
        public static float[] ColorToArray(Color c){
            return new float[4]{c.r, c.g, c.b, c.a};
        }
        public static Color ArrayToColor(float[] ca){
            return new Color(ca[0], ca[1], ca[2], ca[3]);
        }

        public static byte[] Texture3DToByteArray(Texture3D texture){
            byte[] bytes = new byte[texture.width * texture.height * texture.depth * 4];
            for(int x = 0; x < texture.width; x++)
                for(int y = 0; y < texture.height; y++)
                    for(int z = 0; z < texture.depth; z++){
                        int index = (x + z * texture.width + y * texture.width * texture.depth) * 4;
                        Color pixel = texture.GetPixel(x,y,z);
                        bytes[index] = (byte)(pixel.r * 255);
                        bytes[index + 1] = (byte)(pixel.g * 255);
                        bytes[index + 2] = (byte)(pixel.b * 255);
                        bytes[index + 3] = (byte)(pixel.a * 255);
                    }
            return bytes;
        }

        public static void Texture3DFromByteArray(Texture3D texture, byte[] bytes){
            for(int x = 0; x < texture.width; x++)
                for(int y = 0; y < texture.height; y++)
                    for(int z = 0; z < texture.depth; z++){
                        int index = (x + z * texture.width + y * texture.width * texture.depth) * 4;
                        Color pixel = Color.clear;
                        pixel.r = (float)(bytes[index] / 255.0f);
                        pixel.g = (float)(bytes[index + 1] / 255.0f);
                        pixel.b = (float)(bytes[index + 2] / 255.0f);
                        pixel.a = (float)(bytes[index + 3] / 255.0f);
                        texture.SetPixel(x,y,z, pixel);
                    }
            texture.Apply();
        }

        public static float GetPrefabRadius(GameObject prefab){
            if(prefab.TryGetComponent<MeshRenderer>(out MeshRenderer mr))
                return (mr.bounds.extents.x + mr.bounds.extents.z) * 0.5f;
            else
                return 0.5f;
        }
    }
}