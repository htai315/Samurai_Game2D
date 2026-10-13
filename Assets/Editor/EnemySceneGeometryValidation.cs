using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// Run in an isolated copy only. Opens scenes without saving or rewriting assets.
public static class EnemySceneGeometryValidation
{
    public static void Run()
    {
        try { Audit(); EditorApplication.Exit(0); }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }

    public static void Audit()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Use an isolated batch-mode project.");
        int count = 0, cases = 0, failures = 0;
        foreach (string scene in AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" }).Select(AssetDatabase.GUIDToAssetPath))
        {
            EditorSceneManager.OpenScene(scene);
            var player = Object.FindFirstObjectByType<PlayerController1>();
            if (!player) player = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab")).GetComponent<PlayerController1>();
            var pc = player.GetComponent<Collider2D>();
            Physics2D.SyncTransforms();
            foreach (var enemy in Object.FindObjectsByType<GhostController>(FindObjectsSortMode.None))
            {
                count++;
                Call(enemy, "Awake");
                Set(enemy, "player", player.transform);
                Set(enemy, "playerController", player);
                Set(enemy, "playerCollider", pc);
                var collider = enemy.GetComponent<Collider2D>();
                var bounds = collider.bounds;
                var filter = new ContactFilter2D { useLayerMask = true, layerMask = (LayerMask)Get(enemy, "obstacleLayers"), useTriggers = false };
                Set(enemy, "bodyCollider", collider);
                Set(enemy, "terrainFilter", filter);
                var hits = new RaycastHit2D[32];
                int n = Physics2D.Raycast(bounds.center, Vector2.down, filter, hits, bounds.extents.y + 5);
                float floor = n > 0 ? hits[0].point.y : float.NaN;
                bool left = (bool)Call(enemy, "CanMove", -1f, .04f);
                bool right = (bool)Call(enemy, "CanMove", 1f, .04f);
                Debug.Log($"[EnemySceneAudit] scene={scene} name={enemy.name} pos={enemy.transform.position:F4} bottom={bounds.min.y:F4} floor={floor:F4} gap={bounds.min.y-floor:F4} groundedType={Get(enemy,"avoidLedges")} move={left}/{right} attackLocal={enemy.attackPoint.localPosition:F4} range={enemy.attackRange} agro={enemy.agroRange}");
                if (!left && !right)
                {
                    failures++;
                    n = Physics2D.BoxCast(bounds.center, (Vector2)bounds.size-Vector2.one*.04f,0,Vector2.right,filter,hits,.06f);
                    for(int i=0;i<n;i++) Debug.Log($"[EnemySceneAudit] BLOCK {enemy.name} center={bounds.center:F4} size={bounds.size:F4} hit={hits[i].collider.name} point={hits[i].point:F4} normal={hits[i].normal} distance={hits[i].distance}");
                }
                int groundHits = 0;
                foreach(float side in new[]{-1f,1f})
                {
                    Call(enemy,"Face",side,true);
                    foreach(float dx in new[]{.3f,.8f,1.5f})
                    {
                        float x=enemy.transform.position.x+side*dx;
                        int floors=Physics2D.Raycast(new Vector2(x,bounds.center.y),Vector2.down,filter,hits,bounds.extents.y+5);
                        if(floors==0) continue;
                        float y=hits[0].point.y-(pc.bounds.min.y-player.transform.position.y)+.01f;
                        foreach(float rise in new[]{0f,.5f,1f})
                        {
                            player.transform.position=new Vector3(x,y+rise,0);
                            Physics2D.SyncTransforms();
                            bool actual=(bool)Call(enemy,"HasAttackTarget");
                            Vector2 ray=(Vector2)pc.bounds.center-(Vector2)bounds.center;
                            bool visible=Physics2D.Raycast(bounds.center,ray.normalized,filter,hits,ray.magnitude)==0;
                            bool expected=Vector2.Distance(pc.ClosestPoint(enemy.attackPoint.position),enemy.attackPoint.position)<=enemy.attackRange &&
                                (pc.bounds.center.x-bounds.center.x)*side>=-.05f && visible;
                            cases++;
                            if(actual!=expected) { failures++; Debug.LogError($"[EnemySceneAudit] ATTACK MISMATCH {scene}/{enemy.name} side={side} dx={dx} rise={rise}"); }
                            if(rise==0 && actual) groundHits++;
                        }
                    }
                }
                if ((bool)Get(enemy,"avoidLedges") && groundHits==0)
                { failures++; Debug.LogError($"[EnemySceneAudit] NO GROUNDED ATTACK {scene}/{enemy.name}"); }
            }
        }
        Debug.Log($"[EnemySceneAudit] TOTAL={count} attackCases={cases} failures={failures}");
        if(failures>0) throw new Exception("Scene geometry validation failed: "+failures);
    }

    private static object Get(GhostController e, string name) => typeof(GhostController).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(e);
    private static void Set(GhostController e, string name, object value) => typeof(GhostController).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(e, value);
    private static object Call(GhostController e, string name, params object[] args) => typeof(GhostController).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(e,args);
}
