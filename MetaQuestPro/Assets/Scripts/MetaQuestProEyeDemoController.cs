using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;

[DefaultExecutionOrder(100)]
public sealed class MetaQuestProEyeDemoController : MonoBehaviour
{
    private const float TargetDistance = 5.0f;
    private const float RayLength = 10.0f;
    private static readonly Vector3 LabelOffset = new Vector3(0.0f, 1.7f, 0.08f);

    [Header("Manual OVREyeGaze Transforms")]
    [Tooltip("GameObject with OVREyeGaze set to Left Eye. The script uses its transform forward as left gaze.")]
    [SerializeField] private Transform leftEyeGazeTransform;

    [Tooltip("GameObject with OVREyeGaze set to Right Eye. The script uses its transform forward as right gaze.")]
    [SerializeField] private Transform rightEyeGazeTransform;

    [Header("Visuals")]
    [SerializeField] private Color idleColor = new Color(0.86f, 0.86f, 0.86f, 1.0f);
    [SerializeField] private Color gazedColor = new Color(0.0f, 0.95f, 0.25f, 1.0f);
    [SerializeField] private Color wallColor = new Color(0.1f, 0.105f, 0.11f, 1.0f);

    [Header("Blink Display")]
    [SerializeField, Range(0.0f, 1.0f)] private float blinkOpenAmountThreshold = 0.25f;

    private readonly GameObject[] targets = new GameObject[3];
    private readonly Renderer[] targetRenderers = new Renderer[3];
    private readonly List<InputDevice> eyeDevices = new List<InputDevice>();
    private readonly StringBuilder infoBuilder = new StringBuilder(512);

    private LineRenderer gazeLine;
    private Text infoText;
    private Transform cameraTransform;
    private OVREyeGaze leftEyeGaze;
    private OVREyeGaze rightEyeGaze;
    private InputDevice eyeDevice;
    private bool hasEyeDevice;

    private void Awake()
    {
        ResolveCameraRig();
        ResolveEyeGazeTransforms();
        CreateBackgroundWall();
        CreateTargets();
        CreateGazeLine();
        CreateInfoLabel();
        RefreshEyeDevice();
    }

    private void LateUpdate()
    {
        ResolveCameraRig();
        ResolveEyeGazeTransforms();

        var hasGazeRay = TryGetGazeRay(out var gazeRay, out var status);
        UpdateTargetsFromGaze(hasGazeRay, gazeRay);
        UpdateGazeLine(hasGazeRay, gazeRay);
        UpdateInfoLabel(hasGazeRay, gazeRay, status);
        FaceLabelTowardCamera();
    }

    private void ResolveCameraRig()
    {
        if (cameraTransform != null)
            return;

        var mainCamera = Camera.main;
        if (mainCamera != null)
            cameraTransform = mainCamera.transform;
    }

    private void ResolveEyeGazeTransforms()
    {
        if (leftEyeGazeTransform == null)
            leftEyeGazeTransform = FindTransformByNames("Left Eye Gaze", "LeftEyeGaze", "Left OVREyeGaze", "OVREyeGaze Left");

        if (rightEyeGazeTransform == null)
            rightEyeGazeTransform = FindTransformByNames("Right Eye Gaze", "RightEyeGaze", "Right OVREyeGaze", "OVREyeGaze Right");

        if (leftEyeGaze == null && leftEyeGazeTransform != null)
            leftEyeGaze = leftEyeGazeTransform.GetComponent<OVREyeGaze>();

        if (rightEyeGaze == null && rightEyeGazeTransform != null)
            rightEyeGaze = rightEyeGazeTransform.GetComponent<OVREyeGaze>();
    }

    private static Transform FindTransformByNames(params string[] names)
    {
        for (var i = 0; i < names.Length; i++)
        {
            var found = GameObject.Find(names[i]);
            if (found != null)
                return found.transform;
        }

        return null;
    }

    private bool TryGetGazeRay(out Ray gazeRay, out string status)
    {
        return TryGetRayFromOVREyeGazeTransforms(out gazeRay, out status);
    }

    private bool TryGetRayFromOVREyeGazeTransforms(out Ray gazeRay, out string status)
    {
        gazeRay = default;

        var hasLeft = leftEyeGazeTransform != null;
        var hasRight = rightEyeGazeTransform != null;
        if (!hasLeft && !hasRight)
        {
            status = "Assign Left/Right OVREyeGaze transforms.";
            return false;
        }

        if ((leftEyeGaze == null && hasLeft) || (rightEyeGaze == null && hasRight))
        {
            status = "Add OVREyeGaze component to both gaze objects.";
            return false;
        }

        if (!IsEyeGazeUsable(leftEyeGaze) && !IsEyeGazeUsable(rightEyeGaze))
        {
            status = "OVREyeGaze not active. Check permission/confidence/Link beta setting.";
            return false;
        }

        var origin = Vector3.zero;
        var direction = Vector3.zero;
        var count = 0;

        if (hasLeft)
        {
            origin += leftEyeGazeTransform.position;
            direction += leftEyeGazeTransform.forward;
            count++;
        }

        if (hasRight)
        {
            origin += rightEyeGazeTransform.position;
            direction += rightEyeGazeTransform.forward;
            count++;
        }

        if (count == 0 || direction.sqrMagnitude <= 0.000001f)
        {
            status = "OVREyeGaze transforms exist but direction is invalid.";
            return false;
        }

        gazeRay = new Ray(origin / count, direction.normalized);
        status = "OK from OVREyeGaze transforms";
        return true;
    }

    private static bool IsEyeGazeUsable(OVREyeGaze eyeGaze)
    {
        return eyeGaze != null &&
               eyeGaze.enabled &&
               eyeGaze.EyeTrackingEnabled &&
               eyeGaze.Confidence >= eyeGaze.ConfidenceThreshold;
    }

    private void RefreshEyeDevice()
    {
        eyeDevices.Clear();
        InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.EyeTracking, eyeDevices);

        if (eyeDevices.Count == 0)
            InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.HeadMounted, eyeDevices);

        hasEyeDevice = false;
        for (var i = 0; i < eyeDevices.Count; i++)
        {
            if (eyeDevices[i].isValid && eyeDevices[i].TryGetFeatureValue(CommonUsages.eyesData, out Eyes _))
            {
                eyeDevice = eyeDevices[i];
                hasEyeDevice = true;
                break;
            }
        }
    }

    private void CreateTargets()
    {
        var positions = new[]
        {
            new Vector3(-2.25f, 1.85f, TargetDistance),
            new Vector3(0.0f, 1.85f, TargetDistance),
            new Vector3(2.25f, 1.85f, TargetDistance),
        };

        var names = new[] { "Gaze Target Left", "Gaze Target Center", "Gaze Target Right" };

        for (var i = 0; i < targets.Length; i++)
        {
            var target = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            target.name = names[i];
            target.transform.SetParent(transform, false);
            target.transform.localPosition = positions[i];
            target.transform.localScale = Vector3.one * 1.05f;

            var renderer = target.GetComponent<Renderer>();
            renderer.material = new Material(Shader.Find("Standard"));
            renderer.material.color = idleColor;

            targets[i] = target;
            targetRenderers[i] = renderer;
        }
    }

    private void CreateBackgroundWall()
    {
        var wall = GameObject.CreatePrimitive(PrimitiveType.Quad);
        wall.name = "Matte Background Wall";
        wall.transform.SetParent(transform, false);
        wall.transform.localPosition = new Vector3(0.0f, 0.9f, TargetDistance + 1.2f);
        wall.transform.localScale = new Vector3(1150.0f, 700.0f, 1.0f);

        var collider = wall.GetComponent<Collider>();
        if (collider != null)
            collider.enabled = false;

        var renderer = wall.GetComponent<Renderer>();
        renderer.material = new Material(Shader.Find("Unlit/Color"));
        renderer.material.color = wallColor;
    }

    private void CreateGazeLine()
    {
        var lineObject = new GameObject("Gaze Center Ray");
        lineObject.transform.SetParent(transform, false);

        gazeLine = lineObject.AddComponent<LineRenderer>();
        gazeLine.positionCount = 2;
        gazeLine.useWorldSpace = true;
        gazeLine.startWidth = 0.018f;
        gazeLine.endWidth = 0.018f;
        gazeLine.material = new Material(Shader.Find("Unlit/Color"));
        gazeLine.material.color = new Color(0.1f, 0.8f, 1.0f, 0.9f);
    }

    private void CreateInfoLabel()
    {
        var canvasObject = new GameObject("Quest Pro Live Data Label");
        canvasObject.transform.SetParent(transform, false);
        canvasObject.transform.localPosition = new Vector3(0.0f, 1.85f, TargetDistance) + LabelOffset;

        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 10;
        canvasObject.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 20.0f;

        var rect = canvas.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(2600.0f, 1800.0f);
        rect.localScale = Vector3.one * 0.012f;

        var textObject = new GameObject("Live Values");
        textObject.transform.SetParent(canvasObject.transform, false);
        infoText = textObject.AddComponent<Text>();
        infoText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        infoText.fontSize = 34;
        infoText.lineSpacing = 1.12f;
        infoText.alignment = TextAnchor.MiddleCenter;
        infoText.color = Color.white;
        infoText.horizontalOverflow = HorizontalWrapMode.Overflow;
        infoText.verticalOverflow = VerticalWrapMode.Overflow;

        var textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
    }

    private void UpdateTargetsFromGaze(bool hasGazeRay, Ray gazeRay)
    {
        GameObject gazedObject = null;
        if (hasGazeRay && Physics.Raycast(gazeRay, out var hit, RayLength))
            gazedObject = hit.collider.gameObject;

        for (var i = 0; i < targetRenderers.Length; i++)
        {
            var isGazed = gazedObject == targets[i];
            targetRenderers[i].material.color = isGazed ? gazedColor : idleColor;
        }
    }

    private void UpdateGazeLine(bool hasGazeRay, Ray gazeRay)
    {
        if (gazeLine == null)
            return;

        gazeLine.enabled = hasGazeRay;
        if (!hasGazeRay)
            return;

        gazeLine.SetPosition(0, gazeRay.origin);
        gazeLine.SetPosition(1, gazeRay.origin + gazeRay.direction.normalized * RayLength);
    }

    private void UpdateInfoLabel(bool hasGazeRay, Ray gazeRay, string status)
    {
        if (infoText == null)
            return;

        infoBuilder.Length = 0;
        infoBuilder.AppendLine("Quest Pro live data");
        infoBuilder.AppendLine("Gaze center ray:");

        if (hasGazeRay)
        {
            infoBuilder.AppendFormat(
                CultureInfo.InvariantCulture,
                "origin({0:0.00}, {1:0.00}, {2:0.00}) & direction({3:0.000}, {4:0.000}, {5:0.000})",
                gazeRay.origin.x,
                gazeRay.origin.y,
                gazeRay.origin.z,
                gazeRay.direction.x,
                gazeRay.direction.y,
                gazeRay.direction.z);
            infoBuilder.AppendLine();
        }
        else
        {
            infoBuilder.AppendLine(status);
        }

        AppendEyeGazeStatus();
        infoText.text = infoBuilder.ToString();
    }

    private void AppendEyeGazeStatus()
    {
        AppendEyeGazeLine("Left OVREyeGaze", leftEyeGaze);
        AppendEyeGazeLine("Right OVREyeGaze", rightEyeGaze);
    }

    private void AppendEyeGazeLine(string label, OVREyeGaze eyeGaze)
    {
        infoBuilder.Append(label).Append(": ");
        if (eyeGaze == null)
        {
            infoBuilder.AppendLine("missing component");
            return;
        }

        infoBuilder.AppendFormat(
            CultureInfo.InvariantCulture,
            "confidence={0:0.00}",
            eyeGaze.Confidence);
        infoBuilder.AppendLine();
    }

    private void AppendEyeOpenAmounts()
    {
        if (!hasEyeDevice || !eyeDevice.isValid)
            RefreshEyeDevice();

        if (!hasEyeDevice || !eyeDevice.TryGetFeatureValue(CommonUsages.eyesData, out Eyes eyes))
        {
            infoBuilder.AppendLine("Left blinking: unknown");
            infoBuilder.AppendLine("Right blinking: unknown");
            return;
        }

        AppendBlinkLine("Left blinking", eyes.TryGetLeftEyeOpenAmount(out var leftOpen), leftOpen);
        AppendBlinkLine("Right blinking", eyes.TryGetRightEyeOpenAmount(out var rightOpen), rightOpen);
    }

    private void AppendBlinkLine(string label, bool hasOpenAmount, float openAmount)
    {
        infoBuilder.Append(label).Append(": ");
        if (!hasOpenAmount)
            infoBuilder.Append("unknown");
        else
            infoBuilder.Append(openAmount < blinkOpenAmountThreshold ? "True" : "False");
        infoBuilder.AppendLine();
    }

    private void FaceLabelTowardCamera()
    {
        if (infoText == null || cameraTransform == null)
            return;

        var labelTransform = infoText.canvas.transform;
        labelTransform.rotation = Quaternion.LookRotation(labelTransform.position - cameraTransform.position, Vector3.up);
    }
}
