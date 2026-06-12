using UnityEngine;

public class AutoRotateXY : MonoBehaviour
{
    [Header("旋转速度")]
    [Tooltip("X轴旋转速度（度/秒）")]
    public float rotateSpeedX = 10f;

    [Tooltip("Y轴旋转速度（度/秒）")]
    public float rotateSpeedY = 15f;

    [Header("旋转方向控制")]
    [Tooltip("是否启用X轴旋转")]
    public bool enableRotationX = true;

    [Tooltip("是否启用Y轴旋转")]
    public bool enableRotationY = true;

    [Header("旋转模式")]
    [Tooltip("使用世界坐标系旋转")]
    public bool useWorldSpace = false;

    private Transform targetTransform;

    private void Awake()
    {
        // 获取当前GameObject的Transform组件
        targetTransform = GetComponent<Transform>();

        if (targetTransform == null)
        {
            Debug.LogError("未找到Transform组件！", gameObject);
        }
    }

    private void Update()
    {
        if (targetTransform == null) return;

        // 计算旋转量
        float rotationX = enableRotationX ? rotateSpeedX * Time.deltaTime : 0;
        float rotationY = enableRotationY ? rotateSpeedY * Time.deltaTime : 0;

        // 应用旋转
        if (useWorldSpace)
        {
            targetTransform.Rotate(Vector3.right * rotationX, Space.World);
            targetTransform.Rotate(Vector3.up * rotationY, Space.World);
        }
        else
        {
            targetTransform.Rotate(Vector3.right * rotationX, Space.Self);
            targetTransform.Rotate(Vector3.up * rotationY, Space.Self);
        }
    }
}