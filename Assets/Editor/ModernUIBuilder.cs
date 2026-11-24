using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class ModernUIBuilder
{
    private static readonly Color Ink = new Color(.035f,.055f,.085f,1f);
    private static readonly Color Card = new Color(.075f,.11f,.15f,1f);
    private static readonly Color White = new Color(.94f,.95f,.91f,1f);
    private static readonly Color Muted = new Color(.57f,.66f,.7f,1f);
    private static readonly Color Mint = new Color(.35f,.87f,.79f,1f);
    private static readonly Color Peach = new Color(.96f,.67f,.55f,1f);
    private static Sprite rounded;
    private static TMP_FontAsset font;

    public static string Build()
    {
        var d = Object.FindAnyObjectByType<StoryDirector>();
        var canvas = d.introPanel.GetComponentInParent<Canvas>();
        font = d.subtitleText.font;
        rounded = CreateRoundedSprite();
        var scaler = canvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600,900);
        scaler.matchWidthOrHeight = .5f;
        var safe = canvas.transform.Find("SafeArea");
        if(safe == null)
        {
            safe = new GameObject("SafeArea",typeof(RectTransform)).transform;
            safe.SetParent(canvas.transform,false);
            Place(safe,0,0,1,1);
            var children = new System.Collections.Generic.List<Transform>();
            foreach(Transform child in canvas.transform) if(child!=safe) children.Add(child);
            foreach(var child in children) child.SetParent(safe,false);
        }
        foreach(var text in safe.GetComponentsInChildren<TMP_Text>(true))
        {
            text.color=White;
            text.raycastTarget=false;
        }
        foreach(var image in safe.GetComponentsInChildren<Image>(true))
        {
            image.color = image.GetComponent<Button>() != null ? Card : new Color(Ink.r,Ink.g,Ink.b,.96f);
            image.sprite=rounded;
            image.type=Image.Type.Sliced;
        }
        foreach(var outline in safe.GetComponentsInChildren<Outline>(true)) outline.enabled=false;
        foreach(var shadow in safe.GetComponentsInChildren<Shadow>(true)) shadow.enabled=false;
        foreach(var button in safe.GetComponentsInChildren<Button>(true))
        {
            var colors=button.colors;
            colors.normalColor=Color.white;
            colors.highlightedColor=new Color(1.18f,1.18f,1.18f);
            colors.pressedColor=new Color(.7f,.85f,.85f);
            colors.selectedColor=Color.white;
            colors.fadeDuration=.15f;
            button.colors=colors;
        }
        var intro=d.introPanel.transform;
        Place(intro,0,0,1,1);
        intro.GetComponent<Image>().sprite=null;
        intro.GetComponent<Image>().color=Ink;
        Decor(intro,"AccentBar",.065f,.82f,.095f,.825f,Mint);
        Text(intro,"Eyebrow","RESPUESTA / AR     —     ARCHIVO 06",.065f,.85f,.6f,.9f,18,Muted);
        var title=intro.Find("Txt_IntroTitle").GetComponent<TMP_Text>();
        Style(title,"Cada segundo\ncuenta.",.065f,.46f,.54f,.79f,76,White);
        title.fontStyle=FontStyles.Bold;
        title.lineSpacing=-7f;
        Style(intro.Find("Txt_IntroSubtitle").GetComponent<TMP_Text>(),
            "Investiga un ciberataque.\nCambia su desenlace.",.07f,.30f,.48f,.45f,28,Muted);
        Text(intro,"CaseMeta","2016   /   CASO FINANCIERO",.07f,.22f,.5f,.27f,17,Mint);
        Decor(intro,"FooterLine",.065f,.13f,.935f,.132f,new Color(.18f,.25f,.29f));
        Text(intro,"Footer","01  OBSERVA     /     02  DECIDE     /     03  ACTÚA",.07f,.055f,.7f,.11f,17,Muted);
        Text(intro,"FooterAR","UNA HISTORIA SOBRE TU MESA",.71f,.055f,.935f,.11f,14,Muted,TextAlignmentOptions.MidlineRight);
        Text(intro,"ChoiceLabel","ELIGE TU PERSPECTIVA",.585f,.79f,.94f,.85f,17,Muted);
        Choice(d.attackModeButton,.585f,.48f,.935f,.76f,Peach,"01 / RECONSTRUCCIÓN",
            "Revive el ataque","Descubre las señales que nadie atendió.","EXPLORAR EL CASO  →");
        Choice(d.defenseModeButton,.585f,.17f,.935f,.45f,Mint,"02 / INTERVENCIÓN",
            "Cambia la historia","Activa el plan. Protege los fondos.","INICIAR DEFENSA  →");
        if(intro.GetComponent<PanelEntrance>()==null) intro.gameObject.AddComponent<PanelEntrance>();

        var header=safe.Find("Panel_HUD_Superior");
        Place(header,.035f,.87f,.965f,.97f);
        Style(d.hud.statusHeaderText,"06 / ATAQUE 2016\nVIE 5 - SALA SWIFT",.025f,.12f,.43f,.9f,20,White);
        CompactButton(header.Find("Btn_CambiarModo"),"Cambiar modo",.48f,.17f,.64f,.83f);
        CompactButton(header.Find("Btn_VerPlan"),"Mi plan",.66f,.17f,.79f,.83f);
        CompactButton(header.Find("Btn_AnclarMesa"),"Centrar mesa",.81f,.17f,.975f,.83f);
        Style(d.incidentTimer.timerText,"00 h / 85 h transcurridas",.64f,.81f,.95f,.86f,20,Muted,TextAlignmentOptions.MidlineRight);
        Place(d.subtitlePanel.transform,.10f,.055f,.90f,.25f);
        Style(d.speakerText,"NARRADOR",.03f,.71f,.65f,.94f,17,Mint);
        Style(d.continueHintText,"Toca para continuar",.65f,.71f,.97f,.94f,15,Muted,TextAlignmentOptions.MidlineRight);
        Style(d.subtitleText,"",.03f,.1f,.97f,.68f,24,White);
        d.subtitleText.enableAutoSizing=true;
        d.subtitleText.fontSizeMin=19;
        d.subtitleText.fontSizeMax=24;
        Place(d.scanPanel.transform,.22f,.27f,.78f,.65f);
        Style(d.scanHintText,"<b>Tu mesa es el escenario.</b>\nApunta a una superficie plana y tócala.",.06f,.39f,.94f,.83f,28,White,TextAlignmentOptions.Center);
        CompactButton(d.scanPanel.transform.Find("Btn_ColocarEnMesa"),"Colocar aquí  →",.23f,.12f,.77f,.31f);
        Place(d.decisionPanel.transform,.12f,.29f,.88f,.84f);
        Style(d.decisionTitleText,"Elige una acción y un responsable",.04f,.8f,.96f,.97f,23,White);
        foreach(var b in d.actionButtons) ButtonText(b,22);
        foreach(var b in d.responsibleButtons) ButtonText(b,22);
        ButtonText(d.confirmButton,21);
        d.confirmButton.GetComponent<Image>().color=new Color(.12f,.33f,.31f);
        var nav=safe.Find("Panel_Controles_Inferior");
        Place(nav,.15f,.02f,.85f,.11f);
        string[] dayNames={"Viernes","Sabado","Lunes","Martes"};
        for(int i=0;i<4;i++)
            CompactButton(nav.Find("Btn_Dia_"+dayNames[i]),i==1?"Sábado":dayNames[i],.015f+i*.25f,.14f,.235f+i*.25f,.86f);
        foreach(Transform child in nav)
            if(!child.name.StartsWith("Btn_Dia_")) child.gameObject.SetActive(false);
        var presentation=canvas.GetComponent<ModernHUDPresentation>();
        if(presentation==null) presentation=canvas.gameObject.AddComponent<ModernHUDPresentation>();
        presentation.director=d;
        presentation.safeArea=(RectTransform)safe;
        presentation.header=header.gameObject;
        presentation.navigation=nav.gameObject;
        presentation.legacyBanner=safe.Find("Panel_Banner_Narrativo").gameObject;
        presentation.timer=d.incidentTimer.timerText.gameObject;
        header.gameObject.SetActive(false);
        nav.gameObject.SetActive(false);
        presentation.legacyBanner.SetActive(false);
        presentation.timer.SetActive(false);
        d.subtitlePanel.SetActive(false);
        d.scanPanel.SetActive(false);
        d.decisionPanel.SetActive(false);
        d.transitionGroup.gameObject.SetActive(false);
        d.introPanel.SetActive(true);
        EditorUtility.SetDirty(d);
        EditorSceneManager.MarkSceneDirty(d.gameObject.scene);
        AssetDatabase.SaveAssets();
        return "Interfaz moderna aplicada.";
    }

    private static void Choice(Button b,float x,float y,float x2,float y2,Color accent,string eyebrow,string title,string description,string action)
    {
        Place(b.transform,x,y,x2,y2);
        b.GetComponent<Image>().color=Card;
        var label=b.transform.Find("Label").GetComponent<TMP_Text>();
        Style(label,title,.07f,.48f,.94f,.71f,34,White);
        label.fontStyle=FontStyles.Bold;
        Text(b.transform,"Eyebrow",eyebrow,.07f,.74f,.94f,.9f,15,accent);
        Text(b.transform,"Description",description,.07f,.29f,.94f,.47f,19,Muted);
        Text(b.transform,"Action",action,.07f,.07f,.94f,.25f,17,accent);
        Decor(b.transform,"Edge",0.005f,.16f,.012f,.84f,accent);
    }

    private static void CompactButton(Transform t,string value,float x,float y,float x2,float y2)
    {
        if(t==null) return;
        Place(t,x,y,x2,y2);
        var label=t.GetComponentInChildren<TMP_Text>(true);
        Style(label,value,.04f,.08f,.96f,.92f,20,White,TextAlignmentOptions.Center);
    }

    private static void ButtonText(Button b,float size)
    {
        if(b==null) return;
        var t=b.GetComponentInChildren<TMP_Text>(true);
        t.fontSize=size;
        t.enableAutoSizing=true;
        t.fontSizeMin=17;
        t.fontSizeMax=size;
    }

    private static void Place(Transform t,float x,float y,float x2,float y2)
    {
        var r=(RectTransform)t;
        r.anchorMin=new Vector2(x,y); r.anchorMax=new Vector2(x2,y2);
        r.pivot=new Vector2(.5f,.5f);
        r.offsetMin=r.offsetMax=Vector2.zero;
    }

    private static void Style(TMP_Text t,string value,float x,float y,float x2,float y2,float size,Color color,TextAlignmentOptions align=TextAlignmentOptions.MidlineLeft)
    {
        Place(t.transform,x,y,x2,y2);
        t.text=value; t.fontSize=size; t.color=color; t.alignment=align;
        t.enableAutoSizing=false; t.fontStyle=FontStyles.Normal;
        t.margin=Vector4.zero; t.raycastTarget=false;
    }

    private static void Text(Transform parent,string name,string value,float x,float y,float x2,float y2,float size,Color color,TextAlignmentOptions align=TextAlignmentOptions.MidlineLeft)
    {
        var child=parent.Find(name);
        if(child==null)
        {
            child=new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI)).transform;
            child.SetParent(parent,false);
        }
        var text=child.GetComponent<TMP_Text>();
        text.font=font;
        Style(text,value,x,y,x2,y2,size,color,align);
    }

    private static void Decor(Transform parent,string name,float x,float y,float x2,float y2,Color color)
    {
        var child=parent.Find(name);
        if(child==null)
        {
            child=new GameObject(name,typeof(RectTransform),typeof(Image)).transform;
            child.SetParent(parent,false);
        }
        Place(child,x,y,x2,y2);
        var image=child.GetComponent<Image>();
        image.color=color; image.raycastTarget=false;
    }

    private static Sprite CreateRoundedSprite()
    {
        const string path="Assets/Art/UI/PanelRounded.png";
        var existing=AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if(existing!=null) return existing;
        System.IO.Directory.CreateDirectory("Assets/Art/UI");
        var texture=new Texture2D(64,64,TextureFormat.RGBA32,false);
        for(int y=0;y<64;y++) for(int x=0;x<64;x++)
        {
            float dx=Mathf.Max(Mathf.Abs(x-31.5f)-17.5f,0f);
            float dy=Mathf.Max(Mathf.Abs(y-31.5f)-17.5f,0f);
            texture.SetPixel(x,y,new Color(1,1,1,Mathf.Clamp01(14f-Mathf.Sqrt(dx*dx+dy*dy))));
        }
        texture.Apply();
        System.IO.File.WriteAllBytes(path,texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path);
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType=TextureImporterType.Sprite;
        importer.spriteBorder=new Vector4(16,16,16,16);
        importer.alphaIsTransparency=true;
        importer.mipmapEnabled=false;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
}
