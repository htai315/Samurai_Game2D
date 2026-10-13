// TutorialEnd.cs
using UnityEngine;

public class TutorialEnd : MonoBehaviour
{
    [SerializeField] string nextSceneName = "SampleScene";
    [SerializeField] GameObject persistentRootPrefab;

    public void FinishTutorial()
    {
        SceneTransition.TryLoad(nextSceneName, () =>
        {
            SaveRuntime.Clear();
            SpawnLocator.NextSpawnId = "A";
            PersistentRootLoader.Ensure(persistentRootPrefab);
        });
        // Do not overwrite a valid save with an uninitialized, scene-only snapshot.
        // Saving remains the responsibility of the initialized player/save menu.
    }
}
