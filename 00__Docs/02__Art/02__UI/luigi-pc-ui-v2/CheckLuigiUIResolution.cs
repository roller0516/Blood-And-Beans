using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
public static class CheckLuigiUIResolution
{
    const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
    static Type ViewType=>typeof(Editor).Assembly.GetType("UnityEditor.GameView");
    static object Group(){var t=typeof(Editor).Assembly.GetType("UnityEditor.GameViewSizes");return t.GetProperty("currentGroup",Flags).GetValue(typeof(ScriptableSingleton<>).MakeGenericType(t).GetProperty("instance",Flags).GetValue(null));}
    static string Set(int width,int height)
    {
        var group=Group();var type=group.GetType();var texts=(string[])type.GetMethod("GetDisplayTexts",Flags).Invoke(group,null);var label="BB UI verification "+width+"x"+height;int index=Array.FindIndex(texts,x=>x.StartsWith(label));
        if(index<0){var assembly=typeof(Editor).Assembly;var sizeType=assembly.GetType("UnityEditor.GameViewSizeType");var size=Activator.CreateInstance(assembly.GetType("UnityEditor.GameViewSize"),new object[]{Enum.Parse(sizeType,"FixedResolution"),width,height,label});type.GetMethod("AddCustomSize",Flags).Invoke(group,new[]{size});index=(int)type.GetMethod("GetTotalCount",Flags).Invoke(group,null)-1;}
        foreach(var view in Resources.FindObjectsOfTypeAll(ViewType))ViewType.GetProperty("selectedSizeIndex",Flags).SetValue(view,index);return width+"x"+height;
    }
    public static string Main()=>Set(2560,1080);
    public static string FullHD()=>Set(1920,1080);
    public static string FourThree()=>Set(1600,1200);
    public static string Restore()
    {
        foreach(var view in Resources.FindObjectsOfTypeAll(ViewType))ViewType.GetProperty("selectedSizeIndex",Flags).SetValue(view,0);
        var group=Group();var type=group.GetType();var texts=(string[])type.GetMethod("GetDisplayTexts",Flags).Invoke(group,null);for(int i=texts.Length-1;i>=0;i--)if(texts[i].StartsWith("BB UI verification "))type.GetMethod("RemoveCustomSize",Flags).Invoke(group,new object[]{i});return "Restored original Free Aspect and removed only task-owned verification sizes";
    }
}
