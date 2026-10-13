using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

// Run only in an isolated project copy. The source scenes are read-only references.
public static class CitadelRenovation
{
    private const string Forest = "Assets/Sprites/Map/Scene3/PF_DeepForest_v1.0/";
    private const string Cave = "Assets/Sprites/Map/Scene4/PixelFantasy_Caves_1.0/";
    private const string Castle = "Assets/Sprites/Map/Scene5/Pixel2DCastle1.1/";
    private const string Woods = "Assets/Sprites/Map/Scene2/";
    private static Transform art;
    private static readonly Dictionary<Texture, Material> materials = new Dictionary<Texture, Material>();

    // Rectangles use the top-left origin of the source atlas. No source import settings change.
    private static Sprite Crop(string name, string path, int x, int top, int width, int height, float ppu=32)
    {
        string output="Assets/Citadel/Renovation/"+name+".asset";
        var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(output);
        if(sprite) return sprite;
        var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        sprite=Sprite.Create(texture,new Rect(x,texture.height-top-height,width,height),Vector2.one*.5f,ppu,0,SpriteMeshType.FullRect);
        sprite.name=name; AssetDatabase.CreateAsset(sprite,output); return sprite;
    }
    private static Material MaterialFor(Sprite sprite)
    {
        if(materials.TryGetValue(sprite.texture,out var result)) return result;
        string path="Assets/Citadel/Texture-"+AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(sprite.texture))+".mat";
        result=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!result)
        {
            result=new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"));
            result.mainTexture=sprite.texture; AssetDatabase.CreateAsset(result,path);
        }
        materials[sprite.texture]=result; return result;
    }
    private static SpriteRenderer Draw(string name, Sprite sprite, float x,float y,float width,float height,int order,Color color,Transform parent=null)
    {
        var go=new GameObject(name); go.transform.SetParent(parent?parent:art);
        go.transform.position=new Vector3(x,y,0);
        var renderer=go.AddComponent<SpriteRenderer>(); renderer.sprite=sprite;
        renderer.sharedMaterial=MaterialFor(sprite); renderer.sortingOrder=order; renderer.color=color;
        go.transform.localScale=new Vector3(width/sprite.bounds.size.x,height/sprite.bounds.size.y,1);
        return renderer;
    }
    private static void Prop(string name,Sprite sprite,float x,float ground,float scale,int order,Color color,bool flip=false)
    {
        var sr=Draw(name,sprite,x,ground+sprite.bounds.size.y*scale*.5f,sprite.bounds.size.x*scale,sprite.bounds.size.y*scale,order,color);
        sr.flipX=flip;
    }
    private static Sprite Whole(string path) => AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().First();

    public static void Run()
    {
        try
        {
            if(!Application.isBatchMode || !Application.dataPath.Contains(".utmp")) throw new Exception("Run renovation in the isolated validation copy only.");
            var scene=EditorSceneManager.OpenScene(CitadelMapBuilder.ScenePath);
            if(GameObject.Find("06 - Renovation v2")) throw new Exception("Already renovated; restore the pre-renovation scene before rebuilding.");
            if(!AssetDatabase.IsValidFolder("Assets/Citadel/Renovation")) AssetDatabase.CreateFolder("Assets/Citadel","Renovation");
            Physics2D.SyncTransforms();
            var routes=Object.FindObjectsByType<BoxCollider2D>(FindObjectsSortMode.None).Where(c=>c.name.StartsWith("Route ")).OrderBy(c=>c.bounds.min.x).ToArray();
            var oldScenery=GameObject.Find("01 - Moonlit architecture").transform;
            foreach(var child in oldScenery.Cast<Transform>().ToArray())
                if(child.name!="Beacon flame" && !child.GetComponent<TextMesh>() && child.name!="Final arch") Object.DestroyImmediate(child.gameObject);
            var backdrop=GameObject.Find("Distant mountains - parallax"); if(backdrop) Object.DestroyImmediate(backdrop);
            // Remove the old visual cells, not the independently authored collision route.
            foreach(var map in Object.FindObjectsByType<Tilemap>(FindObjectsSortMode.None)) map.ClearAllTiles();
            art=new GameObject("06 - Renovation v2").transform;

            var oak=Crop("Ancient oak",Forest+"mainlev_build2.png",624,0,280,308);
            var youngOak=Crop("Twisted oak",Forest+"mainlev_build2.png",16,16,200,196);
            var stump=Crop("Old stump",Forest+"mainlev_build2.png",16,228,88,108);
            var rocks=Crop("Forest rubble",Forest+"mainlev_build2.png",16,936,272,88);
            var boulder=Crop("Weathered rock",Forest+"mainlev_build2.png",304,936,144,88);
            var grass=Crop("Meadow grass",Woods+"Pixel_Woods_Tileset.png",208,232,48,24,16);
            var rail=Crop("Timber railing",Woods+"Pixel_Woods_Tileset.png",112,80,48,32,16);
            var stone=Crop("Cavern stone",Cave+"props1.png",176,96,176,64);
            var stalactite=Crop("Cavern pendant",Cave+"props1.png",16,176,144,144);
            var crystal=Crop("Cavern spire",Cave+"props1.png",720,48,80,112);
            var log=Crop("Balcony timber",Forest+"mainlev_build2.png",1516,912,132,16);
            var grassCap=Crop("Mossy stone lip",Woods+"Pixel_Woods_Tileset.png",32,128,16,16,16);
            var brick=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Citadel/Brick-0.asset");
            var castleCap=AssetDatabase.LoadAllAssetsAtPath(Castle+"ground.png").OfType<Sprite>().First(s=>s.rect.x==16 && s.rect.y==0);
            // Use individual opaque infill tiles, not rectangles cut across the atlas palette.
            var roots=new[] { Crop("Cliff infill A",Forest+"mainlev_build1.png",432,48,32,32),
                Crop("Cliff infill B",Forest+"mainlev_build1.png",400,80,32,32),
                Crop("Cliff infill C",Forest+"mainlev_build1.png",464,16,32,32) };
            var fillGrid=new GameObject("Cliff infill grid").AddComponent<Grid>(); fillGrid.transform.SetParent(art);
            var fillObject=new GameObject("Continuous rock infill"); fillObject.transform.SetParent(fillGrid.transform);
            var fillMap=fillObject.AddComponent<Tilemap>(); var fillRenderer=fillObject.AddComponent<TilemapRenderer>();
            fillRenderer.sharedMaterial=MaterialFor(roots[0]); fillRenderer.sortingOrder=-1;
            var fillTiles=roots.Select(s=>
            {
                string path="Assets/Citadel/Renovation/"+s.name+" tile.asset";
                var tile=AssetDatabase.LoadAssetAtPath<Tile>(path);
                if(!tile) { tile=ScriptableObject.CreateInstance<Tile>(); tile.sprite=s; tile.colliderType=Tile.ColliderType.None; AssetDatabase.CreateAsset(tile,path); }
                return tile;
            }).ToArray();
            var square=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Square.png");
            var arch=Crop("Carved arch",Castle+"env_objects_far.png",112,144,80,96,16);
            var column=Crop("Carved pillar",Castle+"env_objects_far.png",160,0,48,128,16);
            var banner=Crop("Crimson banner",Castle+"environment.png",0,136,64,56,16);
            var flame=Whole("Assets/Sprites/Map/Scene5/PixelPlatformerSet1v.1.1/Anim/torch-A-01.png");

            // Layered treelines instead of opaque rectangular towers; a continuous backdrop
            // prevents seams during parallax, while local cave/architecture stays in its biome.
            var far=new GameObject("Far forest - parallax 0.12").AddComponent<CitadelBackdrop>(); far.horizontalParallax=.12f;
            far.transform.SetParent(art);
            var mid=new GameObject("Near forest - parallax 0.045").AddComponent<CitadelBackdrop>(); mid.horizontalParallax=.045f;
            mid.transform.SetParent(art);
            for(int x=-120;x<780;x+=60)
            {
                Draw("Hazy treeline",Whole(Forest+"background3.png"),x,6,60,22.5f,-45,new Color(.52f,.65f,.83f),far.transform);
                Draw("Layered woodland",Whole(Forest+"background4.png"),x,-1,60,22.5f,-35,new Color(.58f,.7f,.85f),mid.transform);
            }
            var camera=Object.FindFirstObjectByType<Camera>(); camera.backgroundColor=new Color(.085f,.125f,.18f);
            var random=new System.Random(602613);
            for(int i=0;i<routes.Length;i++)
            {
                var r=routes[i]; float x=r.bounds.min.x,y=r.bounds.max.y,w=r.bounds.size.x;
                int region=i/5;
                var group=new GameObject($"{i:00} - "+new[]{"Rootbound approach","Old viaduct","Watchtower gardens","Hollow grotto","Reclaimed ramparts","Guardian sanctuary"}[region]).transform;
                group.SetParent(art); var rootArt=art; art=group;
                // Foundation panels have root/stone detail, with a sharp, readable walkable rim.
                for(int a=0;a<w;a++) for(int b=1;b<=6;b++)
                {
                    var cell=new Vector3Int(Mathf.RoundToInt(x)+a,Mathf.RoundToInt(y)-b,0);
                    fillMap.SetTile(cell,fillTiles[(a+b+i)%fillTiles.Length]);
                }
                for(int a=0;a<w;a++)
                    Draw("Walkable rim",region==0 || region==4?grassCap:castleCap,x+a+.5f,y-.5f,1,1,1,
                        region==3?new Color(.55f,.75f,.88f):region==0||region==4?new Color(.65f,.83f,.71f):new Color(.9f,.94f,1));
                if(region==1 || region==2 || region==5)
                {
                    for(int a=1;a<w-1;a+=5)
                    {
                        Draw("Masonry buttress",brick,x+a,y-3.35f,1,4.7f,0,new Color(.65f,.69f,.78f));
                        Draw("Buttress crown",castleCap,x+a,y-1.1f,1.5f,.5f,2,Color.white);
                    }
                    for(float a=3;a<w-2;a+=7)
                        Prop("Weathered parapet",rail,x+a,y+.02f,.85f,-3,new Color(.6f,.65f,.7f));
                }
                if(region==0 || region==4)
                {
                    Prop("Ancient sentinel tree",i%2==0?oak:youngOak,x+w*.8f,y-.2f,i%2==0?1.05f:1.45f,-12,new Color(.78f,.88f,.96f),i%2==1);
                    Prop("Distant tree",youngOak,x+2,y-1,.9f,-22,new Color(.4f,.54f,.64f),i%2==0);
                    Prop("Stump beside trail",stump,x+w*.35f,y-.12f,.55f,-2,new Color(.8f,.82f,.86f));
                }
                if(region==1 || region==2 || region==4 || region==5)
                {
                    if(i%2==0 || region==5) Prop("Ruined arcade",arch,x+w*.53f,y-.15f,region==5?1.8f:1.35f,-18,new Color(.85f,.91f,1));
                    Prop("Broken colonnade",column,x+w-2,y-.3f,region==5?1.2f:.85f,-14,new Color(.82f,.9f,1));
                    if(i%2==1) Prop("Hanging standard",banner,x+2.5f,y+4.5f,.8f,-10,new Color(.8f,.83f,.9f));
                }
                if(region==3)
                {
                    Draw("Grotto depth",Whole(Cave+"background3.png"),x+w/2,y+3,w+1,22,-28,new Color(.68f,.84f,.96f));
                    for(float a=1;a<w;a+=4.5f)
                    {
                        Prop("Cave backdrop rocks",stone,x+a,y-.1f,1.3f,-10,new Color(.55f,.7f,.83f),((int)a)%2==0);
                        Draw("High cave vault",stalactite,x+a,y+11+(a%3),4.5f,4.5f,-7,new Color(.7f,.79f,.86f));
                    }
                    Prop("Grotto spire",crystal,x+w-3,y-.1f,1.6f,-11,new Color(.65f,.8f,.95f));
                }
                // Small details do not have colliders and stay behind combat silhouettes.
                for(float a=2;a<w-1;a+=5.7f)
                {
                    Prop("Trail grasses",grass,x+a,y-.12f,.6f,-2,new Color(.65f,.8f,.75f),a%2<1);
                    if(((int)a+i)%3==0) Prop("Loose stones",boulder,x+a+1,y-.08f,.32f,-2,new Color(.75f,.8f,.86f));
                }
                Prop("Cliffside rubble",rocks,x+w*.35f,y-5.7f,.85f,2,new Color(.65f,.7f,.78f),i%2==0);
                bool hasBeacon=new[]{5,10,16,22,28}.Contains(i);
                for(int lamp=0;lamp<(hasBeacon?0:region==5?3:1);lamp++)
                {
                    float lx=x+2+lamp*(w-4)/3;
                    Draw("Lantern pedestal",brick,lx,y+.65f,.38f,1.3f,-2,new Color(.65f,.7f,.8f));
                    Draw("Warm waylight",flame,lx,y+1.75f,.7f,1.1f,-1,Color.white);
                }
                // Sparse motes brighten the negative space without concealing enemies.
                for(int n=0;n<7;n++) Draw("Firefly",square,x+(float)random.NextDouble()*w,y+2+(float)random.NextDouble()*5,.045f,.045f,-5,new Color(.6f,.84f,.66f,.7f));
                art=rootArt;
            }

            foreach(var checkpoint in Object.FindObjectsByType<CitadelCheckpoint>(FindObjectsSortMode.None))
            {
                float ground=checkpoint.transform.position.y-1.5f;
                var beacon=checkpoint.beacon;
                beacon.sprite=flame; beacon.sharedMaterial=MaterialFor(flame);
                beacon.transform.position=new Vector3(checkpoint.transform.position.x,ground+2.6f,0);
                beacon.transform.localScale=new Vector3(.7f/flame.bounds.size.x,1.1f/flame.bounds.size.y,1);
                Prop("Checkpoint wayshrine",column,checkpoint.transform.position.x,ground,.3f,-4,new Color(.9f,.94f,1));
            }

            foreach(var balcony in Object.FindObjectsByType<BoxCollider2D>(FindObjectsSortMode.None).Where(c=>c.name.StartsWith("Optional balcony ")))
            {
                int index=int.Parse(balcony.name.Substring("Optional balcony ".Length));
                float floor=routes[index].bounds.max.y;
                var bounds=balcony.bounds;
                foreach(var reward in GameObject.Find("04 - Exploration rewards").transform.Cast<Transform>())
                    if(reward.position.x>=bounds.min.x && reward.position.x<=bounds.max.x && reward.position.y>floor+3.3f && reward.position.y<floor+5)
                    {
                        reward.position+=Vector3.up*.5f;
                        PrefabUtility.RecordPrefabInstancePropertyModifications(reward);
                    }
                balcony.transform.position=new Vector3(bounds.center.x,floor+3.25f,0);
                balcony.size=new Vector2(bounds.size.x,.5f);
                Draw("Raised reward balcony",log,bounds.center.x,floor+3.25f,bounds.size.x,.5f,3,Color.white);
                Prop("Balcony back rail",rail,bounds.center.x,floor+3.5f,1,-3,new Color(.68f,.74f,.8f));
            }
            Physics2D.SyncTransforms();
            ValidateClearance();
            AssetDatabase.SaveAssets(); EditorSceneManager.SaveScene(scene);
            foreach(int index in new[]{0,3,7,12,13,17,22,29})
            {
                var r=routes[index]; Preview("citadel-v2-"+index,new Vector3(r.bounds.center.x,r.bounds.max.y+4,0));
            }
            Debug.Log("[CitadelRenovation] SAVED: six visual regions, six raised balconies, original route/progress IDs retained.");
            EditorApplication.Exit(0);
        }
        catch(Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }

    public static void ValidateClearance()
    {
        Physics2D.SyncTransforms();
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PersistentRoot.prefab");
        var instance=Object.Instantiate(prefab);
        try
        {
            var player=instance.GetComponentInChildren<PlayerController1>(true);
            var body=player.GetComponent<Collider2D>(); Physics2D.SyncTransforms();
            var size=(Vector2)body.bounds.size;
            var routes=Object.FindObjectsByType<BoxCollider2D>(FindObjectsSortMode.None).Where(c=>c.name.StartsWith("Route ")).ToArray();
            int count=0;
            foreach(var balcony in Object.FindObjectsByType<BoxCollider2D>(FindObjectsSortMode.None).Where(c=>c.name.StartsWith("Optional balcony ")))
            {
                var b=balcony.bounds; var route=routes.Single(r=>b.center.x>r.bounds.min.x && b.center.x<r.bounds.max.x);
                float floor=route.bounds.max.y, clearance=b.min.y-floor;
                if(clearance<size.y+.3f) throw new Exception(balcony.name+" insufficient headroom: "+clearance);
                // Sweep a real-sized player body under the full beam in both directions.
                for(int side=-1;side<=1;side+=2)
                {
                    var start=new Vector2(side<0?b.min.x-size.x:b.max.x+size.x,floor+size.y*.5f+.03f);
                    var hits=Physics2D.BoxCastAll(start,size*.98f,0,side<0?Vector2.right:Vector2.left,b.size.x+2*size.x,LayerMask.GetMask("Ground"));
                    if(hits.Any(h=>h.collider && !h.collider.isTrigger)) throw new Exception(balcony.name+" obstructs underpass sweep");
                }
                Debug.Log($"[CitadelClearance] {balcony.name}: clearance={clearance:F2}, player={size.y:F3}, both directions clear"); count++;
            }
            if(count!=6) throw new Exception("Expected six reward balconies");
        }
        finally { Object.DestroyImmediate(instance); }
    }
    public static void InspectReferences()
    {
        try
        {
            foreach (string name in new[] { "LV2-Scene2", "Lv3-Scene3", "Lv4-Scene4" })
            {
                EditorSceneManager.OpenScene("Assets/Scenes/" + name + ".unity");
                foreach (var map in Object.FindObjectsByType<Tilemap>(FindObjectsSortMode.None))
                    Debug.Log($"[Reference] {name}: {map.name} bounds={map.cellBounds} position={map.transform.position} tiles={map.GetUsedTilesCount()} sorting={map.GetComponent<TilemapRenderer>().sortingLayerName}");
                foreach (var batch in Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None).Where(s=>s.sprite).GroupBy(s=>AssetDatabase.GetAssetPath(s.sprite)))
                    Debug.Log($"[Reference] {name}: {batch.Key} count={batch.Count()}");
                var enemy = Object.FindObjectsByType<GhostController>(FindObjectsSortMode.None).OrderBy(e=>e.transform.position.x).First();
                Preview("reference-"+name, enemy.transform.position + new Vector3(5, 3, 0));
            }
            EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }

    public static void Preview(string name, Vector3 position)
    {
        var camera = Object.FindFirstObjectByType<Camera>();
        var brain = camera.GetComponent<Unity.Cinemachine.CinemachineBrain>();
        if (brain) brain.enabled = false;
        camera.orthographic = true; camera.orthographicSize = 8;
        camera.transform.position = new Vector3(position.x, position.y, -10);
        var rt = new RenderTexture(1280,720,24);
        camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
        var image = new Texture2D(1280,720,TextureFormat.RGB24,false);
        image.ReadPixels(new Rect(0,0,1280,720),0,0); image.Apply();
        var output = Path.GetFullPath(Path.Combine(Application.dataPath,"../Logs"));
        Directory.CreateDirectory(output); File.WriteAllBytes(Path.Combine(output,name+".png"),image.EncodeToPNG());
        camera.targetTexture=null; RenderTexture.active=null;
        Object.DestroyImmediate(image); Object.DestroyImmediate(rt);
    }
}
