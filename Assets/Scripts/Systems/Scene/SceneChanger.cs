using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 场景切换管理器
/// 负责管理场景的加载、卸载和过渡动画
/// 使用单例模式，通过事件响应场景切换请求
/// </summary>
public class SceneChanger : YSingleton<SceneChanger>
{
    /// <summary>玩家初始位置</summary>
    ///
    [Header("Init Config")]
    [Tooltip("启动时玩家的初始位置：游戏一开始把 player 传送到这里（之后换场景按请求位置/场景 SO 出生点传送）")]
    public Vector3 initialPosition = Vector3.zero;

    [Tooltip("玩家对象：每次场景加载完成后传送到目标出生点（请求传入的位置，零向量则用场景 SO 的 initialPosition）")]
    public GameObject player;//用于设置每次加载的玩家位置，主要在sceneSO里配置

    [Tooltip("启动时自动加载的初始场景组：Start 时通过 loadEventSO 按列表顺序 Additive 装入（通常填菜单场景）")]
    public List<GameSceneSO> initScene = new List<GameSceneSO>();

    [Tooltip("随场景类型开关的对象：目标场景为 Menu 时全部禁用、Location 时全部启用（例如玩家本体）")]
    public Object[] objectsToUnableWhileGameReset;

    [Header("Fade Config")]
    [Tooltip("场景过渡的淡入淡出时长（秒，用真实时间计算，不受 timeScale 暂停影响）")]
    public float fadeDuration = 1f;

    [Tooltip("过渡遮罩的 CanvasGroup：transitionImagesDuringFade 为空时用它做代码 alpha 渐变黑屏（淡入时挡输入）")]
    public CanvasGroup fadeCanva;

    [Tooltip("过渡动画 Animator 数组（旧方案）：非空时播放 FadeIn/FadeOut 动画；为空则用 fadeCanva 代码渐变。二选一")]
    public Animator[] transitionImagesDuringFade;
    /// <summary>过渡动画播放器数组</summary>
    ///
    [Header("Events")]
    [Tooltip("场景加载请求事件：Trigger / 按钮 / RetryManager 都通过它请求换场景，本组件订阅并执行卸载+加载流程")]
    public SceneLoadEventSO loadEventSO;

    [Tooltip("场景组全部加载完成后广播的完成事件：订阅方做各自复位（回血、动画复位、Ending 双判等）")]
    public VoidEventSO sceneLoadedEvent;

    private readonly List<GameSceneSO> currentScenes = new List<GameSceneSO>();
    /// <summary>已加载的场景对象</summary>
    private Scene loadedScene;
    /// <summary>玩家新位置</summary>
    private Vector3 newPosition;
    /// <summary>是否需要淡入淡出</summary>
    private bool isToFade;
    private bool isInitialScene = true;

    /// <summary>
    /// 获取当前活动场景
    /// </summary>
    /// <returns>当前场景对象</returns>
    public Scene GetCurrentScene()
    {
        return loadedScene != null ? loadedScene : SceneManager.GetActiveScene();
    }
    // 供存档系统在切场前读取当前场景 SO。
    public GameSceneSO GetCurrentGameScene()
    {
        return currentScenes.Count > 0 ? currentScenes[0] : null;
    }

    /// <summary>
    /// 当前已加载的场景组副本（RetryManager 整组重载用；
    /// 返回副本是因为本类加载流程中会 Clear 原列表，不能把原引用传回去）。
    /// </summary>
    public List<GameSceneSO> GetCurrentScenes()
    {
        return new List<GameSceneSO>(currentScenes);
    }

    /// <summary>
    /// 启动时通过统一的场景切换事件加载初始场景列表
    /// </summary>
    private void Start()
    {
        SetPlayerPostion(initialPosition);
        if (loadEventSO != null && initScene != null && initScene.Count > 0)
            loadEventSO.RaiseLoadRequestEvent(initScene, initialPosition, false);
    }

    /// <summary>
    /// 启用时订阅场景加载事件
    /// </summary>
    private void OnEnable()
    {
        if (loadEventSO != null)
            loadEventSO.LoadRequestEvent += OnLoadRequestEvent;
    }

    /// <summary>
    /// 禁用时取消订阅场景加载事件
    /// </summary>
    private void OnDisable()
    {
        if (loadEventSO != null)
            loadEventSO.LoadRequestEvent -= OnLoadRequestEvent;
    }

    /// <summary>
    /// 播放过渡动画（协程驱动 alpha 渐变）
    /// 兼容旧的 Animator 方案：若 transitionImagesDuringFade 非空，仍走 Animator；
    /// 否则用代码对 fadeCanva.alpha 做线性渐变。
    /// </summary>
    /// <param name="name">动画状态名称（FadeIn: alpha 0→1; FadeOut: alpha 1→0）</param>
    private void PlayLoadingAnimation(string name)
    {
        // 旧 Animator 方案
        if (transitionImagesDuringFade != null && transitionImagesDuringFade.Length > 0)
        {
            if (fadeCanva != null) fadeCanva.alpha = 1;
            foreach (Animator transitionImage in transitionImagesDuringFade)
            {
                if (transitionImage != null) transitionImage.Play(name);
            }
            return;
        }

        // 代码方案：协程驱动 alpha
        if (fadeCanva == null) return;
        float target = (name == "FadeIn") ? 1f : 0f;
        StartCoroutine(FadeRoutine(target));
    }

    /// <summary>
    /// alpha 渐变协程，持续 fadeDuration 秒（用真实时间，不受 timeScale 影响）
    /// </summary>
    private IEnumerator FadeRoutine(float targetAlpha)
    {
        if (fadeCanva == null) yield break;

        float startAlpha = fadeCanva.alpha;
        // 淡入开始时启用 blocksRaycasts 挡住输入；淡出结束时关闭
        fadeCanva.blocksRaycasts = true;

        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            fadeCanva.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / fadeDuration);
            yield return null;
        }
        fadeCanva.alpha = targetAlpha;

        // 淡出结束后关闭射线阻挡，避免遮挡 UI
        if (targetAlpha == 0f)
            fadeCanva.blocksRaycasts = false;
    }

    /// <summary>
    /// 设置玩家位置
    /// </summary>
    /// <param name="newPosition">新位置坐标</param>
    private void SetPlayerPostion(Vector3 newPosition)
    {
        if (player == null) return;
        player.GetComponent<Transform>().position = newPosition;
    }

    /// <summary>
    /// 场景加载请求事件回调
    /// </summary>
    /// <param name="scenes">目标场景列表</param>
    /// <param name="newPosition">玩家新位置</param>
    /// <param name="isToFade">是否显示过渡动画</param>
    private void OnLoadRequestEvent(List<GameSceneSO> scenes, Vector3 newPosition, bool isToFade)
    {
        if (scenes == null || scenes.Count == 0)
            return;

        ForbidInput();
        if (TimeManager.Instance != null)
            TimeManager.Instance.PauseGame();
        // 注：原 ARPG 的 StatsManager.Instance.Respawn() 回血逻辑已移除，
        // 切场景时的回血/重置应由具体玩法在 sceneLoadedEvent 订阅方里处理。

        this.newPosition = newPosition == Vector3.zero ? scenes[0].initialPosition : newPosition;
        //如果传入位置为零向量，则使用场景预设的初始位置
        this.isToFade = isToFade;
        if (isToFade && !isInitialScene)
        {
            PlayLoadingAnimation("FadeIn");
        }
        StartCoroutine(UnloadCurrentScenes(scenes));//卸载当前场景列表
    }

    /// <summary>
    /// 卸载当前场景列表并加载目标场景列表
    /// </summary>
    /// <param name="scenes">要加载的场景列表</param>
    private IEnumerator UnloadCurrentScenes(List<GameSceneSO> scenes)
    {
        if (isToFade && !isInitialScene)
            yield return new WaitForSecondsRealtime(fadeDuration);

        for (int i = currentScenes.Count - 1; i >= 0; i--)
        {
            GameSceneSO scene = currentScenes[i];
            if (scene == null || string.IsNullOrEmpty(scene.sceneName))
                continue;

            if (PersistentSceneRegistry.IsPersistent(scene))
            {
                Debug.LogWarning(
                    $"[SceneChanger] Skipped unload of registered persistent scene '{scene.sceneName}'.");
                continue;
            }

            Scene loaded = SceneManager.GetSceneByName(scene.sceneName);
            if (loaded.IsValid() && loaded.isLoaded)
                yield return SceneManager.UnloadSceneAsync(scene.sceneName);
        }

        currentScenes.Clear();
        yield return LoadScenesRoutine(scenes);
    }

    /// <summary>
    /// 按列表顺序加载场景，全部完成后调用 OnLoadCompleted
    /// </summary>
    private IEnumerator LoadScenesRoutine(List<GameSceneSO> scenes)
    {
        foreach (GameSceneSO scene in scenes)
        {
            if (scene == null || string.IsNullOrEmpty(scene.sceneName))
                continue;

            if (scene.sceneType == MyEnums.SceneType.Menu)
                SetObjects(false);
            else if (scene.sceneType == MyEnums.SceneType.Location)
                SetObjects(true);

            AsyncOperation loadOperation = SceneManager.LoadSceneAsync(scene.sceneName, LoadSceneMode.Additive);
            while (loadOperation != null && !loadOperation.isDone)
                yield return null;

            currentScenes.Add(scene);
        }

        SetPlayerPostion(newPosition);
        OnLoadCompleted();
    }

    private void SetObjects(bool state)
    {
        if (objectsToUnableWhileGameReset == null)
            return;

        foreach (Object obj in objectsToUnableWhileGameReset)
        {
            if (obj is GameObject go)
            {
                go.SetActive(state);
            }
        }
    }

    /// <summary>
    /// 场景加载完成回调
    /// 更新当前场景引用，播放淡出动画
    /// </summary>
    private void OnLoadCompleted()
    {
        GameSceneSO firstScene = GetCurrentGameScene();
        loadedScene = firstScene != null
            ? SceneManager.GetSceneByName(firstScene.sceneName)
            : SceneManager.GetActiveScene();
        if (isToFade && !isInitialScene)
        {
            PlayLoadingAnimation("FadeOut");
        }
        isInitialScene = false;
        sceneLoadedEvent?.OnEventRaised();
        AllowInput();
        if (TimeManager.Instance != null)
            TimeManager.Instance.ForceResumeGame();
    }
    private void ForbidInput()
    {
        // player.GetComponent<PlayerMovement>().enabled = false;
    }
    private void AllowInput()
    {
        // player.GetComponent<PlayerMovement>().enabled = true;
    }
}
