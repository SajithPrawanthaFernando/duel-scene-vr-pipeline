using System.Collections;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

public enum EraScene { Modern, Historical }

public class BootstrapLoader : MonoBehaviour
{
    private static BootstrapLoader instance;

    [Header("Scene References (Addressables)")]
    public AssetReference modernSceneRef;
    public AssetReference historicalSceneRef;

    [Header("Startup")]
    public EraScene startEra = EraScene.Modern;

    [Header("Geo Sync")]
    public Transform geoAnchor;
    public string modernRootName = "ModernRoot";
    public string historicalRootName = "HistoricalRoot";

    [Header("Player (Freeze during load)")]
    public Transform xrOrigin;                 
    public CharacterController characterCtrl;  
    public string spawnPointName = "SpawnPoint"; 

    private AsyncOperationHandle<SceneInstance>? modernHandle;
    private AsyncOperationHandle<SceneInstance>? historicalHandle;

    public EraScene? CurrentEra { get; private set; } = null;
    public bool IsBusy { get; private set; }

    private Scene bootstrapScene;

    private Vector3 modernBasePos;
    private Quaternion modernBaseRot;
    private bool modernBaseCached;

    private Vector3 historicalBasePos;
    private Quaternion historicalBaseRot;
    private bool historicalBaseCached;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;

        bootstrapScene = SceneManager.GetActiveScene();
        DontDestroyOnLoad(gameObject);
    }

    private IEnumerator Start()
    {
        // Freeze so we don't fall before ground exists
        SetPlayerFrozen(true);

        yield return LoadEra(startEra);

        // one settle frame after scene load + alignment
        yield return null;

        SetPlayerFrozen(false);
    }

    public IEnumerator SwitchTo(EraScene targetEra)
    {
        if (IsBusy) yield break;
        if (CurrentEra.HasValue && targetEra == CurrentEra.Value) yield break;

        IsBusy = true;
        SetPlayerFrozen(true);

        var previous = CurrentEra;

        // Load new first
        yield return LoadEra(targetEra);

        // settle one frame so colliders exist
        yield return null;

        // Unload previous
        if (previous.HasValue)
            yield return UnloadEra(previous.Value);

        SetPlayerFrozen(false);
        IsBusy = false;
    }

    private IEnumerator LoadEra(EraScene era)
    {
        AsyncOperationHandle<SceneInstance> op;

        if (era == EraScene.Modern)
        {
            op = Addressables.LoadSceneAsync(modernSceneRef, LoadSceneMode.Additive, true);
            yield return op;
            if (op.Status != AsyncOperationStatus.Succeeded) yield break;
            modernHandle = op;
        }
        else
        {
            op = Addressables.LoadSceneAsync(historicalSceneRef, LoadSceneMode.Additive, true);
            yield return op;
            if (op.Status != AsyncOperationStatus.Succeeded) yield break;
            historicalHandle = op;
        }

        var loadedScene = op.Result.Scene;
        SetActiveSceneSafe(loadedScene);

        // Align environment to geo anchor
        var eraRoot = AlignEraRootToGeoAnchor(loadedScene, era);

        // move player to spawn point inside this era
        if (eraRoot != null)
            MovePlayerToSpawn(eraRoot);

        yield return WarmUpScene(loadedScene);

        CurrentEra = era;
    }

    private GameObject AlignEraRootToGeoAnchor(Scene loadedScene, EraScene era)
    {
        if (geoAnchor == null) return null;
        if (!loadedScene.IsValid() || !loadedScene.isLoaded) return null;

        string rootName = (era == EraScene.Modern) ? modernRootName : historicalRootName;

        foreach (var go in loadedScene.GetRootGameObjects())
        {
            if (go.name != rootName) continue;

            // Cache authored transform ONCE
            if (era == EraScene.Modern)
            {
                if (!modernBaseCached)
                {
                    modernBasePos = go.transform.position;
                    modernBaseRot = go.transform.rotation;
                    modernBaseCached = true;
                }

                go.transform.SetPositionAndRotation(
                    modernBasePos + geoAnchor.position,
                    geoAnchor.rotation * modernBaseRot
                );
            }
            else
            {
                if (!historicalBaseCached)
                {
                    historicalBasePos = go.transform.position;
                    historicalBaseRot = go.transform.rotation;
                    historicalBaseCached = true;
                }

                go.transform.SetPositionAndRotation(
                    historicalBasePos + geoAnchor.position,
                    geoAnchor.rotation * historicalBaseRot
                );
            }

            return go;
        }

        Debug.LogWarning($"Could not find '{rootName}' in scene '{loadedScene.name}'. It must be a ROOT object.");
        return null;
    }

    private void MovePlayerToSpawn(GameObject eraRoot)
    {
        if (xrOrigin == null) return;

        var spawn = eraRoot.transform.Find(spawnPointName);
        if (spawn == null) return; 

        // Temporarily disable controller to avoid collision issues when teleporting
        bool hadCtrl = characterCtrl != null && characterCtrl.enabled;
        if (characterCtrl != null) characterCtrl.enabled = false;

        xrOrigin.SetPositionAndRotation(spawn.position, spawn.rotation);

        if (characterCtrl != null) characterCtrl.enabled = hadCtrl;
    }

    private IEnumerator UnloadEra(EraScene era)
    {
        if (era == EraScene.Modern && modernHandle.HasValue && modernHandle.Value.IsValid())
        {
            yield return Addressables.UnloadSceneAsync(modernHandle.Value, true);
            modernHandle = null;
        }
        else if (era == EraScene.Historical && historicalHandle.HasValue && historicalHandle.Value.IsValid())
        {
            yield return Addressables.UnloadSceneAsync(historicalHandle.Value, true);
            historicalHandle = null;
        }

        SetActiveSceneSafe(bootstrapScene);
    }

    private void SetPlayerFrozen(bool frozen)
    {
        if (characterCtrl != null)
            characterCtrl.enabled = !frozen;
    }

    private void SetActiveSceneSafe(Scene scene)
    {
        if (!scene.IsValid()) return;
        if (!scene.isLoaded) return;
        if (scene.name == "DontDestroyOnLoad") return;
        SceneManager.SetActiveScene(scene);
    }

    private IEnumerator WarmUpScene(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
            yield break;

        var roots = scene.GetRootGameObjects();
        foreach (var root in roots)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
                _ = r.sharedMaterials;
        }

        yield return null;
        yield return null;
    }
}
