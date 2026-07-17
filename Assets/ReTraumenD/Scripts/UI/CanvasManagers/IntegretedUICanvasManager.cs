using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class IntegratedUICanvasManager : MonoBehaviour,ICanvasManager
{
    public static IntegratedUICanvasManager instance;
    [SerializeField] private List<MyEnums.CanvasToToggle> canvasToToggle;//用枚举类来指定需要切换的画布组
    [SerializeField] private CanvasGroup UICanvasPanel;
    [SerializeField] private GameObject integratedButtonsParent;
    [SerializeField] private Button toggleMenuButton;
    [SerializeField] private Button nextPageButton;
    [SerializeField] private Button prevPageButton;
    [SerializeField] private TMP_Text pageNumText;
    [SerializeField] private ToggleCanvasEventSO toggleIntegratedCanvasEventSO;
    public ToggleCanvasEventSO ToggleCanvasEvent => toggleIntegratedCanvasEventSO;
    private List<Button> integratedButtons = new();

    private TMP_Text toggleMenuText;
    private List<TMP_Text> integratedButtonTexts = new();
    private int currentPageNum = 0;
    private int buttonsEachPage;//用来模拟初始化多页的Panel
    private bool isMenuOpen = false;
    private void Awake()
    {
        if (instance == null)
            instance = this;
        else
            Destroy(gameObject);

        integratedButtons.AddRange(integratedButtonsParent.GetComponentsInChildren<Button>());
        buttonsEachPage = integratedButtons.Count;

        InitiateUICanvasPanel(false);
    }
    private void OnEnable()
    {
        InitiateButtons();

        toggleIntegratedCanvasEventSO.toggleCanvasEvent += OnToggleIntegratedCanvas;

        //此处事件在UIManager里仅索引到editor里，没有在代码层编写
        //特别地，将开闭功能都放在当前这个脚本里
        //最终效果就是UIManger里可以依靠alpha值检测来实现互斥关闭集成面板，而这个脚本直接实现开关
    }
    private void OnDisable()
    {
        toggleIntegratedCanvasEventSO.toggleCanvasEvent -= OnToggleIntegratedCanvas;
        toggleMenuButton.onClick.RemoveAllListeners();
        nextPageButton.onClick.RemoveAllListeners();
        prevPageButton.onClick.RemoveAllListeners();
    }
    private void OnToggleIntegratedCanvas(bool state)
    {
        SetCanvaState(UICanvasPanel, state);

        isMenuOpen = state;

        toggleMenuText.text = !isMenuOpen ? "Open" : "Close";

    }

    private void InitiateButtons()
    {

        toggleMenuButton.onClick.AddListener(OnClickMenuToggleButton);
        nextPageButton.onClick.AddListener(OnClickNextButton);
        prevPageButton.onClick.AddListener(OnClickPrevutton);

        toggleMenuText = toggleMenuButton.GetComponentInChildren<TMP_Text>();
        toggleMenuText.text = "Open";
        for (int i = 0; i < buttonsEachPage; i++)
        {
            integratedButtonTexts.Add(integratedButtons[i].GetComponentInChildren<TMP_Text>());
        }
    }
    private void InitiateUICanvasPanel(bool state)
    {
        pageNumText.text = "1";
        isMenuOpen = state;
        SetCanvaState(UICanvasPanel, isMenuOpen);
        if (isMenuOpen) ShiftPage(0);
    }
    private void OnClickMenuToggleButton()
    {
        InitiateUICanvasPanel(!isMenuOpen);
        toggleMenuText.text = !isMenuOpen ? "Open" : "Close";
        // Debug.Log("toggle");
    }

    private void OnClickNextButton()
    {
        ShiftPage(currentPageNum + 1);
    }

    private void OnClickPrevutton()
    {
        ShiftPage(currentPageNum - 1);
    }
    private bool ShiftPage(int page)
    {
        int canvasNum = canvasToToggle.Count;

        if (page < 0) page = 0;

        bool hasCanvasToDisPlay = false;
        int startNum = page * buttonsEachPage;
        if (startNum >= canvasNum)
            //大于等于的原因是因为startNum刚好对应第二页第一个的序号
            //例如4>=4的时候，实际上要显示的是第五个（从零开始），list正好没有
            hasCanvasToDisPlay = false;//没有能展示的画布组了
        else
        {
            InitiatePage(startNum, canvasNum);
            hasCanvasToDisPlay = true;
        }
        if (hasCanvasToDisPlay)
            currentPageNum = page;
        return hasCanvasToDisPlay;
    }
    private void InitiatePage(int startNum, int canvasNum)
    {
        pageNumText.text = ((startNum / buttonsEachPage) + 1).ToString();
        foreach (var button in integratedButtons)
        {
            button.gameObject.SetActive(false);
            button.onClick.RemoveAllListeners();
        }

        for (int i = startNum, count = 0; i < canvasNum && count < buttonsEachPage; i++, count++)
        {
            int pageButtonNum = i % buttonsEachPage;
            MyEnums.CanvasToToggle canvasToToggle = this.canvasToToggle[i];
            Button button = integratedButtons[pageButtonNum];

            integratedButtonTexts[pageButtonNum].text = canvasToToggle.ToString();

            button.gameObject.SetActive(true);
            button.onClick.AddListener(() => OnIntegratedButtonClick(canvasToToggle));

        }
    }
    private void OnIntegratedButtonClick(MyEnums.CanvasToToggle canvasToToggle)
    {
        UIManager.instance.RequestCanvasToggle(canvasToToggle);
    }

    private void SetCanvaState(CanvasGroup canva, bool state)
    {
        canva.alpha = state ? 1 : 0;
        canva.blocksRaycasts = state;
        canva.interactable = state;
        UIManager.instance.ReportCanvasState(MyEnums.CanvasToToggle.Integrated, state);
    }
}
