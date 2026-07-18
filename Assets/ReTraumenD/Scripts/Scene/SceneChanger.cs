using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

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
    public GameSceneSO initScene;
    public GameObject player;
    public CanvasGroup fadeCanva;
    /// <summary>过渡动画播放器数组</summary>
    ///     
    [Header("Events")]
    public SceneLoadEventSO loadEventSO;
    public VoidEventSO sceneLoadedEvent;
    public Animator[] transitionImagesDuringFade;
    public Object[] objectsToUnableWhileGameReset;
    private GameSceneSO sceneToLoad;

    private GameSceneSO currentScene;
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
        return currentScene;
    }
    /// <summary>
    /// 唤醒时初始化单例并加载首个场景
    /// </summary>
    private void Awake()
    {
        instance = this;
        sceneToLoad = initScene;
        SetPlayerPostion(initialPosition);
        LoadScene(sceneToLoad);
    }

    /// <summary>
    /// 启用时订阅场景加载事件
    /// </summary>
    private void OnEnable()
    {
        loadEventSO.LoadRequestEvent += OnLoadRequestEvent;
    }

    /// <summary>
    /// 禁用时取消订阅场景加载事件
    /// </summary>
    private void OnDisable()
    {
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
    /// <param name="scene">目标场景</param>
    /// <param name="newPosition">玩家新位置</param>
    /// <param name="isToFade">是否显示过渡动画</param>
    private void OnLoadRequestEvent(GameSceneSO scene, Vector3 newPosition, bool isToFade)
    {
        ForbidInput();
        TimeManager.instance.PauseGame();
        sceneToLoad = scene;

        // 注：原 ARPG 的 StatsManager.instance.Respawn() 回血逻辑已移除，
        // 切场景时的回血/重置应由具体玩法在 sceneLoadedEvent 订阅方里处理。

        this.newPosition = newPosition == Vector3.zero ? sceneToLoad.initialPosition : newPosition;
        //如果传入位置为零向量，则使用场景预设的初始位置
        this.isToFade = isToFade;
        if (isToFade && !isInitialScene)
        {
            PlayLoadingAnimation("FadeIn");
        }
        StartCoroutine(UnloadCurrentScene(sceneToLoad));//卸载当前场景
    }

    /// <summary>
    /// 卸载当前场景协程
    /// 等待淡入动画完成后卸载旧场景，然后加载新场景
    /// </summary>
    /// <param name="sceneToLoad">要加载的目标场景</param>
    private IEnumerator UnloadCurrentScene(GameSceneSO sceneToLoad)
    {

        yield return new WaitForSecondsRealtime(fadeDuration);

        if (currentScene != null && !string.IsNullOrEmpty(currentScene.sceneName))
            yield return SceneManager.UnloadSceneAsync(currentScene.sceneName);
        LoadScene(sceneToLoad);
        SetPlayerPostion(newPosition);

    }

    /// <summary>
    /// 异步加载场景
    /// 使用 SceneManager 以 additive 模式加载场景
    /// </summary>
    /// <param name="sceneToLoad">要加载的场景</param>
    private void LoadScene(GameSceneSO sceneToLoad)
    {
        if (sceneToLoad.sceneType == MyEnums.SceneType.Menu)
        {
            SetObjects(false);
        }

        else if (sceneToLoad.sceneType == MyEnums.SceneType.Location)
        {
            SetObjects(true);
        }
        if (sceneToLoad != null && !string.IsNullOrEmpty(sceneToLoad.sceneName))
        {
            StartCoroutine(LoadSceneRoutine(sceneToLoad));
        }
    }

    /// <summary>
    /// 场景加载协程：等待异步加载完成后调用 OnLoadCompleted
    /// </summary>
    private IEnumerator LoadSceneRoutine(GameSceneSO sceneToLoad)
    {
        var op = SceneManager.LoadSceneAsync(sceneToLoad.sceneName, LoadSceneMode.Additive);
        while (op != null && !op.isDone)
            yield return null;
        OnLoadCompleted();
    }

    private void SetObjects(bool state)
    {
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
        currentScene = sceneToLoad;
        loadedScene = SceneManager.GetSceneByName(sceneToLoad.sceneName);
        if (isToFade && !isInitialScene)
        {
            PlayLoadingAnimation("FadeOut");
        }
        isInitialScene = false;
        sceneLoadedEvent?.OnEventRaised();
        AllowInput();
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
