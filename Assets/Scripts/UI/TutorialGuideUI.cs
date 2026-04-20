using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 教学引导 UI
/// 单个 Image + Animator，通过状态切换显示 Q / E / Space 按键提示动画
/// 挂在 Canvas 下的引导 UI 物体上
/// </summary>
public class TutorialGuideUI : MonoBehaviour
{
    public static TutorialGuideUI Instance { get; private set; }

    public enum KeyType { Q, E, Space }

    [Header("引导动画")]
    [SerializeField] private GameObject guideRoot;    // 引导 UI 根节点
    [SerializeField] private Animator   guideAnimator;

    [Header("引导文字")]
    [SerializeField] private GameObject messageRoot;
    [SerializeField] private TextMeshProUGUI messageText;

    // Animator 状态名
    private static readonly int HashStateQ     = Animator.StringToHash("State_Q");
    private static readonly int HashStateE     = Animator.StringToHash("State_E");
    private static readonly int HashStateSpace = Animator.StringToHash("State_Space");

    private Coroutine autoHideRoutine;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        if (guideRoot) guideRoot.SetActive(false);
        if (messageRoot) messageRoot.SetActive(false);
    }

    /// <summary>显示指定按键的引导动画</summary>
    public void ShowKey(KeyType key)
    {
        if (guideRoot) guideRoot.SetActive(true);

        if (guideAnimator)
        {
            guideAnimator.enabled = true;
            guideAnimator.updateMode = AnimatorUpdateMode.UnscaledTime;

            int stateHash;
            switch (key)
            {
                case KeyType.Q:     stateHash = HashStateQ;     break;
                case KeyType.E:     stateHash = HashStateE;     break;
                case KeyType.Space: stateHash = HashStateSpace; break;
                default:            stateHash = HashStateE;     break;
            }

            guideAnimator.Play(stateHash, 0, 0f);
            guideAnimator.Update(0f);
        }
    }

    /// <summary>显示按键动画 + 说明文字</summary>
    public void ShowKeyWithMessage(KeyType key, string message)
    {
        ShowKey(key);
        ShowMessage(message);
    }

    /// <summary>显示文字提示（不自动隐藏）</summary>
    public void ShowMessage(string message)
    {
        if (autoHideRoutine != null)
        {
            StopCoroutine(autoHideRoutine);
            autoHideRoutine = null;
        }

        if (messageRoot) messageRoot.SetActive(true);
        if (messageText) messageText.text = message;
    }

    /// <summary>显示纯文字提示（指定时长后自动隐藏）</summary>
    public void ShowMessageTransient(string message, float duration = 1.2f)
    {
        ShowMessage(message);
        autoHideRoutine = StartCoroutine(AutoHideMessage(duration));
    }

    /// <summary>隐藏引导 UI</summary>
    public void HideKey()
    {
        if (guideRoot) guideRoot.SetActive(false);
    }

    /// <summary>隐藏文字提示</summary>
    public void HideMessage()
    {
        if (autoHideRoutine != null)
        {
            StopCoroutine(autoHideRoutine);
            autoHideRoutine = null;
        }
        if (messageRoot) messageRoot.SetActive(false);
    }

    /// <summary>隐藏所有引导元素</summary>
    public void HideAll()
    {
        HideKey();
        HideMessage();
    }

    private System.Collections.IEnumerator AutoHideMessage(float duration)
    {
        yield return new WaitForSeconds(duration);
        autoHideRoutine = null;
        if (messageRoot) messageRoot.SetActive(false);
    }
}
