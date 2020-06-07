using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace JamathansMarchingTerrain{
    [Brush("Subtract", "Builtin/Subtract")]
    public class SubtractBrush : Brush{
        public override void Process(ref float[,,] points, Vector3 brushPos, float brushRadius, float normalizedBrushRadius, AnimationCurve falloff, float intensity){
            for(int x = 0; x < points.GetLength(0); x++)
                for(int y = 0; y < points.GetLength(1); y++)
                    for(int z = 0; z < points.GetLength(2); z++){
                        float dist = Vector3.Distance(brushPos, new Vector3(x, y, z));
                        if(dist <= normalizedBrushRadius)
                            points[x,y,z] = Mathf.Clamp01(points[x,y,z] - intensity * Time.deltaTime * falloff.Evaluate(1.0f - dist / brushRadius));
                    }
        }
        public override void ProcessPoint(ref float point, Vector3 pointPos, Vector3 normBrushPos, float normBrushRadius, AnimationCurve falloff, float brushIntensity){
            float dist = Vector3.Distance(normBrushPos, pointPos);
            if(dist <= normBrushRadius)
                point -= brushIntensity * Time.deltaTime * falloff.Evaluate(1.0f - dist / normBrushRadius);
        }
    }
}