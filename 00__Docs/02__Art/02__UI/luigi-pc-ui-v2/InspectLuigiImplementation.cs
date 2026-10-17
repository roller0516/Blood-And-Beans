using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEditor;
using TMPro;
public static class InspectLuigiImplementation
{
    public static string Main()
    {
        const string folder="Assets/Art/UI/Prefabs";
        var output=new StringBuilder();
        foreach(var guid in AssetDatabase.FindAssets("t:Prefab",new[]{folder}))
        {
            var path=AssetDatabase.GUIDToAssetPath(guid);var root=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            output.AppendLine("\nASSET "+path);
            foreach(var t in root.GetComponentsInChildren<Transform>(true))
            {
                var r=t as RectTransform;var text=t.GetComponent<TMP_Text>();var img=t.GetComponent<UnityEngine.UI.Image>();
                output.Append(new string(' ',Depth(t,root.transform)*2)).Append(t.name).Append(t.gameObject.activeSelf?"":" [inactive]");
                if(r!=null)output.Append(" pos=").Append(r.anchoredPosition).Append(" size=").Append(r.sizeDelta).Append(" anchors=").Append(r.anchorMin).Append("/").Append(r.anchorMax);
                if(text!=null)output.Append(" text='").Append(text.text.Replace("\n"," / ")).Append("' font=").Append(text.fontSize);
                if(img!=null)output.Append(" image=").Append(AssetDatabase.GetAssetPath(img.sprite)).Append(" tint=").Append(img.color);
                output.Append(" components=").AppendLine(string.Join(",",t.GetComponents<Component>().Where(x=>x!=null).Select(x=>x.GetType().Name)));
            }
            foreach(var c in root.GetComponentsInChildren<MonoBehaviour>(true).Where(c=>c!=null&&c.GetType().Assembly.GetName().Name=="BB.Client"))
            {
                output.AppendLine(" FIELDS "+c.GetType().Name+" @ "+c.name);
                var so=new SerializedObject(c);var it=so.GetIterator();bool enter=true;
                while(it.NextVisible(enter)){enter=false;if(it.propertyType==SerializedPropertyType.ObjectReference)output.AppendLine("  "+it.propertyPath+" = "+(it.objectReferenceValue!=null?it.objectReferenceValue.name+" ("+AssetDatabase.GetAssetPath(it.objectReferenceValue)+")":"NULL"));}
            }
        }
        var file=Path.GetFullPath("../00__Docs/02__Art/02__UI/luigi-pc-ui-v2/luigi-prefab-inventory.txt");File.WriteAllText(file,output.ToString());return file;
    }
    static int Depth(Transform t,Transform root){int i=0;while(t!=root){i++;t=t.parent;}return i;}
}
