using UnityEditor;
using UnityEngine;

// 카페 가구와 장식만 편집한다. 네트워크 구성과 기획 수치는 보존한다.
// 여기서 지은 CafeDecor는 공개 껍데기로 떼어 내 쓴다. 이 함수를 돌린 뒤에는
// CafeShellSetup.Apply()도 같이 돌려야 껍데기가 갱신되고 본체에서 장식이 다시 빠진다.
public static class CafeLayoutSetup
{
    const string Path = "Assets/Art/Environment/Prefabs/Cafe.prefab";
    const string Materials = "Assets/Art/Environment/Materials/";
    public static void Apply()
    {
        var root = PrefabUtility.LoadPrefabContents(Path);
        try
        {
            var cream = Material("CafeCream", new Color(.84f,.77f,.62f));
            var wood = Material("CafeWalnut", new Color(.25f,.13f,.075f));
            var green = Material("CafeSage", new Color(.26f,.40f,.31f));
            var brass = Material("CafeBrass", new Color(.66f,.45f,.20f));
            var floor = root.transform.Find("Floor");
            floor.GetComponent<Renderer>().sharedMaterial = cream;
            Place(root,"CupRack",new Vector3(-3.6f,.5f,-5.5f));
            Place(root,"PlateRack",new Vector3(3.6f,.5f,-5.5f));
            Place(root,"PrepIsland",new Vector3(0,.5f,1.5f));
            Place(root,"Counter",new Vector3(0,.5f,8.0f));
            var left = new[]{"Milk","Cream","Chocolate","Ice"};
            var right = new[]{"Almond","Berry","BloodBean"};
            for(int i=0;i<left.Length;i++) Place(root,"Finish_"+left[i],new Vector3(-8.8f,.5f,-5+i*3.1f),-90);
            for(int i=0;i<right.Length;i++) Place(root,"Finish_"+right[i],new Vector3(8.8f,.5f,-3.5f+i*3.1f),90);
            var old = root.transform.Find("CafeDecor");
            if(old != null) Object.DestroyImmediate(old.gameObject);
            var decor = new GameObject("CafeDecor").transform;
            decor.SetParent(root.transform,false);
            Box(decor,"EntryRunner",new Vector3(0,.015f,-4.6f),new Vector3(2.25f,.02f,6.2f),green);
            Box(decor,"ServiceRug",new Vector3(0,.012f,6.0f),new Vector3(9,.02f,2.1f),green);
            Box(decor,"BackPanel",new Vector3(0,.6f,14.0f),new Vector3(21.6f,1.2f,.25f),wood);
            Box(decor,"LeftPanel",new Vector3(-10.7f,.6f,3.0f),new Vector3(.25f,1.2f,21.5f),wood);
            Box(decor,"RightPanel",new Vector3(10.7f,.6f,3.0f),new Vector3(.25f,1.2f,21.5f),wood);
            Box(decor,"BackCap",new Vector3(0,1.24f,14.0f),new Vector3(21.7f,.09f,.32f),brass);
            foreach(var x in new[]{-10.7f,10.7f}) Box(decor,"SideCap",new Vector3(x,1.24f,3),new Vector3(.32f,.09f,21.5f),brass);
            foreach(var x in new[]{-9.5f,9.5f})
            {
                Plant(decor,new Vector3(x,0,-7.0f),wood,green);
                Plant(decor,new Vector3(x,0,12.7f),wood,green);
            }
            Furniture(decor,"tableCoffee",new Vector3(-6,0,10.5f),0);
            Furniture(decor,"chair",new Vector3(-7.6f,0,10.5f),90);
            Furniture(decor,"chair",new Vector3(-4.4f,0,10.5f),-90);
            foreach(var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = root.layer;
            PrefabUtility.SaveAsPrefabAsset(root,Path);
            AssetDatabase.SaveAssets();
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
    static void Place(GameObject root,string name,Vector3 position,float yaw=0)
    {
        var t=root.transform.Find(name);
        if(t==null) throw new System.InvalidOperationException(name+" 누락");
        t.localPosition=position;
        t.localRotation=Quaternion.Euler(0,yaw,0);
    }
    static Material Material(string name,Color color)
    {
        var path=Materials+name+".mat";
        var material=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(material==null)
        {
            material=new Material(AssetDatabase.LoadAssetAtPath<Material>(Materials+"CafeFloor.mat"));
            AssetDatabase.CreateAsset(material,path);
        }
        material.color=color;
        if(material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap",null);
        EditorUtility.SetDirty(material);
        return material;
    }
    static GameObject Box(Transform parent,string name,Vector3 position,Vector3 scale,Material material)
    {
        var item=GameObject.CreatePrimitive(PrimitiveType.Cube);
        item.name=name; item.transform.SetParent(parent,false);
        item.transform.localPosition=position; item.transform.localScale=scale;
        item.GetComponent<Renderer>().sharedMaterial=material;
        Object.DestroyImmediate(item.GetComponent<Collider>());
        return item;
    }
    static void Furniture(Transform parent,string name,Vector3 position,float yaw)
    {
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/AssetStore/Kenney/cafe-selection/Models/"+name+".fbx");
        var item=(GameObject)PrefabUtility.InstantiatePrefab(prefab,parent);
        item.transform.localPosition=position;
        item.transform.localRotation=Quaternion.Euler(0,yaw,0);
        item.transform.localScale=Vector3.one;
        var renderers=item.GetComponentsInChildren<Renderer>();
        var bounds=renderers[0].bounds;
        foreach(var renderer in renderers) bounds.Encapsulate(renderer.bounds);
        item.transform.localScale=Vector3.one*((name=="chair" ? 1.6f : 1.0f)/bounds.size.y);
        bounds=renderers[0].bounds;
        foreach(var renderer in renderers) bounds.Encapsulate(renderer.bounds);
        item.transform.position+=Vector3.up*(parent.position.y-bounds.min.y);
    }
    static void Plant(Transform parent,Vector3 position,Material pot,Material leaves)
    {
        var baseObject=GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        baseObject.name="Planter"; baseObject.transform.SetParent(parent,false);
        baseObject.transform.localPosition=position+Vector3.up*.35f;
        baseObject.transform.localScale=new Vector3(.85f,.35f,.85f);
        baseObject.GetComponent<Renderer>().sharedMaterial=pot;
        Object.DestroyImmediate(baseObject.GetComponent<Collider>());
        var bush=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/AssetStore/Kenney/nature-kit/Models/FBX format/plant_bushLarge.fbx");
        var model=(GameObject)PrefabUtility.InstantiatePrefab(bush,parent);
        model.transform.localPosition=position+Vector3.up*.6f;
        model.transform.localScale=Vector3.one*3f;
        foreach(var r in model.GetComponentsInChildren<Renderer>()) r.sharedMaterial=leaves;
    }
}
