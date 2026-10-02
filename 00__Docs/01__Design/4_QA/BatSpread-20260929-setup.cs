var path = "Assets/Art/VFX/BatGlide.prefab";
var root = UnityEditor.PrefabUtility.LoadPrefabContents(path);
try
{
    foreach (var p in root.GetComponentsInChildren<UnityEngine.ParticleSystem>(true))
    {
        if (p.gameObject == root) continue;
        p.Stop(true, UnityEngine.ParticleSystemStopBehavior.StopEmittingAndClear);
        if (p.name == "WingAfterimages") { p.gameObject.SetActive(false); continue; }
        var main = p.main;
        main.loop = false;
        var emission = p.emission;
        emission.rateOverTime = 0f;
        emission.rateOverDistance = 0f;
        short count = p.name == "SpectralWings" || p.name == "LaunchFlash" ? (short)1 : (short)8;
        emission.SetBursts(new[] { new UnityEngine.ParticleSystem.Burst(0f, count) });
        if (p.name != "SpectralWings") continue;
        main.startLifetime = 0.75f;
        var size = p.sizeOverLifetime;
        size.enabled = true;
        size.separateAxes = true;
        size.x = new UnityEngine.ParticleSystem.MinMaxCurve(1f, new UnityEngine.AnimationCurve(new UnityEngine.Keyframe(0f,0.06f),new UnityEngine.Keyframe(0.22f,1.08f),new UnityEngine.Keyframe(0.36f,1f),new UnityEngine.Keyframe(1f,1.04f)));
        size.y = new UnityEngine.ParticleSystem.MinMaxCurve(1f, new UnityEngine.AnimationCurve(new UnityEngine.Keyframe(0f,0.55f),new UnityEngine.Keyframe(0.24f,1f),new UnityEngine.Keyframe(1f,1f)));
        size.z = 1f;
        var color = p.colorOverLifetime;
        color.enabled = true;
        var gradient = new UnityEngine.Gradient();
        gradient.SetKeys(new[]{new UnityEngine.GradientColorKey(UnityEngine.Color.white,0f),new UnityEngine.GradientColorKey(UnityEngine.Color.white,1f)},new[]{new UnityEngine.GradientAlphaKey(0f,0f),new UnityEngine.GradientAlphaKey(1f,0.08f),new UnityEngine.GradientAlphaKey(1f,0.5f),new UnityEngine.GradientAlphaKey(0f,1f)});
        color.color = gradient;
    }
    UnityEditor.PrefabUtility.SaveAsPrefabAsset(root,path);
}
finally { UnityEditor.PrefabUtility.UnloadPrefabContents(root); }
return "Single wing burst saved; trailing wing disabled; spread 0.165s, fade by 0.75s.";
