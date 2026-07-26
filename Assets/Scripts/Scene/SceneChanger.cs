using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 场景切换管理器
/// 负责管理场景的加载、卸载和过渡动画
/// 使用单例模式，通过事件响应场景切换请求
/// </summary>
public class SceneChanger : MonoBehaviour
{
    public static SceneChanger instance;

    /// <summary>玩家初始位置</summary>
    public Vector3 initialPosition = Vector3.zero;
    public float fadeDuration = 1f;
    public List<GameSceneSO> initScene = new List<GameSceneSO>();
    public GameObject player;
    public CanvasGroup fadeCanva;
    /// <summary>过渡动画播放器数组</summary>
    ///     
    [Header("Events")]
    public SceneLoadEventSO loadEventSO;
    public VoidEventSO sceneLoadedEvent;
    public Animator[] transitionImagesDuringFade;
    public Object[] objectsToUnableWhileGameReset;
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
    /// 唤醒时初始化单例
    /// </summary>
    private void Awake()
    {
        instance = this;
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

        AudioManager.Instance?.PlaySFX(MyEnums.SFXType.LoadScene);
        ForbidInput();
        if (TimeManager.instance != null)
            TimeManager.instance.PauseGame();
        // 注：原 ARPG 的 StatsManager.instance.Respawn() 回血逻辑已移除，
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
            if (scene != null && !string.IsNullOrEmpty(scene.sceneName))
            //用字符串找到一个已经加载的场景，然后异步卸载它
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
        if (TimeManager.instance != null)
            TimeManager.instance.ForceResumeGame();
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
