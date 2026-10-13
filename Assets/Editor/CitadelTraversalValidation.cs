using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

// Real Rigidbody2D movement complements the editor's geometric sweeps.
[InitializeOnLoad]
public static class CitadelTraversalValidation
{
    private static PlayerController1 player;
    private static Rigidbody2D body;
    private static Collider2D shape;
    private static BoxCollider2D[] balconies;
    private static int index, stage;
    private static float floor, deadline;
    private static bool started, roseAbove, captured;
    static CitadelTraversalValidation() { EditorApplication.update+=Tick; }

    public static void Run()
    {
        if(!Application.isBatchMode || !Application.dataPath.Contains(".utmp")) throw new Exception("Use isolated validation project");
        PlayerSettings.companyName="SamuraiRepairValidation";
        PlayerSettings.productName="CitadelTraversal-"+Guid.NewGuid().ToString("N");
        EditorSceneManager.OpenScene(CitadelMapBuilder.ScenePath);
        SessionState.SetBool("CitadelTraversal.Active",true);
        SessionState.SetFloat("CitadelTraversal.Timeout",(float)EditorApplication.timeSinceStartup+120);
        EditorApplication.EnterPlaymode();
    }
    private static void Place(float x,float footY)
    {
        var offset=shape.bounds.center-player.transform.position;
        body.position=new Vector2(x-offset.x,footY+shape.bounds.extents.y-offset.y);
        body.linearVelocity=Vector2.zero; Physics2D.SyncTransforms();
    }
    private static void BeginBalcony()
    {
        var b=balconies[index].bounds;
        var route=Object.FindObjectsByType<BoxCollider2D>(FindObjectsSortMode.None)
            .Single(r=>r.name.StartsWith("Route ") && b.center.x>r.bounds.min.x && b.center.x<r.bounds.max.x);
        floor=route.bounds.max.y; stage=0; roseAbove=false;
        Place(b.min.x-1,floor+.03f); deadline=Time.time+12;
    }
    private static void Tick()
    {
        if(!SessionState.GetBool("CitadelTraversal.Active",false)) return;
        try
        {
            if(EditorApplication.timeSinceStartup>SessionState.GetFloat("CitadelTraversal.Timeout",0)) throw new Exception("Traversal runner timed out");
            if(!EditorApplication.isPlaying || Time.time<.5f) return;
            if(!started)
            {
                player=Object.FindFirstObjectByType<PlayerController1>(); if(!player) return;
                player.enabled=false;
                player.GetComponent<PlayerOutOfBoundsKiller>().enabled=false;
                foreach(var enemy in Object.FindObjectsByType<GhostController>(FindObjectsSortMode.None)) enemy.gameObject.SetActive(false);
                body=player.GetComponent<Rigidbody2D>(); shape=player.GetComponent<Collider2D>();
                balconies=Object.FindObjectsByType<BoxCollider2D>(FindObjectsSortMode.None).Where(c=>c.name.StartsWith("Optional balcony ")).OrderBy(c=>c.bounds.center.x).ToArray();
                if(balconies.Length!=6) throw new Exception("Expected six balconies");
                started=true; BeginBalcony();
            }
            if(Time.time>deadline) throw new Exception($"{balconies[index].name}: movement stage {stage} timed out");
            var b=balconies[index].bounds;
            if(stage<2)
            {
                body.linearVelocity=new Vector2(stage==0?5:-5,body.linearVelocity.y);
                if(shape.bounds.min.y<floor-.15f || shape.bounds.min.y>floor+.3f) throw new Exception("Grounded underpass changed player height");
                if(index==2 && stage==0 && !captured && shape.bounds.center.x>b.center.x)
                {
                    CitadelRenovation.Preview("citadel-v2-underpass",new Vector3(b.center.x,floor+4,0)); captured=true;
                }
                if(stage==0 && shape.bounds.center.x>b.max.x+1)
                { stage=1; Place(b.max.x+1,floor+.03f); }
                else if(stage==1 && shape.bounds.center.x<b.min.x-1)
                {
                    stage=2; Place(b.min.x-1,floor+.03f);
                    float jump=new SerializedObject(player.GetComponent<PlayerJump>()).FindProperty("jumpForce").floatValue;
                    body.linearVelocity=new Vector2(5,jump);
                }
            }
            else
            {
                body.linearVelocity=new Vector2(shape.bounds.center.x<b.min.x+1?5:0,body.linearVelocity.y);
                if(shape.bounds.min.y>b.max.y+.03f) roseAbove=true;
                if(roseAbove && Mathf.Abs(shape.bounds.min.y-b.max.y)<.08f && Mathf.Abs(body.linearVelocity.y)<.1f)
                {
                    Debug.Log("[CitadelTraversal] "+balconies[index].name+": walk right/left + single jump landing PASSED");
                    if(++index==balconies.Length)
                    {
                        SessionState.SetBool("CitadelTraversal.Active",false);
                        Debug.Log("[CitadelTraversal] ALL 6 UNDERPASSES AND LANDINGS PASSED"); EditorApplication.Exit(0); return;
                    }
                    BeginBalcony();
                }
            }
        }
        catch(Exception e)
        {
            SessionState.SetBool("CitadelTraversal.Active",false); Debug.LogException(e); EditorApplication.Exit(1);
        }
    }
}
