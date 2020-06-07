using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

namespace JamathansMarchingTerrain{
    [AttributeUsage(AttributeTargets.Class)]
    public class BrushAttribute : Attribute{
        public string name;
        public string path;
        public BrushAttribute(string name, string path){
            this.name = name;
            this.path = path;
        }
    }

    public abstract class Brush
    {
        public abstract void Process(ref float[,,] points, Vector3 brushPos, float brushRadius, float normalizedBrushRadius, AnimationCurve falloff, float intensity);
        public abstract void ProcessPoint(ref float point, Vector3 pointPos, Vector3 normBrushPos, float normBrushRadius, AnimationCurve falloff, float brushIntensity);
    }
}