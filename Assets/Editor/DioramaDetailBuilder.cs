using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Detalles estáticos combinados por material: pocos draw calls para AR móvil.
public static class DioramaDetailBuilder
{
    private const string Folder = "Assets/Art/DioramaDetails";
    private static Material dark, silver, paper, cyan, gold, skin;

    public static string Build()
    {
        System.IO.Directory.CreateDirectory(Folder);
        AssetDatabase.Refresh();
        dark = Mat("Grafito", new Color(.035f,.055f,.085f), .35f);
        silver = Mat("Aluminio", new Color(.48f,.57f,.65f), .8f);
        paper = Mat("Marfil", new Color(.88f,.9f,.86f), 0f);
        cyan = Mat("Indicadores", new Color(.05f,.72f,.85f), .25f);
        gold = Mat("Laton", new Color(.85f,.57f,.16f), .75f);
        skin = Mat("Piel", new Color(.61f,.36f,.23f), 0f);
        var director = Object.FindAnyObjectByType<StoryDirector>();
        int count = 0;
        foreach (var day in director.days)
        {
            // Copia antes de añadir hijos para que reconstruir sea idempotente.
            var roots = new List<Transform>();
            foreach (Transform t in day.diorama.transform) roots.Add(t);
            foreach (var t in roots)
            {
                if (t.Find("Detalle_Modelo") != null) continue;
                var root = new GameObject("Detalle_Modelo");
                root.transform.SetParent(t, false);
                string n = t.name;
                if (n.Contains("Impresora"))
                {
                    Box(root, "Tapa", new Vector3(0,.5f,0), new Vector3(.94f,.12f,.9f), silver);
                    Box(root, "Salida", new Vector3(0,-.12f,-.51f), new Vector3(.78f,.18f,.04f), dark);
                    Box(root, "Hoja", new Vector3(0,-.2f,-.68f), new Vector3(.65f,.025f,.45f), paper);
                    for(int i=0;i<5;i++) Box(root,"Texto",new Vector3(0,-.18f,-.57f-i*.05f),new Vector3(.42f,.008f,.012f),silver);
                    Box(root,"Panel",new Vector3(.24f,.38f,-.51f),new Vector3(.22f,.15f,.025f),dark);
                }
                else if (n.Contains("Servidor"))
                {
                    for(int i=0;i<6;i++)
                    {
                        float y=-.39f+i*.15f;
                        Box(root,"Modulo",new Vector3(0,y,-.515f),new Vector3(.86f,.12f,.04f),silver);
                        Box(root,"Ventilacion",new Vector3(-.12f,y,-.54f),new Vector3(.49f,.045f,.025f),dark);
                        Box(root,"Estado",new Vector3(.31f,y,-.55f),new Vector3(.045f,.035f,.025f),cyan);
                    }
                }
                else if (n.Contains("Edificio_Banco"))
                {
                    Box(root,"Cornisa",new Vector3(0,.45f,0),new Vector3(1.08f,.12f,1.08f),paper);
                    Box(root,"Entrada",new Vector3(0,-.22f,-.52f),new Vector3(.23f,.5f,.04f),dark);
                    for(int x=-1;x<=1;x+=2)
                        for(int y=0;y<3;y++)
                            Box(root,"Ventana",new Vector3(x*.31f,-.27f+y*.25f,-.52f),new Vector3(.16f,.15f,.035f),cyan);
                    Box(root,"Dintel",new Vector3(0,.09f,-.55f),new Vector3(.36f,.07f,.09f),gold);
                }
                else if (n.Contains("Boveda_Bancaria"))
                {
                    Box(root,"Interior",new Vector3(0,0,-.505f),new Vector3(.81f,.83f,.03f),dark);
                    for(int x=-1;x<=1;x+=2)
                        Box(root,"Marco",new Vector3(x*.45f,0,-.55f),new Vector3(.09f,1.02f,.14f),silver);
                    for(int y=-1;y<=1;y+=2)
                        Box(root,"Marco",new Vector3(0,y*.46f,-.55f),new Vector3(.96f,.09f,.14f),silver);
                    for(int y=0;y<3;y++)
                        Box(root,"Estante",new Vector3(0,-.3f+y*.29f,-.53f),new Vector3(.78f,.035f,.12f),gold);
                }
                else if (n.Contains("Puerta_Boveda"))
                {
                    for(int i=0;i<8;i++)
                    {
                        float a=i*Mathf.PI/4f;
                        Box(root,"Perno",new Vector3(Mathf.Cos(a)*.38f,Mathf.Sin(a)*.38f,-.55f),new Vector3(.065f,.065f,.18f),silver);
                    }
                    Box(root,"Cerradura",new Vector3(0,0,-.58f),new Vector3(.22f,.22f,.16f),gold);
                }
                else if (n.StartsWith("Lote_Dinero"))
                {
                    Box(root,"Faja",new Vector3(0,0,-.01f),new Vector3(.22f,1.04f,1.04f),paper);
                    for(int i=0;i<5;i++) Box(root,"Billete",new Vector3(0,-.4f+i*.18f,-.51f),new Vector3(.96f,.018f,.02f),paper);
                }
                else if (n.Contains("Hugo_Benavides"))
                {
                    Box(root,"Camisa",new Vector3(0,.25f,-.4f),new Vector3(.4f,.65f,.16f),paper);
                    Box(root,"Corbata",new Vector3(0,.2f,-.5f),new Vector3(.12f,.5f,.08f),cyan);
                    for(int x=-1;x<=1;x+=2)
                    {
                        Box(root,"Manga",new Vector3(x*.52f,.02f,0),new Vector3(.24f,.65f,.55f),dark);
                        Box(root,"Mano",new Vector3(x*.52f,-.36f,0),new Vector3(.2f,.17f,.4f),skin);
                    }
                }
                else if (n.Contains("Cabeza_Hugo"))
                {
                    for(int x=-1;x<=1;x+=2)
                        Box(root,"Gafas",new Vector3(x*.2f,.06f,-.44f),new Vector3(.29f,.16f,.08f),dark);
                    Box(root,"Puente",new Vector3(0,.06f,-.48f),new Vector3(.15f,.035f,.04f),silver);
                    Box(root,"Cabello",new Vector3(0,.37f,.04f),new Vector3(.77f,.22f,.68f),dark);
                }
                else if (n.Contains("Teclado"))
                {
                    for(int x=0;x<9;x++) for(int z=0;z<3;z++)
                        Box(root,"Tecla",new Vector3(-.42f+x*.105f,.54f,-.3f+z*.27f),new Vector3(.075f,.1f,.17f),paper);
                }
                if(root.transform.childCount==0) { Object.DestroyImmediate(root); continue; }
                Combine(root, n);
                PrefabUtility.SaveAsPrefabAsset(root, Folder+"/"+n+".prefab");
                count++;
            }
        }
        EditorSceneManager.MarkSceneDirty(director.gameObject.scene);
        AssetDatabase.SaveAssets();
        return count+" modelos detallados";
    }

    private static Material Mat(string name, Color color, float metallic)
    {
        string path=Folder+"/"+name+".mat";
        var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(m!=null) return m;
        m=new Material(Shader.Find("Universal Render Pipeline/Lit"));
        m.SetColor("_BaseColor",color);
        m.SetFloat("_Metallic",metallic);
        m.SetFloat("_Smoothness",.38f);
        AssetDatabase.CreateAsset(m,path);
        return m;
    }

    private static void Box(GameObject root,string name,Vector3 pos,Vector3 size,Material mat)
    {
        var g=GameObject.CreatePrimitive(PrimitiveType.Cube);
        g.name=name;
        g.transform.SetParent(root.transform,false);
        g.transform.localPosition=pos;
        g.transform.localScale=size;
        Object.DestroyImmediate(g.GetComponent<Collider>());
        g.GetComponent<Renderer>().sharedMaterial=mat;
    }

    private static void Combine(GameObject root,string key)
    {
        var groups=new Dictionary<Material,List<CombineInstance>>();
        var filters=root.GetComponentsInChildren<MeshFilter>();
        foreach(var f in filters)
        {
            var material=f.GetComponent<Renderer>().sharedMaterial;
            if(!groups.ContainsKey(material)) groups[material]=new List<CombineInstance>();
            groups[material].Add(new CombineInstance { mesh=f.sharedMesh, transform=root.transform.worldToLocalMatrix*f.transform.localToWorldMatrix });
        }
        foreach(var group in groups)
        {
            var mesh=new Mesh { name=key+"_"+group.Key.name };
            mesh.CombineMeshes(group.Value.ToArray(),true,true);
            AssetDatabase.CreateAsset(mesh,Folder+"/"+mesh.name+".asset");
            var go=new GameObject(group.Key.name,typeof(MeshFilter),typeof(MeshRenderer));
            go.transform.SetParent(root.transform,false);
            go.GetComponent<MeshFilter>().sharedMesh=mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial=group.Key;
        }
        foreach(var f in filters) Object.DestroyImmediate(f.gameObject);
    }
}
