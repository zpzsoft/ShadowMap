/******************************************************************************
 * -- Author        :   Ryan Zheng
 * -- Company       :   Metek 
 * -- Date          :   2018-01-29
 * -- Description   :   基本ShadowMap实现
 *  1, 实现阴影(√)
 *  2, RenderTexture设置边缘Color避免阴影被拉伸现象 (Discard border in shader.)(√)
 *  3, MainCamera远平面的及时调整, 避免阴影质量太差的问题(UpdateClipPlane & UpdateDepthCamera)(√)
 *  4, 移动过程中阴影抖动的问题(TODO:)(×)
 * 
******************************************************************************/

using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEditor;

[RequireComponent(typeof(Light))]
public class ShadowMap : MonoBehaviour {
    private int m_DepthTextureWidth = 1024;
    private int m_DepthTextureHeight = 1024;
    private RenderTexture m_DepthTexture = null;
    private Camera m_DepthCamera = null;
    private Matrix4x4 m_LightVPMatrix;              //View&Projection's matrix in light space.
    private Light m_DirectionalLight = null;

    public Shader m_CaptureDepthShader = null;
    public float m_MaxSceneHeight = 10;             //场景中可能出现的物体的最高高度.
    public float m_MinSceneHeight = 0;              //场景中可能出现的额物体的最低高度.

    [HideInInspector]
    public int m_CullingMask = -1;
    [HideInInspector]
    public int m_CullingMaskSelection = 0;

    // Use this for initialization
    void Start () {
        m_DirectionalLight = GetComponent<Light>();
        m_DepthTexture = InitRenderTexture();

        //方向光的下一层挂载一个摄像机用于渲染获取目标物体的深度图.
        m_DepthCamera = InitDepthCamera(gameObject, m_DepthTexture);
    }
	
	// Update is called once per frame
	void Update () {
        Graphics.SetRenderTarget(m_DepthTexture);
        GL.Clear(true, true, Color.white);

        if (null == m_DepthCamera) return;
        if (null == m_CaptureDepthShader) return;

        UpdateClipPlane(Camera.main, m_MinSceneHeight, m_MaxSceneHeight);
        UpdateDepthCamera(Camera.main, m_DepthCamera, m_DirectionalLight);
        Prepare4Shader();

        m_DepthCamera.RenderWithShader(m_CaptureDepthShader, "RenderType");
    }

    RenderTexture InitRenderTexture()
    {
        RenderTexture rt = new RenderTexture(m_DepthTextureWidth, m_DepthTextureHeight, 0, RenderTextureFormat.Default);
        rt.antiAliasing = 8;
        rt.Create();

        return rt;
    }

    Camera InitDepthCamera(GameObject parentObj, RenderTexture rt)
    {
        GameObject depthCameraObj = new GameObject("DepthCamera");
        Camera depthCamera = depthCameraObj.AddComponent<Camera>();
        depthCameraObj.transform.SetParent(parentObj.transform, false);

        if (null == depthCamera) return null;
        depthCamera.orthographic = true;
        depthCamera.backgroundColor = Color.white;
        depthCamera.clearFlags = CameraClearFlags.Color;
        depthCamera.enabled = false;
        depthCamera.targetTexture = rt;
        depthCamera.nearClipPlane = 1.0f;
        depthCamera.farClipPlane = 10.0f;
        depthCamera.cullingMask = (int)1<< m_CullingMask;

        return depthCamera;
    }

    /// <summary>
    /// 初始化RenderTexture和矩阵信息传递到Shader中.
    /// </summary>
    void Prepare4Shader()
    {
        //从方相光角度(也就是深度摄像机)获取矩阵, 方便接受投影的地方做转换.
        Matrix4x4 world2View = m_DepthCamera.worldToCameraMatrix;
        Matrix4x4 projection = GL.GetGPUProjectionMatrix(m_DepthCamera.projectionMatrix, false);
        m_LightVPMatrix = projection * world2View;

        Shader.SetGlobalTexture("_ShadowDepthTex", m_DepthTexture);
        Shader.SetGlobalMatrix("_LightViewClipMatrix", m_LightVPMatrix);
    }

    /// <summary>
    /// 精简当前主摄像机的Clip区域, 方便后续深度摄像机也用一个最小包围盒覆盖主摄像机区域.
    /// </summary>
    void UpdateClipPlane(Camera mainCamera, float minHeight, float maxHeight)
    {
        float angle = (mainCamera.fieldOfView / 180) * Mathf.PI;
        float nDis = mainCamera.nearClipPlane;
        float fDis = mainCamera.farClipPlane;
        List<Vector4> nearClipPoints = new List<Vector4>();
        List<Vector4> farClipPoints = new List<Vector4>();

        //近平面的四个点
        nearClipPoints.Add(new Vector4(nDis * Mathf.Tan(angle / 2) * mainCamera.aspect, -nDis * Mathf.Tan(angle / 2), nDis, 1));
        nearClipPoints.Add(new Vector4(nDis * Mathf.Tan(angle / 2) * mainCamera.aspect, nDis * Mathf.Tan(angle / 2), nDis, 1));
        nearClipPoints.Add(new Vector4(-nDis * Mathf.Tan(angle / 2) * mainCamera.aspect, nDis * Mathf.Tan(angle / 2), nDis, 1));
        nearClipPoints.Add(new Vector4(-nDis * Mathf.Tan(angle / 2) * mainCamera.aspect, -nDis * Mathf.Tan(angle / 2), nDis, 1));

        //远平面的四个角.
        farClipPoints.Add(new Vector4(fDis * Mathf.Tan(angle / 2) * mainCamera.aspect, -fDis * Mathf.Tan(angle / 2), fDis, 1));
        farClipPoints.Add(new Vector4(fDis * Mathf.Tan(angle / 2) * mainCamera.aspect, fDis * Mathf.Tan(angle / 2), fDis, 1));
        farClipPoints.Add(new Vector4(-fDis * Mathf.Tan(angle / 2) * mainCamera.aspect, fDis * Mathf.Tan(angle / 2), fDis, 1));
        farClipPoints.Add(new Vector4(-fDis * Mathf.Tan(angle / 2) * mainCamera.aspect, -fDis * Mathf.Tan(angle / 2), fDis, 1));

        //转换到世界坐标系, 并获取远平面最高的点和近平面最低的点.
        Matrix4x4 local2WorldMatrix = mainCamera.transform.localToWorldMatrix;
        Vector4 nLowestPoint = local2WorldMatrix * nearClipPoints[0];
        Vector4 fHighestPoint = local2WorldMatrix * farClipPoints[0];

        //近平面的最低点.
        for (int i = 0; i < nearClipPoints.Count; i++)
        {
            Vector4 point = local2WorldMatrix * nearClipPoints[i];
            if (nLowestPoint.y > point.y) nLowestPoint = point;
        }
        nLowestPoint = mainCamera.transform.worldToLocalMatrix * nLowestPoint;
        nLowestPoint /= nLowestPoint.z;

        //远平面的最高点.
        for (int i = 0; i < farClipPoints.Count; i++)
        {
            Vector4 point = local2WorldMatrix * farClipPoints[i];
            if (fHighestPoint.y < point.y) fHighestPoint = point;
        }
        fHighestPoint = mainCamera.transform.worldToLocalMatrix * fHighestPoint;
        fHighestPoint /= fHighestPoint.z;

        float lowestDistance = (maxHeight - local2WorldMatrix.m13) / (nLowestPoint.x * local2WorldMatrix.m10 + nLowestPoint.y * local2WorldMatrix.m11 + nLowestPoint.z * local2WorldMatrix.m12);
        float highestDistance = (minHeight - local2WorldMatrix.m13) / (fHighestPoint.x * local2WorldMatrix.m10 + fHighestPoint.y * local2WorldMatrix.m11 + fHighestPoint.z * local2WorldMatrix.m12);
            
        mainCamera.nearClipPlane = lowestDistance;
        mainCamera.farClipPlane = highestDistance;
    }
    void UpdateDepthCamera(Camera mainCamera, Camera depthCamera, Light globalLight)
    {
        //获取mainCamera中远近平面八个顶点在方向光的坐标系下的上下左右坐标.
        float angle = (mainCamera.fieldOfView / 180) * Mathf.PI;
        float nDis = mainCamera.nearClipPlane;
        float fDis = mainCamera.farClipPlane;
        List<Vector4> mainClipPoints = new List<Vector4>();

        //MainCamera 的NearClip 的四个点和FarClip的四个点
        mainClipPoints.Add(new Vector4(nDis * Mathf.Tan(angle / 2) * mainCamera.aspect, -nDis * Mathf.Tan(angle / 2), nDis, 1));
        mainClipPoints.Add(new Vector4(nDis * Mathf.Tan(angle / 2) * mainCamera.aspect, nDis * Mathf.Tan(angle / 2), nDis, 1));
        mainClipPoints.Add(new Vector4(-nDis * Mathf.Tan(angle / 2) * mainCamera.aspect, nDis * Mathf.Tan(angle / 2), nDis, 1));
        mainClipPoints.Add(new Vector4(-nDis * Mathf.Tan(angle / 2) * mainCamera.aspect, -nDis * Mathf.Tan(angle / 2), nDis, 1));
        mainClipPoints.Add(new Vector4(fDis * Mathf.Tan(angle / 2) * mainCamera.aspect, -fDis * Mathf.Tan(angle / 2), fDis, 1));
        mainClipPoints.Add(new Vector4(fDis * Mathf.Tan(angle / 2) * mainCamera.aspect, fDis * Mathf.Tan(angle / 2), fDis, 1));
        mainClipPoints.Add(new Vector4(-fDis * Mathf.Tan(angle / 2) * mainCamera.aspect, fDis * Mathf.Tan(angle / 2), fDis, 1));
        mainClipPoints.Add(new Vector4(-fDis * Mathf.Tan(angle / 2) * mainCamera.aspect, -fDis * Mathf.Tan(angle / 2), fDis, 1));

        //转换到世界坐标系, 并获取远平面最高的点和近平面最低的点.
        Matrix4x4 local2WorldMatrix = mainCamera.transform.localToWorldMatrix;
        for (int i = 0; i < mainClipPoints.Count; i++)
            mainClipPoints[i] = local2WorldMatrix * mainClipPoints[i];

        //在灯光坐标系下计算orthographic摄像机的Near/Far Clip Plane.
        Vector2 xSection = new Vector2(int.MinValue, int.MaxValue);
        Vector2 ySection = new Vector2(int.MinValue, int.MaxValue);
        Vector2 zSection = new Vector2(int.MinValue, int.MaxValue);
        Matrix4x4 world2LightMatrix = globalLight.transform.worldToLocalMatrix;
        for (int i = 0; i < mainClipPoints.Count; i++)
        {
            Vector4 point = world2LightMatrix * mainClipPoints[i];
            mainClipPoints[i] = point;

            if (point.x > xSection.x)
                xSection.x = point.x;
            if (point.x < xSection.y)
                xSection.y = point.x;

            if (point.y > ySection.x)
                ySection.x = point.y;
            if (point.y < ySection.y)
                ySection.y = point.y;

            if (point.z > zSection.x)
                zSection.x = point.z;
            if (point.z < zSection.y)
                zSection.y = point.z;
        }

        depthCamera.transform.localPosition = new Vector3((xSection.x + xSection.y)/2, (ySection.x + ySection.y)/2, zSection.y);
        depthCamera.nearClipPlane = 0;
        depthCamera.farClipPlane = zSection.x - zSection.y;
        depthCamera.orthographicSize = (ySection.x - ySection.y) / 2;
        depthCamera.aspect = (xSection.x - xSection.y) / (ySection.x - ySection.y);
    }
}

[CustomEditor(typeof(ShadowMap))]
public class ShadowMapInspectorExtension : Editor
{
    static List<string> options = new List<string>();

    public override void OnInspectorGUI()
    {
        ShadowMap shadowMap = (ShadowMap)target;

        DrawDefaultInspector();

        //first init.
        //if (shadowMap.m_CullingMask < 0)
        {
            options.Clear();
            for (int i = 0; i < 32; i++)
            {
                string name = LayerMask.LayerToName(i);
                if (!string.IsNullOrEmpty(name) && !options.Contains(name))
                    options.Add(name);
            }
        }
        shadowMap.m_CullingMaskSelection = EditorGUILayout.MaskField("Culling Mask", shadowMap.m_CullingMaskSelection, options.ToArray());

        if (shadowMap.m_CullingMaskSelection >= 0)
        {
            shadowMap.m_CullingMask = 0;
            var myBitArray = new BitArray(BitConverter.GetBytes(shadowMap.m_CullingMaskSelection));

            for (int i = myBitArray.Count - 1; i >= 0; i--)
            {
                if (myBitArray[i] == true)
                    shadowMap.m_CullingMask |= LayerMask.NameToLayer(options[i]);
            }

            Debug.Log("");
        }
        else
            shadowMap.m_CullingMask = shadowMap.m_CullingMaskSelection;
    }
}
