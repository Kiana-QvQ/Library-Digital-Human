using UnityEngine;
using UnityEngine.UI;

public class Look2 : MonoBehaviour
{
    [Header("Eye References")]
    public Transform eye; // 合并为一个眼睛对象

    [Header("Camera Settings")]
    public Camera mainCamera;
    public RawImage displayImage;
    public AspectRatioFitter aspectRatioFitter;
    public bool mirrorVideo = true;

    [Header("Rotation Limits")]
    public float minXRotation = -1.5f; // X轴旋转范围
    public float maxXRotation = 1.5f;
    public float minYRotation = -7f;   // Y轴旋转范围
    public float maxYRotation = 7f;

    [Header("Tracking Settings")]
    public float trackingSpeed = 5f;
    public bool useSmoothing = true;
    public float smoothingFactor = 0.2f;
    public bool trackMouse = true;    // 是否跟踪鼠标
    public bool trackTouch = true;    // 是否跟踪触摸

    private Vector3 targetEyeRotation;
    private Vector3 currentEyeRotation;
    private Vector3 targetPosition;   // 目标位置（归一化坐标）
    private bool isTracking = false;

    void Start()
    {
        if (eye == null)
        {
            Debug.LogError("Eye transform not assigned!");
            return;
        }

        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        // 设置眼睛的初始旋转位置为(0,0,0)
        eye.localRotation = Quaternion.Euler(0f, 0f, 0f);
        targetPosition = new Vector3(0.5f, 0.5f, 0f); // 默认居中
        isTracking = true;
    }

    void Update()
    {
        if (!isTracking) return;

        // 处理鼠标输入
        if (trackMouse && Input.GetMouseButton(0))
        {
            Vector3 mousePos = Input.mousePosition;
            targetPosition = new Vector3(
                mousePos.x / Screen.width,
                mousePos.y / Screen.height,
                0f
            );
        }

        // 处理触摸输入
        if (trackTouch && Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            targetPosition = new Vector3(
                touch.position.x / Screen.width,
                touch.position.y / Screen.height,
                0f
            );
        }

        // 计算眼睛旋转角度
        CalculateEyeRotation();

        // 应用平滑和旋转
        if (useSmoothing)
        {
            currentEyeRotation = Vector3.Lerp(currentEyeRotation, targetEyeRotation, smoothingFactor);
        }
        else
        {
            currentEyeRotation = targetEyeRotation;
        }

        eye.localRotation = Quaternion.Slerp(eye.localRotation,
                                             Quaternion.Euler(currentEyeRotation),
                                             Time.deltaTime * trackingSpeed);
    }

    void CalculateEyeRotation()
    {
        // 将归一化坐标(0-1)转换为旋转角度
        float xOffset = targetPosition.x * 2f - 1f; // 转换到-1到1范围
        float yOffset = targetPosition.y * 2f - 1f; // 转换到-1到1范围

        // 限制偏移范围
        xOffset = Mathf.Clamp(xOffset, -1f, 1f);
        yOffset = Mathf.Clamp(yOffset, -1f, 1f);

        // 计算最终旋转角度
        float xRotation = Mathf.Lerp(minXRotation, maxXRotation, (xOffset + 1f) * 0.5f);
        float yRotation = Mathf.Lerp(minYRotation, maxYRotation, (yOffset + 1f) * 0.5f);

        // 设置眼睛的目标旋转
        targetEyeRotation = new Vector3(xRotation, yRotation, 0f);
    }
}