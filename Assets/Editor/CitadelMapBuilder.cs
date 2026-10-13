using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

public static class CitadelMapBuilder
{
    public const string ScenePath = "Assets/Scenes/LV6-MoonlitCitadel.unity";
    private const string Art = "Assets/Sprites/Map/Scene5/Pixel2DCastle1.1/";
    private static Material unlit;
    private static Transform scenery, terrain, actors, rewards;
    private static readonly Dictionary<string,Sprite[]> sprites = new Dictionary<string,Sprite[]>();
    private static readonly Dictionary<Texture,Material> materials = new Dictionary<Texture,Material>();
    private static Material SpriteMaterial(Sprite sprite)
    {
        if(materials.TryGetValue(sprite.texture,out var result)) return result;
        string path="Assets/Citadel/Texture-"+AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(sprite.texture))+".mat";
        result=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!result) { result=new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default")); result.mainTexture=sprite.texture; AssetDatabase.CreateAsset(result,path); }
        materials[sprite.texture]=result; return result;
    }
    public static void Build()
    {
        try
        {
            if (!Application.isBatchMode) throw new Exception("Build in an isolated project copy.");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath)) throw new Exception("Scene already exists; builder refuses to overwrite it.");
            var old = EditorSceneManager.OpenScene("Assets/Scenes/LV5-Scene5.unity");
            var cameraTemplate = Object.FindFirstObjectByType<Camera>().gameObject;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            var camera = Object.Instantiate(cameraTemplate); camera.name = "Main Camera";
            foreach(Transform child in camera.transform.Cast<Transform>().ToArray()) Object.DestroyImmediate(child.gameObject);
            camera.transform.position = new Vector3(8,4,-10);
            EditorSceneManager.CloseScene(old,true);
            if(!AssetDatabase.IsValidFolder("Assets/Citadel")) AssetDatabase.CreateFolder("Assets","Citadel");
            unlit = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"));
            AssetDatabase.CreateAsset(unlit,"Assets/Citadel/CitadelUnlit.mat");
            scenery = new GameObject("01 - Moonlit architecture").transform;
            terrain = new GameObject("02 - Traversable route").transform;
            actors = new GameObject("03 - Encounters").transform;
            rewards = new GameObject("04 - Exploration rewards").transform;
            var level = new GameObject("Citadel - Level systems").AddComponent<CitadelLevel>();
            level.persistentRootPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PersistentRoot.prefab");
            var lighting = new GameObject("Global Light").AddComponent<Light2D>();
            lighting.lightType=Light2D.LightType.Global; lighting.intensity=1;
            var spawn = new GameObject("Spawn A").AddComponent<SceneSpawnPoint>();
            spawn.spawnId="A"; spawn.transform.position=new Vector3(4,1.1f,0);
            var manager = new GameObject("Checkpoint manager").AddComponent<LevelRespawnManager>();
            Set(manager,"respawnPoint",spawn.transform);
            var checkpoints = new List<CitadelCheckpoint>();
            int[] widths={24,18,14,18,16,24,14,16,20,24,14,14,18,16,24,14,18,22,14,16,24,18,16,20,24,18,16,20,22,40};
            int[] heights={0,0,2,4,6,6,4,2,0,0,2,4,6,8,8,6,4,4,2,0,0,2,4,6,6,4,2,0,0,0};
            string[] chapter={"I  /  THE SILENT ROAD","II  /  BROKEN RAMPARTS","III  /  THE HIGH WATCH","IV  /  FALLEN COURT","V  /  THE LAST ASCENT","VI  /  MOONLIT CITADEL"};
            int x=0, coins=0, enemies=0;
            for(int i=0;i<widths.Length;i++)
            {
                int w=widths[i], y=heights[i];
                Platform("Route "+i,x,y,w,6);
                if(i%5==0)
                {
                    Label(chapter[i/5],x+6,y+7.2f,.12f);
                    Mosaic("Gate arch", "env_objects_far.png",112,272,80,96,x+9,y+4.5f,1.5f,-4,new Color(.7f,.73f,.87f));
                }
                // A procession of ruins, pillars and warm torchlight frames the route.
                if(i%2==0) Mosaic("Ruined column","env_objects_far.png",160,384,48,128,x+w-4,y+4,1,-6,new Color(.5f,.55f,.7f));
                if(i%3==1) Mosaic("Crimson standard","environment.png",0,288,64,56,x+5,y+5.5f,1,-3,Color.white);
                Torch(x+2,y+.6f); Torch(x+w-2,y+.6f);
                bool beacon = new[]{5,10,16,22,28}.Contains(i);
                if(beacon)
                {
                    var cp=new GameObject("Beacon "+checkpoints.Count).AddComponent<CitadelCheckpoint>();
                    cp.transform.position=new Vector3(x+3,y+1.5f,0); cp.level=level; cp.index=checkpoints.Count;
                    var trigger=cp.gameObject.AddComponent<BoxCollider2D>(); trigger.isTrigger=true; trigger.size=new Vector2(2,3);
                    cp.respawnPoint=new GameObject("Safe respawn").transform; cp.respawnPoint.SetParent(cp.transform); cp.respawnPoint.localPosition=new Vector3(0,-.4f,0);
                    cp.beacon=Draw("Beacon flame",AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Square.png"),x+3,y+2,.45f,1.1f,4,new Color(.9f,.65f,.25f),scenery);
                    Label("CHECKPOINT",x+3,y+3.6f,.075f);
                    checkpoints.Add(cp);
                    Prefab("HealingPotion",x+6,y+1,rewards);
                }
                if(i>0 && i<29 && !beacon)
                {
                    string kind=new[]{"Skeleton","Goblin","Golem","Mushroom"}[i%4];
                    var enemy=Enemy(kind,x+w*.72f,y); enemies++;
                    enemy.patrolDistance=Mathf.Min(3,w*.2f); enemy.agroRange=6;
                }
                if(i==7 || i==13 || i==19 || i==25) { Enemy("Bat",x+w*.8f,y+1.2f); enemies++; }
                for(int c=0;c<4;c++) { var coin=Prefab("Coin",x+5+c*2,y+1.3f,rewards); Set(coin.GetComponent<CoinPickup>(),"pickupId","citadel-coin-"+coins++); }
                // Optional higher balcony: visible reward, never needed to finish the route.
                if(i%5==3)
                {
                    Platform("Optional balcony "+i,x+3,y+3,6,1);
                    for(int c=0;c<3;c++) { var coin=Prefab("Coin",x+4+c*1.6f,y+4.3f,rewards); Set(coin.GetComponent<CoinPickup>(),"pickupId","citadel-coin-"+coins++); }
                    if(i==13) { var sword=Prefab("Sword",x+6,y+4.4f,rewards); Set(sword.GetComponent<SwordPickup>(),"pickupId","citadel-upper-sword"); }
                }
                if(i==29)
                {
                    var boss=Enemy("Boss1",x+20,y); enemies++;
                    boss.attackRange=2.2f; boss.agroRange=9; boss.attackDamage=2; boss.moveSpeed=1.8f; boss.attackCooldown=2;
                    var point=boss.attackPoint.localPosition; point.x=-2; boss.attackPoint.localPosition=point;
                    level.boss=boss.GetComponent<BossHealth>(); level.boss.maxHealth=120;
                    var portal=new GameObject("Exit - opens after guardian"); portal.transform.position=new Vector3(x+w-4,y+2,0);
                    var gate=portal.AddComponent<BoxCollider2D>(); gate.isTrigger=true; gate.size=new Vector2(2,4); gate.enabled=false;
                    portal.AddComponent<EndItemGate>(); level.exitTrigger=gate;
                    Mosaic("Final arch","env_objects_far.png",112,272,80,96,x+w-4,y+3,1,-2,new Color(.7f,.9f,1));
                    Label("DEFEAT THE GUARDIAN",x+20,y+8,.095f);
                    Label("EXIT",x+w-4,y+6.7f,.1f);
                }
                x+=w+(i%4==2?3:2);
            }
            level.checkpoints=checkpoints.ToArray();
            var bounds=new GameObject("CameraBounds").AddComponent<CameraBoundsSetup>();
            bounds.minX=-3; bounds.maxX=x; bounds.minY=-14; bounds.maxY=26;
            bounds.SendMessage("OnValidate");
            Background(x);
            BakeTerrain();
            new GameObject("Foundation art"); // Foundation is already baked by Platform().
            foreach(var component in Object.FindObjectsByType<Component>(FindObjectsSortMode.None))
                if(component && PrefabUtility.IsPartOfPrefabInstance(component)) PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            EditorSceneManager.SaveScene(scene,ScenePath);
            var entries=EditorBuildSettings.scenes.ToList();
            entries.Insert(entries.FindIndex(e=>e.path.EndsWith("Ending.unity")),new EditorBuildSettingsScene(ScenePath,true));
            EditorBuildSettings.scenes=entries.ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log($"[CitadelBuild] length={x} segments={widths.Length} coins={coins} enemies={enemies} checkpoints={checkpoints.Count}");
            EditorApplication.Exit(0);
        }
        catch(Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }

    private static Sprite Tile(string file,int x,int y)
    {
        if(!sprites.ContainsKey(file)) sprites[file]=AssetDatabase.LoadAllAssetsAtPath(Art+file).OfType<Sprite>().ToArray();
        return sprites[file].FirstOrDefault(s=>Mathf.RoundToInt(s.rect.x)==x && Mathf.RoundToInt(s.rect.y)==y);
    }
    public static void FinalizeAndPreview()
    {
        try
        {
            var scene=EditorSceneManager.OpenScene(ScenePath);
            var material=AssetDatabase.LoadAssetAtPath<Material>("Assets/Citadel/CitadelUnlit.mat");
            material.shader=Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            EditorUtility.SetDirty(material); AssetDatabase.SaveAssets();
            foreach(var sr in Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
                if(sr.sprite && sr.sharedMaterial==material) sr.sharedMaterial=SpriteMaterial(sr.sprite);
            BakeTerrain();
            var existingCamera=Object.FindFirstObjectByType<Camera>();
            foreach(Transform child in existingCamera.transform.Cast<Transform>().ToArray()) Object.DestroyImmediate(child.gameObject);
            Physics2D.SyncTransforms();
            var routes=Object.FindObjectsByType<BoxCollider2D>(FindObjectsSortMode.None).Where(c=>c.name.StartsWith("Route ")).ToArray();
            // Fill the foundation with deliberately sliced brick sprites; the source sheet's
            // importer contains large irregular sprites rather than a uniform 16px grid.
            if(!GameObject.Find("Foundation art"))
            {
                var foundation=new GameObject("Foundation art").transform;
                foreach(var route in routes)
                    for(int x=Mathf.RoundToInt(route.bounds.min.x);x<Mathf.RoundToInt(route.bounds.max.x);x++)
                        for(int y=Mathf.RoundToInt(route.bounds.min.y);y<Mathf.RoundToInt(route.bounds.max.y)-1;y++)
                            Draw("Masonry",Brick((x%4+4)%4),x+.5f,y+.5f,1,1,0,new Color(.72f,.78f,.9f),foundation);
                BakeTerrain();
            }
            foreach(var enemy in Object.FindObjectsByType<GhostController>(FindObjectsSortMode.None))
            {
                if(enemy.GetComponent<BossHealth>()) continue;
                var route=routes.First(c=>enemy.transform.position.x>c.bounds.min.x && enemy.transform.position.x<c.bounds.max.x);
                var p=enemy.transform.position; p.x=route.bounds.min.x+route.bounds.size.x*(enemy.name.StartsWith("Bat")?.8f:.72f); enemy.transform.position=p;
                PrefabUtility.RecordPrefabInstancePropertyModifications(enemy.transform);
            }
            EditorSceneManager.SaveScene(scene);
            var camera=Object.FindFirstObjectByType<Camera>();
            var brain=camera.GetComponent<Unity.Cinemachine.CinemachineBrain>(); if(brain) brain.enabled=false;
            camera.orthographic=true; camera.orthographicSize=8; camera.backgroundColor=new Color(.035f,.045f,.085f);
            int shot=0;
            foreach(var p in new[]{new Vector3(15,5,-10),new Vector3(290,10,-10),new Vector3(620,5,-10)})
            {
                camera.transform.position=p;
                var rt=new RenderTexture(1280,720,24); camera.targetTexture=rt; camera.Render();
                RenderTexture.active=rt;
                var texture=new Texture2D(1280,720,TextureFormat.RGB24,false); texture.ReadPixels(new Rect(0,0,1280,720),0,0); texture.Apply();
                var output=System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath,"../Logs"));
                System.IO.Directory.CreateDirectory(output);
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(output,"citadel-preview-"+(shot++)+".png"),texture.EncodeToPNG());
                camera.targetTexture=null; RenderTexture.active=null; Object.DestroyImmediate(texture); Object.DestroyImmediate(rt);
            }
            EditorApplication.Exit(0);
        }
        catch(Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
    private static SpriteRenderer Draw(string name,Sprite sprite,float x,float y,float w,float h,int order,Color color,Transform parent)
    {
        if(!sprite) return null;
        var go=new GameObject(name); go.transform.SetParent(parent); go.transform.position=new Vector3(x,y,0);
        var sr=go.AddComponent<SpriteRenderer>(); sr.sprite=sprite; sr.sharedMaterial=SpriteMaterial(sprite); sr.sortingOrder=order; sr.color=color;
        go.transform.localScale=new Vector3(w/sprite.bounds.size.x,h/sprite.bounds.size.y,1);
        return sr;
    }
    private static void Platform(string name,int x,int top,int width,int depth)
    {
        var go=new GameObject(name); go.transform.SetParent(terrain); go.layer=LayerMask.NameToLayer("Ground");
        go.transform.position=new Vector3(x+width*.5f,top-depth*.5f,0);
        var box=go.AddComponent<BoxCollider2D>(); box.size=new Vector2(width,depth);
        // Tile-sized renderers keep the original pixel density instead of stretching texture.
        for(int a=0;a<width;a++) for(int b=0;b<depth;b++)
            Draw("Masonry",b==0?Tile("ground.png",16+(a%4)*16,0):Brick(a%4),x+a+.5f,top-b-.5f,1,1,0,b==0?Color.white:new Color(.72f,.78f,.9f),go.transform);
    }
    private static Sprite Brick(int variation)
    {
        string path="Assets/Citadel/Brick-"+variation+".asset";
        var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if(!sprite)
        {
            sprite=Sprite.Create(AssetDatabase.LoadAssetAtPath<Texture2D>(Art+"walls.png"),new Rect(320+variation*16,320,16,16),Vector2.one*.5f,16,0,SpriteMeshType.FullRect);
            sprite.name="CitadelBrick"+variation; AssetDatabase.CreateAsset(sprite,path);
        }
        return sprite;
    }
    private static void BakeTerrain()
    {
        var cells=Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None).Where(s=>s.name=="Masonry").ToArray();
        if(cells.Length==0) return;
        if(!AssetDatabase.IsValidFolder("Assets/Citadel/Tiles")) AssetDatabase.CreateFolder("Assets/Citadel","Tiles");
        var grid=new GameObject("Ground art grid").AddComponent<Grid>();
        // Separate texture batches prevent material/atlas ambiguity while retaining compact tilemaps.
        foreach(var batch in cells.GroupBy(s=>s.sprite.texture))
        {
            var go=new GameObject("Masonry - "+batch.Key.name); go.transform.SetParent(grid.transform);
            var map=go.AddComponent<Tilemap>(); var renderer=go.AddComponent<TilemapRenderer>(); renderer.sharedMaterial=SpriteMaterial(batch.First().sprite);
            foreach(var sr in batch)
            {
                string path="Assets/Citadel/Tiles/"+sr.sprite.name+".asset";
                var tile=AssetDatabase.LoadAssetAtPath<Tile>(path);
                if(!tile) { tile=ScriptableObject.CreateInstance<Tile>(); tile.sprite=sr.sprite; tile.colliderType=UnityEngine.Tilemaps.Tile.ColliderType.None; AssetDatabase.CreateAsset(tile,path); }
                var cell=Vector3Int.FloorToInt(sr.transform.position); map.SetTile(cell,tile); map.SetTileFlags(cell,TileFlags.None); map.SetColor(cell,sr.color);
                Object.DestroyImmediate(sr.gameObject);
            }
        }
    }
    private static void Mosaic(string name,string file,int sx,int sy,int width,int height,float x,float y,float scale,int order,Color color)
    {
        var group=new GameObject(name).transform; group.SetParent(scenery);
        for(int a=0;a<width;a+=16) for(int b=0;b<height;b+=16)
            Draw("Stonework",Tile(file,sx+a,sy+b),x+(a/16f-width/32f+.5f)*scale,y+(b/16f-height/32f+.5f)*scale,scale,scale,order,color,group);
    }
    private static GameObject Prefab(string name,float x,float y,Transform parent)
    {
        var go=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/"+name+".prefab"));
        go.transform.SetParent(parent); go.transform.position=new Vector3(x,y,0); return go;
    }
    private static GhostController Enemy(string name,float x,float floor)
    {
        var go=Prefab(name,x,0,actors); Physics2D.SyncTransforms();
        go.transform.position+=Vector3.up*(floor-go.GetComponent<Collider2D>().bounds.min.y+.04f);
        PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
        return go.GetComponent<GhostController>();
    }
    private static void Set(Object target,string field,Object value) { var so=new SerializedObject(target); so.FindProperty(field).objectReferenceValue=value; so.ApplyModifiedPropertiesWithoutUndo(); }
    private static void Set(Object target,string field,string value) { var so=new SerializedObject(target); so.FindProperty(field).stringValue=value; so.ApplyModifiedPropertiesWithoutUndo(); }
    private static void Label(string text,float x,float y,float size)
    {
        var go=new GameObject(text); go.transform.SetParent(scenery); go.transform.position=new Vector3(x,y,0);
        var mesh=go.AddComponent<TextMesh>(); mesh.text=text; mesh.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); mesh.fontSize=48; mesh.characterSize=size; mesh.anchor=TextAnchor.MiddleCenter; mesh.color=new Color(.88f,.79f,.55f);
        var renderer=go.GetComponent<MeshRenderer>(); renderer.sharedMaterial=mesh.font.material; renderer.sortingOrder=5;
    }
    private static void Torch(float x,float y)
    {
        Draw("Torch bracket",Tile("walls.png",336,320),x,y,.18f,1,-1,new Color(.4f,.32f,.25f),scenery);
        var path="Assets/Sprites/Map/Scene5/PixelPlatformerSet1v.1.1/Anim/torch-A-01.png";
        var sprite=AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();
        if(sprite) Draw("Amber torch",sprite,x,y+.7f,.7f,1.1f,2,Color.white,scenery);
    }
    private static void Background(int length)
    {
        var backdrop=new GameObject("Distant mountains - parallax").AddComponent<CitadelBackdrop>();
        var mountain=AssetDatabase.LoadAllAssetsAtPath(Art+"background.png").OfType<Sprite>().First();
        for(int x=-90;x<length+120;x+=60)
            Draw("Moonlit peaks",mountain,x,8,60,30,-30,new Color(.24f,.28f,.48f),backdrop.transform);
        var square=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Square.png");
        var random=new System.Random(6026);
        for(int i=0;i<180;i++) Draw("Star",square,(float)random.NextDouble()*length,15+(float)random.NextDouble()*9,.055f,.055f,-20,new Color(.6f,.7f,.85f),scenery);
        // Repeated silhouettes tie the six regions together without hiding combat silhouettes.
        for(int x=15;x<length;x+=33)
        {
            Draw("Far watchtower",square,x,4,6,18,-15,new Color(.07f,.085f,.13f),scenery);
            for(int t=0;t<4;t++) Draw("Battlement",square,x-2.5f+t*1.7f,13.5f,.9f,1,-15,new Color(.07f,.085f,.13f),scenery);
            for(int y=1;y<12;y+=4) Draw("Distant window",square,x,y,.28f,.8f,-14,new Color(.43f,.3f,.16f),scenery);
        }
    }
    public static void Inspect()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/LV5-Scene5.unity");
        foreach(var root in SceneManager.GetActiveScene().GetRootGameObjects())
            if(!root.GetComponent<GhostController>() && !root.GetComponent<CoinPickup>())
                Debug.Log("[MapInspect] ROOT "+root.name+" components="+string.Join(",",root.GetComponents<Component>().Select(c=>c.GetType().Name)));
        foreach(string file in new[]{"ground.png","walls.png","walls_far.png","environment.png","env_objects_far.png","background.png"})
            foreach(var s in AssetDatabase.LoadAllAssetsAtPath(Art+file).OfType<Sprite>())
                Debug.Log($"[MapInspect] {file} {s.name} rect={s.rect} ppu={s.pixelsPerUnit}");
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PersistentRoot.prefab");
        Debug.Log("[MapInspect] cameras="+prefab.GetComponentsInChildren<Camera>(true).Length);
        EditorApplication.Exit(0);
    }
}
