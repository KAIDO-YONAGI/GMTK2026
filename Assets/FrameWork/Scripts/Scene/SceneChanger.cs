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
    [Header("初始配置")]
    [Tooltip("启动时玩家的初始位置。游戏开始时传送到这里；后续换场景则使用请求位置或场景 SO 的出生点。")]
    public Vector3 initialPosition = Vector3.zero;

    [Tooltip("玩家对象。每次场景加载完成后传送到目标出生点；请求位置为零向量时使用场景 SO 的初始位置。")]
    public GameObject player;// 用于设置每次加载后的玩家位置，主要在场景 SO 中配置。

    [Tooltip("启动时自动加载的初始场景组。通过场景加载事件按列表顺序以叠加模式加载，通常配置为菜单场景。")]
    public List<GameSceneSO> firstSceneToLoad = new List<GameSceneSO>();

    [Tooltip("随场景类型开关的对象。目标场景类型为 Menu 时全部禁用，为 Location 时全部启用，例如玩家本体。")]
    public Object[] objectsToUnableWhileGameReset;

    [Header("淡入淡出配置")]
    [Tooltip("场景过渡的淡入淡出时长（秒），以真实时间计算，不受暂停影响。")]
    public float fadeDuration = 1f;

    [Tooltip("过渡遮罩的画布组。未配置过渡动画控制器时，用它以代码改变透明度形成黑屏；淡入时会阻挡输入。")]
    public CanvasGroup fadeCanva;

    [Tooltip("过渡动画控制器数组（旧方案）。已配置时播放 FadeIn/FadeOut 动画；未配置时改用过渡遮罩的代码渐变，两者二选一。")]
    public Animator[] transitionImagesDuringFade;
    /// <summary>过渡动画播放器数组</summary>
    ///
    [Header("事件")]
    [Tooltip("场景加载请求事件。关卡触发器、按钮和重试管理器都通过它请求换场景，本组件订阅后执行卸载与加载流程。")]
    public SceneLoadEventSO loadEventSO;

    [Tooltip("场景组全部加载完成后广播的完成事件。订阅方据此执行各自复位，例如回满生命值、重置动画和检查结局显示条件。")]
    public VoidEventSO sceneLoadedEvent;

    private readonly List<GameSceneSO> currentScenes = new List<GameSceneSO>();
    /// <summary>已加载的场景对象</summary>
    private Scene loadedScene;
    /// <summary>玩家新位置</summary>
    private Vector3 newPosition;
    /// <summary>是否需要淡入淡出</summary>
    private bool isToFade;
    private bool isInitialScene = true;
    
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
        if (loadEventSO != null && firstSceneToLoad != null && firstSceneToLoad.Count > 0)
            loadEventSO.RaiseLoadRequestEvent(firstSceneToLoad, initialPosition, false);
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
    /// 播放过渡动画（协程驱动透明度渐变）。
    /// 兼容旧的 Animator 方案：若 transitionImagesDuringFade 非空，仍使用 Animator；
    /// 否则通过代码线性渐变 fadeCanva 的透明度。
    /// </summary>
    /// <param name="name">动画状态名称（FadeIn：透明度 0→1；FadeOut：透明度 1→0）。</param>
    private void PlayLoadingAnimation(string name)
    {
        // 旧 Animator 方案。
        if (transitionImagesDuringFade != null && transitionImagesDuringFade.Length > 0)
        {
            if (fadeCanva != null) fadeCanva.alpha = 1;
            foreach (Animator transitionImage in transitionImagesDuringFade)
            {
                if (transitionImage != null) transitionImage.Play(name);
            }
            return;
        }

        // 代码方案：协程驱动透明度。
        if (fadeCanva == null) return;
        float target = (name == "FadeIn") ? 1f : 0f;
        StartCoroutine(FadeRoutine(target));
    }

    /// <summary>
    /// 透明度渐变协程，持续 fadeDuration 秒（使用真实时间，不受 timeScale 影响）。
    /// </summary>
    private IEnumerator FadeRoutine(float targetAlpha)
    {
        if (fadeCanva == null) yield break;

        float startAlpha = fadeCanva.alpha;
        // 淡入开始时启用射线阻挡以拦截输入；淡出结束时关闭。
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
        // 注：原 ARPG 的 StatsManager.Instance.Respawn() 回血逻辑已移除。
        // 切场景时的回血和重置应由具体玩法在 sceneLoadedEvent 的订阅方中处理。

        this.newPosition = newPosition == Vector3.zero ? scenes[0].initialPosition : newPosition;
        // 如果传入位置为零向量，则使用场景预设的初始位置。
        this.isToFade = isToFade;
        if (isToFade && !isInitialScene)
        {
            PlayLoadingAnimation("FadeIn");
        }
        StartCoroutine(UnloadCurrentScenes(scenes));// 卸载当前场景列表。
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
