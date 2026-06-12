/*using UnityEngine;
using System.Collections;
using OpenCVForUnity.CoreModule;
using OpenCVForUnity.ObjdetectModule;
using OpenCVForUnity.VideoioModule;
using OpenCVForUnity.ImgprocModule;
using OpenCVForUnity.UnityUtils;
using UnityEngine.UI;

public class EyesTrackingController : MonoBehaviour
{
   [Header("Eyes References")]
   public Transform leftEyes;
   public Transform rightEyes;

   [Header("Camera Settings")]
   public RawImage displayImage;
   public AspectRatioFitter aspectRatioFitter;
   public bool mirrorVideo = true;

   [Header("Rotation Limits")]
   public float minXRotation = -15f;
   public float maxXRotation = 15f;
   public float minYRotation = -10f;
   public float maxYRotation = 10f;

   [Header("Tracking Settings")]
   public float trackingSpeed = 5f;
   public bool useSmoothing = true;
   public float smoothingFactor = 0.2f;
   public float faceDetectionScale = 1.1f;
   public int faceDetectionMinNeighbors = 3;

   private VideoCapture videoCapture;
   private CascadeClassifier faceCascade;
   private Mat frame;
   private Texture2D texture;
   private Vector3 targetLeftEyesRotation;
   private Vector3 targetRightEyesRotation;
   private Vector3 currentLeftEyesRotation;
   private Vector3 currentRightEyesRotation;
   private bool isTracking = false;
   private bool isInitialized = false;
   private Point previousFaceCenter;

   void Start()
   {
       if (leftEyes == null || rightEyes == null)
       {
           Debug.LogError("Eyes transforms not assigned!");
           return;
       }

       InitializeOpenCV();
   }

   void InitializeOpenCV()
   {
       try
       {
           videoCapture = new VideoCapture();
           if (!videoCapture.open(0)) // 打开默认摄像头
           {
               Debug.LogError("Failed to open camera!");
               return;
           }

           // 获取摄像头帧尺寸
           int width = (int)videoCapture.get(Videoio.CAP_PROP_FRAME_WIDTH);
           int height = (int)videoCapture.get(Videoio.CAP_PROP_FRAME_HEIGHT);
           Debug.Log($"Camera resolution: {width}x{height}");

           frame = new Mat(height, width, CvType.CV_8UC3);
           texture = new Texture2D(width, height, TextureFormat.RGBA32, false);

           // 初始化Haar级联分类器
           faceCascade = new CascadeClassifier();

           if (!faceCascade.load(Application.dataPath + "/Arsts/Coding/OpenCV/Resources/haarcascade_frontalface_default.xml"))
           {
               Debug.LogError("Failed to load face cascade classifier!");
               return;
           }

           isInitialized = true;
           isTracking = true;

           // 设置UI显示
           if (displayImage != null)
           {
               displayImage.texture = texture;
               if (aspectRatioFitter != null)
               {
                   aspectRatioFitter.aspectRatio = (float)width / height;
               }
           }

           StartCoroutine(TrackEyess());
       }
       catch (System.Exception e)
       {
           Debug.LogError("OpenCV initialization error: " + e.Message);
       }
   }

   IEnumerator TrackEyess()
   {
       WaitForEndOfFrame wait = new WaitForEndOfFrame();

       while (isTracking)
       {
           if (videoCapture.grab() && videoCapture.retrieve(frame))
           {
               if (frame != null && frame.width() > 0 && frame.height() > 0)
               {
                   ProcessFrame();

                   // 更新显示
                   if (displayImage != null)
                   {
                       Utils.matToTexture2D(frame, texture);
                   }
               }
           }

           yield return wait;
       }
   }

   void ProcessFrame()
   {
       try
       {
           Mat grayFrame = new Mat();
           Imgproc.cvtColor(frame, grayFrame, Imgproc.COLOR_BGR2GRAY);
           Imgproc.equalizeHist(grayFrame, grayFrame); // 直方图均衡化提高对比度

           // 检测人脸
           MatOfRect faces = new MatOfRect();
           faceCascade.detectMultiScale(
               grayFrame,
               faces,
               faceDetectionScale,
               faceDetectionMinNeighbors,
               0,
               new Size(30, 30),
               new Size());

           OpenCVForUnity.CoreModule.Rect[] facesArray = faces.toArray();

           // 处理检测到的人脸
           foreach (OpenCVForUnity.CoreModule.Rect face in facesArray)
           {
               Imgproc.rectangle(frame, new Point(face.x, face.y),
                                 new Point(face.x + face.width, face.y + face.height),
                                 new Scalar(255, 0, 0), 2);

               // 计算人脸中心点
               Point faceCenter = new Point(face.x + face.width / 2, face.y + face.height / 2);

               if (previousFaceCenter != null)
               {
                   CalculateEyesRotationsBasedOnFaceMovement(faceCenter);
               }

               previousFaceCenter = faceCenter;
           }

           grayFrame.Dispose();
       }
       catch (System.Exception e)
       {
           Debug.LogError("Error processing frame: " + e.Message);
       }
   }

   void CalculateEyesRotationsBasedOnFaceMovement(Point currentFaceCenter)
   {
       float xOffset = (float)(currentFaceCenter.x - previousFaceCenter.x) / (float)frame.width();
       float yOffset = (float)(currentFaceCenter.y - previousFaceCenter.y) / (float)frame.height();

       // 限制偏移范围
       xOffset = Mathf.Clamp(xOffset, -1f, 1f);
       yOffset = Mathf.Clamp(yOffset, -1f, 1f);

       // 转换为旋转角度
       float xRotation = Mathf.Lerp(minXRotation, maxXRotation, xOffset * 0.5f + 0.5f);
       float yRotation = Mathf.Lerp(minYRotation, maxYRotation, yOffset * 0.5f + 0.5f);

       // 设置目标旋转
       targetLeftEyesRotation = new Vector3(yRotation, -xRotation, 0);
       targetRightEyesRotation = new Vector3(yRotation, xRotation, 0);
   }

   void Update()
   {
       if (!isInitialized || !isTracking) return;

       // 应用眼睛旋转
       if (useSmoothing)
       {
           currentLeftEyesRotation = Vector3.Lerp(currentLeftEyesRotation, targetLeftEyesRotation, smoothingFactor);
           currentRightEyesRotation = Vector3.Lerp(currentRightEyesRotation, targetRightEyesRotation, smoothingFactor);
       }
       else
       {
           currentLeftEyesRotation = targetLeftEyesRotation;
           currentRightEyesRotation = targetRightEyesRotation;
       }

       // 应用旋转
       leftEyes.localRotation = Quaternion.Slerp(leftEyes.localRotation,
                                                Quaternion.Euler(currentLeftEyesRotation),
                                                Time.deltaTime * trackingSpeed);
       rightEyes.localRotation = Quaternion.Slerp(rightEyes.localRotation,
                                                 Quaternion.Euler(currentRightEyesRotation),
                                                 Time.deltaTime * trackingSpeed);
   }

   void OnDestroy()
   {
       isTracking = false;

       if (videoCapture != null)
       {
           videoCapture.release();
           videoCapture.Dispose();
       }

       if (frame != null)
       {
           frame.Dispose();
       }

       if (faceCascade != null)
       {
           faceCascade.Dispose();
       }
   }
}
*/


using UnityEngine;
using System.Collections;
using OpenCVForUnity.CoreModule;
using OpenCVForUnity.ObjdetectModule;
using OpenCVForUnity.VideoioModule;
using OpenCVForUnity.ImgprocModule;
using OpenCVForUnity.UnityUtils;
using UnityEngine.UI;

public class EyesTrackingController : MonoBehaviour
{
   [Header("Eyes References")]
   public Transform eyes; //合并为一个眼睛对象

   [Header("Camera Settings")]
   public RawImage displayImage;
   public AspectRatioFitter aspectRatioFitter;
   public bool mirrorVideo = true;

   [Header("Rotation Limits")]
   public float minXRotation = -1.5f; // 设置x旋转最小值
   public float maxXRotation = 1.5f;  // 设置x旋转最大值
   public float minYRotation = -7f;   // 设置y旋转最小值
   public float maxYRotation = 7f;    // 设置y旋转最大值

   [Header("Tracking Settings")]
   public float trackingSpeed = 5f;
   public bool useSmoothing = true;
   public float smoothingFactor = 0.2f;
   public float faceDetectionScale = 1.1f;
   public int faceDetectionMinNeighbors = 3;

   private VideoCapture videoCapture;
   private CascadeClassifier faceCascade;
   private Mat frame;
   private Texture2D texture;
   private Vector3 targetEyesRotation;
   private Vector3 currentEyesRotation;
   private bool isTracking = false;
   private bool isInitialized = false;
   private Point previousFaceCenter;

   void Start()
   {
       if (eyes == null)
       {
           Debug.LogError("Eyes transform not assigned!");
           return;
       }

       // 设置眼睛的初始旋转位置为(0,0,0)
       eyes.localRotation = Quaternion.Euler(0f, 0f, 0f);

       InitializeOpenCV();
   }

   void InitializeOpenCV()
   {
       try
       {
           videoCapture = new VideoCapture();
           if (!videoCapture.open(0))
           {
               Debug.LogError("Failed to open camera!");
               return;
           }

           int width = (int)videoCapture.get(Videoio.CAP_PROP_FRAME_WIDTH);
           int height = (int)videoCapture.get(Videoio.CAP_PROP_FRAME_HEIGHT);
           Debug.Log($"Camera resolution: {width}x{height}");

           frame = new Mat(height, width, CvType.CV_8UC3);
           texture = new Texture2D(width, height, TextureFormat.RGBA32, false);

           faceCascade = new CascadeClassifier();

           if (!faceCascade.load(Application.dataPath + "/Arsts/Coding/OpenCV/Resources/haarcascade_frontalface_default.xml"))
           {
               Debug.LogError("Failed to load face cascade classifier!");
               return;
           }

           isInitialized = true;
           isTracking = true;

           if (displayImage != null)
           {
               displayImage.texture = texture;
               if (aspectRatioFitter != null)
               {
                   aspectRatioFitter.aspectRatio = (float)width / height;
               }
           }

           StartCoroutine(TrackEyess());
       }
       catch (System.Exception e)
       {
           Debug.LogError("OpenCV initialization error: " + e.Message);
       }
   }

   IEnumerator TrackEyess()
   {
       WaitForEndOfFrame wait = new WaitForEndOfFrame();

       while (isTracking)
       {
           if (videoCapture.grab() && videoCapture.retrieve(frame))
           {
               if (frame != null && frame.width() > 0 && frame.height() > 0)
               {
                   ProcessFrame();

                   if (displayImage != null)
                   {
                       Utils.matToTexture2D(frame, texture);
                   }
               }
           }

           yield return wait;
       }
   }

   void ProcessFrame()
   {
       try
       {
           Mat grayFrame = new Mat();
           Imgproc.cvtColor(frame, grayFrame, Imgproc.COLOR_BGR2GRAY);
           Imgproc.equalizeHist(grayFrame, grayFrame);

           MatOfRect faces = new MatOfRect();
           faceCascade.detectMultiScale(
               grayFrame,
               faces,
               faceDetectionScale,
               faceDetectionMinNeighbors,
               0,
               new Size(30, 30),
               new Size());

           OpenCVForUnity.CoreModule.Rect[] facesArray = faces.toArray();

           foreach (OpenCVForUnity.CoreModule.Rect face in facesArray)
           {
               Imgproc.rectangle(frame, new Point(face.x, face.y),
                                 new Point(face.x + face.width, face.y + face.height),
                                 new Scalar(255, 0, 0), 2);

               Point faceCenter = new Point(face.x + face.width / 2, face.y + face.height / 2);

               if (previousFaceCenter != null)
               {
                   CalculateEyesRotationsBasedOnFaceMovement(faceCenter);
               }

               previousFaceCenter = faceCenter;
           }

           grayFrame.Dispose();
       }
       catch (System.Exception e)
       {
           Debug.LogError("Error processing frame: " + e.Message);
       }
   }

   void CalculateEyesRotationsBasedOnFaceMovement(Point currentFaceCenter)
   {
       if (previousFaceCenter == null)
       {
           // 首次检测，不计算旋转
           previousFaceCenter = currentFaceCenter;
           targetEyesRotation = Vector3.zero;
           return;
       }

       float xOffset = (float)(currentFaceCenter.x - previousFaceCenter.x) / (float)frame.width();
       float yOffset = (float)(currentFaceCenter.y - previousFaceCenter.y) / (float)frame.height();

       // 限制偏移范围，避免剧烈跳动
       xOffset = Mathf.Clamp(xOffset, -0.1f, 0.1f);  // 减小偏移范围
       yOffset = Mathf.Clamp(yOffset, -0.1f, 0.1f);

       // 使用更平滑的映射函数
       float normalizedX = xOffset * 10f + 0.5f;  // 映射到 0-1 范围
       float normalizedY = yOffset * 10f + 0.5f;

       // 计算眼睛旋转角度
       float xRotation = Mathf.Lerp(minXRotation, maxXRotation, normalizedX);
       float yRotation = Mathf.Lerp(minYRotation, maxYRotation, normalizedY);

       // 设置眼睛的目标旋转
       targetEyesRotation = new Vector3(xRotation, yRotation, 0f);

       // 更新上一帧的人脸中心
       previousFaceCenter = currentFaceCenter;
   }

   void Update()
   {
       if (!isInitialized || !isTracking) return;

       if (useSmoothing)
       {
           // 使用更大的平滑因子，减少抖动
           float adjustedSmoothing = smoothingFactor * 0.5f;  // 进一步降低响应速度
           currentEyesRotation = Vector3.Lerp(currentEyesRotation, targetEyesRotation, adjustedSmoothing);
       }
       else
       {
           currentEyesRotation = targetEyesRotation;
       }
       // 限制最终旋转值，避免眼睛转到不自然的角度
       Vector3 clampedRotation = new Vector3(
           Mathf.Clamp(currentEyesRotation.x, -30f, 30f),
           Mathf.Clamp(currentEyesRotation.y, -30f, 30f),
           0f
       );
       // 使用更慢的插值速度
       eyes.localRotation = Quaternion.Slerp(eyes.localRotation,
                                            Quaternion.Euler(clampedRotation),
                                            Time.deltaTime * trackingSpeed * 0.5f);  // 降低插值速度
   }

   /// <summary>
   /// 获取当前摄像头帧（供 VisionClient 使用）
   /// </summary>
   public Mat GetCurrentFrame()
   {
       if (frame != null && !frame.empty() && isInitialized)
       {
           // 返回当前帧的副本，避免外部修改影响内部逻辑
           Mat frameCopy = new Mat();
           frame.copyTo(frameCopy);
           return frameCopy;
       }
       return null;
   }
   
   /// <summary>
   /// 检查摄像头是否已初始化
   /// </summary>
   public bool IsInitialized()
   {
       return isInitialized;
   }

   void OnDestroy()
   {
       isTracking = false;

       if (videoCapture != null)
       {
           videoCapture.release();
           videoCapture.Dispose();
       }

       if (frame != null)
       {
           frame.Dispose();
       }

       if (faceCascade != null)
       {
           faceCascade.Dispose();
       }
   }
}
