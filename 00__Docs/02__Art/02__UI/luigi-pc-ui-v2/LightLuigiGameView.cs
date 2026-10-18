using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// 현재 설치 에디터에서 조사한 GameView API를 사용한다. 사용자 원래 선택은 SessionState에 보존한다.
public static class LightLuigiGameView
{
    const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
    const string Key="BB.LightLuigi.OriginalGameView";
    public static string Main(int width,int height)
    {
        var assembly=typeof(Editor).Assembly;var viewType=assembly.GetType("UnityEditor.GameView");
        var view=EditorWindow.GetWindow(viewType);var selected=viewType.GetProperty("selectedSizeIndex",Flags);
        if(SessionState.GetInt(Key,-1)<0)SessionState.SetInt(Key,(int)selected.GetValue(view));
        if(width==0){selected.SetValue(view,SessionState.GetInt(Key,0));SessionState.EraseInt(Key);view.Repaint();return "Original Game View selection restored";}
        var sizesType=assembly.GetType("UnityEditor.GameViewSizes");var sizes=sizesType.BaseType.GetProperty("instance",BindingFlags.Public|BindingFlags.Static).GetValue(null);
        var group=sizesType.GetProperty("currentGroup",Flags).GetValue(sizes);var type=group.GetType();var count=(int)type.GetMethod("GetTotalCount",Flags).Invoke(group,null);
        var sizeType=assembly.GetType("UnityEditor.GameViewSize");var kind=assembly.GetType("UnityEditor.GameViewSizeType");
        var size=Activator.CreateInstance(sizeType,Flags,null,new object[]{Enum.Parse(kind,"FixedResolution"),width,height,"BB UI capture "+width+"x"+height},null);
        type.GetMethod("AddCustomSize",Flags).Invoke(group,new[]{size});selected.SetValue(view,count);view.Repaint();
        return "Requested actual Game View "+width+"x"+height+"; query Screen size on the next frame";
    }
}
